using CS.PlasmaLibrary;

namespace CS.PlasmaServer
{
    public class Server : IServer
    {
        public ManualResetEvent PortNumberEvent = new ManualResetEvent(false);

        private CancellationTokenSource source_ = new CancellationTokenSource();
        private DatabaseDefinition? definition_ = null;
        private Engine? engine_ = null;
        private DatabaseState? state_ = null;
        private int serverNumber_;

        public DatabaseDefinition? Definition { get => definition_; set => definition_ = value; }

        public int ServerNumber { get => serverNumber_; }

        public int? PortNumber
        {
            get
            {
                if (engine_ is not null)
                {
                    return engine_.PortNumber;
                }

                return null;
            }
        }

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

        public Engine? Engine
        {
            get => engine_;
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

            s.Close();
            return ErrorNumber.Success;
        }

        public bool? IsRunning
        {
            get => engine_?.IsRunning;
        }

        public ErrorNumber Start(int serverNumber, StreamReader? definitionStream = null)
        {
            if (definition_ is not null
                && engine_ is not null)
            {
                return ErrorNumber.AlreadyStarted;
            }

            serverNumber_ = serverNumber;

            if (definition_ is null)
            {
                if (definitionStream is null)
                {
                    throw new ArgumentNullException(nameof(definitionStream));
                }

                definition_ = new DatabaseDefinition();
                ErrorNumber loadResult = definition_.LoadConfiguration(definitionStream);
                if (loadResult != ErrorNumber.Success)
                {
                    return loadResult;
                }
            }

            engine_ = new Engine(definition_, serverNumber_);
            engine_.Start();

            Task.Run(() =>
            {
                engine_.PortNumberEvent.WaitOne();
                PortNumberEvent.Set();
            }, source_.Token);

            return ErrorNumber.Success;
        }

        public async Task Stop()
        {
            if (engine_ is not null)
            {
                await engine_.Stop();
                engine_ = null;
            }
        }
    }
}
