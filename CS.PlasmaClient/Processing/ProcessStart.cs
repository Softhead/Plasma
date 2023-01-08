using CS.PlasmaLibrary;
using System.Diagnostics;

namespace CS.PlasmaClient
{
    internal class ProcessStart : IDatabaseClientProcess
    {
        public DatabaseRequestType DatabaseRequestType => DatabaseRequestType.Start;

        public DatabaseResponse? Process(Client client, DatabaseRequest request)
        {
            // start server process
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\db\\CS.PlasmaMain\\bin\\Debug\\net6.0\\CS.PlasmaMain.exe",
                    WorkingDirectory = "C:\\db\\CS.PlasmaMain",
                    Arguments = "local.cfg",
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };
            process.Start();

            return new DatabaseResponse { MessageType = DatabaseResponseType.Success };
        }
    }
}
