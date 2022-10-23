namespace CS.PlasmaLibrary
{
    public struct DatabaseSlot
    {
        public byte ServerNumber;  // the 0 based server number that is responsible for this slot
        public byte VersionNumber;  // the version number of this slot;  incremented when the slot is migrated to a different server
    };
}
