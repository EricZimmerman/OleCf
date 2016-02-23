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

          while (index < rawBytes.Length)
          {

                var pathSize = BitConverter.ToInt16(rawBytes, index + 112);
                //now that we know pathSize we can determine how big each record is
                var entrySize = 114 + (pathSize * 2);

              var entryBytes = new byte[entrySize];
                Buffer.BlockCopy(rawBytes,index,entryBytes,0,entrySize);

                var entry = new DestListEntry(entryBytes);

                Entries.Add(entry);

              index += entrySize;
          }

          Debug.WriteLine(1);


      }
    }
}
