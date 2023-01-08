using CS.PlasmaLibrary;

namespace CS.PlasmaClient
{
    internal interface IDatabaseClientProcess
    {
        public DatabaseRequestType DatabaseRequestType { get; }
        public DatabaseResponse? Process(Client client, DatabaseRequest request);
    }
}
