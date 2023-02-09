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
            // arrange
            Stream? configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CS.UnitTest.PlasmaServer.local.cfg");
            StreamReader configStreamReader = new StreamReader(configStream!);
            CancellationTokenSource source = new CancellationTokenSource();
            Logger.Log("Start servers");
            Server[] servers = ServerHelper.StartServers(source.Token, configStreamReader);

            configStream!.Position = 0;
            Logger.Log("Start client");
            Client client = await ClientHelper.StartClient(source.Token, configStreamReader);

            // wait for all servers to start
            while (!servers.Where(o => o.IsRunning is not null).All(o => (bool)o.IsRunning!))
            {
                await Task.Delay(TimeSpan.FromSeconds(0.25), source.Token);
            }

            Parallel.For(0, 2, async (index) =>
            {
                Logger.Log("Start write data");
                string key = $"key{index}";
                string value = $"value{index}";
                DatabaseRequest write = DatabaseRequestHelper.WriteRequest(key, value);
                DatabaseResponse? writeResult = await client.Request(write);

                // attest
                Assert.AreEqual(DatabaseResponseType.Success, writeResult!.MessageType);

                // act
                // corrupt the value in one server
                Logger.Log("Start corrupting data");
                byte[]? keyBytes = write.GetWriteKeyBytes();
                servers[0].Engine!.Dictionary![keyBytes!] = Encoding.UTF8.GetBytes("corrupted value");

                // read the value
                Logger.Log("Start read data");
                DatabaseRequest read = DatabaseRequestHelper.ReadRequest(key);
                DatabaseResponse? response = await client.Request(read);

                // wait for all workers to complete
                while (!client.WorkerQueueEmpty)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), source.Token);
                }

                // assert
                Logger.Log("Start assert");
                Assert.AreEqual(DatabaseResponseType.Success, response!.MessageType);
                Assert.AreEqual(value, response!.ReadValue());
                byte[] bytes = servers[0]!.Engine!.Dictionary![keyBytes!];
                Assert.AreEqual(value, Encoding.UTF8.GetString(bytes));
            });
        }
    }
}