using CS.PlasmaLibrary;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CS.PlasmaServer
{
    public class Engine
    {
        public ManualResetEvent PortNumberEvent = new ManualResetEvent(false);

        private readonly DatabaseDefinition? definition_ = null;
        private DatabaseState? state_ = null;
        private Task? task_ = null;
        private bool isRunning_ = false;
        private readonly CancellationTokenSource source_ = new();
        private Dictionary<byte[], byte[]> dictionary_ = new(StructuralEqualityComparer<byte[]>.Default);
        private int? portNumber_ = null;
        private readonly int? serverNumber_ = null;

        private static List<IDatabaseServerProcess?>? processors_ = null;

        public Engine(DatabaseDefinition definition, int serverNumber)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            serverNumber_ = serverNumber;
            definition_ = definition;
        }

        public bool IsRunning { get => isRunning_; }

        public DatabaseState? State { get => state_; set => state_ = value; }

        public int? PortNumber { get => portNumber_; }

        public Dictionary<byte[], byte[]> Dictionary { get => dictionary_; set => dictionary_ = value; }

        public ErrorNumber Start()
        {
            if (definition_ is null
                || definition_.IpAddress is null)
            {
                return ErrorNumber.DefinitionNotSet;
            }

            processors_ = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(o => o.GetInterfaces().Contains(typeof(IDatabaseServerProcess)))
                .Select(o => (IDatabaseServerProcess?)Activator.CreateInstance(o))
                .ToList();

            isRunning_ = true;

            task_ = Task.Factory.StartNew(() =>
            {
                _ = RunQuicAsync();
            }, 
            source_.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

            return ErrorNumber.Success;
        }

        public async Task Stop()
        {
            isRunning_ = false;
            _ = Task.Run(source_.Cancel);

            if (task_ != null)
            {
                await task_.WaitAsync(TimeSpan.FromSeconds(2));
                task_ = null;
            }

            source_.Dispose();
        }

        private static readonly X509Certificate2 serverCertificate = new("c:\\tmp\\iis.pfx", "sofuto");

        public static Task<QuicServerConnectionOptions> QuicCallback(QuicConnection conn, SslClientHelloInfo info, CancellationToken token)
        {
            return Task.FromResult(new QuicServerConnectionOptions
            {
                DefaultCloseErrorCode = 0x0a,
                DefaultStreamErrorCode = 0x0b,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    ServerCertificate = serverCertificate
                }
            });
        }

        public async Task RunQuicAsync()
        {
            CancellationToken token = source_.Token;

            while (isRunning_
                && !token.IsCancellationRequested)
            {
                try
                {
                    IPEndPoint endpoint = new(definition_!.IpAddress!, 0);
                    QuicListener? listener = await QuicListener.ListenAsync(
                        new QuicListenerOptions
                        {
                            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                            ListenEndPoint = endpoint,
                            ConnectionOptionsCallback = async (conn, info, token) =>
                            {
                                return await QuicCallback(conn, info, token);
                            }
                        }, token);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    while (listener is not null
                        && listener.LocalEndPoint is not null)
                    {
                        // communicate the port number out via IPC
                        portNumber_ = listener.LocalEndPoint.Port;
                        PortNumberEvent.Set();
                        QuicConnection? conn = await listener.AcceptConnectionAsync(token);

                        if (isRunning_
                            && conn is not null
                            && !token.IsCancellationRequested)
                        {
                            QuicStream? stream = null;

                            try
                            {
                                stream = await conn.AcceptInboundStreamAsync(token);

                                byte[] buffer = new byte[4];
                                int received = 0;
                                while (received == 0 && !token.IsCancellationRequested)
                                {
                                    received = await stream.ReadAsync(buffer.AsMemory(received, buffer.Length - received), token);
                                }

                                int length = BitConverter.ToInt32(buffer, 0);
                                byte[] bytesReceived = new byte[length];
                                received = 0;
                                while (received == 0 && !token.IsCancellationRequested)
                                {
                                    received = await stream.ReadAsync(bytesReceived.AsMemory(received, bytesReceived.Length - received), token);
                                }
                                DatabaseRequest request = new() { Bytes = bytesReceived };
                                Logger.Log($"Quic server {serverNumber_} received {length} bytes from {conn.RemoteEndPoint}  {request}");

                                DatabaseResponse response = new();
                                byte[]? bytesReturned;
                                if (bytesReceived.Length > 0)
                                {
                                    bytesReturned = Process(bytesReceived!);
                                    if (bytesReturned is not null)
                                    {
                                        response.Bytes = bytesReturned;
                                    }
                                    else
                                    {
                                        response.MessageType = DatabaseResponseType.CouldNotProcessCommand;
                                        bytesReturned = response.Bytes;
                                    }
                                }
                                else
                                {
                                    response.MessageType = DatabaseResponseType.NoBytesReceived;
                                    bytesReturned = response.Bytes;
                                }

                                buffer = new byte[bytesReturned!.Length + 4];
                                BitConverter.GetBytes(bytesReturned.Length).CopyTo(buffer, 0);
                                bytesReturned.CopyTo(buffer, 4);
                                stream.Write(buffer, 0, buffer.Length);
                                Logger.Log($"Quic server {serverNumber_} sent {bytesReturned.Length} bytes to {conn.RemoteEndPoint}  {response}");
                            }
                            catch (QuicException e)
                            {
                                if (e.QuicError == QuicError.ConnectionIdle)
                                {
                                    Logger.Log($"Quic server {serverNumber_} connection idle on {conn?.RemoteEndPoint}");

                                    if (conn is not null)
                                    {
                                        await conn.DisposeAsync();
                                        conn = null;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Quic server {serverNumber_} exception on {conn?.RemoteEndPoint}: {e.Message}");
                            }
                            finally
                            {
                                if (stream is not null)
                                {
                                    await stream.DisposeAsync();
                                    stream = null;
                                }

                                if (conn is not null)
                                {
                                    await conn.DisposeAsync();
                                    conn = null;
                                }
                            }
                        }
                    }

                    if (listener is not null)
                    {
                        await listener.DisposeAsync();
                        listener = null;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"Error in Engine.RunQuic: {e}");
                }
            }

            await Stop();
        }

        private byte[]? Process(byte[] bytes)
        {
            DatabaseRequest request = new() { Bytes = bytes! };

            foreach (IDatabaseServerProcess? processor in processors_!)
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
