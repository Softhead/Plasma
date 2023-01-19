using CS.PlasmaLibrary;

namespace CS.PlasmaClient
{
    internal interface IDatabaseClientProcess
    {
        public DatabaseRequestType DatabaseRequestType { get; }
        public Task<DatabaseResponse?> ProcessAsync(Client client, DatabaseRequest request);
    }
}
