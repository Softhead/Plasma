using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessPing : IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Ping;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            return new DatabaseResponse { MessageType = DatabaseResponseType.Ping };
        }
    }
}
