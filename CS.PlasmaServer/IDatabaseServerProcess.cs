using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal interface IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType { get; }
        public DatabaseResponse? Process(Engine engine, DatabaseRequest request);
    }
}
