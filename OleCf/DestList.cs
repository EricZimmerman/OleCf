using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OleCf
{
  public  class DestList
    {
        public DestListHeader Header { get; }

        public List<DestListEntry> Entries { get; }

      public DestList(byte[] rawBytes)
      {
          Entries = new List<DestListEntry>();

          var headerBytes = new byte[32];
            Buffer.BlockCopy(rawBytes,0,headerBytes,0,32);

            Header = new DestListHeader(headerBytes);

            var index = 32;
          var pathSize = 0;
          var entrySize = 0;

            if (Header.Version == 1)
            {
                index = 32;

                while (index < rawBytes.Length)
                {
                    pathSize = BitConverter.ToInt16(rawBytes, index + 112);
                    //now that we know pathSize we can determine how big each record is
                    entrySize = 114 + (pathSize * 2);

                    var entryBytes1 = new byte[entrySize];
                    Buffer.BlockCopy(rawBytes, index, entryBytes1, 0, entrySize);

                    var entry1 = new DestListEntry(entryBytes1,Header.Version);

                    Entries.Add(entry1);

                    index += entrySize;
                }
            }
          else
          {
                //windows 10 has version 3              

                 index = 32;

                while (index < rawBytes.Length)
                {
                    pathSize = BitConverter.ToInt16(rawBytes, index + 128);
                    //now that we know pathSize we can determine how big each record is
                    entrySize = 128 + 2 + (pathSize * 2) + 4; //128 is offset to the string, 2 for the size itself, double path for unicode, then 4 extra at the end

                    var entryBytes2 = new byte[entrySize];
                    Buffer.BlockCopy(rawBytes, index, entryBytes2, 0, entrySize);

                    var entry2 = new DestListEntry(entryBytes2, Header.Version);

                    Entries.Add(entry2);

                    index += entrySize;
                }

            }

      }
    }
}
