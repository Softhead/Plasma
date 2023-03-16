using System.Text;

namespace CS.PlasmaLibrary
{
    public static class DatabaseMessageHelper
    {
        public static string BytesToString(string? messageType, byte[]? bytes)
        {
            string data = "";
            if (bytes?.Length > 1)
            {
                data = Encoding.UTF8.GetString(bytes.AsSpan().Slice(1));
                if (data.Length > 30)
                {
                    data = $"{data.Substring(0, 30)}...";
                }
            }
            return $"Command: '{messageType}' Data: '{data}'";
        }
    }
}
