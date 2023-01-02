using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessStop : IDatabaseProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Stop;

        public DatabaseResponse Process(DatabaseRequest request)
        {
            DatabaseResponse response = new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.Stopped };
            return response;
        }
    }
}
