using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessRead : IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Read;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            ReadOnlySpan<byte> key = request.Bytes.AsSpan().Slice(1);
            byte[] keyArray = key.ToArray();
            byte[]? value;
            if (engine.Dictionary!.TryGetValue(keyArray, out value))
            {
                Span<byte> result = new byte[value.Length + 1].AsSpan();
                result[0] = (byte)DatabaseResponseType.Success;
                value.CopyTo(result.Slice(1));
                return new DatabaseResponse { Bytes = result.ToArray() };
            }
            else
            {
                return new DatabaseResponse { MessageType = DatabaseResponseType.KeyNotFound };
            }
        }
    }
}
