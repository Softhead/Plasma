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
    }
}
