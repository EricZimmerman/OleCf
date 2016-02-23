using System;
using System.Text;

namespace OleCf
{
    public class DestListHeader
    {
        public DestListHeader(byte[] rawBytes)
        {
            Version = BitConverter.ToInt32(rawBytes, 0);
            NumberOfEntries = BitConverter.ToInt32(rawBytes, 4);
            NumberOfPinnedEntries = BitConverter.ToInt32(rawBytes, 8);
            Unknown0 = BitConverter.ToInt32(rawBytes, 12);
            LastEntryNumber = BitConverter.ToInt32(rawBytes, 16);
            Unknown1 = BitConverter.ToInt32(rawBytes, 20);
            LastRevisionNumber = BitConverter.ToInt32(rawBytes, 24);
            Unknown2 = BitConverter.ToInt32(rawBytes, 28);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Version: {Version}");
            sb.AppendLine($"NumberOfEntries: {NumberOfEntries}");
            sb.AppendLine($"NumberOfPinnedEntries: {NumberOfPinnedEntries}");
            sb.AppendLine($"LastEntryNumber: {LastEntryNumber}");
            sb.AppendLine($"LastRevisionNumber: {LastRevisionNumber}");
            sb.AppendLine($"Unknown0: {Unknown0}");
            sb.AppendLine($"Unknown1: {Unknown1}");
            sb.AppendLine($"Unknown2: {Unknown2}");

            return sb.ToString();
        }

        public int Version { get; }
        public int NumberOfEntries { get; }
        public int NumberOfPinnedEntries { get; }
        public int Unknown0 { get; }
        public int LastEntryNumber { get; }
        public int Unknown1 { get; }
        public int LastRevisionNumber { get; }
        public int Unknown2 { get; }
    }
}