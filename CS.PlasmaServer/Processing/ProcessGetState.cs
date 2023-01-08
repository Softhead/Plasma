using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    internal class ProcessGetState : IDatabaseServerProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.GetState;

        public DatabaseResponse? Process(Engine engine, DatabaseRequest request)
        {
            Span<byte> result = new byte[Constant.SlotCount * 2 + 1].AsSpan();
            result[0] = (byte)DatabaseResponseType.Success;

            for (int index = 0; index < Constant.SlotCount; index++)
            {
                result[index * 2 + 1] = engine.State!.Slots[index].ServerNumber;
                result[index * 2 + 2] = engine.State!.Slots[index].VersionNumber;
            }
            return new DatabaseResponse { Bytes = result.ToArray() };
        }
    }
}
