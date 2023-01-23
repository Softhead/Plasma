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
    }
}
