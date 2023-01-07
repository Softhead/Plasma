namespace CS.PlasmaLibrary
{
    public enum DatabaseResponseType
    {
        Invalid = 0,
        Ping = 1,
        NoBytesReceived = 2,
        CouldNotProcessCommand = 3,
        Started = 4,
        Stopped = 5,
        Success = 6,
        KeyNotFound = 7,
    }
}
