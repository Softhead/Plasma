namespace CS.PlasmaLibrary
{
    public class DatabaseRequest
    {
        public DatabaseRequestType DatabaseRequestType { get; set; }

        // request as byte array
        // format:
        // byte 0: database request type
        // byte 1: 
        public byte[] Bytes
        {
            get
            {
                byte[] bytes = new byte[1];
                bytes[0] = (byte)DatabaseRequestType;
                return bytes;
            }
            set
            {
                DatabaseRequestType = (DatabaseRequestType)value[0];
            }
        }
    }
}
