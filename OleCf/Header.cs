using System;
using System.Collections.Generic;
using System.Text;

namespace OleCf;

/// <summary>
///     The 512 byte compound file header ([MS-CFB] 2.2).
/// </summary>
public class Header
{
    private const ulong _signature = 0xe11ab1a1e011cfd0;

    /// <summary>
    ///     Number of SAT sector ids held in the header itself. More are held in MSAT sectors.
    /// </summary>
    public const int HeaderMsatEntries = 109;

    // Without the file length (public constructor), cap the SAT sector count so a corrupt header can't allocate
    // a huge array. 4M SAT sectors of 512 bytes describe a 256 GB file
    private const int MaxSatSectorsWithoutFileLength = 1 << 22;

    public List<byte[]> SectorIds;

    /// <summary>
    ///     Parses a header.
    /// </summary>
    /// <exception cref="OleCfParseException">Bad signature, big endian, invalid sizes or counts</exception>
    public Header(byte[] rawBytes) : this(rawBytes, -1)
    {
    }

    /// <param name="rawBytes">The header (at least 512 bytes)</param>
    /// <param name="fileLength">Length of the whole file, used to check counts, or -1 when unknown</param>
    internal Header(byte[] rawBytes, long fileLength)
    {
        if (rawBytes == null)
        {
            throw new ArgumentNullException(nameof(rawBytes));
        }

        if (rawBytes.Length < 512)
        {
            throw new OleCfParseException(
                $"The header is {rawBytes.Length} bytes, less than the 512 bytes of an OLE header", 0, "Header");
        }

        var sig = BitConverter.ToUInt64(rawBytes, 0);

        if (sig != _signature)
        {
            throw new OleCfParseException("Invalid signature!", 0, "Signature");
        }

        var classBytes = new byte[16];
        Buffer.BlockCopy(rawBytes, 8, classBytes, 0, 16);

        ClassId = new Guid(classBytes);

        RevisionMinor = BitConverter.ToInt16(rawBytes, 24);
        RevisionMajor = BitConverter.ToInt16(rawBytes, 26);

        IsLittleEndian = BitConverter.ToUInt16(rawBytes, 28) == 0xfffe; //0xfeff == big endian

        if (IsLittleEndian == false)
        {
            throw new OleCfParseException("Big endian compound files are not supported", 28, "ByteOrder");
        }

        SectorSize = BitConverter.ToInt16(rawBytes, 30);
        ShortSectorSize = BitConverter.ToInt16(rawBytes, 32);

        // [MS-CFB] uses 9 (512 bytes, version 3) or 12 (4096 bytes, version 4); allow other sane powers of two
        if (SectorSize < 7 || SectorSize > 16)
        {
            throw new OleCfParseException($"Invalid sector size shift {SectorSize}", 30, "SectorSize");
        }

        if (ShortSectorSize < 1 || ShortSectorSize >= SectorSize)
        {
            throw new OleCfParseException($"Invalid short sector size shift {ShortSectorSize}", 32,
                "ShortSectorSize");
        }

        TotalSATSectors = BitConverter.ToInt32(rawBytes, 44);
        DirectoryStreamFirstSectorId = BitConverter.ToInt32(rawBytes, 48);
        MinimumStandardStreamSize = BitConverter.ToUInt32(rawBytes, 56);

        SSATFirstSectorId = BitConverter.ToInt32(rawBytes, 60);
        TotalSSATSectors = BitConverter.ToUInt32(rawBytes, 64);

        MSATFirstSectorId = BitConverter.ToInt32(rawBytes, 68); //-2 denotes there are no additional msat sectors
        MSATTotalSectors = BitConverter.ToInt32(rawBytes, 72);

        var maxSatSectors = fileLength < 0 ? MaxSatSectorsWithoutFileLength : fileLength / SectorSizeAsBytes + 1;

        if (TotalSATSectors < 0 || TotalSATSectors > maxSatSectors)
        {
            throw new OleCfParseException(
                $"Invalid number of SAT sectors ({TotalSATSectors}) - is this file corrupt?", 44, "TotalSATSectors");
        }

        SectorIds = new List<byte[]>();

        SATSectors = new int[TotalSATSectors];

        for (var i = 0; i < HeaderMsatEntries; i++)
        {
            var sectorId = new byte[4];
            Buffer.BlockCopy(rawBytes, 76 + i * 4, sectorId, 0, 4);

            var satAddr = BitConverter.ToInt32(sectorId, 0);

            if (satAddr >= 0)
            {
                if (i >= SATSectors.Length)
                {
                    throw new OleCfParseException(
                        $"The current sector ({i}) was larger than the total number of sectors expected from the header data ({TotalSATSectors}) - is this file corrupt?",
                        76 + i * 4, "MSAT");
                }

                SATSectors[i] = SectorOffset(satAddr);
            }

            SectorIds.Add(sectorId);
        }
    }

    public Guid ClassId { get; }

    public short RevisionMinor { get; }
    public short RevisionMajor { get; }
    public bool IsLittleEndian { get; }

    public short SectorSize { get; }

    public int SectorSizeAsBytes => 1 << SectorSize;

    public short ShortSectorSize { get; }

    public int ShortSectorSizeAsBytes => 1 << ShortSectorSize;

    public int TotalSATSectors { get; }
    public int DirectoryStreamFirstSectorId { get; }

    public uint MinimumStandardStreamSize { get; }

    public int SSATFirstSectorId { get; }
    public uint TotalSSATSectors { get; }

    public int MSATFirstSectorId { get; }

    public int MSATTotalSectors { get; }

    /// <summary>
    ///     Contains absolute sector addresses for where SAT sectors are located
    /// </summary>
    public int[] SATSectors { get; }

    /// <summary>
    ///     Absolute offset of a sector. The header occupies the first sector (512 bytes, or 4096 with 4096 byte sectors).
    /// </summary>
    public int SectorOffset(int sectorId)
    {
        return (int)Math.Min(int.MaxValue, ((long)sectorId + 1) * SectorSizeAsBytes);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ClassId: {ClassId}");
        sb.AppendLine($"Revision: {RevisionMajor}.{RevisionMinor}");
        sb.AppendLine($"Is Little Endian: {IsLittleEndian}");
        sb.AppendLine();
        sb.AppendLine($"Sector Size: {SectorSize} ({SectorSizeAsBytes} bytes)");
        sb.AppendLine($"Short Sector Size: {ShortSectorSize} ({ShortSectorSizeAsBytes} bytes)");
        sb.AppendLine();

        sb.AppendLine($"Total SAT Sectors: {TotalSATSectors}");
        sb.AppendLine($"Directory Stream First Sector Id: {DirectoryStreamFirstSectorId}");

        sb.AppendLine($"Minimum Standard StreamSize: {MinimumStandardStreamSize}");

        sb.AppendLine();
        sb.AppendLine($"SSAT First SectorId {SSATFirstSectorId}");
        sb.AppendLine($"SSAT Total Sectors {TotalSSATSectors}");

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
                sb.AppendLine(
                    $"Sector #{i}: {BitConverter.ToString(sectorId)} ({val}) Absolute Offset: {SectorOffset(val)}");
            }

            i += 1;
        }

        return sb.ToString();
    }
}
