#include "pch.h"
#include "DatabaseDefinition.h"
#include "Constant.h"
#include "DatabaseSlot.h"
#include "DatabaseState.h"
#include "..\PlasmaLibrary\ErrorNumber.h"

namespace Plasma::Database
{
	DatabaseState::DatabaseState(Database::DatabaseDefinition &definition)
	{
		definition_ = &definition;
		slots_ = new DatabaseSlot[Constant::SlotCount];
	}

	// FindNextCopySlot
	// 
	// In a ring of slots, jump around the ring as follows to select the slot where the next copy will reside.
	//
	//         0
	//     5       6
	//
	//   2           3
	//
	//     7       4
	//         1
	//
	int DatabaseState::FindNextCopySlot(DatabaseSlotInfo &currentSlotInfo, DatabaseSlotInfo& nextSlotInfo)
	{
		if (currentSlotInfo.CopyNumber == definition_->ServerCopyCount - 1)
			return ErrorNumber::CopyNumberOutOfRange;

		nextSlotInfo.CopyNumber = currentSlotInfo.CopyNumber + 1;

		switch (currentSlotInfo.CopyNumber)
		{
		case 0:  // next at 180 degrees
		case 2:
		case 4:
		case 6:
			nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant::SlotCount / 2)) % Constant::SlotCount;
			break;

		case 1:  // next at 90 degrees
		case 5:
			nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant::SlotCount / 4)) % Constant::SlotCount;
			break;

		case 3:  // next at 45 degrees
			nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant::SlotCount / 8)) % Constant::SlotCount;
			break;
		}

		return ErrorNumber::Success;
	}
}
