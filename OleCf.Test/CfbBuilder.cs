using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OleCf.Test;

/// <summary>
///     Writes minimal compound files ([MS-CFB]) for tests: a root entry and streams chained as right siblings.
///     Streams below the cutoff go in the mini stream. Output was checked with Python's olefile.
/// </summary>
internal sealed class CfbBuilder
{
    private const int FreeSect = -1;
    private const int EndOfChain = -2;
    private const int FatSect = -3;
    private const int DifSect = -4;

    public int SectorShift { get; init; } = 9;
    public uint MiniStreamCutoff { get; init; } = 4096;
    public Guid RootClassId { get; init; } = Guid.Empty;
    public long RootModifiedFileTime { get; init; }
    public List<(string Name, byte[] Data)> Streams { get; } = new();

    // Filled in by Build, for tests that corrupt the output
    public int SatSectorCount { get; private set; }
    public int MsatSectorCount { get; private set; }
    public int FirstDirectorySector { get; private set; }
    public int[] SatSectorIds { get; private set; }
    public Dictionary<string, int> FirstSectors { get; } = new();

    public int SectorSize => 1 << SectorShift;

    public long SectorOffset(int sector) => ((long)sector + 1) * SectorSize;

    public long DirectoryEntryOffset(int id) => SectorOffset(FirstDirectorySector) + id * 128L;

    /// <summary>
    ///     Offset of a SAT entry. SAT sectors are contiguous, so this works for any sector id.
    /// </summary>
    public long SatEntryOffset(int sector) => SectorOffset(SatSectorIds[0]) + sector * 4L;

    public CfbBuilder Add(string name, byte[] data)
    {
        Streams.Add((name, data));
        return this;
    }

