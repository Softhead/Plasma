using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessPing : IDatabaseProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Ping;

        public DatabaseResponse Process(DatabaseRequest request)
        {
            DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.Ping };
            return response;
        }
    }
}
