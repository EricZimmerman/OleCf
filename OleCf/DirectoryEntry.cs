using System;
using System.Collections.Generic;
using System.Text;

namespace OleCf;

/// <summary>
///     A 128 byte directory entry ([MS-CFB] 2.6).
/// </summary>
public class DirectoryEntry
{
    public enum DirectoryEntryTypes
    {
        Empty = 0,
        Storage = 1,
        Stream = 2,
        LockBytes = 3,
        Property = 4,
        RootStorage = 5
    }

    public enum NodeColors
    {
        Red = 0,
        Black = 1
    }

    public DirectoryEntry(byte[] rawBytes) : this(rawBytes, -1)
    {
    }

    /// <param name="rawBytes">The 128 byte entry</param>
    /// <param name="id">Index of the entry in the directory stream, which sibling and child ids refer to</param>
    public DirectoryEntry(byte[] rawBytes, int id)
    {
        if (rawBytes == null)
        {
            throw new ArgumentNullException(nameof(rawBytes));
        }

        if (rawBytes.Length < 128)
        {
            throw new OleCfParseException($"A directory entry is 128 bytes, not {rawBytes.Length}", 0,
                "DirectoryEntry");
        }

        Id = id;

        // The name field is 64 bytes and the length includes the NUL terminator
        var dirLen = (int)BitConverter.ToInt16(rawBytes, 64);
        if (dirLen < 2 || dirLen > 64 || dirLen % 2 != 0)
        {
            ParseErrors.Add($"Invalid name length {dirLen}");
            dirLen = Math.Max(2, Math.Min(64, dirLen & ~1));
        }

        DirectoryName = Encoding.Unicode.GetString(rawBytes, 0, dirLen - 2);

        DirectoryType = (DirectoryEntryTypes)rawBytes[66];
        NodeColor = (NodeColors)rawBytes[67];

        PreviousDirectoryId = BitConverter.ToInt32(rawBytes, 68);
        NextDirectoryId = BitConverter.ToInt32(rawBytes, 72);
        SubDirectoryId = BitConverter.ToInt32(rawBytes, 76);

        var classBytes = new byte[16];
        Buffer.BlockCopy(rawBytes, 80, classBytes, 0, 16);

        ClassId = new Guid(classBytes);

        UserFlags = BitConverter.ToUInt32(rawBytes, 96);

        CreationTime = ReadTime(rawBytes, 100, "Creation time");
        ModifiedTime = ReadTime(rawBytes, 108, "Modified time");

        FirstDirectorySectorId = BitConverter.ToUInt32(rawBytes, 116);
        DirectorySize = BitConverter.ToInt32(rawBytes, 120);
    }

    /// <summary>
    ///     Index of the entry in the directory stream, which <see cref="PreviousDirectoryId" />,
    ///     <see cref="NextDirectoryId" /> and <see cref="SubDirectoryId" /> refer to. -1 when not known.
    /// </summary>
    public int Id { get; }

    public Guid ClassId { get; }
    public uint UserFlags { get; }
    public DateTimeOffset? CreationTime { get; }
    public DateTimeOffset? ModifiedTime { get; }
    public uint FirstDirectorySectorId { get; }
    public int DirectorySize { get; }
    public int PreviousDirectoryId { get; }
    public int NextDirectoryId { get; }
    public int SubDirectoryId { get; }

    public string DirectoryName { get; }

    public DirectoryEntryTypes DirectoryType { get; }

    public NodeColors NodeColor { get; }

    /// <summary>
    ///     Problems with this entry, such as an invalid name length or an out-of-range time.
    /// </summary>
    public List<string> ParseErrors { get; } = new List<string>();

    private DateTimeOffset? ReadTime(byte[] rawBytes, int offset, string field)
    {
        var value = BitConverter.ToInt64(rawBytes, offset);

        if (value == 0)
        {
            return null;
        }

        try
        {
            var time = new DateTimeOffset(DateTime.FromFileTimeUtc(value));

            // Treat times in 1601 (a FILETIME near 0) as not set, as before
            return time.Year == 1601 ? null : time;
        }
        catch (ArgumentOutOfRangeException)
        {
            ParseErrors.Add($"{field} is out of range (0x{value:X16})");
            return null;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Directory Name: {DirectoryName}");
        sb.AppendLine($"Directory Type: {DirectoryType}");
        sb.AppendLine($"Node Color: {NodeColor}");
        sb.AppendLine($"Previous Directory Id: {PreviousDirectoryId}");
        sb.AppendLine($"Next Directory Id: {NextDirectoryId}");
        sb.AppendLine($"Sub Directory Id: {SubDirectoryId}");
        sb.AppendLine($"Class Id: {ClassId}");
        sb.AppendLine($"User Flags: {UserFlags}");
        sb.AppendLine($"Creation Time: {CreationTime}");
        sb.AppendLine($"Modified Time: {ModifiedTime}");
        sb.AppendLine($"First Directory SectorId: {FirstDirectorySectorId}");
        sb.AppendLine($"Directory Size: {DirectorySize}");

        return sb.ToString();
    }
}
