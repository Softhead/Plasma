using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    public class Server
    {
        private DatabaseDefinition definition_ = null;
        private Engine engine_ = null;

        public ErrorNumber CreateNew(DatabaseDefinition definition, string definitionFileName)
        {
            StreamWriter s = File.CreateText(definitionFileName);

            s.WriteLine($"{DatabaseDefinitionKey.ServerCopyCount}={definition.ServerCopyCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitCount}={definition.ServerCommitCount}");
            s.WriteLine($"{DatabaseDefinitionKey.SlotPushPeriod}={definition.SlotPushPeriod}");
            s.WriteLine($"{DatabaseDefinitionKey.SlotPushTriggerCount}={definition.SlotPushTriggerCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ClientQueryCount}={definition.ClientQueryCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ClientCommitCount}={definition.ClientCommitCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitPeriod}={definition.ServerCommitPeriod}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitTriggerCount}={definition.ServerCommitTriggerCount}");
            s.WriteLine($"{DatabaseDefinitionKey.UdpPort}={definition.UdpPort}");

            s.Close();
            return ErrorNumber.Success;
        }

        public ErrorNumber Start(string definitionFileName)
        {
            if (definition_ is not null
                && engine_ is not null)
                return ErrorNumber.AlreadyStarted;

            if (definition_ is null)
            {
                definition_ = new DatabaseDefinition();

                StreamReader s = File.OpenText(definitionFileName);

                string? line = s.ReadLine();

                while (line is not null)
                {
                    ErrorNumber result = SetDefinition(line);
                    if (result != ErrorNumber.Success)
                    {
                        definition_ = null;
                        return result;
                    }

                    line = s.ReadLine();
                }
            }

            engine_ = new Engine(definition_);
            engine_.Start();

            return ErrorNumber.Success;
        }

        public void Stop()
        {
            engine_.Stop();
            engine_ = null;
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

            string key = line.Substring(0, split - 1).Trim();
            string value = line.Substring(split).Trim();

            if (!Enum.TryParse(typeof(DatabaseDefinitionKey), key, out object? definitionKey))
            {
                ErrorMessage.ContextualMessage = "Unrecognized key: " + key;
                return ErrorNumber.ConfigUnrecognizedKey;
            }

            switch ((DatabaseDefinitionKey)definitionKey)
            {
                case DatabaseDefinitionKey.ServerCopyCount:
                    definition_.ServerCopyCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitCount:
                    definition_.ServerCommitCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.SlotPushPeriod:
                    definition_.SlotPushPeriod = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.SlotPushTriggerCount:
                    definition_.SlotPushTriggerCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ClientQueryCount:
                    definition_.ClientQueryCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ClientCommitCount:
                    definition_.ClientCommitCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitPeriod:
                    definition_.ServerCommitPeriod = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.ServerCommitTriggerCount:
                    definition_.ServerCommitTriggerCount = int.Parse(value);
                    break;

                case DatabaseDefinitionKey.UdpPort:
                    definition_.UdpPort = int.Parse(value);
                    break;
            }

            return ErrorNumber.Success;
        }
    }
}
