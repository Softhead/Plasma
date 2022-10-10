#pragma once

namespace Plasma::Database
{
	struct DatabaseSlotInfo
	{
		int SlotNumber;
		int CopyNumber;  // 0 based current copy count
	};
}
