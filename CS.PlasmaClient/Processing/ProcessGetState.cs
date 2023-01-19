using CS.PlasmaLibrary;
using System.Net;
using System.Net.Quic;
using System.Net.Security;

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
                || client.Definition.IpAddress is null)
            {
                return null;
            }

            CancellationToken token = new CancellationToken();
            int serverNumber = 0;
            int port = client.ServerPortDictionary[serverNumber];
            IPEndPoint endpoint = new IPEndPoint(client.Definition.IpAddress, port);
            QuicConnection conn = await QuicConnection.ConnectAsync(
                new QuicClientConnectionOptions 
                {
                    RemoteEndPoint = endpoint,
                    DefaultCloseErrorCode = 0x0a,
                    DefaultStreamErrorCode = 0x0b,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions { ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 } },
                    MaxInboundUnidirectionalStreams = 10,
                    MaxInboundBidirectionalStreams = 100
                }, token);
            QuicStream stream = await conn.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);

            byte[] buffer = new byte[request.Bytes.Length + 4];
            BitConverter.GetBytes(request.Bytes.Length).CopyTo(buffer, 0);
            request.Bytes.CopyTo(buffer, 4);
            await stream.WriteAsync(buffer, 0, buffer.Length, token);
            stream.CompleteWrites();

            buffer = new byte[4];
            await stream.ReadAsync(buffer, 0, buffer.Length, token);

            int length = BitConverter.ToInt32(buffer, 0);
            buffer = new byte[length];
            int received = 0;
            bool stillGoing = true;
            while (stillGoing)
            {
                int currentReceived = await stream.ReadAsync(buffer, received, length - received, token);
                received += currentReceived;
                if (received == length)
                {
                    stillGoing = false;
                }
            }

            stream.Close();
            conn.CloseAsync(0x0c);
            conn.DisposeAsync();

            if (length == 1 + Constant.SlotCount * 2)
            {
                for (int index = 0; index < Constant.SlotCount; index++)
                {
                    client.State.Slots[index].ServerNumber = buffer[1 + index * 2];
                    client.State.Slots[index].VersionNumber = buffer[2 + index * 2];
                }
                return new DatabaseResponse { MessageType = DatabaseResponseType.Success };
            }

            return new DatabaseResponse { MessageType = DatabaseResponseType.Invalid };
        }
    }
}
