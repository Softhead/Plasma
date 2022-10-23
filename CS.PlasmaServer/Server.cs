using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    public class Server
    {
        private DatabaseDefinition definition_ = null;

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

            s.Close();
            return ErrorNumber.Success;
        }

        public ErrorNumber Start(string definitionFileName)
        {
            if (definition_ != null)
                return ErrorNumber.AlreadyStarted;

            definition_ = new DatabaseDefinition();

            StreamReader s = File.OpenText(definitionFileName);

            string? line = s.ReadLine();

            while (line != null)
            {
                ErrorNumber result = SetDefinition(line);
                if (result != ErrorNumber.Success)
                {
                    definition_ = null;
                    return result;
                }

                line = s.ReadLine();
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
            }

            return ErrorNumber.Success;
        }

        public static string GetErrorText(ErrorNumber errorNumber)
        {
            switch (errorNumber)
            {
                case ErrorNumber.Success:
                    return ErrorMessage.Success;
                case ErrorNumber.AlreadyStarted:
                    return ErrorMessage.AlreadyStarted;
                case ErrorNumber.InvalidConfiguration:
                    return ErrorMessage.InvalidConfiguration;
                case ErrorNumber.ConfigNoEquals:
                    return ErrorMessage.ConfigNoEquals;
                case ErrorNumber.ConfigNoKey:
                    return ErrorMessage.ConfigNoKey;
                case ErrorNumber.ConfigUnrecognizedKey:
                    return ErrorMessage.ConfigUnrecognizedKey;
            }

            return $"Unrecognized error number: {(int)errorNumber}";
        }
    }
}
