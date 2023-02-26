namespace CS.PlasmaLibrary
{
    public enum ErrorNumber
    {
        Success = 0,
        AlreadyStarted = 1,
        InvalidConfiguration = 2,
        CopyNumberOutOfRange = 3,
        ConfigUnrecognizedKey = 4,
        ConfigNoEquals = 5,
        ConfigNoKey = 6,
        ConfigFileMissing = 7,
        DefinitionNotSet = 8,
        CannotGetState = 9
    }
}
