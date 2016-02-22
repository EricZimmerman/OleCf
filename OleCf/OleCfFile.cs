using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OleCf
{
    public class OleCfFile
    {
        public Header Header { get; }

        private byte[] _rawBytes;

        public List<DirectoryItem> DirectoryItems { get; }
        public int[] Sat {get; }
        public int[] SSat {get; }

        public OleCfFile(byte[] rawBytes)
        {
            _rawBytes = rawBytes;
            var headerBytes = new byte[512];

            Buffer.BlockCopy(rawBytes,0,headerBytes,0,512);

            Header = new Header(headerBytes);

        
            var satbytes = new byte[Header.TotalSATSectors * Header.SectorSizeAsBytes];

            for (int i = 0; i < Header.SATSectors.Length; i++)
            {
                //go to offset stored at i
                //get sectorsizebytes
                //copy to satbytes

                var satChunk = new byte[Header.SectorSizeAsBytes];
                Buffer.BlockCopy(rawBytes, Header.SATSectors[i], satChunk, 0, Header.SectorSizeAsBytes);

             //   Debug.WriteLine($"satChunk sig: {BitConverter.ToInt32(satChunk,0)}");
                
                Buffer.BlockCopy(satChunk,0,satbytes,i * Header.SectorSizeAsBytes,Header.SectorSizeAsBytes);
            }

            Sat = new int[satbytes.Length/4];

            //Debug.WriteLine(BitConverter.ToString(satbytes));

            for (int i = 0; i < satbytes.Length/4 ;i++)
            {
                var satAddr = BitConverter.ToInt32(satbytes, i*4);
                Sat[i] = satAddr;
            }
            
            
            DumpSat();

            var ssatbytes = GetSector(Header.SSATFirstSectorId, (int)(Header.SSATTotalSectors * Header.SectorSizeAsBytes));

            SSat = new int[ssatbytes.Length / 4];

            for (int i = 0; i < ssatbytes.Length / 4; i++)
            {
                var ssatAddr = BitConverter.ToInt32(ssatbytes, i * 4);
                SSat[i] = ssatAddr;
            }

            DumpSSat();

            var dirBytes = GetSector(Header.DirectoryStreamFirstSectorId,4097);

            //Buffer.BlockCopy(rawBytes,(Header.DirectoryStreamFirstSectorId * Header.SectorSizeAsBytes)+512,dirBytes,0,Header.SectorSizeAsBytes);

            var dirIndex = 0;

            DirectoryItems = new List<DirectoryItem>();

            while (dirIndex<dirBytes.Length)
            {
                var dBytes = new byte[128];

                Buffer.BlockCopy(dirBytes,dirIndex,dBytes,0,128);

                if (dBytes[66] != 0) //0 is empty directory structure
                {
                    var d = new DirectoryItem(dBytes);

                    DirectoryItems.Add(d);
                }
                
                dirIndex += 128;
            }


            Debug.WriteLine(DirectoryItems.Count);
            foreach (var directoryItem in DirectoryItems)
            {
                Debug.WriteLine($"Name: {directoryItem.DirectoryName}, Size: {directoryItem.DirectorySize}");

                if (directoryItem.DirectoryName == "d")
                    Debug.WriteLine(1);

                var b = GetSector((int) directoryItem.FirstDirectorySectorId, (int) directoryItem.DirectorySize);
            }

        }

        private void DumpSat()
        {
            Debug.WriteLine("SAT");
            for (int i = 0; i < Sat.Length; i++)
            {
                Debug.WriteLine($"Slot {i}: {Sat[i]}");
            }
            Debug.WriteLine("----------------");
        }

        private void DumpSSat()
        {
            Debug.WriteLine("SSAT");
            for (int i = 0; i < SSat.Length; i++)
            {
                Debug.WriteLine($"SSlot {i}: {SSat[i]}");
            }
            Debug.WriteLine("----------------");
        }

        private byte[] GetSector(int sectorNumber, int size)
        {
            var sn = sectorNumber;

            var runInfo = new List<int>();

            runInfo.Add(sectorNumber);

            var _sectorSize = Header.SectorSizeAsBytes;

            if (SSat!= null && size < Header.MinimumStandardStreamSize)
            {
                _sectorSize = Header.ShortSectorSizeAsBytes;
                while (SSat[sn] >= 0)
                {
                    //    Debug.WriteLine($"{Sat[sn]} 0x{Sat[sn]:x}");

                    runInfo.Add(SSat[sn]);

                    sn = SSat[sn];
                }
            }
            else
            {
                while (Sat[sn] >= 0)
                {
                    //    Debug.WriteLine($"{Sat[sn]} 0x{Sat[sn]:x}");

                    runInfo.Add(Sat[sn]);

                    sn = Sat[sn];
                }
            }

            Debug.WriteLine($"Run info len: {runInfo.Count}");

            //this needs to handle short sectors too

            var retBytes = new byte[_sectorSize * runInfo.Count];

            var offset = 0;
            foreach (var i in runInfo)
            {

                var index = 512 + (_sectorSize * i); //header + relative offset

                var readSize = _sectorSize;

                

                if (size < _sectorSize)
                {
                    _sectorSize = size;
                }

                if (_rawBytes.Length - index < _sectorSize)
                {
                    readSize = _rawBytes.Length - index;
                }

                    Buffer.BlockCopy(_rawBytes, index, retBytes, _sectorSize * offset, readSize);
                offset += 1;

            }
            
            return retBytes;
        }
    }

    
}
