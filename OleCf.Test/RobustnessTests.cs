using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test;

/// <summary>
///     Corrupt files built with <see cref="CfbBuilder" />. Damage to the structure throws OleCfParseException;
///     damage to a single entry goes to ParseErrors.
/// </summary>
[TestFixture]
public class RobustnessTests
{
    private static CfbBuilder Builder() =>
        new CfbBuilder().Add("small", CfbBuilder.Data(300, 1)).Add("big", CfbBuilder.Data(10000, 2));

    private static void Write(byte[] data, long offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(data, offset);

    private static void Write(byte[] data, long offset, short value) =>
        BitConverter.GetBytes(value).CopyTo(data, offset);

    /// <summary>
    ///     Runs act on another thread so a regression to an endless loop fails instead of hanging the run
    /// </summary>
    private static void Within10Seconds(Action act)
    {
        var task = Task.Run(act);
        task.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("parsing should not hang");
    }

    [TestCase(0)]
    [TestCase(511)]
    public void FileSmallerThanTheHeaderThrows(int size)
    {
        Action act = () => new OleCfFile(new byte[size], "x");

        act.Should().Throw<OleCfParseException>().Where(e => e.Field == "Header");
    }

    [TestCase(30, (short)4, "SectorSize")]
    [TestCase(30, (short)40, "SectorSize")]
    [TestCase(32, (short)0, "ShortSectorSize")]
    [TestCase(32, (short)9, "ShortSectorSize")]
    public void InvalidSectorSizesThrow(int offset, short value, string field)
    {
        var data = Builder().Build();
        Write(data, offset, value);

        Action act = () => new OleCfFile(data, "x");

        act.Should().Throw<OleCfParseException>().Where(e => e.Field == field);
    }

    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    [TestCase(1000)]
    public void InvalidSatSectorCountThrows(int count)
    {
        var data = Builder().Build();
        Write(data, 44, count);

        Action act = () => new OleCfFile(data, "x");

        act.Should().Throw<OleCfParseException>().Where(e => e.Field == "TotalSATSectors");
    }

    [Test]
    public void BigEndianThrows()
    {
        var data = Builder().Build();
        Write(data, 28, unchecked((short)0xFEFF));

        Action act = () => new OleCfFile(data, "x");

        act.Should().Throw<OleCfParseException>().Where(e => e.Field == "ByteOrder");
    }

    [Test]
    public void LoopInADirectoryChainThrows()
    {
        var builder = Builder();
        var data = builder.Build();
        // The directory sector points back to itself
        Write(data, builder.SatEntryOffset(builder.FirstDirectorySector), builder.FirstDirectorySector);

        Within10Seconds(() =>
        {
            Action act = () => new OleCfFile(data, "x");
            act.Should().Throw<OleCfParseException>().WithMessage("*loops back*");
        });
    }

    [Test]
    public void LoopInAStreamChainThrowsOnlyForThatStream()
    {
        var builder = Builder();
        var data = builder.Build();
        var first = builder.FirstSectors["big"];
        Write(data, builder.SatEntryOffset(first + 1), first);

        Within10Seconds(() =>
        {
            var o = new OleCfFile(data, "x");

            o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "small")).Should()
                .Equal(CfbBuilder.Data(300, 1));
            Action act = () => o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big"));
            act.Should().Throw<OleCfParseException>().WithMessage("*loops back*");
        });
    }

    [TestCase(1_000_000)]
    [TestCase(int.MaxValue)]
    public void SectorIdOutsideTheSatThrows(int sector)
    {
        var builder = Builder();
        var data = builder.Build();
        Write(data, builder.DirectoryEntryOffset(2) + 116, sector);

        var o = new OleCfFile(data, "x");
        Action act = () => o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big"));

        act.Should().Throw<OleCfParseException>().WithMessage("*outside the SAT*");
    }

    [Test]
    public void SectorPastTheEndOfTheFileGivesThePartOfTheStreamBeforeIt()
    {
        // Sector 120 is inside the SAT (128 entries) but past the end of this ~17 KB file
        var builder = Builder();
        var data = builder.Build();
        Write(data, builder.SatEntryOffset(builder.FirstSectors["big"]), 120);

        var o = new OleCfFile(data, "x");
        var payload = o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big"));

        payload.Should().Equal(CfbBuilder.Data(10000, 2).Take(512));
        o.ParseErrors.Should().ContainSingle().Which.Should().Be("Stream 'big' is truncated: 512 of 10000 bytes are in the file");
    }

    [Test]
    public void StreamLargerThanItsSectorsGivesWhatIsThere()
    {
        var builder = Builder();
        var data = builder.Build();
        Write(data, builder.DirectoryEntryOffset(2) + 120, 20000);

        var o = new OleCfFile(data, "x");
        var payload = o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big"));

        // 20 sectors of 512 bytes; the data is followed by the zero padding of its last sector
        payload.Should().HaveCount(10240);
        payload.Take(10000).Should().Equal(CfbBuilder.Data(10000, 2));
        o.ParseErrors.Should().ContainSingle().Which.Should().Contain("10240 of 20000 bytes");
    }

    [Test]
    public void TruncatedMiniStreamGivesPartialSmallStreams()
    {
        // As in a real truncated jump list: the mini stream's second sector is past the end of the file. "small"
        // (300 bytes) fills short sectors 0-4 of the mini stream's first 512 byte sector, "tiny" starts in the second
        var builder = new CfbBuilder().Add("small", CfbBuilder.Data(300, 1)).Add("pad", CfbBuilder.Data(200, 3))
            .Add("tiny", CfbBuilder.Data(100, 4));
        var data = builder.Build();
        var o0 = new OleCfFile(data, "x");
        var miniFirst = (int)o0.Directory[0].FirstDirectorySectorId;
        Write(data, builder.SatEntryOffset(miniFirst), 120); // inside the SAT, past the end of the file

        var o = new OleCfFile(data, "x");

        o.ParseErrors.Should().ContainSingle().Which.Should().StartWith("The mini stream is truncated: 512 of");
        o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "small")).Should()
            .Equal(CfbBuilder.Data(300, 1));
        o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "tiny")).Should().BeEmpty();
        o.ParseErrors.Should().Contain("Stream 'tiny' is truncated: 0 of 100 bytes are in the file");
    }

    [Test]
    public void NegativeStreamSizeThrows()
    {
        var builder = Builder();
        var data = builder.Build();
        Write(data, builder.DirectoryEntryOffset(2) + 120, -5);

        var o = new OleCfFile(data, "x");
        Action act = () => o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big"));

        act.Should().Throw<OleCfParseException>().Where(e => e.Field == "DirectorySize");
    }

    [TestCase((short)1)]
    [TestCase((short)3)]
    [TestCase((short)200)]
    public void BadNameLengthIsReportedAndTheEntryIsKept(short length)
    {
        var builder = Builder();
        var data = builder.Build();
        Write(data, builder.DirectoryEntryOffset(1) + 64, length);

        var o = new OleCfFile(data, "x");

        o.Directory.Should().HaveCount(3);
        o.Directory[1].ParseErrors.Should().ContainSingle().Which.Should().Contain($"Invalid name length {length}");
        o.ParseErrors.Should().ContainSingle().Which.Should().StartWith("Directory entry 1");
    }

    [Test]
    public void OutOfRangeTimeIsReportedAndNull()
    {
        var builder = Builder();
        var data = builder.Build();
        BitConverter.GetBytes(-5L).CopyTo(data, builder.DirectoryEntryOffset(1) + 100);

        var o = new OleCfFile(data, "x");

        o.Directory[1].CreationTime.Should().BeNull();
        o.Directory[1].ParseErrors.Should().ContainSingle().Which.Should().Contain("Creation time");
    }

    [Test]
    public void MissingSatSectorIsReported()
    {
        // Claim two SAT sectors when the header lists one
        var data = Builder().Build();
        Write(data, 44, 2);

        var o = new OleCfFile(data, "x");

        o.ParseErrors.Should().ContainSingle().Which.Should().Be("SAT sector 1 is missing");
        o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "big")).Should()
            .Equal(CfbBuilder.Data(10000, 2));
    }

    [Test]
    public void NullArgumentsThrow()
    {
        ((Action)(() => new OleCfFile(null, "x"))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new Header(null))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new DirectoryEntry(null))).Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void RandomCorruptionOnlyThrowsParseExceptions()
    {
        var random = new Random(11);
        var good = new CfbBuilder().Add("a", CfbBuilder.Data(300, 1)).Add("b", CfbBuilder.Data(5000, 2))
            .Add("c", new byte[0]).Build();

        Within10Seconds(() =>
        {
            for (var i = 0; i < 3000; i++)
            {
                var data = (byte[])good.Clone();
                for (var flips = random.Next(1, 10); flips > 0; flips--)
                {
                    data[random.Next(data.Length)] = (byte)random.Next(256);
                }

                try
                {
                    var o = new OleCfFile(data, "x");
                    foreach (var entry in o.Directory)
                    {
                        try
                        {
                            o.GetPayloadForDirectory(entry);
                        }
                        catch (OleCfParseException)
                        {
                            // Expected for a damaged stream
                        }
                    }
                }
                catch (OleCfParseException)
                {
                    // Expected for a damaged header, SAT or directory
                }
            }
        });
    }
}
