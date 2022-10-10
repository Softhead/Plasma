#pragma once
#include "Constant.h"
#include "DatabaseSlot.h"
#include "DatabaseSlotInfo.h"
#include "DatabaseDefinition.h"

namespace Plasma::Database
{
	class DatabaseState
	{
	private:
		DatabaseDefinition *definition_;
		DatabaseSlot *slots_;

	public:
		DatabaseState(DatabaseDefinition &definition);

		// FindNextCopySlot
		// inputs:
		//   DatabaseSlotInfo currentSlotInfo = the current slot information
		//   DatabaseSlotInfo &nextSlotIndex = the resulting next slot information is placed here
		//
		// outputs:
		//   int result = error number
		int FindNextCopySlot(DatabaseSlotInfo &currentSlotInfo, DatabaseSlotInfo& nextSlotInfo);
	};
}
