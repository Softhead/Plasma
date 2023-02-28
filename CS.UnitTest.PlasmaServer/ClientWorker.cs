using CS.PlasmaClient;
using CS.PlasmaLibrary;
using CS.PlasmaServer;
using System.Reflection;
using System.Text;

namespace CS.UnitTest.PlasmaServer
{
    [TestClass]
    public class ClientWorker : TestBase
    {
        [TestMethod]
        public async Task OneServerHasBadResult()
        {
            int clientCount = 10;
            ThreadPool.SetMinThreads(10 * clientCount, 10 * clientCount);

            // arrange
            Stream? configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CS.UnitTest.PlasmaServer.local.cfg");
            StreamReader configStreamReader = new(configStream!);
            CancellationTokenSource source = new();
            Logger.Log("Start servers");
            Server[] servers = ServerHelper.StartServers(source.Token, configStreamReader);

            Logger.Log("Start client");
            Client[] clients = new Client[clientCount];
            for (int clientIndex = 0; clientIndex < clients.Length; clientIndex++)
            {
                configStream!.Position = 0;
                clients[clientIndex] = await ClientHelper.StartClient(source.Token, configStreamReader, clientIndex == 0);
            }

            // wait for all servers to start
            while (!servers.Where(o => o.IsRunning is not null).All(o => (bool)o.IsRunning!))
            {
                await Task.Delay(TimeSpan.FromSeconds(0.25), source.Token);
            }

            Parallel.For(0, clientCount, new ParallelOptions { MaxDegreeOfParallelism = clientCount }, async (index) =>
            {
                Client client = clients[index];
                Logger.Log($"Start write data for index {index}");
                string key = $"key{index}";
                string value = $"value{index}";
                DatabaseRequest write = DatabaseRequestHelper.WriteRequest(key, value);
                DatabaseResponse? writeResult = await client.ProcessRequest(write);

                // attest
                Assert.AreEqual(DatabaseResponseType.Success, writeResult?.MessageType);

                // act
                // corrupt the value in one server
                Logger.Log($"Start corrupting data for index {index}");
                byte[]? keyBytes = write.GetWriteKeyBytes();
                servers[0].Engine!.Dictionary![keyBytes!] = Encoding.UTF8.GetBytes("corrupted value");

                // read the value
                Logger.Log($"Start read data for index {index}");
                DatabaseRequest read = DatabaseRequestHelper.ReadRequest(key);
                DatabaseResponse? response = await client.ProcessRequest(read);

                // wait for all workers to complete
                while (!client.WorkerQueueEmpty)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), source.Token);
                }

                // assert
                Logger.Log($"Start assert for index {index}");
                Assert.AreEqual(DatabaseResponseType.Success, response!.MessageType);
                Assert.AreEqual(value, response!.ReadValue());
                byte[] bytes = servers[0]!.Engine!.Dictionary![keyBytes!];
                Assert.AreEqual(value, Encoding.UTF8.GetString(bytes));
            });

            Logger.Log("End test");
        }
    }
}