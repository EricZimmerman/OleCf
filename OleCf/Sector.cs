using System;

namespace OleCf
{
    public class Sector
    {
        public enum SectorTypes
        {
            Unused = 1,
            EndOfSectorChain = 2,
            SATSector = -3,
            MSATSector = -4
        }

        public Sector(byte[] rawBytes)
        {
            SectorType = (SectorTypes) BitConverter.ToInt32(rawBytes, 0);
        }

        public SectorTypes SectorType { get; }

        public int AbsoluteOffset { get; }
    }
}