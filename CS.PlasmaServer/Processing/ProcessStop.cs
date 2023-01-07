using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessStop : IDatabaseProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Stop;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            engine.Stop();
            return new DatabaseResponse { MessageType = DatabaseResponseType.Stopped };
        }
    }
}
