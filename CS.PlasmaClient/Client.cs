using CS.PlasmaLibrary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaClient
{
    public class Client : IDisposable
    {
        private DatabaseDefinition? definition_ = null;
        private IPEndPoint? endPoint_ = null;
        private static List<IDatabaseClientProcess?>? processors_ = null;
        private DatabaseState? state_ = null;

        public Client()
        {
        }

        public void Dispose()
        {
        }

        public DatabaseState State { get => state_; set => state_ = value; }

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

            foreach (IDatabaseClientProcess? processor in processors_)
            {
                if (processor?.DatabaseRequestType == request.MessageType)
                {
                    return processor.Process(this, request);
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
            {
                return null;
            }

            if (endPoint_ is null)
            {
                endPoint_ = new IPEndPoint(definition_!.IpAddress!, definition_.UdpPort);
            }

            Barrier barrier = new Barrier(definition_.ClientCommitCount);
            for (int index = 0; index < definition_.ClientQueryCount; index++)
            {
                UdpClient client = new UdpClient();
                client.Connect(endPoint_);
                client.Send(data, data.Length);

                data = client.Receive(ref endPoint_);
            }

            return data;
        }
    }
}
