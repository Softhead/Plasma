using CS.PlasmaLibrary;
using CS.PlasmaServer;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Text;

namespace CS.PlasmaMain
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Plasma Server");

            if (args.Length < 1)
            {
                Console.WriteLine("Must specify filename for server configuration, or \"create\" to create a new server.");
                Console.WriteLine("Aborting.");
                return 0;
            }

            if (string.Compare(args[0], "create", StringComparison.OrdinalIgnoreCase) == 0)
            {
                DatabaseDefinition definition = new DatabaseDefinition();
                string? fileName = null;

                Console.WriteLine("Create a new server.\n");
                Console.WriteLine("Enter configuration parameters:");

                Console.WriteLine("# of data copies (1-8): ");
                definition.ServerCopyCount = ReadInt();

                while (definition.ServerCommitCount < 1 || definition.ServerCommitCount > definition.ServerCopyCount)
                {
                    Console.WriteLine($"Quorum count for server to assume a commit (1-{definition.ServerCopyCount}): ");
                    definition.ServerCommitCount = ReadInt();
                }

                Console.WriteLine("Milliseconds before scheduling a slot push: ");
                definition.SlotPushPeriod = ReadInt();

                Console.WriteLine("# of slot changes that trigger a slot data push: ");
                definition.SlotPushTriggerCount = ReadInt();

                while (definition.ClientQueryCount < 1 || definition.ClientQueryCount > definition.ServerCopyCount)
                {
                    Console.WriteLine($"Number of servers for the client to query (1-{definition.ServerCopyCount}): ");
                    definition.ClientQueryCount = ReadInt();
                }

                while (definition.ClientCommitCount < 1 || definition.ClientCommitCount > definition.ClientQueryCount)
                {
                    Console.WriteLine($"Quorum count for the client to assume a commit (1-{definition.ClientQueryCount}): ");
                    definition.ClientCommitCount = ReadInt();
                }

                Console.WriteLine("Milliseconds before scheduling a commit reconciliation: ");
                definition.ServerCommitPeriod = ReadInt();

                Console.WriteLine("# of commits that trigger a commit reconciliation: ");
                definition.ServerCommitTriggerCount = ReadInt();

                Console.WriteLine("UDP port to bind to: ");
                definition.UdpPort = ReadInt();

                Console.WriteLine("IP address to bind to: ");
                definition.IpAddress = ReadIpAddress();

                Console.WriteLine("File name to save server config file: ");
                while (fileName is null || string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = Console.ReadLine();
                }

                Server server = new Server();
                ErrorNumber response = server.CreateNew(definition, fileName);

                if (response == ErrorNumber.Success)
                {
                    Console.WriteLine($"Successfully created server configuration file: {fileName}");
                }
                else
                {
                    Console.WriteLine($"Error creating server configuation: {ErrorMessage.GetErrorText(response)}");
                }
                return (int)response;
            }
            else
            {
                Console.WriteLine($"Starting server with configuration file: {args[0]}");

                // IPC to the client
                var message = new byte[20];
                var messageWait = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_wait");
                var messageHandled = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_handled");
                var mmf = MemoryMappedFile.CreateOrOpen("Plasma_mmf", message.Length);
                var viewStream = mmf.CreateViewStream();


                // start first server
                Server server = new Server();
                server.Start(0, args[0]);

                DatabaseState state = new DatabaseState(server.Definition);
                state.SetupInitialSlots();
                server.State = state;

                Server[] servers = new Server[server.Definition!.ServerCount];
                servers[0] = server;

                // start the rest of the servers
                for (int index = 1; index < server.Definition!.ServerCount; index++)
                {
                    DatabaseDefinition definition = new DatabaseDefinition();
                    definition.LoadConfiguration(args[0]);
                    definition.UdpPort += index;
                    servers[index] = new Server { Definition = definition };
                    servers[index].Start(index);
                    servers[index].State = state;
                }

                // set up IPC for server port information outbound communication
                for (int index = 0; index < server.Definition!.ServerCount; index++)
                {
                    Server serverLocal = servers[index];
                    Task.Run(() =>
                    {
                        serverLocal.PortNumberEvent.WaitOne();
                        Console.WriteLine($"Server: {serverLocal.ServerNumber} Port: {serverLocal.PortNumber}");

                        messageHandled.WaitOne();
                        string messageString = $"{serverLocal.ServerNumber} {serverLocal.PortNumber}";
                        Encoding.UTF8.GetBytes(messageString).CopyTo(message.AsSpan());
                        viewStream.Position = 0;
                        viewStream.Write(message, 0, message.Length);
                        messageWait.Set();
                    });
                }

                CancellationTokenSource source = new();

                // wait for at least one server to start
                while (servers.Where(o => o.IsRunning is not null).Any(o => (bool)o.IsRunning!))
                {
                    source.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }

                // wait for all the servers to end
                while (servers.Where(o => o.IsRunning is not null).All(o => (bool)!o.IsRunning!))
                {
                    source.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }

                return 0;
            }
        }

        private static int ReadInt()
        {
            int number;
            string? value = Console.ReadLine();
            while (!int.TryParse(value, out number))
            {
                value = Console.ReadLine();
            }
            return number;
        }

        private static IPAddress ReadIpAddress()
        {
            IPAddress? address;
            string? value = Console.ReadLine();
            while (!IPAddress.TryParse(value, out address))
            {
                value = Console.ReadLine();
            }
            return address;
        }
    }
}
