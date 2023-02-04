using System.Text;
using System.Xml.Linq;

namespace CS.PlasmaLibrary
{
    public class DatabaseMessage<T> where T : Enum
    {
        private byte[]? bytes_ = null;

        public T? MessageType
        {
            get
            {
                if (bytes_ is not null)
                {
                    int value = bytes_[0];
                    return (T)(object)value;
                }
                return default;
            }
            set
            {
                if (value is null)
                {
                    return;
                }

                if (bytes_ is null)
                {
                    bytes_ = new byte[1];
                }
                bytes_[0] = (byte)(int)(object)value;
            }
        }

        // request as byte array
        // format:
        // byte 0: database request type
        // byte 1: 
        public byte[]? Bytes
        {
            get
            {
                return bytes_;
            }
            set
            {
                bytes_ = value;
            }
        }

        public override string ToString()
        {
            return DatabaseMessageHelper.BytesToString(MessageType?.ToString(), Bytes);
        }

        public override int GetHashCode()
        {
            if (bytes_ is null || bytes_.Length == 0)
            {
                return 0;
            }

            uint hash = 7;
            for (int index = 0; index < bytes_.Length; index++)
            {
                hash = (hash << 5) - hash + bytes_[index];
            }

            return (int)(hash & 0xffff);
        }
    }
}
