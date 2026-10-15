using System;
using System.Collections.Generic;
using System.Linq;

namespace OleCf;

/// <summary>
///     An OLE compound file ([MS-CFB]), such as a jump list (*.automaticDestinations-ms).
/// </summary>
public class OleCfFile
{
    private const int DirectoryEntrySize = 128;

    private readonly byte[] _rawBytes;

    // Contents of the mini stream that are present in the file (shorter than the root entry's size if truncated)
    private readonly byte[] _miniStream = new byte[0];

    /// <summary>
    ///     Parses a compound file.
    /// </summary>
    /// <exception cref="OleCfParseException">
    ///     The header, the allocation tables or the directory can't be read (not a compound file, or corrupt)
    /// </exception>
    public OleCfFile(byte[] rawBytes, string sourceFile)
    {
        if (rawBytes == null)
        {
            throw new ArgumentNullException(nameof(rawBytes));
        }

        SourceFile = sourceFile;
        _rawBytes = rawBytes;

        if (rawBytes.Length < 512)
        {
            throw new OleCfParseException(
                $"The file length ({rawBytes.Length}) is less than the length of an OLESS header (512 bytes) - this file ({sourceFile}) is either not an OLE structured storage document or is corrupt.",
                0, "Header");
        }

        var headerBytes = new byte[512];
        Buffer.BlockCopy(rawBytes, 0, headerBytes, 0, 512);

        Header = new Header(headerBytes, rawBytes.Length);

        if (Header.TotalSATSectors > Header.HeaderMsatEntries)
        {
            ReadMsatSectors();
        }

        Sat = ReadSat();

        //Just as with the SAT, but this time, with the SmallSectorAllocationTable
        // ENDOFCHAIN (-2) means there is no SSAT; treat any other negative id the same way
        if (Header.SSATFirstSectorId >= 0)
        {
            var ssatbytes = GetDataFromSat(Header.SSATFirstSectorId, "SSAT", out var ssatTruncated);
            if (ssatTruncated)
            {
                ParseErrors.Add("The SSAT is truncated (the file ends before its last sector)");
            }

            SSat = new int[ssatbytes.Length / 4];

            for (var i = 0; i < SSat.Length; i++)
            {
                SSat[i] = BitConverter.ToInt32(ssatbytes, i * 4);
            }
        }

        Directory = ReadDirectory();

        //the Root Entry directory item holds the mini stream, which small streams are read from in 64 byte sectors via the SSAT
        var rootDir = Directory.FirstOrDefault(t => t.DirectoryType == DirectoryEntry.DirectoryEntryTypes.RootStorage) ??
                      Directory.FirstOrDefault(t => t.DirectoryName.ToLowerInvariant() == "root entry");

        if (rootDir != null && rootDir.DirectorySize > 0)
        {
            _miniStream = GetDataFromSat((int)rootDir.FirstDirectorySectorId, "Mini stream", out var miniTruncated);

            if (miniTruncated && _miniStream.Length < rootDir.DirectorySize)
            {
                ParseErrors.Add(
                    $"The mini stream is truncated: {_miniStream.Length} of {rootDir.DirectorySize} bytes are in the file");
            }
        }
    }

    public Header Header { get; }

    /// <summary>
    ///     Directory entries in use (empty entries are left out). <see cref="DirectoryEntry.Id" /> is each entry's index
    ///     in the directory stream.
    /// </summary>
    public List<DirectoryEntry> Directory { get; }

    public int[] Sat { get; }
    public int[] SSat { get; }

    public string SourceFile { get; }

    /// <summary>
    ///     Problems that didn't stop parsing, such as a damaged directory entry or missing SAT sectors.
    /// </summary>
    public List<string> ParseErrors { get; } = new List<string>();

    /// <summary>
    ///     True when <see cref="ParseErrors" /> is not empty.
    /// </summary>
    public bool ParsingError => ParseErrors.Count > 0;

