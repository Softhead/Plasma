namespace CS.PlasmaLibrary
{
    public class ErrorMessage
    {
        public static string ContextualMessage { get; set; } = string.Empty;

        public const string Success = "Success";
        public const string AlreadyStarted = "The database engine is already running.";
        public const string InvalidConfiguration = "There is an error in the database configuration.";
        public const string ConfigNoEquals = "No equals in configuration.";
        public const string ConfigNoKey = "No key in configuration.";
        public const string ConfigUnrecognizedKey = "Unrecognized key in configuration.";

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
