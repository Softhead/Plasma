using System.IO.MemoryMappedFiles;
using System.Text;

namespace CS.PlasmaClient
{
    public class ClientHelper
    {
        public static async Task<Client> StartClient(CancellationToken token, StreamReader definitionStream, bool receiveServerInformation)
        {
            Client client = new Client();

            if (receiveServerInformation)
            {
                var message = new byte[20];
                var messageWait = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_wait");
                var messageHandled = new EventWaitHandle(false, EventResetMode.AutoReset, "Plasma_handled");
                var mmf = MemoryMappedFile.CreateOrOpen("Plasma_mmf", message.Length);
                var viewStream = mmf.CreateViewStream();
                messageHandled.Set();

                ManualResetEvent taskStarted = new ManualResetEvent(false);
                _ = Task.Run(() =>
                {
                    bool isTaskStarted = false;

                    while (!token.IsCancellationRequested)
                    {
                        messageWait.WaitOne();
                        if (!isTaskStarted)
                        {
                            isTaskStarted = true;
                            taskStarted.Set();
                        }
                        viewStream.Position = 0;
                        viewStream.Read(message, 0, message.Length);

                        // handle the message
                        string messageString = Encoding.UTF8.GetString(message);
                        string[] parts = messageString.Split(' ');
                        if (int.TryParse(parts[0], out int server)
                            && int.TryParse(parts[1], out int port))
                        {
                            Client.ServerPortDictionary.TryAdd(server, port);
                        }
                        messageHandled.Set();
                    }
                }, token);

                taskStarted.WaitOne();
            }

            client.Start(definitionStream);

            return client;
        }
    }
}
