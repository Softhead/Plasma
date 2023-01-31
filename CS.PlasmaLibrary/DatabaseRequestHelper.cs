using System.Text;

namespace CS.PlasmaLibrary
{
    public class DatabaseRequestHelper
    {
        public static DatabaseRequest WriteRequest(string? key, string? value)
        {
            return WriteRequest(key, value is null ? null : Encoding.UTF8.GetBytes(value));
        }

        public static DatabaseRequest WriteRequest(string? key, byte[]? value)
        {
            // message bytes are:
            // 0: request type
            // 1..delimiter index: key as UTF8 bytes
            // delimiter index: the delimiter character
            // delimiter index + 1..end: value as UTF8 bytes

            int keyLength = Encoding.UTF8.GetByteCount(key!);
            byte[] data = new byte[1 + keyLength + 1 + value?.Length ?? 0];
            data[0] = (byte)DatabaseRequestType.Write;
            Span<byte> dataSpan = data.AsSpan();
            Span<byte> keySpan = Encoding.UTF8.GetBytes(key!).AsSpan();
            keySpan.CopyTo(dataSpan.Slice(1));
            dataSpan[keyLength + 1] = Constant.Delimiter;
            if (value is not null)
            {
                value.CopyTo(dataSpan.Slice(keyLength + 2));
            }
            return new DatabaseRequest { Bytes = dataSpan.ToArray() };
        }

        public static DatabaseRequest ReadRequest(string? key)
        {
            // message bytes are:
            // 0: request type
            // 1..end: key as UTF8 bytes

            byte[] data = new byte[1 + Encoding.UTF8.GetByteCount(key!)];
            data[0] = (byte)DatabaseRequestType.Read;
            Span<byte> dataSpan = data.AsSpan();
            Span<byte> keySpan = Encoding.UTF8.GetBytes(key!).AsSpan();
            keySpan.CopyTo(dataSpan.Slice(1));
            return new DatabaseRequest { Bytes = dataSpan.ToArray() };
        }
    }
}
