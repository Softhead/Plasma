#pragma once

namespace Plasma::Database
{
	struct DatabaseDefinition
	{
	public:
		int ServerCopyCount;  // number of copies of data, 1 to 8;  2 or more creates redundancy
		int ServerCommitCount;  // number of commits, 1 to NumberOfCopy;  defines the quorum count for the server to assume a commit
		int SlotPushPeriod;  // milliseconds before scheduling a slot data push, if SlotPushTriggerCount has not been met
		int SlotPushTriggerCount;  // number of slot changes that trigger a slot data push
		int ClientQueryCount;  // number of servers for the client to query, 1 to ServerCopyCount
		int ClientCommitCount;  // number of commits, 1 to ClientQueryCount;  defines the quorum count for the client to assume a commit
		int ServerCommitPeriod;  // milliseconds before scheduling a commit reconciliation
		int ServerCommitTriggerCount;  // number of commits that trigger a commit reconciliation
	};
}
