#pragma once
#include <cstddef>
using namespace std;

namespace Plasma::Database
{
	struct DatabaseSlot
	{
		byte ServerNumber;  // the 0 based server number that is responsible for this slot
		byte VersionNumber;  // the version number of this slot;  incremented when the slot is migrated to a different server
	};
}
