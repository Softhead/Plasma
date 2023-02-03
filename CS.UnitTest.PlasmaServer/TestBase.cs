using CS.PlasmaLibrary;

namespace CS.UnitTest.PlasmaServer
{
    public class TestBase
    {
        [TestInitialize]
        public void Init()
        {
            Logger.Sinks.Add(new LoggerSinkFile());
            Logger.Log("Start test init");
        }
    }
}
