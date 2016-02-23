using System;
using System.Linq;
using System.Text;

namespace OleCf
{
    public class DestListEntry
    {
        public long Checksum;
      
        public Guid VolumeDroid;
        public Guid FileDroid;
        public Guid VolumeBirthDroid;
        public Guid FileBirthDroid;
        public string Hostname;
        public int EntryNumber;
        public int Unknown0;
        public int Unknown1;
        public DateTimeOffset LastMod;
        public int PinStatus;
        public string Path;

        public DestListEntry(byte[] rawBytes)
        {
            Checksum = BitConverter.ToInt64(rawBytes, 0);

          var  volDroidBytes = new byte[16];
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


            //at offset 112 is the size of the path found at offset 114
            var pathSize = BitConverter.ToInt16(rawBytes, 112);

            Path = Encoding.Unicode.GetString(rawBytes, 114,rawBytes.Length - 114);
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

            return sb.ToString();
        }
    }
}