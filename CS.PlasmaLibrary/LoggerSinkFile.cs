using System.Collections.Concurrent;

namespace CS.PlasmaLibrary
{
    public class LoggerSinkFile : ILoggerSink, IDisposable
    {
        private StreamWriter? streamWriter_ = null;
        private ConcurrentQueue<string> queue_ = new ConcurrentQueue<string>();
        private bool stillGoing_ = true;
        private bool disposedValue;

        public LoggerSinkFile(string fileName = @"c:\tmp\Plasma.log")
        {
            FileMode mode = FileMode.Create;
            if (File.Exists(fileName))
            {
                mode = FileMode.Truncate;
            }
            Stream stream = File.Open(fileName, mode, FileAccess.Write);
            streamWriter_ = new StreamWriter(stream);

            _ = Task.Run(Writer);
        }

        public void Write(string message)
        {
            queue_.Enqueue(message);
        }

        private void Writer()
        {
            while (stillGoing_)
            {
                while (stillGoing_ && queue_.Count > 0)
                {
                    queue_.TryDequeue(out string? message);
                    if (message is not null)
                    {
                        streamWriter_?.WriteLine(message);
                        streamWriter_?.Flush();
                    }
                }

                Thread.Sleep(100);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            stillGoing_ = false;

            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state
                    streamWriter_?.Close();
                    streamWriter_ = null;

                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
