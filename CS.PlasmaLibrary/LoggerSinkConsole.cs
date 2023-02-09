using System.Collections.Concurrent;
using System.IO;

namespace CS.PlasmaLibrary
{
    public class LoggerSinkConsole : ILoggerSink
    {
        private BlockingCollection<string>? queue_ = null;
        private CancellationTokenSource tokenSource_ = new CancellationTokenSource();
        private bool disposedValue;

        public LoggerSinkConsole()
        {
            queue_ = new BlockingCollection<string>();
            _ = Task.Run(Writer);
        }

        public void Write(string message)
        {
            queue_?.Add(message);
        }

        private void Writer()
        {
            while (!tokenSource_.IsCancellationRequested)
            {
                string? message = queue_?.Take(tokenSource_.Token);
                if (message is not null)
                {
                    Console.WriteLine(message);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                tokenSource_.Cancel();

                if (disposing)
                {
                    // dispose managed state
                    queue_?.CompleteAdding();
                    queue_?.Dispose();
                    queue_ = null;

                    tokenSource_.Dispose();
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

        async Task ILoggerSink.WaitForQueue()
        {
            while (!tokenSource_.IsCancellationRequested)
            {
                if (queue_?.Count == 0)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), tokenSource_.Token);
            }
        }
    }
}
