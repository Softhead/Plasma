using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessStop : IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Stop;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            engine.Stop();
            return new DatabaseResponse { MessageType = DatabaseResponseType.Stopped };
        }
    }
}
