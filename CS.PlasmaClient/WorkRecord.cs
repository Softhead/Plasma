using CS.PlasmaLibrary;

namespace CS.PlasmaClient
{
    internal class WorkRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DatabaseRequest? Request { get; set; }
        public ResponseRecord? Response { get; set; }
        public byte[]? Value { get; set; }
        public WorkItemState State 
        { 
            get { return State; } 
            set { State = value; RetryCount = 0; } 
        }
        public int RetryCount { get; set; } = 0;
    }
}