    /// <summary>
    ///     Reads a stream. Streams smaller than <see cref="global::OleCf.Header.MinimumStandardStreamSize" /> are in the mini
    ///     stream; if the file has no SSAT, null is returned for them.
    ///     If the file ends before the end of the stream (or the stream's chain is shorter than its size), the bytes
    ///     that are there are returned, so the result is shorter than <see cref="DirectoryEntry.DirectorySize" />, and
    ///     the problem is added to <see cref="ParseErrors" />.
    /// </summary>
    /// <exception cref="OleCfParseException">The stream's chain loops, or has a sector id outside the table</exception>
    public byte[] GetPayloadForDirectory(DirectoryEntry dir)
    {
        if (dir == null)
        {
            throw new ArgumentNullException(nameof(dir));
        }

        if (dir.DirectorySize < 0)
        {
            throw new OleCfParseException($"Invalid size ({dir.DirectorySize}) for directory ({dir.DirectoryName})",
                -1, "DirectorySize");
        }

        byte[] b;

        if (dir.DirectorySize >= Header.MinimumStandardStreamSize)
        {
            b = GetDataFromSat((int)dir.FirstDirectorySectorId, dir.DirectoryName, out _);
        }
        else if (SSat != null)
        {
            if (dir.DirectorySize == 0)
            {
                return new byte[0];
            }

            b = GetDataFromSSat((int)dir.FirstDirectorySectorId, dir.DirectoryName);
        }
        else
        {
            return null;
        }

        var length = dir.DirectorySize;

        if (length > b.Length)
        {
            var message =
                $"Stream '{dir.DirectoryName}' is truncated: {b.Length} of {dir.DirectorySize} bytes are in the file";
            if (!ParseErrors.Contains(message))
            {
                ParseErrors.Add(message);
            }

            length = b.Length;
        }

        var payLoadBytes = new byte[length];
        Buffer.BlockCopy(b, 0, payLoadBytes, 0, length);

        return payLoadBytes;
    }

    private void ReadMsatSectors()
    {
        var sectorSize = Header.SectorSizeAsBytes;
        var idsPerSector = sectorSize / 4 - 1; // the last slot points to the next MSAT sector
        var remaining = Header.TotalSATSectors - Header.HeaderMsatEntries;
        var satIndex = Header.HeaderMsatEntries;
        var msatSector = Header.MSATFirstSectorId;
        var visited = new HashSet<int>();

        while (remaining > 0)
        {
            var offset = Header.SectorOffset(msatSector);

            if (msatSector < 0 || !visited.Add(msatSector) || (long)offset + sectorSize > _rawBytes.Length)
            {
                throw new OleCfParseException(
                    $"MSAT sector {msatSector} is invalid, repeated or past the end of the file, with {remaining} SAT sector ids still to read - is this file corrupt?",
                    offset, "MSAT");
            }

            var count = Math.Min(remaining, idsPerSector);
            for (var i = 0; i < count; i++)
            {
                var sectorId = new byte[4];
                Buffer.BlockCopy(_rawBytes, offset + i * 4, sectorId, 0, 4);

                var satAddr = BitConverter.ToInt32(sectorId, 0);
                if (satAddr >= 0)
                {
                    Header.SATSectors[satIndex] = Header.SectorOffset(satAddr);
                }

                Header.SectorIds.Add(sectorId);
                satIndex += 1;
            }

            remaining -= count;
            msatSector = BitConverter.ToInt32(_rawBytes, offset + idsPerSector * 4);
        }
    }

    private int[] ReadSat()
    {
        var sectorSize = Header.SectorSizeAsBytes;
        var sat = new int[Header.TotalSATSectors * (sectorSize / 4)];

        for (var i = 0; i < Header.SATSectors.Length; i++)
        {
            var offset = Header.SATSectors[i];

            // 0 is the header, so the sector id was missing (or negative)
            if (offset == 0 || (long)offset + sectorSize > _rawBytes.Length)
            {
                ParseErrors.Add(offset == 0
                    ? $"SAT sector {i} is missing"
                    : $"SAT sector {i} at 0x{offset:X} is past the end of the file");

                for (var j = 0; j < sectorSize / 4; j++)
                {
                    sat[i * (sectorSize / 4) + j] = -1;
                }

                continue;
            }

            Buffer.BlockCopy(_rawBytes, offset, sat, i * sectorSize, sectorSize);
        }

        return sat;
    }

