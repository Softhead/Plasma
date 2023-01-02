using System.Net;
using CS.PlasmaClient;
using CS.PlasmaLibrary;

namespace CS.PlasmaCommandLineClient
{
    internal class Program
    {
        // usage: PlasmaCommandLineClient -server <server IP address> -port <server UDP port>
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("usage: PlasmaCommandLineClient -server <server IP address> -port <server UDP port>");
                return;
            }
            Console.WriteLine("For help, use command 'help'.\n");

            IPAddress address = IPAddress.Parse(args[1]);
            int port = int.Parse(args[3]);

            DatabaseDefinition definition = new DatabaseDefinition { IpAddress = address, UdpPort = port };

            using (Client client = new Client(definition))
            {
                while (true)
                {
                    Console.Write("Enter command: ");
                    string? command = Console.ReadLine()?.Trim();

                    if (string.Compare("help", command, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        PrintHelp();
                    }
                    else if (string.Compare("exit", command, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        break;
                    }
                    else
                    {
                        DatabaseRequest? request = DecodeCommand(command);
                        if (request is not null)
                        {
                            DatabaseResponse response = client.Request(request);
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
                return new DatabaseRequest { DatabaseRequestType = DatabaseRequestType.Ping };
            }
            else if (string.Compare("start", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { DatabaseRequestType = DatabaseRequestType.Start };
            }
            else if (string.Compare("stop", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new DatabaseRequest { DatabaseRequestType = DatabaseRequestType.Stop };
            }

            return null;
        }

        private static string? DecodeResponse(DatabaseResponse response)
        {
            switch (response.DatabaseResponseType)
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
            }

            return null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("ping");
            Console.WriteLine("stop");
            Console.WriteLine("start");
        }
    }
}
