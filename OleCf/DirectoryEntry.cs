using System;
using System.Text;

namespace OleCf;

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

    public DirectoryEntry(byte[] rawBytes)
    {
        var dirLen = (int) BitConverter.ToInt16(rawBytes, 64); // includes null terminator
        dirLen = dirLen - 2;

        DirectoryName = Encoding.Unicode.GetString(rawBytes, 0, dirLen);

        DirectoryType = (DirectoryEntryTypes) rawBytes[66];
        NodeColor = (NodeColors) rawBytes[67];

        PreviousDirectoryId = BitConverter.ToInt32(rawBytes, 68);
        NextDirectoryId = BitConverter.ToInt32(rawBytes, 72);
        SubDirectoryId = BitConverter.ToInt32(rawBytes, 76);

        var classBytes = new byte[16];
        Buffer.BlockCopy(rawBytes, 8, classBytes, 0, 16);

        ClassId = new Guid(classBytes);

        UserFlags = BitConverter.ToUInt32(rawBytes, 96);

        CreationTime = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, 100)).ToUniversalTime();
        ModifiedTime = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, 108)).ToUniversalTime();

        if (CreationTime.Value.Year == 1601)
        {
            CreationTime = null;
        }

        if (ModifiedTime.Value.Year == 1601)
        {
            ModifiedTime = null;
        }

        FirstDirectorySectorId = BitConverter.ToUInt32(rawBytes, 116);
        DirectorySize = BitConverter.ToInt32(rawBytes, 120);
    }

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