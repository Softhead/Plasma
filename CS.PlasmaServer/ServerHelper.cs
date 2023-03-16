using CS.PlasmaLibrary;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace CS.PlasmaServer
{
    public class ServerHelper
    {
        public static Server[] StartServers(CancellationToken token, StreamReader definitionStream)
        {
            // IPC to the client
            var message = new byte[20];
            var messageWait = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_wait");
            var messageHandled = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_handled");
            var mmf = MemoryMappedFile.CreateOrOpen("Plasma_mmf", message.Length);
            var viewStream = mmf.CreateViewStream();

            // start first server
            Server server = new Server();
            server.Start(0, definitionStream);

            DatabaseState state = new DatabaseState(server.Definition);
            state.SetupInitialSlots();
            server.State = state;

            Server[] servers = new Server[server.Definition!.ServerCount];
            servers[0] = server;

            // start the rest of the servers
            for (int index = 1; index < server.Definition!.ServerCount; index++)
            {
                DatabaseDefinition definition = new DatabaseDefinition();
                definitionStream.BaseStream.Position = 0;
                definition.LoadConfiguration(definitionStream);
                servers[index] = new Server { Definition = definition };
                servers[index].Start(index);
                servers[index].State = state;
            }

            // set up IPC for server port information outbound communication
            for (int index = 0; index < server.Definition!.ServerCount; index++)
            {
                Server serverLocal = servers[index];
                _ = Task.Run(() =>
                {
                    serverLocal.PortNumberEvent.WaitOne();
                    Logger.Log($"Server: {serverLocal.ServerNumber} Port: {serverLocal.PortNumber}", LoggingLevel.Debug);

                    messageHandled.WaitOne();
                    string messageString = $"{serverLocal.ServerNumber} {serverLocal.PortNumber}";
                    Encoding.UTF8.GetBytes(messageString).CopyTo(message.AsSpan());
                    viewStream.Position = 0;
                    viewStream.Write(message, 0, message.Length);
                    messageWait.Set();
                }, token);
            }

            return servers;
        }
    }
}
