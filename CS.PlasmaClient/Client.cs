using CS.PlasmaLibrary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaClient
{
    public class Client : IDisposable
    {
        private DatabaseDefinition? definition_ = null;
        private static List<IDatabaseClientProcess?>? processors_ = null;
        private DatabaseState? state_ = null;

        public Client()
        {
        }

        public void Dispose()
        {
        }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public DatabaseDefinition? Definition { get => definition_; }

        public ErrorNumber Start(string definitionFileName)
        {
            if (definition_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            definition_ = new DatabaseDefinition();
            return definition_.LoadConfiguration(definitionFileName);
        }

        public DatabaseResponse? Request(DatabaseRequest request)
        {
            if (processors_ is null)
            {
                processors_ = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseClientProcess)))
                    .Select(o => (IDatabaseClientProcess?)Activator.CreateInstance(o))
                    .ToList();
            }

            //if (state_ is null)
            //{
            //    DatabaseResponse? responseGetState = ProcessRequest(new DatabaseRequest { MessageType = DatabaseRequestType.GetState });
            //    if (responseGetState?.MessageType != DatabaseResponseType.Success)
            //    {
            //        return responseGetState;
            //    }
            //}

            return ProcessRequest(request);
        }

        private DatabaseResponse? ProcessRequest(DatabaseRequest request)
        {

            foreach (IDatabaseClientProcess? processor in processors_!)
            {
                if (processor?.DatabaseRequestType == request.MessageType)
                {
                    return processor!.Process(this, request);
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
                || definition_.IpAddress is null)
 //               || state_ is null)
            {
                return null;
            }

            Barrier? barrier = new Barrier(definition_.ClientCommitCount + 1);
            ManualResetEvent manualEvent = new ManualResetEvent(false);
            Task[] tasks = new Task[definition_.ClientQueryCount];
            ConcurrentBag<byte[]?> responses = new ConcurrentBag<byte[]?>();
            DatabaseSlotInfo currentSlotInfo = new DatabaseSlotInfo();
            currentSlotInfo.SlotNumber = data.GetHashCode() % Constant.SlotCount;

            for (byte index = 0; index < definition_.ClientQueryCount; index++)
            {
                byte serverNumber = 0;// state_.Slots[currentSlotInfo.SlotNumber].ServerNumber;
                tasks[index] = Task.Run(() =>
                    {
                        manualEvent.WaitOne();
                        responses.Add(RequestWithServer(data, serverNumber));

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
            manualEvent.Set();
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

            IPEndPoint endPoint = new IPEndPoint(definition_!.IpAddress!, definition_.UdpPort + serverNumber);
            UdpClient client = new UdpClient();
            client.Connect(endPoint);
            client.Send(data, data.Length);

            return client.Receive(ref endPoint);
        }
    }
}
