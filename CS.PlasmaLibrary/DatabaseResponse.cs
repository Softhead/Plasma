namespace CS.PlasmaLibrary
{
    public class DatabaseResponse
    {
        public DatabaseResponseType DatabaseResponseType { get; set; }

        // response as byte array
        // format:
        // byte 0: database response type
        // byte 1: 
        public byte[] Bytes
        {
            get
            {
                byte[] bytes = new byte[1];
                bytes[0] = (byte)DatabaseResponseType;
                return bytes;
            }
            set
            {
                DatabaseResponseType = (DatabaseResponseType)value[0];
            }
        }
    }
}
