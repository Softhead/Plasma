namespace CS.PlasmaClient
{
    internal class ResponseTally
    {
        public int Tally { get; set; }
        public byte[]? Value { get; set; }
        public ResponseRecord? Response { get; set; }
    }
}
