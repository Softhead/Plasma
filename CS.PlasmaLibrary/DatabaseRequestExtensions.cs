using System.Text;

namespace CS.PlasmaLibrary
{
    public static class DatabaseRequestExtensions
    {
        public static string? GetReadKey(this DatabaseRequest request)
        {
            if (request is null
                || request.Bytes is null
                || request.Bytes.Length == 1)
            {
                return null;
            }

            return Encoding.UTF8.GetString(request.Bytes.AsSpan().Slice(1));
        }

        public static string? GetWriteKey(this DatabaseRequest request)
        {
            if (request is null
                || request.Bytes is null
                || request.Bytes.Length == 1)
            {
                return null;
            }

            byte[]? bytes = request.GetWriteKeyBytes();
            return bytes is null ? null : Encoding.UTF8.GetString(bytes);
        }

        public static byte[]? GetWriteKeyBytes(this DatabaseRequest request)
        {
            if (request is null
                || request.Bytes is null
                || request.Bytes.Length == 1)
            {
                return null;
            }

            int index = request.Bytes.AsSpan().IndexOf(Constant.Delimiter);
            return request.Bytes.AsSpan().Slice(1, index - 1).ToArray();
        }
    }
}
