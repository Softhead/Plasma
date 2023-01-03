using CS.PlasmaLibrary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaServer
{
    internal class Engine
    {
        private DatabaseDefinition? definition_ = null;
        private Task? task_ = null;
        private static bool isRunning_ = false;
        private static UdpClient? server_ = null;
        private static Engine? instance_ = null;
        private static CancellationTokenSource? source_ = null;

        public Engine(DatabaseDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            instance_ = this;
            definition_ = definition;
        }

        public bool IsRunning
        {
            get => isRunning_;
        }

        public void Start()
        {
            isRunning_= true;
            source_ = new CancellationTokenSource();
            server_ = new UdpClient(definition_!.UdpPort);

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
                        DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.CouldNotProcessCommand };
                        bytesReturned = response.Bytes;
                    }
                }
                else
                {
                    DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.NoBytesReceived };
                    bytesReturned = response.Bytes;
                }

                _ = server_!.SendAsync(bytesReturned, bytesReturned.Length, result.RemoteEndPoint);
            }

            _ = Task.Run(() =>
            {
                instance_!.Stop();
            });
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
                        source_?.Cancel();
                    }

                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
