using CS.PlasmaLibrary;
using System.Collections;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaServer
{
    public class StructuralEqualityComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
        }

        public int GetHashCode(T? obj)
        {
            return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj!);
        }

        private static StructuralEqualityComparer<T>? defaultComparer;
        public static StructuralEqualityComparer<T> Default
        {
            get
            {
                StructuralEqualityComparer<T>? comparer = defaultComparer;
                if (comparer == null)
                {
                    comparer = new StructuralEqualityComparer<T>();
                    defaultComparer = comparer;
                }
                return comparer;
            }
        }
    }

    internal class Engine
    {
        private DatabaseDefinition? definition_ = null;
        private Task? task_ = null;
        private static bool isRunning_ = false;
        private static UdpClient? server_ = null;
        private static Engine? instance_ = null;
        private static CancellationTokenSource? source_ = null;
        private static Dictionary<byte[], byte[]>? dictionary_ = null;

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
            }

            _ = Task.Run(() =>
            {
                instance_!.Stop();
            });
        }

        public ReadOnlySpan<byte> Read(ReadOnlySpan<byte> key)
        {
            byte[]? value;
            byte[] keyArray = key.ToArray();
            if (dictionary_!.TryGetValue(keyArray, out value))
            {
                Span<byte> result = new byte[value.Length + 1].AsSpan();
                result[0] = (byte)DatabaseResponseType.Success;
                value.CopyTo(result.Slice(1));
                return result;
            }
            else
            {
                Span<byte> result = new byte[1].AsSpan();
                result[0] = (byte)DatabaseResponseType.KeyNotFound;
                return result;
            }
        }

        public ReadOnlySpan<byte> Write(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            byte[] keyArray = key.ToArray();
            byte[] valueArray = value.ToArray();
            if (dictionary_!.ContainsKey(keyArray))
            {
                dictionary_[keyArray] = valueArray;
            }
            else
            {
                dictionary_.Add(keyArray, valueArray);
            }

            Span<byte> result = new byte[1].AsSpan();
            result[0] = (byte)DatabaseResponseType.Success;
            return result;
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
                if (process?.DatabaseRequestType == request.MessageType)
                {
                    DatabaseResponse? response = process.Process(instance_!, request);
                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
