﻿using CS.PlasmaLibrary;
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
        private DatabaseState? state_ = null;
        private Task? task_ = null;
        private bool isRunning_ = false;
        private CancellationTokenSource source_ = new();
        private readonly Queue<WorkRecord> workQueue_ = new();
        private readonly ManualResetEvent workComplete_ = new(true);
        private readonly int clientNumber_ = ++clientCount_;

        private static int clientCount_ = 0;
        private static List<IDatabaseClientProcess?>? processors_ = null;
        private static readonly ConcurrentDictionary<int, int> serverPortDictionary_ = new();

        public void Dispose()
        {
        }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public DatabaseDefinition? Definition { get => definition_; }

        public static ConcurrentDictionary<int, int> ServerPortDictionary { get => serverPortDictionary_; }

        public bool IsReady
        {
            get
            {
                if (definition_ is null)
                {
                    return false;
                }

                return serverPortDictionary_.Count == definition_.ServerCount;
            }
        }

        public bool WorkerQueueEmpty
        {
            get
            {
                return workQueue_.Count == 0 && workComplete_.WaitOne();
            }
        }

        public async Task<ErrorNumber> Start(StreamReader definitionStream)
        {
            if (definition_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            definition_ = new DatabaseDefinition();
            ErrorNumber result = definition_.LoadConfiguration(definitionStream);

            if (result == ErrorNumber.Success)
            {
                processors_ = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseClientProcess)))
                    .Select(o => (IDatabaseClientProcess?)Activator.CreateInstance(o))
                    .ToList();

                DatabaseResponse? responseGetState = await ProcessRequest(new DatabaseRequest { MessageType = DatabaseRequestType.GetState });
                if (responseGetState?.MessageType != DatabaseResponseType.Success)
                {
                    return ErrorNumber.CannotGetState;
                }

                isRunning_ = true;

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
            source_.Cancel();

            if (task_ != null)
            {
                task_.Wait(TimeSpan.FromSeconds(2));
                task_ = null;
            }

            source_.Dispose();
        }

        public async Task<DatabaseResponse?> ProcessRequest(DatabaseRequest request)
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
                || state_ is null)
            {
                return null;
            }

            int clientCommitCount = overrideClientCommitCount ?? definition_.ClientCommitCount;
            int clientQueryCount = overrideClientCommitCount ?? definition_.ClientQueryCount;
            int slotCount = 100;// Constant.SlotCount / definition_.ServerCopyCount;

            Barrier? barrier = new(clientCommitCount + 1);
            ManualResetEvent startAllRequestsEvent = new(false);
            Task[] tasks = new Task[clientQueryCount];
            ConcurrentBag<ResponseRecord> responses = new();
            DatabaseSlotInfo currentSlotInfo = new()
            {
                SlotNumber = request.GetHashCode() % slotCount
            };

            for (int index = 0; index < clientQueryCount; index++)
            {
                Logger.Log($"Client {clientNumber_} slot number: {currentSlotInfo.SlotNumber}");
                int serverNumber = overrideServerNumber ?? state_.Slots[currentSlotInfo.SlotNumber].ServerNumber;
                tasks[index] = Task.Run(async () =>
                    {
                        startAllRequestsEvent.WaitOne();
                        Logger.Log($"Client {clientNumber_} sending {request.Bytes.Length} bytes to server {serverNumber}.  {request}");
                        byte[]? receivedData = await RequestWithServerQuic(request.Bytes, serverNumber);
                        responses.Add(new ResponseRecord { Data = receivedData, ServerNumber = serverNumber });
                        try
                        {
                            // since we are waiting for ClientCommitCount responses, this could cause an exception
                            // here because more than the required number of responses was received, or the barrier
                            // instance may have been disposed of already
                            if (barrier is not null)
                            {
                                barrier.SignalAndWait();
                            }
                            else
                            {
                                Logger.Log($"Client {clientNumber_} excess response from server {serverNumber}.");
                            }
                        }
                        catch { }

                        DatabaseResponse response = new() { Bytes = receivedData };
                        Logger.Log($"Client {clientNumber_} received {receivedData?.Length} bytes from server {serverNumber}.  {response}");
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

            List<ResponseTally> tallies = new();
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

            int maxCount = 0;
            ResponseTally? maxTally = null;

            if (tallies.Count > 0)
            {
                maxCount = tallies.Max(o => o.Tally);

                try
                {
                    maxTally = tallies.Where(o => o.Tally == maxCount).Single();
                }
                catch
                {
                    foreach (ResponseTally tally in tallies)
                    {
                        string rr = string.Empty;
                        if (tally.Value?.Length > 1)
                        {
                            rr = Encoding.UTF8.GetString(tally.Value.AsSpan().Slice(1));
                        }
                        Logger.Log($"Client {clientNumber_} error tally state: '{tally.Value?[0]}' value: '{rr}'");
                    }
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
                            Logger.Log($"Client {clientNumber_} unmatched response for reading key {readKey} from server {tally.Response.ServerNumber}; updating server.");
                            workQueue_.Enqueue(new WorkRecord
                            {
                                Request = request,
                                Response = tally.Response,
                                Value = maxTally?.Value.AsSpan().Slice(1).ToArray(),
                                State = WorkItemState.UpdateServer
                            });
                        }
                    }
                }
            }

            if (passedQuorum)
            {
                Logger.Log($"Client {clientNumber_} passed quorum with {maxCount} matching responses out of {count}.");
                return maxTally?.Value;
            }
            else
            {
                Logger.Log($"Client {clientNumber_} failed quorum with {maxCount} matching responses out of {count}.");
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

            CancellationToken token = new();
            int port = ServerPortDictionary[serverNumber];
            Logger.Log($"Client {clientNumber_} using port {port}.");
            QuicConnection? conn = null;

            IPEndPoint endpoint = new(Definition.IpAddress, port);
            Logger.Log($"Client {clientNumber_} connection to server {serverNumber}, creating endpoint to port {endpoint.Port}.");
            conn = await QuicConnection.ConnectAsync(
                new QuicClientConnectionOptions
                {
                    RemoteEndPoint = endpoint,
                    DefaultCloseErrorCode = 0x0a,
                    DefaultStreamErrorCode = 0x0b,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions { ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 } },
                    MaxInboundUnidirectionalStreams = 10,
                    MaxInboundBidirectionalStreams = 100
                }, token);

            QuicStream stream = await conn!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);

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
            });

            return buffer;
        }

        public async Task RunWorker()
        {
            if (source_ is null)
            {
                throw new Exception("Client {clientNumber_} RunWorker aborting because source_ is null.");
            }

            CancellationToken token = source_.Token;

            while (isRunning_
                && !token.IsCancellationRequested)
            {
                try
                {
                    workComplete_.Reset();
                    if (workQueue_.TryDequeue(out WorkRecord? workRecord))
                    {
                        Logger.Log($"Client {clientNumber_} worker dequeued work record with id '{workRecord.Id}' with retry count {workRecord.RetryCount} and state {workRecord.State}.");

                        if (workRecord.RetryCount > WORKER_MAX_RETRY_COUNT)
                        {
                            throw new Exception($"Client {clientNumber_} workRecord id '{workRecord.Id}' retry count exceeded.");
                        }

                        switch (workRecord.State)
                        {
                            case WorkItemState.UpdateServer:
                                await UpdateServerBegin(workRecord, token);
                                break;
                        }
                    }

                    // exponential decay in wait time;  shorter wait with more items in queue
                    int depth = workQueue_.Count;
                    depth--;
                    double delay = 1000 * Math.Exp(-depth);
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), token);
                }
                catch (Exception e)
                {
                    Logger.Log($"Client {clientNumber_} exception in RunWorker: {e}");
                }
                finally
                {
                    workComplete_.Set();
                }
            }

            _ = Task.Run(Stop);
        }

        private async Task UpdateServerBegin(WorkRecord workRecord, CancellationToken token)
        {
            if (workRecord.Response is null)
            {
                Logger.Log($"Client {clientNumber_} error in UpdateServerBegin: Response is null.");
                return;
            }

            string? readKey = workRecord.Request?.GetReadKey();

            if (readKey is null)
            {
                Logger.Log($"Client {clientNumber_} error in Client.UpdateServerBegin: readKey is null.");
                return;
            }

            DatabaseRequest updateRequest = DatabaseRequestHelper.WriteRequest(readKey, workRecord.Value);
            await Task.Run(async () =>
            {
                Logger.Log($"Client {clientNumber_} UpdateServerBegin sending update request to server number {workRecord.Response.ServerNumber}.");
                byte[]? updateResult = SendRequest(updateRequest, 1, workRecord.Response.ServerNumber);
                if (updateResult is not null)
                {
                    // update was handled properly
                    Logger.Log($"Client {clientNumber_} UpdateServerBegin update request handled properly by server number {workRecord.Response.ServerNumber}.");
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
