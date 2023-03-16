using CS.PlasmaLibrary;
using CS.PlasmaServer;
using System.Net;

namespace CS.PlasmaMain
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Logger.Sinks.Add(new LoggerSinkFile(@"c:\tmp\PlasmaServer.log"));
            Logger.LoggingLevel = LoggingLevel.Info;

            CancellationTokenSource source = new CancellationTokenSource();
            Logger.Log("Plasma Server");

            if (args.Length < 1)
            {
                Logger.Log("Must specify filename for server configuration, or \"create\" to create a new server.");
                Logger.Log("Aborting.");
                return 0;
            }

            if (string.Compare(args[0], "create", StringComparison.OrdinalIgnoreCase) == 0)
            {
                DatabaseDefinition definition = new DatabaseDefinition();
                string? fileName = null;

                Logger.Log("Create a new server.\n");
                Logger.Log("Enter configuration parameters:");

                Logger.Log("# of data copies (1-8): ");
                definition.ServerCopyCount = ReadInt();

                while (definition.ServerCommitCount < 1 || definition.ServerCommitCount > definition.ServerCopyCount)
                {
                    Logger.Log($"Quorum count for server to assume a commit (1-{definition.ServerCopyCount}): ");
                    definition.ServerCommitCount = ReadInt();
                }

                Logger.Log("Milliseconds before scheduling a slot push: ");
                definition.SlotPushPeriod = ReadInt();

                Logger.Log("# of slot changes that trigger a slot data push: ");
                definition.SlotPushTriggerCount = ReadInt();

                while (definition.ClientQueryCount < 1 || definition.ClientQueryCount > definition.ServerCopyCount)
                {
                    Logger.Log($"Number of servers for the client to query (1-{definition.ServerCopyCount}): ");
                    definition.ClientQueryCount = ReadInt();
                }

                while (definition.ClientCommitCount < 1 || definition.ClientCommitCount > definition.ClientQueryCount)
                {
                    Logger.Log($"Quorum count for the client to assume a commit (1-{definition.ClientQueryCount}): ");
                    definition.ClientCommitCount = ReadInt();
                }

                Logger.Log("Milliseconds before scheduling a commit reconciliation: ");
                definition.ServerCommitPeriod = ReadInt();

                Logger.Log("# of commits that trigger a commit reconciliation: ");
                definition.ServerCommitTriggerCount = ReadInt();

                Logger.Log("IP address to bind to: ");
                definition.IpAddress = ReadIpAddress();

                Logger.Log("File name to save server config file: ");
                while (fileName is null || string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = Console.ReadLine();
                }

                Server server = new Server();
                ErrorNumber response = server.CreateNew(definition, fileName);

                if (response == ErrorNumber.Success)
                {
                    Logger.Log($"Successfully created server configuration file: {fileName}");
                }
                else
                {
                    Logger.Log($"Error creating server configuation: {ErrorMessage.GetErrorText(response)}");
                }
                return (int)response;
            }
            else
            {
                Logger.Log($"Starting server with configuration file: {args[0]}");

                StreamReader definitionStream = File.OpenText(args[0]);
                Server[] servers = ServerHelper.StartServers(source.Token, definitionStream);

                // wait for at least one server to start
                while (!servers.Where(o => o.IsRunning is not null).Any(o => (bool)o.IsRunning!))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), source.Token);
                }

                // wait for all the servers to end
                while (!servers.Where(o => o.IsRunning is not null).All(o => (bool)!o.IsRunning!))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), source.Token);
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
