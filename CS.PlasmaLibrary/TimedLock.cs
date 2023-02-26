namespace CS.PlasmaLibrary
{
    // this class is not reentrant, so each instance of TimedLock must only be called once per thread at a time
    public class TimedLock
    {
        private readonly SemaphoreSlim toLock;

        public TimedLock()
        {
            toLock = new SemaphoreSlim(1, 1);
        }

        public async Task<LockReleaser> Lock(TimeSpan timeout)
        {
            if (await toLock.WaitAsync(timeout))
            {
                return new LockReleaser(toLock);
            }
            throw new TimeoutException();
        }

        public readonly struct LockReleaser : IDisposable
        {
            private readonly SemaphoreSlim toRelease;

            public LockReleaser(SemaphoreSlim toRelease)
            {
                this.toRelease = toRelease;
            }
            public void Dispose()
            {
                toRelease.Release();
            }
        }
    }
}
