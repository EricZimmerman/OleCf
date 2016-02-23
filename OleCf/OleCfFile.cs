using System;
using System.Collections.Generic;
using System.Linq;

namespace OleCf
{
    public class OleCfFile
    {
        private readonly byte[] _rawBytes;
        private readonly List<byte[]> _shortSectors;
        public DestList DestList;

        public OleCfFile(byte[] rawBytes, string sourceFile)
        {
            SourceFile = sourceFile;
            _rawBytes = rawBytes;
            var headerBytes = new byte[512];

            Buffer.BlockCopy(rawBytes, 0, headerBytes, 0, 512);

            Header = new Header(headerBytes);

            //We need to get all the bytes that make up the SectorAllocationTable
            //start with empty array to hold our bytes
            var satbytes = new byte[Header.TotalSATSectors*Header.SectorSizeAsBytes];

            //fill the bytes
            for (var i = 0; i < Header.SATSectors.Length; i++)
            {
                var satChunk = new byte[Header.SectorSizeAsBytes];
                Buffer.BlockCopy(rawBytes, Header.SATSectors[i], satChunk, 0, Header.SectorSizeAsBytes);

                Buffer.BlockCopy(satChunk, 0, satbytes, i*Header.SectorSizeAsBytes, Header.SectorSizeAsBytes);
            }

            //satbytes now contains a bunch of bytes we need to convert to signed ints which will act as our map to different sectors
            //ints are 4 bytes each
            Sat = new int[satbytes.Length/4];

            //fill the Sat
            for (var i = 0; i < satbytes.Length/4; i++)
            {
                var satAddr = BitConverter.ToInt32(satbytes, i*4);
                Sat[i] = satAddr;
            }

            //Just as with the SAT, but this time, with the SmallSectorAllocationTable
            if (Header.SSATFirstSectorId != -2)
            {
                var ssatbytes = GetDataFromSat(Header.SSATFirstSectorId);

            SSat = new int[ssatbytes.Length/4];

            for (var i = 0; i < ssatbytes.Length/4; i++)
            {
                var ssatAddr = BitConverter.ToInt32(ssatbytes, i*4);
                SSat[i] = ssatAddr;
            }
            }
            else
            {
                
            }
            

            //build our directory items
            //first, get all the bytes we need
            var dirBytes = GetDataFromSat(Header.DirectoryStreamFirstSectorId);

            var dirIndex = 0;

            DirectoryItems = new List<DirectoryItem>();

            //process all directories
            while (dirIndex < dirBytes.Length)
            {
                var dBytes = new byte[128];

                Buffer.BlockCopy(dirBytes, dirIndex, dBytes, 0, 128);

                if (dBytes[66] != 0) //0 is empty directory structure
                {
                    var d = new DirectoryItem(dBytes);

                    DirectoryItems.Add(d);
                }

                dirIndex += 128;
            }


            //the Root Entry directory item contains all the sectors we need for small sector stuff, so get the data and cut it up so we can use it later

            //when we are done we will have a list of byte arrays, each 64 bytes long, that we can string together later based on SSAT
            _shortSectors = new List<byte[]>();

            var rootDir = DirectoryItems.SingleOrDefault(t => t.DirectoryName.ToLowerInvariant() == "root entry");
            if (rootDir != null && rootDir.DirectorySize>0)
            {
                var b = GetDataFromSat((int) rootDir.FirstDirectorySectorId);

                var shortIndex = 0;

                while (shortIndex < b.Length)
                {
                    var shortChunk = new byte[Header.ShortSectorSizeAsBytes];

                    Buffer.BlockCopy(b, shortIndex, shortChunk, 0, Header.ShortSectorSizeAsBytes);

                    _shortSectors.Add(shortChunk);

                    shortIndex += 64;
                }
            }

            var destList = DirectoryItems.SingleOrDefault(t => t.DirectoryName.ToLowerInvariant() == "destlist");
            if (destList != null && destList.DirectorySize>0)
            {
                var destBytes = GetPayloadForDirectory(destList);

                DestList = new DestList(destBytes);
            }
        }

        public Header Header { get; }

        public List<DirectoryItem> DirectoryItems { get; }
        public int[] Sat { get; }
        public int[] SSat { get; }

        public string SourceFile { get; }

        public byte[] GetPayloadForDirectory(DirectoryItem dir)
        {
            byte[] payLoadBytes = null;

            if (dir.DirectorySize > Header.MinimumStandardStreamSize)
            {
                var b = GetDataFromSat((int) dir.FirstDirectorySectorId);

                payLoadBytes = new byte[dir.DirectorySize];
                Buffer.BlockCopy(b, 0, payLoadBytes, 0, dir.DirectorySize);
            }
            else
            {
                var b = GetDataFromSSat((int) dir.FirstDirectorySectorId);

                payLoadBytes = new byte[dir.DirectorySize];
                Buffer.BlockCopy(b, 0, payLoadBytes, 0, dir.DirectorySize);
            }

            return payLoadBytes;
        }

        private byte[] GetDataFromSat(int sectorNumber)
        {
            var sn = sectorNumber;

            var runInfo = new List<int>();

            runInfo.Add(sectorNumber);

            var sectorSize = Header.SectorSizeAsBytes;

            while (Sat[sn] >= 0)
            {
                runInfo.Add(Sat[sn]);
                sn = Sat[sn];
            }

            var retBytes = new byte[sectorSize*runInfo.Count];

            var offset = 0;
            foreach (var i in runInfo)
            {
                var index = 512 + sectorSize*i; //header + relative offset

                var readSize = sectorSize;

                if (_rawBytes.Length - index < sectorSize)
                {
                    readSize = _rawBytes.Length - index;
                }

                Buffer.BlockCopy(_rawBytes, index, retBytes, sectorSize*offset, readSize);
                offset += 1;
            }

            return retBytes;
        }

        private byte[] GetDataFromSSat(int sectorNumber)
        {
            var sn = sectorNumber;

            var runInfo = new List<int>();

            runInfo.Add(sectorNumber);

            var sectorSize = Header.ShortSectorSizeAsBytes;

            while (SSat[sn] >= 0)
            {
                runInfo.Add(SSat[sn]);

                sn = SSat[sn];
            }

            var retBytes = new byte[sectorSize*runInfo.Count];

            var offset = 0;
            foreach (var i in runInfo)
            {
                Buffer.BlockCopy(_shortSectors[i], 0, retBytes, sectorSize*offset, sectorSize);
                offset += 1;
            }

            return retBytes;
        }
    }
}