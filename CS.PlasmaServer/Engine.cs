﻿using CS.PlasmaLibrary;
using System.Net.Sockets;
using System.Reflection;

namespace CS.PlasmaServer
{
    internal class Engine
    {
        private DatabaseDefinition? definition_ = null;
        private DatabaseState? state_ = null;
        private Task? task_ = null;
        private bool isRunning_ = false;
        private UdpClient? server_ = null;
        private CancellationTokenSource? source_ = null;
        private Dictionary<byte[], byte[]>? dictionary_ = null;
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

        public Dictionary<byte[], byte[]>? Dictionary { get => dictionary_; set => dictionary_ = value; }

        public void Start()
        {
            isRunning_ = true;
            source_ = new CancellationTokenSource();
            server_ = new UdpClient(definition_!.UdpPort);
            dictionary_ = new Dictionary<byte[], byte[]>(StructuralEqualityComparer<byte[]>.Default);

            task_ = Task.Run(() =>
            {
                _ = Run(this);
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

        public static async Task Run(Engine instance)
        {
            CancellationToken token = instance.source_!.Token;

            while (instance.isRunning_
                && !token.IsCancellationRequested)
            {
                var result = await instance.server_!.ReceiveAsync(token);
                token.ThrowIfCancellationRequested();

                byte[] bytesReceived = result.Buffer;
                Console.WriteLine($"Received {bytesReceived.Length} bytes from {result.RemoteEndPoint}");

                byte[]? bytesReturned;
                if (bytesReceived.Length > 0)
                {
                    bytesReturned = Process(instance, bytesReceived!);
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

                _ = instance.server_!.SendAsync(bytesReturned!, bytesReturned!.Length, result.RemoteEndPoint);
                Console.WriteLine($"Sent {bytesReturned.Length} bytes to {result.RemoteEndPoint}");
            }

            _ = Task.Run(() =>
            {
                instance.Stop();
            });
        }

        private static byte[]? Process(Engine instance, byte[] bytes)
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

            foreach (IDatabaseServerProcess? process in processors_)
            {
                if (process?.DatabaseRequestType == request.MessageType)
                {
                    DatabaseResponse? response = process.Process(instance, request);
                    return response?.Bytes;
                }
            }

            return null;
        }
    }
}
