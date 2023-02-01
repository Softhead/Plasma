using CS.PlasmaLibrary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Reflection;
using System.Text;

namespace CS.PlasmaClient
{
    public class Client : IDisposable
    {
        private const int WORKER_MAX_RETRY_COUNT = 5;  // number of retries for worker records

        private DatabaseDefinition? definition_ = null;
        private static List<IDatabaseClientProcess?>? processors_ = null;
        private DatabaseState? state_ = null;
        private ConcurrentDictionary<int, int> serverPortDictionary_ = new ConcurrentDictionary<int, int>();
        private Task? task_ = null;
        private bool isRunning_ = false;
        private CancellationTokenSource? source_ = null;
        private Queue<WorkRecord> workQueue_ = new Queue<WorkRecord>();

        public void Dispose()
        {
        }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public DatabaseDefinition? Definition { get => definition_; }

        public ConcurrentDictionary<int, int> ServerPortDictionary { get => serverPortDictionary_; }

        public bool IsReady
        {
            get
            {
                if (definition_ is null)
                {
                    return false;
                }

                return serverPortDictionary_.Count() == definition_.ServerCount;
            }
        }

        public int WorkerQueueCount
        {
            get
            {
                return workQueue_.Count;
            }
        }

        public ErrorNumber Start(StreamReader definitionStream)
        {
            if (definition_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            definition_ = new DatabaseDefinition();
            ErrorNumber result = definition_.LoadConfiguration(definitionStream);

            if (result == ErrorNumber.Success)
            {
                isRunning_ = true;
                source_ = new CancellationTokenSource();

                task_ = Task.Run(() =>
                {
                    _ = RunWorker();
                }, source_.Token);
            }

            return result;
        }

        public void Stop()
        {
            isRunning_ = false;
            source_?.Cancel();

            if (task_ != null)
            {
                task_.Wait(TimeSpan.FromSeconds(2));
                task_ = null;
            }

            source_?.Dispose();
            source_ = null;
        }

        public async Task<DatabaseResponse?> Request(DatabaseRequest request)
        {
            if (processors_ is null)
            {
                processors_ = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseClientProcess)))
                    .Select(o => (IDatabaseClientProcess?)Activator.CreateInstance(o))
                    .ToList();
            }

            if (state_ is null)
            {
                DatabaseResponse? responseGetState = await ProcessRequest(new DatabaseRequest { MessageType = DatabaseRequestType.GetState });
                if (responseGetState?.MessageType != DatabaseResponseType.Success)
                {
                    return responseGetState;
                }
            }

