using CS.PlasmaClient;
using CS.PlasmaLibrary;

namespace CS.PlasmaCommandLineClient
{
    internal class Program
    {
        private static bool stillGoing_ = true;

        // usage: PlasmaCommandLineClient <config file>
        static async Task Main(string[] args)
        {
            Logger.Sinks.Add(new LoggerSinkFile(@"c:\tmp\PlasmaClient.log"));

            if (args.Length != 1)
            {
                Logger.Log("usage: PlasmaCommandLineClient <config file>");
                return;
            }
            Logger.Log("For help, use command 'help'.\n");

            CancellationTokenSource source = new();
            StreamReader definitionStream = File.OpenText(args[0]);

            using (Client client = await ClientHelper.StartClientAsync(source.Token, definitionStream, true))
            {
                // wait until client is ready
                while (!client.IsReady)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), source.Token);
                }

                while (stillGoing_)
                {
                    await Logger.WaitForQueues();
                    Logger.Log("Enter command: ");
                    string? command = Console.ReadLine()?.Trim();

                    DatabaseRequest? request = DecodeCommand(command);
                    if (request is not null)
                    {
                        if (request.MessageType != DatabaseRequestType.Invalid)
                        {
                            DatabaseResponse? response = await client.ProcessRequestAsync(request);
                            Logger.Log($"Response: {DecodeResponse(response)}");
                        }
                        else
                        {
                            Logger.Log($"The command '{command}' is not a valid command.");
                        }
                    }
                }
            }
        }

        private static DatabaseRequest? DecodeCommand(string? command)
        {
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
                return DatabaseRequestHelper.ReadRequest(key);
            }
            else if (string.Compare("write", command, StringComparison.OrdinalIgnoreCase) == 0)
            {
                Console.Write("    Enter key: ");
                string? key = Console.ReadLine()?.Trim();
                Console.Write("    Enter value: ");
                string? value = Console.ReadLine()?.Trim();
                return DatabaseRequestHelper.WriteRequest(key, value);
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

            return response.ReadValue();
        }

        private static void PrintHelp()
        {
            Logger.Log("Commands:");
            Logger.Log("ping");
            Logger.Log("stop");
            Logger.Log("start");
            Logger.Log("read");
            Logger.Log("write");
            Logger.Log("getstate");
            Logger.Log("exit");
        }
    }
}
