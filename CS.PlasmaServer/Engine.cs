using CS.PlasmaLibrary;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CS.PlasmaServer
{
    internal class Engine
    {
        public ManualResetEvent PortNumberEvent = new ManualResetEvent(false);

        private DatabaseDefinition? definition_ = null;
        private DatabaseState? state_ = null;
        private Task? task_ = null;
        private bool isRunning_ = false;
        private UdpClient? server_ = null;
        private CancellationTokenSource? source_ = null;
        private Dictionary<byte[], byte[]>? dictionary_ = null;
        private int? portNumber_ = null;

        private static List<IDatabaseServerProcess?>? processors_ = null;

        public Engine(DatabaseDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition_ = definition;
        }

        public bool IsRunning { get => isRunning_; }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public int? PortNumber { get => portNumber_; }

        public Dictionary<byte[], byte[]>? Dictionary { get => dictionary_; set => dictionary_ = value; }

        public ErrorNumber Start()
        {
            if (definition_ is null
                || definition_.IpAddress is null)
            {
                return ErrorNumber.DefinitionNotSet;
            }

            isRunning_ = true;
            source_ = new CancellationTokenSource();
            server_ = new UdpClient(definition_!.UdpPort);
            dictionary_ = new Dictionary<byte[], byte[]>(StructuralEqualityComparer<byte[]>.Default);

            task_ = Task.Run(() =>
            {
                _ = Run();
            });

            Task.Run(() =>
            {
                _ = RunQuic();
            });

            return ErrorNumber.Success;
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

        private static X509Certificate2 serverCertificate = new X509Certificate2("c:\\tmp\\iis.pfx", "sofuto");

        public static async Task<QuicServerConnectionOptions> QuicCallback(QuicConnection conn, SslClientHelloInfo info, CancellationToken token)
        {
            return new QuicServerConnectionOptions
            {
                DefaultCloseErrorCode = 0x0a,
                DefaultStreamErrorCode = 0x0b,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions 
                { 
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    ServerCertificate = serverCertificate
                }
            };
        }

        public async Task RunQuic()
        {
            CancellationToken token = source_!.Token;

            while (isRunning_
                && !token.IsCancellationRequested)
            {
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(definition_!.IpAddress!, 0);
                    QuicListener listener = await QuicListener.ListenAsync(
                        new QuicListenerOptions
                        {
                            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                            ListenEndPoint = endpoint,
                            ConnectionOptionsCallback = async (conn, info, token) =>
                            {
                                return await QuicCallback(conn, info, token);
                            }
                        }, token);
                    token.ThrowIfCancellationRequested();
                    if (listener is not null
                        && listener.LocalEndPoint is not null)
                    {
                        portNumber_ = listener.LocalEndPoint.Port;
                        PortNumberEvent.Set();
                    }

                    QuicConnection conn = await listener.AcceptConnectionAsync(token);
                    QuicStream stream = await conn.AcceptInboundStreamAsync(token);

                    byte[] buffer = new byte[4];
                    int received = 0;
                    while (received == 0 && !token.IsCancellationRequested)
                    {
                        received = await stream.ReadAsync(buffer, received, buffer.Length - received, token);
                    }

                    int length = BitConverter.ToInt32(buffer, 0);
                    byte[] bytesReceived = new byte[length];
                    received = 0;
                    while (received == 0 && !token.IsCancellationRequested)
                    {
                        received = await stream.ReadAsync(bytesReceived, received, bytesReceived.Length - received, token);
                    }
                    Console.WriteLine($"Quic received {length} bytes from {conn.RemoteEndPoint}");

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

                    buffer = new byte[bytesReturned.Length + 4];
                    BitConverter.GetBytes(bytesReturned.Length).CopyTo(buffer, 0);
                    bytesReturned.CopyTo(buffer, 4);
                    stream.Write(buffer, 0, buffer.Length);
                    stream.CompleteWrites();
                    Console.WriteLine($"Quic sent {bytesReturned.Length} bytes to {conn.RemoteEndPoint}");

                    stream.Close();
                    conn.CloseAsync(0x0c);
                    conn.DisposeAsync();
                    listener.DisposeAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e}");
                }
            }

            _ = Task.Run(() =>
            {
                Stop();
            });
        }

        public async Task Run()
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
                Stop();
            });
        }

        private byte[]? Process(byte[] bytes)
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
                    DatabaseResponse? response = processor.Process(this, request);
                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
