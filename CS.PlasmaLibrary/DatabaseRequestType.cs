﻿namespace CS.PlasmaLibrary
{
    public enum DatabaseRequestType
    {
        Invalid = 0,
        Ping = 1,
        Start = 2,
        Stop = 3,
        Read = 4,
        Write = 5,
        GetState = 6,  // get entire database state
    }
}
