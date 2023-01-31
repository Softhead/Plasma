using System.Text;

namespace CS.PlasmaLibrary
{
    public static class DatabaseResponseExtensions
    {
        public static string? ReadValue(this DatabaseResponse response)
        {
            if (response.Bytes is null
                || response.Bytes.Length == 0)
            {
                return null;
            }

            ReadOnlySpan<byte> value = response.Bytes.AsSpan().Slice(1);
            return Encoding.UTF8.GetString(value);
        }
    }
}
