namespace CS.PlasmaLibrary
{
    public class LoggerSinkConsole : ILoggerSink
    {
        public void Write(string message)
        {
            Console.WriteLine(message);
        }
    }
}
