namespace CS.PlasmaLibrary
{
    public class ErrorMessage
    {
        public static string ContextualMessage { get; set; } = string.Empty;

        public const string Success = "Success";
        public const string AlreadyStarted = "The database engine is already running.";
        public const string InvalidConfiguration = "There is an error in the database configuration.";
        public const string CopyNumberOutOfRange = "The copy number is out of range.";
        public const string ConfigUnrecognizedKey = "Unrecognized key in configuration.";
        public const string ConfigNoEquals = "No equals in configuration.";
        public const string ConfigNoKey = "No key in configuration.";
        public const string DefinitionNotSet = "The database definition is not set.";
        public const string CannotGetState = "Could not get the client state from the server.";

        public static string GetErrorText(ErrorNumber errorNumber)
        {
            switch (errorNumber)
            {
                case ErrorNumber.Success:
                    return Success;
                case ErrorNumber.AlreadyStarted:
                    return AlreadyStarted;
                case ErrorNumber.InvalidConfiguration:
                    return InvalidConfiguration;
                case ErrorNumber.CopyNumberOutOfRange:
                    return CopyNumberOutOfRange;
                case ErrorNumber.ConfigNoEquals:
                    return ConfigNoEquals;
                case ErrorNumber.ConfigNoKey:
                    return ConfigNoKey;
                case ErrorNumber.ConfigUnrecognizedKey:
                    return ConfigUnrecognizedKey;
                case ErrorNumber.DefinitionNotSet:
                    return DefinitionNotSet;
                case ErrorNumber.CannotGetState:
                    return CannotGetState;
            }

            return $"Unrecognized error number: {(int)errorNumber}";
        }
    }
}
