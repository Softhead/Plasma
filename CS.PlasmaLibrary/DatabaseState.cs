namespace CS.PlasmaLibrary
{
    public class DatabaseState
    {
        private DatabaseDefinition? definition_ = null;
        private DatabaseSlot[] slots_;

        public DatabaseState(DatabaseDefinition? definition)
        {
            definition_ = definition;
            slots_ = new DatabaseSlot[Constant.SlotCount];
        }

        public DatabaseSlot[] Slots { get => slots_; }

        // FindNextCopySlot
        // inputs:
        //   DatabaseSlotInfo currentSlotInfo = the current slot information
        //   DatabaseSlotInfo &nextSlotIndex = the resulting next slot information is placed here
        //
        // outputs:
        //   int result = error number
        //
        // FindNextCopySlot
        // 
        // In a ring of slots, jump around the ring as follows to select the slot where the next copy will reside.
        // Even case:
        //
        //         0
        //     5       6
        //
        //   2           3
        //
        //     7       4
        //         1
        //
        // Odd case:
        //
        //         0
        //     3       4
        //
        //   7           8
        //
        //    6         1
        //       2   5
        //
        public ErrorNumber FindNextCopySlot(DatabaseSlotInfo currentSlotInfo, ref DatabaseSlotInfo nextSlotInfo, int serverCount)
        {
            if (definition_ is null)
            {
                return ErrorNumber.InvalidConfiguration;
            }

            if (currentSlotInfo.CopyNumber == definition_.ServerCopyCount - 1)
            {
                return ErrorNumber.CopyNumberOutOfRange;
            }

            nextSlotInfo.CopyNumber = (byte)(currentSlotInfo.CopyNumber + 1);

            if (serverCount % 2 == 0)
            {
                // even case
                switch (currentSlotInfo.CopyNumber)
                {
                    case 0:  // next at 180 degrees
                    case 2:
                    case 4:
                    case 6:
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant.SlotCount / 2)) % Constant.SlotCount;
                        break;

                    case 1:  // next at 90 degrees
                    case 5:
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant.SlotCount / 4)) % Constant.SlotCount;
                        break;

                    case 3:  // next at 45 degrees
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant.SlotCount / 8)) % Constant.SlotCount;
                        break;
                }
            }
            else
            {
                // odd case
                switch (currentSlotInfo.CopyNumber)
                {
                    case 0:  // next at 120 degrees
                    case 2:
                    case 4:
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant.SlotCount / 3)) % Constant.SlotCount;
                        break;

                    case 1:  // next at 80 degrees
                    case 3:
                    case 5:
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (2 * Constant.SlotCount / 9)) % Constant.SlotCount;
                        break;

                    case 6:  // next at 40 degrees
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (Constant.SlotCount / 9)) % Constant.SlotCount;
                        break;

                    case 7:  // next at 200 degrees
                        nextSlotInfo.SlotNumber = (currentSlotInfo.SlotNumber + (5 * Constant.SlotCount / 9)) % Constant.SlotCount;
                        break;
                }
            }

            return ErrorNumber.Success;
        }

        public void SetupInitialSlots()
        {
            for (int index = 0; index < Constant.SlotCount; index++)
            {
                slots_[index] = new DatabaseSlot { ServerNumber = 255, VersionNumber = 0 };
            }

            DatabaseSlotInfo currentSlotInfo = new DatabaseSlotInfo();
            DatabaseSlotInfo nextSlotInfo = new DatabaseSlotInfo();
            for (int index = 0; index < Constant.SlotCount; index++)
            {
                if (slots_[index].ServerNumber != 255)
                {
                    break;
                }

                int currentIndex = index;
                for (byte replica = 0; replica < definition_!.ServerCopyCount; replica++)
                {
                    slots_[currentIndex].ServerNumber = replica;
                    currentSlotInfo.SlotNumber = currentIndex;
                    currentSlotInfo.CopyNumber = replica;
                    FindNextCopySlot(currentSlotInfo, ref nextSlotInfo, definition_.ServerCount);
                    currentIndex = nextSlotInfo.SlotNumber;
                }
            }
        }
    };
}
