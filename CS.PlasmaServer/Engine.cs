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
        private static UdpClient? client_ = null;

        public Engine(DatabaseDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition_ = definition;
        }

        public void Start()
        {
            client_ = new UdpClient(definition_!.UdpPort);

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
            IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

            while (!stop_)
            {
                byte[]? bytesReceived = client_!.Receive(ref remoteEndpoint);
                Console.WriteLine($"Received {bytesReceived?.Length} bytes from {remoteEndpoint}");

                if (bytesReceived is not null)
                {
                    byte[]? bytesReturned = Process(bytesReceived!);
                    if (bytesReturned is not null)
                    {
                        client_!.Send(bytesReturned);
                    }
                    else
                    {
                        DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.CouldNotProcessCommand };
                        client_!.Send(response.Bytes);
                    }
                }
                else
                {
                    DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.NoBytesReceived };
                    client_!.Send(response.Bytes);
                }
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
                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
