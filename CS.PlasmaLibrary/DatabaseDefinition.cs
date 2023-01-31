using System.Net;

namespace CS.PlasmaLibrary
{
    public class DatabaseDefinition
    {
        public int ServerCount { get; set; }  // number of servers
        public int ServerCopyCount { get; set; }  // number of copies of data, 1 to 8;  2 or more creates redundancy
        public int ServerCommitCount { get; set; }  // number of commits, 1 to NumberOfCopy;  defines the quorum count for the server to assume a commit
        public int SlotPushPeriod { get; set; }  // milliseconds before scheduling a slot data push, if SlotPushTriggerCount has not been met
        public int SlotPushTriggerCount { get; set; }  // number of slot changes that trigger a slot data push
        public int ClientQueryCount { get; set; }  // number of servers for the client to query, 1 to ServerCopyCount
        public int ClientCommitCount { get; set; }  // number of commits, 1 to ClientQueryCount;  defines the quorum count for the client to assume a commit
        public int ServerCommitPeriod { get; set; }  // milliseconds before scheduling a commit reconciliation
        public int ServerCommitTriggerCount { get; set; }  // number of commits that trigger a commit reconciliation
        public IPAddress? IpAddress { get; set; }  // IP address to bind to

        public ErrorNumber LoadConfiguration(StreamReader definitionStream)
        {
            string? line = definitionStream.ReadLine();

            while (line is not null)
            {
                ErrorNumber result = SetDefinition(line);
                if (result != ErrorNumber.Success)
                {
                    return result;
                }

                line = definitionStream.ReadLine();
            }

            return ErrorNumber.Success;
        }

        private ErrorNumber SetDefinition(string line)
        {
            int split = line.IndexOf('=');

            if (split == -1)
            {
                ErrorMessage.ContextualMessage = "No '=' found on line: " + line;
                return ErrorNumber.ConfigNoEquals;
            }

            if (split == 0)
            {
                ErrorMessage.ContextualMessage = "No key found to left of '=' on line: " + line;
                return ErrorNumber.ConfigNoKey;
            }

            string key = line.Substring(0, split).Trim();
            string value = line.Substring(split + 1).Trim();

            if (!Enum.TryParse(typeof(DatabaseDefinitionKey), key, out object? definitionKey))
            {
                ErrorMessage.ContextualMessage = "Unrecognized key: " + key;
                return ErrorNumber.ConfigUnrecognizedKey;
            }

            switch ((DatabaseDefinitionKey)definitionKey!)
            {
                case DatabaseDefinitionKey.ServerCount:
                    ServerCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCopyCount:
                    ServerCopyCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitCount:
                    ServerCommitCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.SlotPushPeriod:
                    SlotPushPeriod = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.SlotPushTriggerCount:
                    SlotPushTriggerCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ClientQueryCount:
                    ClientQueryCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ClientCommitCount:
                    ClientCommitCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitPeriod:
                    ServerCommitPeriod = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitTriggerCount:
                    ServerCommitTriggerCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.IpAddress:
                    IpAddress = IPAddress.Parse(value);
                    break;
            }

            return ErrorNumber.Success;
        }
    }
}
