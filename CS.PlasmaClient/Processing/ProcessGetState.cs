using CS.PlasmaLibrary;

namespace CS.PlasmaClient.Processing
{
    internal class ProcessGetState : IDatabaseClientProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.GetState;

        public DatabaseResponse? Process(Client client, DatabaseRequest request)
        {
            byte[]? requestData = request.Bytes;
            byte[]? responseData = client.Request(requestData);
            if (responseData is not null)
            {
                if (responseData.Length == 1 + Constant.SlotCount * 2)
                {
                    for (int index = 0; index < Constant.SlotCount; index++)
                    {
                        client.State.Slots[index].ServerNumber = responseData[1 + index * 2];
                        client.State.Slots[index].VersionNumber = responseData[2 + index * 2];
                    }
                    return new DatabaseResponse { MessageType = DatabaseResponseType.Success };
                }
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Invalid };
        }
    }
}
