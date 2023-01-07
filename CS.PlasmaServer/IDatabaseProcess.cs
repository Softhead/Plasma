using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal interface IDatabaseProcess
    {
        public DatabaseRequestType DatabaseRequestType { get; }
        public DatabaseResponse? Process(Engine engine, DatabaseRequest request);
    }
}
