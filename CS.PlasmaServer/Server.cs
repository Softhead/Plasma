using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    public class Server
    {
        private DatabaseDefinition? definition_ = null;
        private Engine? engine_ = null;
        private DatabaseState? state_ = null;

        public DatabaseDefinition? Definition { get => definition_; set => definition_ = value; }

        public DatabaseState? State
        {
            get => state_;

            set
            {
                state_ = value;
                if (engine_ is not null)
                {
                    engine_.State = value;
                }
            }
        }

        public ErrorNumber CreateNew(DatabaseDefinition definition, string definitionFileName)
        {
            StreamWriter s = File.CreateText(definitionFileName);

            s.WriteLine($"{DatabaseDefinitionKey.ServerCopyCount}={definition.ServerCopyCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitCount}={definition.ServerCommitCount}");
            s.WriteLine($"{DatabaseDefinitionKey.SlotPushPeriod}={definition.SlotPushPeriod}");
            s.WriteLine($"{DatabaseDefinitionKey.SlotPushTriggerCount}={definition.SlotPushTriggerCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ClientQueryCount}={definition.ClientQueryCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ClientCommitCount}={definition.ClientCommitCount}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitPeriod}={definition.ServerCommitPeriod}");
            s.WriteLine($"{DatabaseDefinitionKey.ServerCommitTriggerCount}={definition.ServerCommitTriggerCount}");
            s.WriteLine($"{DatabaseDefinitionKey.UdpPort}={definition.UdpPort}");

            s.Close();
            return ErrorNumber.Success;
        }

        public bool? IsRunning
        {
            get => engine_?.IsRunning;
        }

        public ErrorNumber Start(string? definitionFileName = null)
        {
            if (definition_ is not null
                && engine_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            if (definition_ is null)
            {
                definition_ = new DatabaseDefinition();
                ErrorNumber loadResult = definition_.LoadConfiguration(definitionFileName);
                if (loadResult != ErrorNumber.Success)
                {
                    return loadResult;
                }
            }

            engine_ = new Engine(definition_);
            engine_.Start();

            return ErrorNumber.Success;
        }

        public void Stop()
        {
            if (engine_ is not null)
            {
                engine_!.Stop();
                engine_ = null;
            }
        }
    }
}
