using CS.PlasmaLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS.PlasmaServer
{
    internal class ProcessWrite : IDatabaseProcess
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
            ReadOnlySpan<byte> data = engine.Write(key, value);
            return new DatabaseResponse { Bytes = data.ToArray() };
        }
    }
}
