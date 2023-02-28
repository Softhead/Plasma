namespace CS.PlasmaLibrary
{
    public class Logger
    {
        private static List<ILoggerSink> sinks_ = new() { new LoggerSinkConsole() };

        public static List<ILoggerSink> Sinks => sinks_;

        public static void Log(string message)
        {
            foreach (var sink in sinks_)
            {
                sink.Write(message);
            }
        }

        public static async Task WaitForQueues()
        {
            int count = sinks_.Count;
            Task[] tasks = new Task[count];
            for (int index = 0; index < count; index++)
            {
                tasks[index] = sinks_[index].WaitForQueue();
            }
            await Task.WhenAll(tasks);
        }
    }
}
