using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

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
                if (data.Length > 10)
                {
                    data = $"{data.Substring(0, 10)}...";
                }
            }
            return $"Command: '{messageType}' Data: '{data}'";
        }
    }
}
