using CS.PlasmaClient;
using CS.PlasmaLibrary;
using CS.PlasmaServer;
using Microsoft.VisualStudio.Threading;
using System.Reflection;
using System.Text;

namespace CS.UnitTest.PlasmaServer
{
    [TestClass]
    public class _10kLoad : TestBase
    {
        Client[]? clients;
        Server[]? servers;
        CancellationTokenSource? source;

        [TestMethod]
        public async Task _10kLoadAsync()
        {
            int clientCount = 10;
            ThreadPool.SetMinThreads(clientCount * 2, clientCount * 2);

            // arrange
            Stream? configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CS.UnitTest.PlasmaServer.local.cfg");
            StreamReader configStreamReader = new(configStream!);
            source = new();
            Logger.Log("Start servers", LoggingLevel.Always);
            servers = ServerHelper.StartServers(source.Token, configStreamReader);

            Logger.Log("Start client", LoggingLevel.Always);
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
                            await _10kLoadWorkAsync(localIndex);
                        },
                        source.Token,
                        TaskCreationOptions.None,
                        TaskScheduler.Default
                    )
                );
            }

            await Task.WhenAll(tasks);
            Logger.Log("End test", LoggingLevel.Always);
        }

        private async Task _10kLoadWorkAsync(int index)
        {
            Client client = clients![index];
            Logger.Log($"Start write data for index {index}", LoggingLevel.Always);
            string key = $"key{index:d4}-0000";
            string value = $"value{index:d4}-0000";
            DatabaseRequest writeRequest = DatabaseRequestHelper.WriteRequest(key, value);
            int writeLength = writeRequest.Bytes!.Length;

            // act
            for (int i = 0; i < 100; i++)
            {
                DatabaseResponse? writeResult = await client.ProcessRequestAsync(writeRequest);
                Assert.AreEqual(DatabaseResponseType.Success, writeResult?.MessageType);
                IncrementCounter(writeRequest, 9);
                IncrementCounter(writeRequest, writeLength - 4);
            }

            // assert
            Logger.Log($"Start assert for index {index}", LoggingLevel.Always);
            DatabaseRequest readRequest = DatabaseRequestHelper.ReadRequest(key);
            int readLength = readRequest.Bytes!.Length;
            Memory<byte> expectedValue = new byte [4];
            Encoding.UTF8.GetBytes("0000").CopyTo(expectedValue);
            for (int i = 0; i < 100; i++)
            {
                DatabaseResponse? readResponse = await client.ProcessRequestAsync(readRequest);
                Assert.AreEqual(DatabaseResponseType.Success, readResponse!.MessageType);
                Assert.IsTrue(expectedValue.Span.SequenceEqual(readResponse.Bytes!.AsSpan().Slice(11)));
                IncrementCounter(expectedValue.Span);
                IncrementCounter(readRequest, readLength - 4);
            }
        }

        private static void IncrementCounter(DatabaseRequest request, int startIndex)
        {
            Span<byte> number = request.Bytes.AsSpan().Slice(startIndex, 4);
            IncrementCounter(number);
        }

        private static void IncrementCounter(Span<byte> number)
        {
            for (int index = 3; index >= 0; index--)
            {
                if (number[index] == '9')
                {
                    number[index] -= 9;
                }
                else
                {
                    number[index] += 1;
                    break;
                }
            }
        }
    }
}
