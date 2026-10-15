using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test;

/// <summary>
///     Layouts the jump list samples don't have, built with <see cref="CfbBuilder" />
/// </summary>
[TestFixture]
public class StructureTests
{
    private static OleCfFile Load(CfbBuilder builder) => new OleCfFile(builder.Build(), "built");

    private static byte[] Payload(OleCfFile o, string name) =>
        o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == name));

    [TestCase(9)]
    [TestCase(12)]
    public void SmallAndBigStreams(int sectorShift)
    {
        var small = CfbBuilder.Data(300, 1);
        var big = CfbBuilder.Data(10000, 2);
        var exact = CfbBuilder.Data(4096, 3);

        var o = Load(new CfbBuilder { SectorShift = sectorShift }
            .Add("small", small).Add("big", big).Add("exact", exact));

        o.Header.SectorSizeAsBytes.Should().Be(1 << sectorShift);
        o.ParseErrors.Should().BeEmpty();
        Payload(o, "small").Should().Equal(small);
        Payload(o, "big").Should().Equal(big);
        Payload(o, "exact").Should().Equal(exact);
    }

    [Test]
    public void SatSectorOffsetsAccountForTheHeaderSector()
    {
        // With 4096 byte sectors the header occupies all of sector -1, so sector n is at (n + 1) * 4096
        var builder = new CfbBuilder { SectorShift = 12 }.Add("big", CfbBuilder.Data(20000, 2));
        var o = Load(builder);

        o.Header.SATSectors[0].Should().Be((int)builder.SectorOffset(builder.SatSectorIds[0]));
        o.Header.SectorOffset(0).Should().Be(4096);
    }

    [TestCase(10 * 1024 * 1024, 1)]
    [TestCase(20 * 1024 * 1024, 2)]
    [TestCase(26 * 1024 * 1024, 3)]
    public void LargeFileWithMsatSectors(int size, int msatSectors)
    {
        var huge = CfbBuilder.Data(size, 4);
        var builder = new CfbBuilder().Add("huge", huge).Add("small", CfbBuilder.Data(100, 5));

        var o = Load(builder);

        builder.MsatSectorCount.Should().Be(msatSectors);
        o.Header.TotalSATSectors.Should().Be(builder.SatSectorCount);
        o.ParseErrors.Should().BeEmpty();
        Payload(o, "huge").Should().Equal(huge);
        Payload(o, "small").Should().Equal(CfbBuilder.Data(100, 5));
    }

    [Test]
    public void EmptyStreamWithMiniStreamIsEmpty()
    {
        var o = Load(new CfbBuilder().Add("data", CfbBuilder.Data(100, 1)).Add("empty", new byte[0]));

        Payload(o, "empty").Should().BeEmpty();
    }

    [Test]
    public void DirectoryEntryFieldsAreReadFromTheRightOffsets()
    {
        var classId = Guid.Parse("00021401-0000-0000-c000-000000000046");
        var modified = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);

        var o = Load(new CfbBuilder { RootClassId = classId, RootModifiedFileTime = modified.ToFileTime() }
            .Add("a", CfbBuilder.Data(10, 1)).Add("b", CfbBuilder.Data(10, 2)));

        var root = o.Directory[0];
        root.ClassId.Should().Be(classId);
        root.ModifiedTime.Should().Be(modified);
        root.CreationTime.Should().BeNull();
        root.SubDirectoryId.Should().Be(1);

        o.Directory[1].Id.Should().Be(1);
        o.Directory[1].NextDirectoryId.Should().Be(2);
        o.Directory[2].NextDirectoryId.Should().Be(-1);
    }
}
