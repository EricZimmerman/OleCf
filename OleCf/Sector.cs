using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace OleCf
{
  public  class Sector
    {
      public enum SectorTypes
      {
          Unused = 1,
          EndOfSectorChain = 2,
          SATSector = -3,
          MSATSector = -4
      }

        public SectorTypes SectorType { get; }

        public int AbsoluteOffset { get; }

      public Sector(byte[] rawBytes)
      {
          
            SectorType = (SectorTypes)BitConverter.ToInt32(rawBytes, 0);
        }
    }
}
