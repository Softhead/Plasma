#pragma once

namespace Plasma::Database
{
	enum DatabaseDefinitionKey
	{
		ServerCopyCount,
		ServerCommitCount,
		SlotPushPeriod,
		SlotPushTriggerCount,
		ClientQueryCount,
		ClientCommitCount,
		ServerCommitPeriod,
		ServerCommitTriggerCount
	};
}