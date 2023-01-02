using CS.PlasmaLibrary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaServer
{
    internal class Engine
    {
        private DatabaseDefinition? definition_ = null;
        private Thread? t_ = null;
        private static bool stop_ = false;
        private static UdpClient? server_ = null;
        private static Engine? instance_ = null;

        public Engine(DatabaseDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            instance_ = this;
            definition_ = definition;
        }

        public void Start()
        {
            server_ = new UdpClient(definition_!.UdpPort);

            t_ = new Thread(new ThreadStart(Run));
            t_?.Start();
        }

        public void Stop()
        {
            if (t_ != null)
            {
                stop_ = true;
                t_?.Join();
                t_ = null;
            }
        }

        public static void Run()
        {
            while (!stop_)
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[]? bytesReceived = server_!.Receive(ref remoteEndpoint);
                Console.WriteLine($"Received {bytesReceived?.Length} bytes from {remoteEndpoint}");

                UdpClient responseClient = new UdpClient();
                responseClient.Connect(remoteEndpoint);
                byte[]? bytesReturned;

                if (bytesReceived is not null)
                {
                    bytesReturned = Process(bytesReceived!);
                    if (bytesReturned is null)
                    {
                        DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.CouldNotProcessCommand };
                        bytesReturned = response.Bytes;
                    }
                }
                else
                {
                    DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.NoBytesReceived };
                    bytesReturned = response.Bytes;
                }

                server_.Send(bytesReturned, bytesReturned.Length, remoteEndpoint);
            }
        }

        private static List<IDatabaseProcess?>? process_ = null;

        private static byte[]? Process(byte[] bytes)
        {
            DatabaseRequest request = new DatabaseRequest { Bytes = bytes! };

            if (process_ is null)
            {
                process_ = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseProcess)))
                    .Select(o => (IDatabaseProcess?)Activator.CreateInstance(o))
                    .ToList();
            }

            foreach (IDatabaseProcess? process in process_)
            {
                if (process?.DatabaseRequestType == request.DatabaseRequestType)
                {
                    DatabaseResponse response = process.Process(request);
                    if (response.DatabaseResponseType == DatabaseResponseType.Stopped)
                    {
                        instance_!.Stop();
                    }

                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
