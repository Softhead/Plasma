using CS.PlasmaLibrary;
using System.Net;
using System.Net.Sockets;

namespace CS.PlasmaClient
{
    public class Client : IDisposable
    {
        private DatabaseDefinition definition_ = null;
        private IPEndPoint endPoint_ = null;

        public Client(DatabaseDefinition definition)
        {
            definition_ = definition;
        }

        public void Dispose()
        {
        }

        public DatabaseResponse Request(DatabaseRequest request)
        {
            byte[] requestData = request.Bytes;
            byte[] responseData = Request(requestData);
            if (responseData is not null)
            {
                return new DatabaseResponse { Bytes = responseData };
            }

            return new DatabaseResponse { DatabaseResponseType = DatabaseResponseType.Invalid };
        }

        public byte[] Request(byte[] data)
        {
            if (endPoint_ is null)
            {
                endPoint_ = new IPEndPoint(definition_.IpAddress, definition_.UdpPort);
            }

            UdpClient client = new UdpClient();
            client.Connect(endPoint_);
            client.Send(data, data.Length);

            data = client.Receive(ref endPoint_);

            return data;
        }
    }
}
