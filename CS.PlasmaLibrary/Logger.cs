namespace CS.PlasmaLibrary
{
    public class Logger
    {
        private static List<ILoggerSink> sinks_ = new List<ILoggerSink> { new LoggerSinkConsole() };

        public static List<ILoggerSink> Sinks => sinks_;

        public static void Log(string message)
        {
            foreach (var sink in sinks_)
            {
                sink.Write(message);
            }
        }
    }
}
