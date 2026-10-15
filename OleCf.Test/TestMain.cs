using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test;

/// <summary>
///     Assertions on the real jump lists in TestFiles/. Expected names, sizes and MD5s (first 12 hex digits) come
///     from Python's olefile.
/// </summary>
[TestFixture]
public class TestMain
{
    internal static string Sample(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "TestFiles" }.Concat(parts).ToArray());

    internal static string Md5(byte[] data) => Convert.ToHexString(MD5.HashData(data))[..12].ToLowerInvariant();

    private static readonly object[] Streams =
    {
        new object[] { "Win10", "7e4dca80246863e3", "1", 203, "31704fe0f78e" },
        new object[] { "Win10", "7e4dca80246863e3", "DestList", 332, "35ab50b26849" },
        new object[] { "Win10", "f01b4d95cf55d32a", "1", 678, "52b192223bc4" },
        new object[] { "Win10", "f01b4d95cf55d32a", "DestList", 1812, "46a03cb817b2" },
        new object[] { "Win10", "f01b4d95cf55d32a", "9", 541, "37b6482ba2e0" },
        new object[] { "Win7", "1b4dd67f29cb1962", "1", 411, "a7e22e886662" },
        new object[] { "Win7", "1b4dd67f29cb1962", "DestList", 960, "1105a574d3ec" },
        new object[] { "Win7", "1b4dd67f29cb1962", "4", 408, "6a1755783e5a" },
        new object[] { "Win80", "f01b4d95cf55d32a", "DestList", 960, "e55a1fbfffdf" },
        new object[] { "Win80", "f01b4d95cf55d32a", "4", 436, "2613fa5019cb" },
        new object[] { "Win81", "f01b4d95cf55d32a", "DestList", 960, "b2c0f32680ab" },
        new object[] { "Win81", "f01b4d95cf55d32a", "4", 436, "18ed87372181" }
    };

    [TestCaseSource(nameof(Streams))]
    public void StreamPayloadMatchesOlefile(string folder, string appId, string name, int size, string md5)
    {
        var o = OleCf.LoadFile(Sample(folder, appId + ".automaticDestinations-ms"));

        var payload = o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == name));

        payload.Should().HaveCount(size);
        Md5(payload).Should().Be(md5);
    }

    [TestCase("Win10", "5f7b5f1e01b83767", 2)]
    [TestCase("Win10", "7e4dca80246863e3", 3)]
    [TestCase("Win10", "f01b4d95cf55d32a", 11)]
    [TestCase("Win7", "1b4dd67f29cb1962", 6)]
    [TestCase("Win80", "f01b4d95cf55d32a", 6)]
    [TestCase("Win81", "f01b4d95cf55d32a", 6)]
    public void EverySampleParsesCleanly(string folder, string appId, int entries)
    {
        var o = OleCf.LoadFile(Sample(folder, appId + ".automaticDestinations-ms"));

        o.Header.SectorSizeAsBytes.Should().Be(512);
        o.Header.ShortSectorSizeAsBytes.Should().Be(64);
        o.Directory.Should().HaveCount(entries);
        o.Directory[0].DirectoryName.Should().Be("Root Entry");
        o.Directory[0].DirectoryType.Should().Be(DirectoryEntry.DirectoryEntryTypes.RootStorage);
        o.Directory.Select(t => t.Id).Should().Equal(Enumerable.Range(0, entries));
        o.ParseErrors.Should().BeEmpty();

        foreach (var entry in o.Directory.Where(t => t.DirectoryType == DirectoryEntry.DirectoryEntryTypes.Stream))
        {
            entry.ParseErrors.Should().BeEmpty();
            var payload = o.GetPayloadForDirectory(entry);

            // Small streams come from the mini stream, so a file without one (5f7b...) gives null
            if (o.SSat == null && entry.DirectorySize < o.Header.MinimumStandardStreamSize)
            {
                payload.Should().BeNull();
            }
            else
            {
                payload.Should().HaveCount(entry.DirectorySize);
            }
        }
    }

    [Test]
    public void EmptyStreamInFileWithoutSsatIsNull()
    {
        // The Win10 5f7b... jump list has an empty DestList and no mini stream at all
        var o = OleCf.LoadFile(Sample("Win10", "5f7b5f1e01b83767.automaticDestinations-ms"));

        o.SSat.Should().BeNull();
        o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == "DestList")).Should().BeNull();
    }

    [Test]
    public void InvalidFileShouldThrowException()
    {
        var badFile = Sample("Bad", "CALC.EXE-3FBEF7FD.pf");
        Action action = () => OleCf.LoadFile(badFile);

        // Still an Exception with the same message for existing callers
        action.Should().Throw<OleCfParseException>().WithMessage("Invalid signature!")
            .Where(e => e.Field == "Signature" && e.Offset == 0);
    }
}
