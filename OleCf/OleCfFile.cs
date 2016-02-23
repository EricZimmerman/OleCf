using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OleCf
{
    public class OleCfFile
    {
        private readonly byte[] _rawBytes;
        private readonly List<byte[]> _shortSectors;

        public OleCfFile(byte[] rawBytes, string sourceFile)
        {
            SourceFile = sourceFile;
            _rawBytes = rawBytes;
            var headerBytes = new byte[512];

            Buffer.BlockCopy(rawBytes, 0, headerBytes, 0, 512);

            Header = new Header(headerBytes);


            var satbytes = new byte[Header.TotalSATSectors*Header.SectorSizeAsBytes];

            for (var i = 0; i < Header.SATSectors.Length; i++)
            {
                //go to offset stored at i
                //get sectorsizebytes
                //copy to satbytes

                var satChunk = new byte[Header.SectorSizeAsBytes];
                Buffer.BlockCopy(rawBytes, Header.SATSectors[i], satChunk, 0, Header.SectorSizeAsBytes);

                //   Debug.WriteLine($"satChunk sig: {BitConverter.ToInt32(satChunk,0)}");

                Buffer.BlockCopy(satChunk, 0, satbytes, i*Header.SectorSizeAsBytes, Header.SectorSizeAsBytes);
            }

            Sat = new int[satbytes.Length/4];

            //Debug.WriteLine(BitConverter.ToString(satbytes));

            for (var i = 0; i < satbytes.Length/4; i++)
            {
                var satAddr = BitConverter.ToInt32(satbytes, i*4);
                Sat[i] = satAddr;
            }


            // DumpSat();

            var ssatbytes = GetDataFromSat(Header.SSATFirstSectorId);

            SSat = new int[ssatbytes.Length/4];

            for (var i = 0; i < ssatbytes.Length/4; i++)
            {
                var ssatAddr = BitConverter.ToInt32(ssatbytes, i*4);
                SSat[i] = ssatAddr;
            }

            //  DumpSSat();

            var dirBytes = GetDataFromSat(Header.DirectoryStreamFirstSectorId);

            //Buffer.BlockCopy(rawBytes,(Header.DirectoryStreamFirstSectorId * Header.SectorSizeAsBytes)+512,dirBytes,0,Header.SectorSizeAsBytes);

            var dirIndex = 0;

            DirectoryItems = new List<DirectoryItem>();

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


            //the Root Entry directory item contains all the sectors we need for short sector stuff, so get the data and cut it up so we can use it later

            _shortSectors = new List<byte[]>();

            var rootDir = DirectoryItems.SingleOrDefault(t => t.DirectoryName.ToLowerInvariant() == "root entry");
            if (rootDir != null)
            {
                var b = GetDataFromSat((int) rootDir.FirstDirectorySectorId);


                var shortIndex = 0;
                var shortCounter = 0;

                while (shortIndex < b.Length)
                {
                    var shortChunk = new byte[Header.ShortSectorSizeAsBytes];

                    Buffer.BlockCopy(b, shortIndex, shortChunk, 0, Header.ShortSectorSizeAsBytes);

                    _shortSectors.Add(shortChunk);

                    shortIndex += 64;
                    shortCounter += 1;
                }
            }


            Debug.WriteLine(DirectoryItems.Count);


            foreach (var directoryItem in DirectoryItems)
            {
                if (directoryItem.DirectoryName.ToLowerInvariant() == "root entry")
                {
                    continue;
                }

                Debug.WriteLine($"Name: {directoryItem.DirectoryName}, Size: {directoryItem.DirectorySize}");

                if (directoryItem.DirectoryName == "d")
                    Debug.WriteLine(1);


                byte[] payLoadBytes = null;

                if (directoryItem.DirectorySize > Header.MinimumStandardStreamSize)
                {
                    var b = GetDataFromSat((int) directoryItem.FirstDirectorySectorId);

                    payLoadBytes = new byte[directoryItem.DirectorySize];
                    Buffer.BlockCopy(b, 0, payLoadBytes, 0, directoryItem.DirectorySize);
                }
                else
                {
                    var b = GetDataFromSSat((int) directoryItem.FirstDirectorySectorId);

                    payLoadBytes = new byte[directoryItem.DirectorySize];
                    Buffer.BlockCopy(b, 0, payLoadBytes, 0, directoryItem.DirectorySize);
                }

                var ddd = Path.GetFileNameWithoutExtension(sourceFile);
                var basePath = Path.Combine(@"C:\temp", ddd);
                if (Directory.Exists(basePath))
                {
                    try
                    {
                        Directory.Delete(basePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                Directory.CreateDirectory(basePath);

                var rf = Path.Combine(@"C:\temp\", basePath, directoryItem.DirectoryName + ".lnk.test");


                if (payLoadBytes[0] == 0x4c)
                {
                    File.WriteAllBytes(rf, payLoadBytes);
                }

                
            }
        }

        public Header Header { get; }

        public List<DirectoryItem> DirectoryItems { get; }
        public int[] Sat { get; }
        public int[] SSat { get; }

        public string SourceFile { get; }

        private void DumpSat()
        {
            Debug.WriteLine("SAT");
            for (var i = 0; i < Sat.Length; i++)
            {
                Debug.WriteLine($"Slot {i}: {Sat[i]}");
            }
            Debug.WriteLine("----------------");
        }

        private void DumpSSat()
        {
            Debug.WriteLine("SSAT");
            for (var i = 0; i < SSat.Length; i++)
            {
                Debug.WriteLine($"SSlot {i}: {SSat[i]}");
            }
            Debug.WriteLine("----------------");
        }

        private byte[] GetDataFromSat(int sectorNumber)
        {
            var sn = sectorNumber;

            var runInfo = new List<int>();

            runInfo.Add(sectorNumber);

            var _sectorSize = Header.SectorSizeAsBytes;


            while (Sat[sn] >= 0)
            {
                runInfo.Add(Sat[sn]);
                sn = Sat[sn];
            }

            //  Debug.WriteLine($"Run info len: {runInfo.Count}");

            //this needs to handle short sectors too

            var retBytes = new byte[_sectorSize*runInfo.Count];

            var offset = 0;
            foreach (var i in runInfo)
            {
                var index = 512 + _sectorSize*i; //header + relative offset

                var readSize = _sectorSize;

                if (_rawBytes.Length - index < _sectorSize)
                {
                    readSize = _rawBytes.Length - index;
                }

                Buffer.BlockCopy(_rawBytes, index, retBytes, _sectorSize*offset, readSize);
                offset += 1;
            }

            return retBytes;
        }

        private byte[] GetDataFromSSat(int sectorNumber)
        {
            var sn = sectorNumber;

            var runInfo = new List<int>();

            runInfo.Add(sectorNumber);

            var _sectorSize = Header.ShortSectorSizeAsBytes;

            while (SSat[sn] >= 0)
            {
                //    Debug.WriteLine($"{Sat[sn]} 0x{Sat[sn]:x}");

                runInfo.Add(SSat[sn]);

                sn = SSat[sn];
            }


            var retBytes = new byte[_sectorSize*runInfo.Count];

            var offset = 0;
            foreach (var i in runInfo)
            {
                Buffer.BlockCopy(_shortSectors[i], 0, retBytes, _sectorSize*offset, _sectorSize);
                offset += 1;
            }

            return retBytes;
        }
    }
}