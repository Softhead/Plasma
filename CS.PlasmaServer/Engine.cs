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
        private static bool waiting_ = false;
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

        public void Start()
        {
            source_ = new CancellationTokenSource();
            server_ = new UdpClient(definition_!.UdpPort);

            t_ = new Thread(new ThreadStart(Run));
            t_?.Start();
        }

        public void Stop()
        {
            source_?.Cancel();

            if (t_ != null)
            {
                stop_ = true;
                t_?.Join(TimeSpan.FromSeconds(2));
                t_ = null;
            }

            server_?.Dispose();
            server_ = null;

            source_?.Dispose();
            source_ = null;
        }

        public static void ReceiveCallback(IAsyncResult ar)
        {
            IPEndPoint remoteEndpoint = (IPEndPoint)ar.AsyncState!;
            byte[]? bytesReceived = server_!.EndReceive(ar, ref remoteEndpoint!);
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
            waiting_ = false;
        }

        public static void Run()
        {
            CancellationToken token = source_!.Token;

            while (!stop_
                && !token.IsCancellationRequested)
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                waiting_ = true;
                server_!.BeginReceive(new AsyncCallback(ReceiveCallback), remoteEndpoint);

                while (waiting_
                    && !token.IsCancellationRequested)
                {
                    if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        token.ThrowIfCancellationRequested();
                    }
                }
            }

            Task.Run(() =>
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
                        stop_ = true;
                    }

                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
