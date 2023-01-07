using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessRead : IDatabaseProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Read;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            ReadOnlySpan<byte> key = request.Bytes.AsSpan().Slice(1);
            ReadOnlySpan<byte> data = engine.Read(key);
            return new DatabaseResponse { Bytes = data.ToArray() };
        }
    }
}