    private List<DirectoryEntry> ReadDirectory()
    {
        var dirBytes = GetDataFromSat(Header.DirectoryStreamFirstSectorId, "Directory", out var dirTruncated);
        if (dirTruncated)
        {
            ParseErrors.Add("The directory is truncated (the file ends before its last sector)");
        }

        var directory = new List<DirectoryEntry>();

        for (var dirIndex = 0; dirIndex + DirectoryEntrySize <= dirBytes.Length; dirIndex += DirectoryEntrySize)
        {
            var dBytes = new byte[DirectoryEntrySize];
            Buffer.BlockCopy(dirBytes, dirIndex, dBytes, 0, DirectoryEntrySize);

            var dirLen = (int)BitConverter.ToInt16(dBytes, 64);

            if (dBytes[66] != 0 && dirLen > 0) //0 is empty directory structure
            {
                var d = new DirectoryEntry(dBytes, dirIndex / DirectoryEntrySize);

                foreach (var parseError in d.ParseErrors)
                {
                    ParseErrors.Add($"Directory entry {d.Id} ({d.DirectoryName}): {parseError}");
                }

                directory.Add(d);
            }
        }

        return directory;
    }

    /// <summary>
    ///     Follows a chain in the SAT and returns the contents of its sectors that are in the file. A truncated file
    ///     gives fewer bytes (the last sector may be partial) and sets truncated.
    /// </summary>
    private byte[] GetDataFromSat(int sectorNumber, string name, out bool truncated)
    {
        var sectorSize = Header.SectorSizeAsBytes;
        var chain = FollowChain(Sat, sectorNumber, name, "SAT");

        // Only sectors that start inside the file are read, so a corrupt SAT can't make a buffer bigger than the file
        var present = 0;
        while (present < chain.Count && ((long)chain[present] + 1) * sectorSize < _rawBytes.Length)
        {
            present += 1;
        }

        var retBytes = new byte[(long)sectorSize * present];

        var offset = 0;
        truncated = present < chain.Count;

        for (var i = 0; i < present; i++)
        {
            long index = Header.SectorOffset(chain[i]);

            var readSize = (int)Math.Min(sectorSize, _rawBytes.Length - index);
            Buffer.BlockCopy(_rawBytes, (int)index, retBytes, offset, readSize);

            offset += readSize;

            if (readSize < sectorSize)
            {
                // Only the last sector in the file can be partial; nothing after it is in the file
                truncated = i < chain.Count - 1 || truncated;
                break;
            }
        }

        if (offset < retBytes.Length)
        {
            Array.Resize(ref retBytes, offset);
        }

        return retBytes;
    }

    /// <summary>
    ///     Follows a chain in the SSAT and returns the contents of its short sectors that are in the mini stream
    /// </summary>
    private byte[] GetDataFromSSat(int sectorNumber, string name)
    {
        var sectorSize = Header.ShortSectorSizeAsBytes;
        var chain = FollowChain(SSat, sectorNumber, name, "SSAT");

        var retBytes = new byte[sectorSize * chain.Count];

        var offset = 0;
        foreach (var sector in chain)
        {
            var start = (long)sector * sectorSize;
            if (start >= _miniStream.Length)
            {
                break;
            }

            var readSize = (int)Math.Min(sectorSize, _miniStream.Length - start);
            Buffer.BlockCopy(_miniStream, (int)start, retBytes, offset, readSize);
            offset += readSize;

            if (readSize < sectorSize)
            {
                break;
            }
        }

        if (offset < retBytes.Length)
        {
            Array.Resize(ref retBytes, offset);
        }

        return retBytes;
    }

    /// <summary>
    ///     Sector ids of a chain, stopping at the first negative entry. Throws on an id outside the table or a loop.
    /// </summary>
    private static List<int> FollowChain(int[] table, int first, string name, string tableName)
    {
        var chain = new List<int>();
        var visited = new HashSet<int>();
        var sector = first;

        while (sector >= 0)
        {
            if (sector >= table.Length)
            {
                throw new OleCfParseException(
                    $"Sector {sector} of '{name}' is outside the {tableName} ({table.Length} entries) - is this file corrupt?",
                    -1, name);
            }

            if (!visited.Add(sector))
            {
                throw new OleCfParseException(
                    $"The {tableName} chain of '{name}' loops back to sector {sector} - is this file corrupt?", -1,
                    name);
            }

            chain.Add(sector);
            sector = table[sector];
        }

        if (chain.Count == 0)
        {
            throw new OleCfParseException($"'{name}' starts at invalid sector {first}", -1, name);
        }

        return chain;
    }
}
