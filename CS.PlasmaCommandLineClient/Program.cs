using CS.PlasmaClient;
using CS.PlasmaLibrary;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace CS.PlasmaCommandLineClient
{
    internal class Program
    {
        private static bool stillGoing_ = true;

        // usage: PlasmaCommandLineClient -server <server IP address> -port <server UDP port>
        static async Task Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: PlasmaCommandLineClient <config file>");
                return;
            }
            Console.WriteLine("For help, use command 'help'.\n");

            Client? client = null;
            var message = new byte[20];
            var messageWait = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_wait");
            var messageHandled = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_handled");
            var mmf = MemoryMappedFile.CreateOrOpen("Plasma_mmf", message.Length);
            var viewStream = mmf.CreateViewStream();
            messageHandled.Set();

            _ = Task.Run(() =>
            {
                while (true)
                {
                    messageWait.WaitOne();
                    viewStream.Position = 0;
                    viewStream.Read(message, 0, message.Length);

                    // handle the message
                    string messageString = Encoding.UTF8.GetString(message);
                    string[] parts = messageString.Split(' ');
                    if (int.TryParse(parts[0], out int server)
                        && int.TryParse(parts[1], out int port))
                    {
                        while (client is null)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }
                        client.ServerPortDictionary.Add(server, port);
                    }
                    messageHandled.Set();
                }
            });

            using (client = new Client())
            {
                client.Start(args[0]);

                while (stillGoing_)
                {
                    Console.Write("Enter command: ");
                    string? command = Console.ReadLine()?.Trim();

                    DatabaseRequest? request = DecodeCommand(command);
                    if (request is not null)
                    {
                        if (request.MessageType != DatabaseRequestType.Invalid)
                        {
                            DatabaseResponse? response = await client.Request(request);
                            Console.WriteLine($"Response: {DecodeResponse(response)}");
                        }
                        else
                        {
                            Console.WriteLine($"The command '{command}' is not a valid command.");
                        }
                    }
                }
            }
        }

        private static DatabaseRequest? DecodeCommand(string? command)
        {
            DatabaseRequest request = new DatabaseRequest();
            if (string.Compare("ping", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { MessageType = DatabaseRequestType.Ping };
            }
            else if (string.Compare("start", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { MessageType = DatabaseRequestType.Start };
            }
            else if (string.Compare("stop", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { MessageType = DatabaseRequestType.Stop };
            }
            else if (string.Compare("read", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                Console.Write("    Enter key: ");
                string? key = Console.ReadLine()?.Trim();

                byte[] data = new byte[1 + Encoding.UTF8.GetByteCount(key!)];
                data[0] = (byte)DatabaseRequestType.Read;
                Span<byte> dataSpan = data.AsSpan();
                Span<byte> keySpan = Encoding.UTF8.GetBytes(key!).AsSpan();
                keySpan.CopyTo(dataSpan.Slice(1));
                return new DatabaseRequest { Bytes = dataSpan.ToArray() };
            }
            else if (string.Compare("write", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                Console.Write("    Enter key: ");
                string? key = Console.ReadLine()?.Trim();
                Console.Write("    Enter value: ");
                string? value = Console.ReadLine()?.Trim();

                int keyLength = Encoding.UTF8.GetByteCount(key!);
                byte[] data = new byte[1 + keyLength + 1 + Encoding.UTF8.GetByteCount(value!)];
                data[0] = (byte)DatabaseRequestType.Write;
                Span<byte> dataSpan = data.AsSpan();
                Span<byte> keySpan = Encoding.UTF8.GetBytes(key!).AsSpan();
                keySpan.CopyTo(dataSpan.Slice(1));
                Span<byte> valueSpan = Encoding.UTF8.GetBytes(value!).AsSpan();
                dataSpan[keyLength + 1] = Constant.Delimiter;
                valueSpan.CopyTo(dataSpan.Slice(keyLength + 2));
                return new DatabaseRequest { Bytes = dataSpan.ToArray() };
            }
            else if (string.Compare("getstate", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { MessageType = DatabaseRequestType.GetState };
            }
            else if (string.Compare("help", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                PrintHelp();
                return null;
            }
            else if (string.Compare("exit", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                stillGoing_ = false;
                return null;
            }

            return new DatabaseRequest { MessageType = DatabaseRequestType.Invalid };
        }

        private static string? DecodeResponse(DatabaseResponse? response)
        {
            switch (response?.MessageType)
            {
                case DatabaseResponseType.Invalid:
                    return "invalid";
                case DatabaseResponseType.Ping:
                    return "ping success";
                case DatabaseResponseType.NoBytesReceived:
                    return "no bytes received";
                case DatabaseResponseType.CouldNotProcessCommand:
                    return "could not process command";
                case DatabaseResponseType.Started:
                    return "successfully started";
                case DatabaseResponseType.Stopped:
                    return "successfully stopped";
                case DatabaseResponseType.Success:
                    return DecodeSuccess(response);
                case DatabaseResponseType.KeyNotFound:
                    return "key not found";
                case DatabaseResponseType.QuorumFailed:
                    return "quorum failed";
            }

            return null;
        }

        private static string? DecodeSuccess(DatabaseResponse response)
        {
            if (response.Bytes?.Length == 1)
            {
                return "successful";
            }

            ReadOnlySpan<byte> value = response.Bytes.AsSpan().Slice(1);
            return Encoding.UTF8.GetString(value);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("ping");
            Console.WriteLine("stop");
            Console.WriteLine("start");
            Console.WriteLine("read");
            Console.WriteLine("write");
            Console.WriteLine("getstate");
            Console.WriteLine("exit");
        }
    }
}