            return await ProcessRequest(request);
        }

        private async Task<DatabaseResponse?> ProcessRequest(DatabaseRequest request)
        {
            foreach (IDatabaseClientProcess? processor in processors_!)
            {
                if (processor?.DatabaseRequestType == request.MessageType)
                {
                    return await processor!.ProcessAsync(this, request);
                }
            }

            byte[]? responseData = SendRequest(request);
            if (responseData is not null)
            {
                return new DatabaseResponse { Bytes = responseData };
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Invalid };
        }

        internal byte[]? SendRequest(DatabaseRequest? request, int? overrideClientCommitCount = null, int? overrideServerNumber = null)
        {
            if (request is null
                || request.Bytes is null
                || definition_ is null
                || definition_.IpAddress is null
                || state_ is null
                || source_ is null)
            {
                return null;
            }

            int clientCommitCount = overrideClientCommitCount ?? definition_.ClientCommitCount;
            int clientQueryCount = overrideClientCommitCount ?? definition_.ClientQueryCount;

            Barrier? barrier = new Barrier(clientCommitCount + 1);
            ManualResetEvent startAllRequestsEvent = new ManualResetEvent(false);
            Task[] tasks = new Task[clientQueryCount];
            ConcurrentBag<ResponseRecord> responses = new ConcurrentBag<ResponseRecord>();
            DatabaseSlotInfo currentSlotInfo = new DatabaseSlotInfo();
            currentSlotInfo.SlotNumber = request.Bytes.GetHashCode() % Constant.SlotCount;

            for (int index = 0; index < clientQueryCount; index++)
            {
                int serverNumber = overrideServerNumber ?? state_.Slots[currentSlotInfo.SlotNumber].ServerNumber;
                Logger.Log($"Client sending {request.Bytes.Length} bytes to server {serverNumber}.  {request}");
                tasks[index] = Task.Run(async () =>
                    {
                        startAllRequestsEvent.WaitOne();
                        byte[]? receivedData = await RequestWithServerQuic(request.Bytes, serverNumber);
                        responses.Add(new ResponseRecord { Data = receivedData, ServerNumber = serverNumber });
                        try
                        {
                            // since we are waiting for ClientCommitCount responses, this could cause an exception
                            // here because more than the required number of responses was received, or the barrier
                            // instance may have been disposed of already
                            barrier.SignalAndWait();
                        }
                        catch { }

                        DatabaseResponse response = new DatabaseResponse { Bytes = receivedData };
                        Logger.Log($"Client received {receivedData?.Length} bytes from server {serverNumber}.  {response}");
                    }, source_.Token);

                if (index < clientQueryCount - 1)
                {
                    if (ErrorNumber.Success != state_.FindNextCopySlot(currentSlotInfo, ref currentSlotInfo, definition_.ServerCount))
                    {
                        return null;
                    }
                }
            }

            // the number of tasks may exceed the barrier participant count
            // so after all the Tasks are created, then signal the event to cause them to start
            // as well, put any Barrier changes here or in the Tasks into a lock to prevent exceptions
            startAllRequestsEvent.Set();
            try
            {
                // since we are waiting for ClientCommitCount responses, this could cause an exception
                // here because more than the required number of responses was received
                barrier.SignalAndWait();
            }
            catch { }

            barrier.Dispose();
            barrier = null;

            List<ResponseTally> tallies = new List<ResponseTally>();
            int count = 0;

            while (!responses.IsEmpty)
            {
                if (responses.TryTake(out ResponseRecord? responseRecord))
                {
                    count++;

                    if (responseRecord is not null)
                    {
                        byte[]? value = responseRecord.Data;

                        ResponseTally? foundTally = null;
                        if (value is not null)
                        {
                            foundTally = tallies.SingleOrDefault(o => o.Value is not null && o.Value.SequenceEqual(value));
                        }

                        if (foundTally is not null)
                        {
                            foundTally.Tally++;
                        }
                        else
                        {
                            tallies.Add(new ResponseTally
                            {
                                Tally = 1,
                                Value = value,
                                Response = responseRecord
                            });
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            int maxCount = tallies.Max(o => o.Tally);

            ResponseTally maxTally = null;
            try
            {
                maxTally = tallies.Where(o => o.Tally == maxCount).Single();
            }
            catch
            {
                foreach (var tally in tallies)
                {
                    string rr = "";
                    if (tally.Value.Length > 1)
                    {
                        rr = Encoding.UTF8.GetString(tally.Value.AsSpan().Slice(1));
                    }
                    Logger.Log($"error tally state: '{tally.Value[0]}' value: '{rr}'");
                }
            }
            bool passedQuorum = false;

            if (maxCount >= clientCommitCount)
            {
                passedQuorum = true;
            }
            else if (maxCount > tallies.Count / 2)
            {
                passedQuorum = true;
            }

            // check for unmatched responses
            if (tallies.Count > 1)
            {
                if (request.MessageType == DatabaseRequestType.Read)
                {
                    string? readKey = request.GetReadKey();

                    foreach (ResponseTally tally in tallies)
                    {
                        if (tally.Tally == maxCount)
                        {
                            continue;
                        }

                        if (tally.Response is not null)
                        {
                            Logger.Log($"Unmatched response for reading key {readKey} from server {tally.Response.ServerNumber}; updating server.");
                            workQueue_.Enqueue(new WorkRecord
                            {
                                Request = request,
                                Response = tally.Response,
                                Value = maxTally.Value.AsSpan().Slice(1).ToArray(),
                                State = WorkItemState.UpdateServer
                            });
                        }
                    }
                }
            }

            if (passedQuorum)
            {
                Logger.Log($"Client passed quorum with {maxCount} matching responses out of {count}.");
                return maxTally.Value;
            }
            else
            {
                Logger.Log($"Client failed quorum with {maxCount} matching responses out of {count}.");
                return new DatabaseResponse { MessageType = DatabaseResponseType.QuorumFailed }.Bytes;
            }
        }

        private async Task<byte[]?> RequestWithServerQuic(byte[]? data, int serverNumber)
        {
            if (Definition is null
                || Definition.IpAddress is null
                || data is null)
            {
                return null;
            }

            CancellationToken token = new CancellationToken();
            int port = ServerPortDictionary[serverNumber];
            IPEndPoint endpoint = new IPEndPoint(Definition.IpAddress, port);
            QuicConnection conn = await QuicConnection.ConnectAsync(
                new QuicClientConnectionOptions
                {
                    RemoteEndPoint = endpoint,
                    DefaultCloseErrorCode = 0x0a,
                    DefaultStreamErrorCode = 0x0b,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions { ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 } },
                    MaxInboundUnidirectionalStreams = 10,
                    MaxInboundBidirectionalStreams = 100
                }, token);
            QuicStream stream = await conn.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);

            // message format is 4 bytes message length, followed by data
            byte[] buffer = new byte[data.Length + 4];
            BitConverter.GetBytes(data.Length).CopyTo(buffer, 0);
            data.CopyTo(buffer, 4);
            await stream.WriteAsync(buffer, token);
            stream.CompleteWrites();

            // read response length
            buffer = new byte[4];
            await stream.ReadAsync(buffer, token);

            // read the response
            int length = BitConverter.ToInt32(buffer, 0);
            buffer = new byte[length];
            int received = 0;
            bool stillGoing = true;
            while (stillGoing)
            {
                int currentReceived = await stream.ReadAsync(buffer.AsMemory(received, length - received), token);
                received += currentReceived;
                if (received == length)
                {
                    stillGoing = false;
                }
            }

            _ = Task.Run(async () =>
            {
                stream.Close();
                await stream.DisposeAsync();
                await conn.CloseAsync(0x0c);
                await conn.DisposeAsync();
            });

            return buffer;
        }

        public async Task RunWorker()
        {
            if (source_ is null)
            {
                throw new Exception("Client.RunWorker aborting because source_ is null.");
            }

            CancellationToken token = source_.Token;

            while (isRunning_
                && !token.IsCancellationRequested)
            {
                try
                {
                    if (workQueue_.TryDequeue(out WorkRecord? workRecord))
                    {
                        Logger.Log($"Client worker dequeued work record with id '{workRecord.Id}' with retry count {workRecord.RetryCount} and state {workRecord.State}.");

                        if (workRecord.RetryCount > WORKER_MAX_RETRY_COUNT)
                        {
                            throw new Exception($"WorkRecord id '{workRecord.Id}' retry count exceeded.");
                        }

                        switch (workRecord.State)
                        {
                            case WorkItemState.UpdateServer:
                                UpdateServerBegin(token, workRecord);
                                break;
                        }
                    }

                    // exponential decay in wait time;  shorter wait with more items in queue
                    int depth = workQueue_.Count();
                    depth--;
                    double delay = 1000 * Math.Exp(-depth);
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), token);
                }
                catch (Exception e)
                {
                    Logger.Log($"Error in Client.RunWorker: {e}");
                }
            }

            _ = Task.Run(Stop);
        }

        private void UpdateServerBegin(CancellationToken token, WorkRecord workRecord)
        {
            if (workRecord.Response is null)
            {
                Logger.Log($"Error in Client.UpdateServerBegin: Response is null.");
                return;
            }

            string? readKey = workRecord.Request?.GetReadKey();

            if (readKey is null)
            {
                Logger.Log($"Error in Client.UpdateServerBegin: readKey is null.");
                return;
            }

            DatabaseRequest updateRequest = DatabaseRequestHelper.WriteRequest(readKey, workRecord.Value);
            _ = Task.Run(async () =>
            {
                Logger.Log($"Client.UpdateServerBegin sending update request to server number {workRecord.Response.ServerNumber}.");
                byte[]? updateResult = SendRequest(updateRequest, 1, workRecord.Response.ServerNumber);
                if (updateResult is not null)
                {
                    // update was handled properly
                    Logger.Log($"Client.UpdateServerBegin update request handled properly by server number {workRecord.Response.ServerNumber}.");
                    return;
                }

                // retry this state after a random delay
                double delay = 1000 * Random.Shared.NextDouble();
                await Task.Delay(TimeSpan.FromMilliseconds(delay), token);

                workRecord.RetryCount++;
                workQueue_.Enqueue(workRecord);
            }, token);
        }
    }
}
