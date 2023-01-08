using CS.PlasmaLibrary;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaServer
{
    internal class Engine
    {
        private DatabaseDefinition? definition_ = null;
        private DatabaseState? state_ = null;
        private Task? task_ = null;
        private static bool isRunning_ = false;
        private static UdpClient? server_ = null;
        private static Engine? instance_ = null;
        private static CancellationTokenSource? source_ = null;
        private static Dictionary<byte[], byte[]>? dictionary_ = null;
        private static List<IDatabaseServerProcess?>? processors_ = null;

        public Engine(DatabaseDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            instance_ = this;
            definition_ = definition;
        }

        public bool IsRunning { get => isRunning_; }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public Dictionary<byte[], byte[]>? Dictionary { get => dictionary_; set => dictionary_ = value; }

        public void Start()
        {
            isRunning_ = true;
            source_ = new CancellationTokenSource();
            server_ = new UdpClient(definition_!.UdpPort);
            dictionary_ = new Dictionary<byte[], byte[]>(StructuralEqualityComparer<byte[]>.Default);

            task_ = Task.Run(() =>
            {
                _ = Run();
            });
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

            server_?.Dispose();
            server_ = null;

            source_?.Dispose();
            source_ = null;
        }

        public static async Task Run()
        {
            CancellationToken token = source_!.Token;

            while (isRunning_
                && !token.IsCancellationRequested)
            {
                var result = await server_!.ReceiveAsync(token);
                token.ThrowIfCancellationRequested();

                byte[] bytesReceived = result.Buffer;
                Console.WriteLine($"Received {bytesReceived.Length} bytes from {result.RemoteEndPoint}");

                byte[]? bytesReturned;
                if (bytesReceived.Length > 0)
                {
                    bytesReturned = Process(bytesReceived!);
                    if (bytesReturned is null)
                    {
                        DatabaseResponse response = new DatabaseResponse { MessageType = DatabaseResponseType.CouldNotProcessCommand };
                        bytesReturned = response.Bytes;
                    }
                }
                else
                {
                    DatabaseResponse response = new DatabaseResponse { MessageType = DatabaseResponseType.NoBytesReceived };
                    bytesReturned = response.Bytes;
                }

                _ = server_!.SendAsync(bytesReturned!, bytesReturned!.Length, result.RemoteEndPoint);
                Console.WriteLine($"Sent {bytesReturned.Length} bytes to {result.RemoteEndPoint}");
            }

            _ = Task.Run(() =>
            {
                instance_!.Stop();
            });
        }

        private static byte[]? Process(byte[] bytes)
        {
            DatabaseRequest request = new DatabaseRequest { Bytes = bytes! };

            if (processors_ is null)
            {
                processors_ = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseServerProcess)))
                    .Select(o => (IDatabaseServerProcess?)Activator.CreateInstance(o))
                    .ToList();
            }

            foreach (IDatabaseServerProcess? processor in processors_)
            {
                if (processor?.DatabaseRequestType == request.MessageType)
                {
                    DatabaseResponse? response = processor.Process(instance_!, request);
                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
