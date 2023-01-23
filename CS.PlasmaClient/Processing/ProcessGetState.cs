using CS.PlasmaLibrary;

namespace CS.PlasmaClient
{
    internal class ProcessGetState : IDatabaseClientProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.GetState;

        public async Task<DatabaseResponse?> ProcessAsync(Client client, DatabaseRequest request)
        {
            if (client.State is null)
            {
                client.State = new DatabaseState(client.Definition);
            }

            if (client is null
                || client.Definition is null
                || client.Definition.IpAddress is null
                || request.Bytes is null)
            {
                return null;
            }

            byte[]? buffer = client.SendRequest(request, 1);
            if (buffer is not null)
            {
                if (buffer.Length == 1 + Constant.SlotCount * 2)
                {
                    for (int index = 0; index < Constant.SlotCount; index++)
                    {
                        client.State.Slots[index].ServerNumber = buffer[1 + index * 2];
                        client.State.Slots[index].VersionNumber = buffer[2 + index * 2];
                    }
                    return new DatabaseResponse { MessageType = DatabaseResponseType.Success };
                }
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Invalid };
        }
    }
}
