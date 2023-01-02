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
            if (args.Length != 5)
            {
                Console.WriteLine("usage: PlasmaCommandLineClient -server <server IP address> -port <server UDP port>");
                return;
            }

            IPAddress address = IPAddress.Parse(args[2]);
            int port = int.Parse(args[4]);

            DatabaseDefinition definition = new DatabaseDefinition { IpAddress = address, UdpPort = port };

            using (Client client = new Client(definition))
            {
                Console.WriteLine("Enter command: ");
                string command = Console.ReadLine().Trim();

                DatabaseRequest request = new DatabaseRequest();
                if (string.Compare("ping", command, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    request.DatabaseRequestType = DatabaseRequestType.Ping;
                }

                client.Request(request);
            }
        }
    }
}