    public byte[] Build()
    {
        var ss = SectorSize;
        var idsPerSector = ss / 4;
        var sectors = new List<byte[]>(); // contents of each sector, by id
        var sat = new List<int>();

        int AddChain(byte[] data)
        {
            var count = (data.Length + ss - 1) / ss;
            if (count == 0)
            {
                return EndOfChain;
            }

            var first = sectors.Count;
            for (var i = 0; i < count; i++)
            {
                var sector = new byte[ss];
                Buffer.BlockCopy(data, i * ss, sector, 0, Math.Min(ss, data.Length - i * ss));
                sectors.Add(sector);
                sat.Add(i == count - 1 ? EndOfChain : first + i + 1);
            }

            return first;
        }

        // Big streams
        foreach (var (name, data) in Streams.Where(t => t.Data.Length >= MiniStreamCutoff))
        {
            FirstSectors[name] = AddChain(data);
        }

        // Mini stream and SSAT
        var mini = new List<byte>();
        var ssat = new List<int>();
        foreach (var (name, data) in Streams.Where(t => t.Data.Length < MiniStreamCutoff))
        {
            var count = (data.Length + 63) / 64;
            if (count == 0)
            {
                FirstSectors[name] = EndOfChain;
                continue;
            }

            var first = ssat.Count;
            FirstSectors[name] = first;
            for (var i = 0; i < count; i++)
            {
                ssat.Add(i == count - 1 ? EndOfChain : first + i + 1);
            }

            mini.AddRange(data);
            mini.AddRange(new byte[count * 64 - data.Length]);
        }

        var miniFirst = AddChain(mini.ToArray());
        var ssatFirst = ssat.Count == 0
            ? EndOfChain
            : AddChain(ToBytes(ssat, FreeSect, (ssat.Count + idsPerSector - 1) / idsPerSector * idsPerSector));

        // Directory
        var entries = new List<byte[]> { Entry("Root Entry", 5, Streams.Count > 0 ? 1 : -1, -1, miniFirst, mini.Count) };
        entries[0] = WithRootExtras(entries[0]);
        for (var i = 0; i < Streams.Count; i++)
        {
            var (name, data) = Streams[i];
            entries.Add(Entry(name, 2, -1, i + 1 < Streams.Count ? i + 2 : -1, FirstSectors[name], data.Length));
        }

        var dirBytes = entries.SelectMany(t => t).ToList();
        while (dirBytes.Count % ss != 0)
        {
            dirBytes.AddRange(EmptyEntry());
        }

        FirstDirectorySector = AddChain(dirBytes.ToArray());

        // SAT and MSAT sectors go last; their count depends on the total
        int satCount = 0, msatCount = 0;
        while (true)
        {
            var total = sectors.Count + satCount + msatCount;
            var newSat = (total + idsPerSector - 1) / idsPerSector;
            var newMsat = newSat > Header.HeaderMsatEntries
                ? (newSat - Header.HeaderMsatEntries + idsPerSector - 2) / (idsPerSector - 1)
                : 0;
            if (newSat == satCount && newMsat == msatCount)
            {
                break;
            }

            satCount = newSat;
            msatCount = newMsat;
        }

        SatSectorCount = satCount;
        MsatSectorCount = msatCount;

        var satIds = Enumerable.Range(sectors.Count, satCount).ToArray();
        var msatIds = Enumerable.Range(sectors.Count + satCount, msatCount).ToArray();
        SatSectorIds = satIds;
        sat.AddRange(satIds.Select(_ => FatSect));
        sat.AddRange(msatIds.Select(_ => DifSect));

        var satBytes = ToBytes(sat, FreeSect, satCount * idsPerSector);
        for (var i = 0; i < satCount; i++)
        {
            sectors.Add(satBytes.Skip(i * ss).Take(ss).ToArray());
        }

        var extraSatIds = satIds.Skip(Header.HeaderMsatEntries).ToList();
        for (var m = 0; m < msatCount; m++)
        {
            var slots = extraSatIds.Skip(m * (idsPerSector - 1)).Take(idsPerSector - 1).ToList();
            var next = m + 1 < msatCount ? msatIds[m + 1] : EndOfChain;
            var sector = ToBytes(slots, FreeSect, idsPerSector - 1).Concat(BitConverter.GetBytes(next)).ToArray();
            sectors.Add(sector);
        }

        // Header, padded to a full sector
        var header = new byte[ss];
        BitConverter.GetBytes(0xe11ab1a1e011cfd0).CopyTo(header, 0);
        BitConverter.GetBytes((short)0x3E).CopyTo(header, 24);
        BitConverter.GetBytes((short)(SectorShift == 12 ? 4 : 3)).CopyTo(header, 26);
        BitConverter.GetBytes((ushort)0xFFFE).CopyTo(header, 28);
        BitConverter.GetBytes((short)SectorShift).CopyTo(header, 30);
        BitConverter.GetBytes((short)6).CopyTo(header, 32);
        BitConverter.GetBytes(SectorShift == 12 ? dirBytes.Count / ss : 0).CopyTo(header, 40);
        BitConverter.GetBytes(satCount).CopyTo(header, 44);
        BitConverter.GetBytes(FirstDirectorySector).CopyTo(header, 48);
        BitConverter.GetBytes(MiniStreamCutoff).CopyTo(header, 56);
        BitConverter.GetBytes(ssatFirst).CopyTo(header, 60);
        BitConverter.GetBytes(ssat.Count == 0 ? 0 : (ssat.Count * 4 + ss - 1) / ss).CopyTo(header, 64);
        BitConverter.GetBytes(msatCount > 0 ? msatIds[0] : EndOfChain).CopyTo(header, 68);
        BitConverter.GetBytes(msatCount).CopyTo(header, 72);
        ToBytes(satIds.Take(Header.HeaderMsatEntries), FreeSect, Header.HeaderMsatEntries).CopyTo(header, 76);

        return header.Concat(sectors.SelectMany(t => t)).ToArray();
    }

    private byte[] WithRootExtras(byte[] entry)
    {
        RootClassId.ToByteArray().CopyTo(entry, 80);
        BitConverter.GetBytes(RootModifiedFileTime).CopyTo(entry, 108);
        return entry;
    }

    private static byte[] Entry(string name, byte type, int child, int right, int start, long size)
    {
        var entry = new byte[128];
        Encoding.Unicode.GetBytes(name).CopyTo(entry, 0);
        BitConverter.GetBytes((short)((name.Length + 1) * 2)).CopyTo(entry, 64);
        entry[66] = type;
        entry[67] = 1; // black
        BitConverter.GetBytes(-1).CopyTo(entry, 68);
        BitConverter.GetBytes(right).CopyTo(entry, 72);
        BitConverter.GetBytes(child).CopyTo(entry, 76);
        BitConverter.GetBytes(start).CopyTo(entry, 116);
        BitConverter.GetBytes(size).CopyTo(entry, 120);
        return entry;
    }

    private static byte[] EmptyEntry()
    {
        var entry = new byte[128];
        BitConverter.GetBytes(-1).CopyTo(entry, 68);
        BitConverter.GetBytes(-1).CopyTo(entry, 72);
        BitConverter.GetBytes(-1).CopyTo(entry, 76);
        return entry;
    }

    private static byte[] ToBytes(IEnumerable<int> values, int fill, int count)
    {
        var list = values.ToList();
        while (list.Count < count)
        {
            list.Add(fill);
        }

        return list.SelectMany(BitConverter.GetBytes).ToArray();
    }

    /// <summary>
    ///     Deterministic test data
    /// </summary>
    public static byte[] Data(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }
}
