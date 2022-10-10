#include <iostream>
#include "..\PlasmaLibrary\ErrorNumber.h"
#include "..\PlasmaLibrary\DatabaseDefinition.h"
#include "PlasmaServer.h"
#include <format>
using namespace std;
using namespace Plasma;

int main(int argc, char* argv[])
{
    cout << "Plasma Server\n";

    if (argc < 2)
    {
        cout << "Must specify filename for server configuration, or \"create\" to create a new server.\n";
        cout << "Aborting.\n";
        return 0;
    }

    string arg1 = argv[1];
    if (arg1.compare("create") == 0)
    {
        Database::DatabaseDefinition definition;
        string fileName;

        cout << "Create a new server.\n\n";
        cout << "Enter configuration parameters:\n";

        cout << "# of redundant copies (1-8): ";
        cin >> definition.ServerCopyCount;

        while (definition.ServerCommitCount < 1 || definition.ServerCommitCount > definition.ServerCopyCount)
        {
            cout << format("Quorum count for server to assume a commit (1-{0}): ", definition.ServerCopyCount);
            cin >> definition.ServerCommitCount;
        }

        cout << "Milliseconds before scheduling a slot push: ";
        cin >> definition.SlotPushPeriod;

        cout << "# of slot changes that trigger a slot data push: ";
        cin >> definition.SlotPushTriggerCount;

        while (definition.ClientQueryCount < 1 || definition.ClientQueryCount > definition.ServerCopyCount)
        {
            cout << format("Number of servers for the client to query (1-{0}): ", definition.ServerCopyCount);
            cin >> definition.ClientQueryCount;
        }

        while (definition.ClientCommitCount < 1 || definition.ClientCommitCount > definition.ClientQueryCount)
        {
            cout << format("Quorum count for the client to assume a commit (1-{0}): ", definition.ClientQueryCount);
            cin >> definition.ClientCommitCount;
        }

        cout << "Milliseconds before scheduling a commit reconciliation: ";
        cin >> definition.ServerCommitPeriod;

        cout << "# of commits that trigger a commit reconciliation: ";
        cin >> definition.ServerCommitTriggerCount;

        cout << "File name to save server config file: ";
        cin >> fileName;

        Server::PlasmaServer* server = new Server::PlasmaServer();
        int response = server->CreateNew(&definition, fileName);

        if (response == ErrorNumber::Success)
        {
            cout << format("Successfully created server configuration file: {0}\n", fileName);
        }
        else
        {
            cout << format("Error creating server configuation: {0}\n", Plasma::Server::GetErrorText(response));
        }
        return response;
    }
    else
    {
        cout << format("Starting server with configuration file: {0}\n", argv[1]);

        Server::PlasmaServer* server = new Server::PlasmaServer();
        server->Start(argv[1]);

        return 0;
    }
}
