using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace OleCf
{
    public class Header
    {
        private const ulong _signature = 0xe11ab1a1e011cfd0;
        public List<byte[]> SectorIds;

        public Header(byte[] rawBytes)
        {
            var sig = BitConverter.ToUInt64(rawBytes, 0);

            if (sig != _signature)
            {
                throw new Exception("Invalid signature!");
            }

            var classBytes = new byte[16];
            Buffer.BlockCopy(rawBytes, 8, classBytes, 0, 16);

            ClassId = new Guid(classBytes);

            RevisionMinor = BitConverter.ToInt16(rawBytes, 24);
            RevisionMajor = BitConverter.ToInt16(rawBytes, 26);

            IsLittleEndian = BitConverter.ToUInt16(rawBytes, 28) == 0xfffe; //0xfeff == big endian

            if (IsLittleEndian == false)
            {
                throw new Exception("Handle this case specifically");
            }

            SectorSize =  BitConverter.ToInt16(rawBytes, 30);
            ShortSectorSize = BitConverter.ToInt16(rawBytes, 32);

            SectorSizeAsBytes = (int) Math.Pow(2, SectorSize);
            ShortSectorSizeAsBytes = (int) Math.Pow(2, ShortSectorSize);

            TotalSATSectors = BitConverter.ToInt32(rawBytes, 44);
            DirectoryStreamFirstSectorId = BitConverter.ToInt32(rawBytes, 48);
            MinimumStandardStreamSize = BitConverter.ToUInt32(rawBytes, 56);
            SSATFirstSectorId = BitConverter.ToInt32(rawBytes, 60);
            SSATTotalSectors = BitConverter.ToUInt32(rawBytes, 64);
            MSATFirstSectorId = BitConverter.ToInt32(rawBytes, 68); //-2 denotes there are no additional msat sectors
            MSATTotalSectors = BitConverter.ToInt32(rawBytes, 72);

            SectorIds = new List<byte[]>();

            SATSectors = new int[TotalSATSectors];

            for (var i = 0; i < 109; i++)
            {
                var sectorId = new byte[4];
                Buffer.BlockCopy(rawBytes, 76 + i*4, sectorId, 0, 4);

                var satAddr = BitConverter.ToInt32(sectorId, 0);

                //Debug.WriteLine($"satAddr: {satAddr}");

                if (satAddr >= 0)
                {
                    SATSectors[i] = (BitConverter.ToInt32(sectorId, 0) * SectorSizeAsBytes) + 512; // 512 is for the header
                }
                

                SectorIds.Add(sectorId);
            }

        

        }

        public Guid ClassId { get; }

        public short RevisionMinor { get; }
        public short RevisionMajor { get; }
        public bool IsLittleEndian { get; }

        public short SectorSize { get; }

        public int SectorSizeAsBytes { get; }

        public short ShortSectorSize { get; }
        public int ShortSectorSizeAsBytes { get; }

        public int TotalSATSectors { get; }
        public int DirectoryStreamFirstSectorId { get; }

        public uint MinimumStandardStreamSize { get; }

        public int SSATFirstSectorId { get; }
        public uint SSATTotalSectors { get; }

        public int MSATFirstSectorId { get; }

        public int MSATTotalSectors { get; }

        /// <summary>
        /// Contains absolute sector addresses for where SAT sectors are located
        /// </summary>
        public int[] SATSectors { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"ClassId: {ClassId}");
            sb.AppendLine($"Revision: {RevisionMajor}.{RevisionMinor}");
            sb.AppendLine($"Is Little Endian: {IsLittleEndian}");
            sb.AppendLine();
            sb.AppendLine($"Sector Size: {SectorSize} ({Math.Pow(2,SectorSize)} bytes)");
            sb.AppendLine($"Short Sector Size: {ShortSectorSize} ({Math.Pow(2, ShortSectorSize)} bytes)");
            sb.AppendLine();

            sb.AppendLine($"Total SAT Sectors: {TotalSATSectors}");
            sb.AppendLine($"Directory Stream First Sector Id: {DirectoryStreamFirstSectorId}");

            sb.AppendLine($"Minimum Standard StreamSize: {MinimumStandardStreamSize}");

            sb.AppendLine();
            sb.AppendLine($"SSAT First SectorId {SSATFirstSectorId}");
            sb.AppendLine($"SSAT Total Sectors {SSATTotalSectors}");

            sb.AppendLine();
            sb.AppendLine($"MSAT First SectorId {MSATFirstSectorId}");
            sb.AppendLine($"MSAT Total Sectors {MSATTotalSectors}");

            sb.AppendLine();
            sb.AppendLine("SectorIDs");
            var i = 0;
            foreach (var sectorId in SectorIds)
            {
                var val = BitConverter.ToInt32(sectorId, 0);
                if (val > -1)
                {
                    sb.AppendLine($"Sector #{i}: {BitConverter.ToString(sectorId)} ({val}) Offset: {512 + (val * Math.Pow(2, SectorSize))}");
                }
                
                i += 1;
            }

            return sb.ToString();
        }
    }
}