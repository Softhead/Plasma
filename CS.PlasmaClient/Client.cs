using CS.PlasmaLibrary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaClient
{
    public class Client : IDisposable
    {
        private DatabaseDefinition? definition_ = null;
        private static List<IDatabaseClientProcess?>? processors_ = null;
        private DatabaseState? state_ = null;
        private Dictionary<int, int> serverPortDictionary_ = new Dictionary<int, int>();

        public Client()
        {
        }

        public void Dispose()
        {
        }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public DatabaseDefinition? Definition { get => definition_; }

        public Dictionary<int, int> ServerPortDictionary { get => serverPortDictionary_; }

        public ErrorNumber Start(string definitionFileName)
        {
            if (definition_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            definition_ = new DatabaseDefinition();
            return definition_.LoadConfiguration(definitionFileName);
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

            byte[]? requestData = request.Bytes;
            byte[]? responseData = Request(requestData);
            if (responseData is not null)
            {
                return new DatabaseResponse { Bytes = responseData };
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Invalid };
        }

        internal byte[]? Request(byte[]? data)
        {
            if (data is null
                || definition_ is null
                || definition_.IpAddress is null
                || state_ is null)
            {
                return null;
            }

            Barrier? barrier = new Barrier(definition_.ClientCommitCount + 1);
            ManualResetEvent startAllRequestsEvent = new ManualResetEvent(false);
            Task[] tasks = new Task[definition_.ClientQueryCount];
            ConcurrentBag<byte[]?> responses = new ConcurrentBag<byte[]?>();
            DatabaseSlotInfo currentSlotInfo = new DatabaseSlotInfo();
            currentSlotInfo.SlotNumber = data.GetHashCode() % Constant.SlotCount;

            for (byte index = 0; index < definition_.ClientQueryCount; index++)
            {
                byte serverNumber = index;// state_.Slots[currentSlotInfo.SlotNumber].ServerNumber;
                tasks[index] = Task.Run(async () =>
                    {
                        startAllRequestsEvent.WaitOne();
                        responses.Add(await RequestWithServerQuic(data, serverNumber));

                        lock (this)
                        {
                            if (barrier is not null)
                            {
                                barrier.SignalAndWait();
                            }
                        }
                    });

                currentSlotInfo.CopyNumber = (byte)(index + 1);
                //state_.FindNextCopySlot(currentSlotInfo, ref currentSlotInfo);
            }

            // the number of tasks may exceed the barrier participant count
            // so after all the Tasks are created, then signal the event to cause them to start
            // as well, put any Barrier changes here or in the Tasks into a lock to prevent exceptions
            startAllRequestsEvent.Set();
            barrier.SignalAndWait();
            lock (this)
            {
                barrier.Dispose();
                barrier = null;
            }

            int tally = 0;
            byte[]? value = null;
            while (!responses.IsEmpty)
            {
                if (responses.TryTake(out byte[]? response))
                {
                    if (value is null)
                    {
                        tally = 1;
                        value = response;
                    }
                    else
                    {
                        if (response is not null
                            && response.SequenceEqual(value))
                        {
                            tally++;
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            if (tally >= definition_.ClientCommitCount)
            {
                return value;
            }
            else
            {
                return new DatabaseResponse { MessageType = DatabaseResponseType.QuorumFailed }.Bytes;
            }
        }

        private byte[]? RequestWithServer(byte[]? data, byte serverNumber)
        {
            if (data is null)
            {
                return null;
            }

            int port = serverPortDictionary_[serverNumber];

            IPEndPoint endPoint = new IPEndPoint(definition_!.IpAddress!, port);
            UdpClient client = new UdpClient();
            client.Connect(endPoint);
            client.Send(data, data.Length);

            return client.Receive(ref endPoint);
        }

        private async Task<byte[]?> RequestWithServerQuic(byte[]? data, byte serverNumber)
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
    }
}
