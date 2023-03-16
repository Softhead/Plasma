using CS.PlasmaClient;
using CS.PlasmaLibrary;
using CS.PlasmaServer;
using Microsoft.VisualStudio.Threading;
using System.Reflection;
using System.Text;

namespace CS.UnitTest.PlasmaServer
{
    [TestClass]
    public class OneServerHasBadResult : TestBase
    {
        Client[]? clients;
        Server[]? servers;
        CancellationTokenSource? source;

        [TestMethod]
        public async Task OneServerHasBadResultAsync()
        {
            int clientCount = 10;
            ThreadPool.SetMinThreads(clientCount, clientCount);

            // arrange
            Stream? configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CS.UnitTest.PlasmaServer.local.cfg");
            StreamReader configStreamReader = new(configStream!);
            source = new();
            Logger.Log("Start servers");
            servers = ServerHelper.StartServers(source.Token, configStreamReader);

            Logger.Log("Start client");
            clients = new Client[clientCount];
            for (int clientIndex = 0; clientIndex < clients.Length; clientIndex++)
            {
                configStream!.Position = 0;
                clients[clientIndex] = await ClientHelper.StartClientAsync(source.Token, configStreamReader, clientIndex == 0);
            }

            // wait for all servers to start
            while (!servers.Where(o => o.IsRunning is not null).All(o => (bool)o.IsRunning!))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), source.Token);
            }

            List<Task> tasks = new();

            for (int index = 0; index < clientCount; index++)
            {
                int localIndex = index;
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await OneServerHasBadResultWorkAsync(localIndex);
                        },
                        source.Token,
                        TaskCreationOptions.None,
                        TaskScheduler.Default
                    )
                );
            }

            await Task.WhenAll(tasks);
            Logger.Log("End test");
        }

        private async Task OneServerHasBadResultWorkAsync(int index)
        {
            Client client = clients![index];
            Logger.Log($"Start write data for index {index}");
            string key = $"key{index}";
            string value = $"value{index}";
            DatabaseRequest write = DatabaseRequestHelper.WriteRequest(key, value);
            DatabaseResponse? writeResult = await client.ProcessRequestAsync(write);

            // attest
            Assert.AreEqual(DatabaseResponseType.Success, writeResult?.MessageType);

            // act
            // corrupt the value in one server
            Logger.Log($"Start corrupting data for index {index}");
            byte[]? keyBytes = write.GetWriteKeyBytes();
            servers![0].Engine!.Dictionary![keyBytes!] = Encoding.UTF8.GetBytes("corrupted value");

            // read the value
            Logger.Log($"Start read data for index {index}");
            DatabaseRequest read = DatabaseRequestHelper.ReadRequest(key);
            DatabaseResponse? response = await client.ProcessRequestAsync(read);

            // wait for all workers to complete
            while (!client.WorkerQueueEmpty)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), source!.Token);
            }

            // assert
            Logger.Log($"Start assert for index {index}");
            Assert.AreEqual(DatabaseResponseType.Success, response!.MessageType);
            Assert.AreEqual(value, response!.ReadValue());
            byte[] bytes = servers[0]!.Engine!.Dictionary![keyBytes!];
            Assert.AreEqual(value, Encoding.UTF8.GetString(bytes));
        }
    }
}