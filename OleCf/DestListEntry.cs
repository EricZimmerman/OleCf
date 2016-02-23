using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OleCf
{
    public class DestListEntry
    {
        public long Checksum;
        public int EntryNumber;
        public Guid FileBirthDroid;
        public Guid FileDroid;
        public string Hostname;
        public DateTimeOffset LastMod;
        public string Path;
        public int PinStatus;
        public int Unknown0;
        public int Unknown1;
        public int Unknown2;
        public int Unknown3;
        public int Unknown4;
        public Guid VolumeBirthDroid;

        public Guid VolumeDroid;

        public DestListEntry(byte[] rawBytes, int version)
        {
            Checksum = BitConverter.ToInt64(rawBytes, 0);

            var volDroidBytes = new byte[16];
            Buffer.BlockCopy(rawBytes, 8, volDroidBytes, 0, 16);

            VolumeDroid = new Guid(volDroidBytes);

            var fileDroidBytes = new byte[16];
            Buffer.BlockCopy(rawBytes, 24, fileDroidBytes, 0, 16);

            FileDroid = new Guid(fileDroidBytes);

            var volBirthDroidBytes = new byte[16];
            Buffer.BlockCopy(rawBytes, 40, volBirthDroidBytes, 0, 16);

            VolumeBirthDroid = new Guid(volBirthDroidBytes);

            var fileBirthDroidBytes = new byte[16];
            Buffer.BlockCopy(rawBytes, 56, fileBirthDroidBytes, 0, 16);

            FileBirthDroid = new Guid(fileBirthDroidBytes);

            Hostname = Encoding.GetEncoding(1252).GetString(rawBytes, 72, 16).Split('\0').First();

            EntryNumber = BitConverter.ToInt32(rawBytes, 88);
            Unknown0 = BitConverter.ToInt32(rawBytes, 92);
            Unknown1 = BitConverter.ToInt32(rawBytes, 96);

            LastMod = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, 100)).ToUniversalTime();

            PinStatus = BitConverter.ToInt32(rawBytes, 108);

            if (version == 3)
            {
                Unknown2 = BitConverter.ToInt32(rawBytes, 116);
                Unknown3 = BitConverter.ToInt32(rawBytes, 120);
                Unknown4 = BitConverter.ToInt32(rawBytes, 124);
                Path = Encoding.Unicode.GetString(rawBytes, 130, rawBytes.Length - 130).Split('\0').First();
            }
            else
            {
                Path = Encoding.Unicode.GetString(rawBytes, 114, rawBytes.Length - 114).Split('\0').First();
            }

            var tempMac = FileDroid.ToString().Split('-').Last();

            MacAddress = Regex.Replace(tempMac, ".{2}", "$0:");
            MacAddress = MacAddress.Substring(0, MacAddress.Length - 1);

            CreationTime = GetDateTimeOffsetFromGuid(FileDroid);
        }

        public DateTimeOffset CreationTime { get; }
        public string MacAddress { get; }

        private DateTimeOffset GetDateTimeOffsetFromGuid(Guid guid)
        {
            // offset to move from 1/1/0001, which is 0-time for .NET, to gregorian 0-time of 10/15/1582
            var gregorianCalendarStart = new DateTimeOffset(1582, 10, 15, 0, 0, 0, TimeSpan.Zero);
            const int versionByte = 7;
            const int versionByteMask = 0x0f;
            const int versionByteShift = 4;
            const byte timestampByte = 0;

            var bytes = guid.ToByteArray();

            // reverse the version
            bytes[versionByte] &= versionByteMask;
            bytes[versionByte] |= 0x01 >> versionByteShift;

            var timestampBytes = new byte[8];
            Array.Copy(bytes, timestampByte, timestampBytes, 0, 8);

            var timestamp = BitConverter.ToInt64(timestampBytes, 0);
            var ticks = timestamp + gregorianCalendarStart.Ticks;

            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Checksum: {Checksum}");
            sb.AppendLine($"VolumeDroid: {VolumeDroid}");
            sb.AppendLine($"VolumeBirthDroid: {VolumeBirthDroid}");
            sb.AppendLine($"FileDroid: {FileDroid}");
            sb.AppendLine($"FileBirthDroid: {FileBirthDroid}");
            sb.AppendLine($"Hostname: {Hostname}");
            sb.AppendLine($"EntryNumber: {EntryNumber}");
            sb.AppendLine($"LastMod: {LastMod}");
            sb.AppendLine($"PinStatus: {PinStatus}");
            sb.AppendLine($"Path: {Path}");
            sb.AppendLine($"MacAddress: {MacAddress}");
            sb.AppendLine($"CreationTime: {CreationTime}");
            sb.AppendLine($"Unknown0: {Unknown0}");
            sb.AppendLine($"Unknown1: {Unknown1}");
            sb.AppendLine($"Unknown2: {Unknown2}");
            sb.AppendLine($"Unknown3: {Unknown3}");
            sb.AppendLine($"Unknown4: {Unknown4}");

            return sb.ToString();
        }
    }
}