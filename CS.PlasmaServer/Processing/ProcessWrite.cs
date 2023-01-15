using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessWrite : IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Write;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            if (request.Bytes is null || request.Bytes.Length < 2)
            {
                return null;
            }

            ReadOnlySpan<byte> bytes = request.Bytes.AsSpan().Slice(1);
            int indexComma = bytes.IndexOf(Constant.Delimiter);

            if (indexComma == -1)
            {
                return null;
            }

            ReadOnlySpan<byte> key = bytes.Slice(0, indexComma);
            ReadOnlySpan<byte> value = bytes.Slice(indexComma + 1);
            byte[] keyArray = key.ToArray();
            byte[] valueArray = value.ToArray();
            if (engine.Dictionary!.ContainsKey(keyArray))
            {
                engine.Dictionary[keyArray] = valueArray;
            }
            else
            {
                engine.Dictionary.Add(keyArray, valueArray);
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Success };
        }
    }
}
