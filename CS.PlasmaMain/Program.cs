using CS.PlasmaLibrary;
using CS.PlasmaServer;

namespace CS.PlasmaMain
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Plasma Server");

            if (args.Length < 2)
            {
                Console.WriteLine("Must specify filename for server configuration, or \"create\" to create a new server.");
                Console.WriteLine("Aborting.");
                return 0;
            }

            if (string.Compare(args[1], "create", StringComparison.OrdinalIgnoreCase) == 0)
            {
                DatabaseDefinition definition = new DatabaseDefinition();
                string fileName = null;

                Console.WriteLine("Create a new server.\n");
                Console.WriteLine("Enter configuration parameters:");

                Console.WriteLine("# of redundant copies (1-8): ");
                int number;
                string value = Console.ReadLine();
                while (!int.TryParse(value, out number)) ;
                definition.ServerCopyCount = number;

                while (definition.ServerCommitCount < 1 || definition.ServerCommitCount > definition.ServerCopyCount)
                {
                    Console.WriteLine($"Quorum count for server to assume a commit (1-{definition.ServerCopyCount}): ");
                    value = Console.ReadLine();
                    while (!int.TryParse(value, out number)) ;
                    definition.ServerCommitCount = number;
                }

                Console.WriteLine("Milliseconds before scheduling a slot push: ");
                value = Console.ReadLine();
                while (!int.TryParse(value, out number)) ;
                definition.SlotPushPeriod = number;

                Console.WriteLine("# of slot changes that trigger a slot data push: ");
                value = Console.ReadLine();
                while (!int.TryParse(value, out number)) ;
                definition.SlotPushTriggerCount = number;

                while (definition.ClientQueryCount < 1 || definition.ClientQueryCount > definition.ServerCopyCount)
                {
                    Console.WriteLine($"Number of servers for the client to query (1-{definition.ServerCopyCount}): ");
                    value = Console.ReadLine();
                    while (!int.TryParse(value, out number)) ;
                    definition.ClientQueryCount = number;
                }

                while (definition.ClientCommitCount < 1 || definition.ClientCommitCount > definition.ClientQueryCount)
                {
                    Console.WriteLine($"Quorum count for the client to assume a commit (1-{definition.ClientQueryCount}): ");
                    value = Console.ReadLine();
                    while (!int.TryParse(value, out number)) ;
                    definition.ClientCommitCount = number;
                }

                Console.WriteLine("Milliseconds before scheduling a commit reconciliation: ");
                value = Console.ReadLine();
                while (!int.TryParse(value, out number)) ;
                definition.ServerCommitPeriod = number;

                Console.WriteLine("# of commits that trigger a commit reconciliation: ");
                value = Console.ReadLine();
                while (!int.TryParse(value, out number)) ;
                definition.ServerCommitTriggerCount = number;

                Console.WriteLine("File name to save server config file: ");
                while (fileName == null || string.IsNullOrWhiteSpace(fileName))
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
                    Console.WriteLine($"Error creating server configuation: {Server.GetErrorText(response)}");
                }
                return (int)response;
            }
            else
            {
                Console.WriteLine($"Starting server with configuration file: {args[1]}");

                Server server = new Server();
                server.Start(args[1]);

                return 0;
            }
        }
    }
}
