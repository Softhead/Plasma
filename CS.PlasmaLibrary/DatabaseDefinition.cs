using System.Net;

namespace CS.PlasmaLibrary
{
    public class DatabaseDefinition
    {
        public int ServerCopyCount { get; set; }  // number of copies of data, 1 to 8;  2 or more creates redundancy
        public int ServerCommitCount { get; set; }  // number of commits, 1 to NumberOfCopy;  defines the quorum count for the server to assume a commit
        public int SlotPushPeriod { get; set; }  // milliseconds before scheduling a slot data push, if SlotPushTriggerCount has not been met
        public int SlotPushTriggerCount { get; set; }  // number of slot changes that trigger a slot data push
        public int ClientQueryCount { get; set; }  // number of servers for the client to query, 1 to ServerCopyCount
        public int ClientCommitCount { get; set; }  // number of commits, 1 to ClientQueryCount;  defines the quorum count for the client to assume a commit
        public int ServerCommitPeriod { get; set; }  // milliseconds before scheduling a commit reconciliation
        public int ServerCommitTriggerCount { get; set; }  // number of commits that trigger a commit reconciliation
        public int UdpPort { get; set; }  // UDP port number to bind to
        public IPAddress? IpAddress { get; set; }  // IP address to bind to
    }
}
