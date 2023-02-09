﻿namespace CS.PlasmaLibrary
{
    public interface ILoggerSink
    {
        public void Write(string message);
        public Task WaitForQueue();
    }
}
