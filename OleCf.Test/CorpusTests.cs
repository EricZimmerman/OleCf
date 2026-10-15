using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test;

/// <summary>
///     Runs over a local collection of real jump lists that is not in the repository: the "Jump lists and lnks"
///     folder next to the repository, or the folder in the JUMPLIST_CORPUS environment variable. Skipped when missing.
///     Every stream in it was checked against Python's olefile.
/// </summary>
[TestFixture]
public class CorpusTests
{
    internal static string CorpusPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("JUMPLIST_CORPUS");
        if (!string.IsNullOrEmpty(fromEnvironment) && Directory.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Jump lists and lnks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        Assert.Ignore("Jump list corpus not found");
        return null;
    }

    [Test]
    public void EveryAutomaticDestinationLoadsAndEveryStreamReads()
    {
        var files = Directory.GetFiles(CorpusPath(), "*.automaticDestinations-ms", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();

        var streams = 0;
        foreach (var file in files)
        {
            var o = OleCf.LoadFile(file);
            var truncated = o.ParseErrors.Any(t => t.Contains("truncated"));

            foreach (var entry in o.Directory.Where(t => t.DirectoryType == DirectoryEntry.DirectoryEntryTypes.Stream))
            {
                var payload = o.GetPayloadForDirectory(entry);
                if (payload == null)
                {
                    continue;
                }

                if (!truncated)
                {
                    payload.Should().HaveCount(entry.DirectorySize, $"{file} {entry.DirectoryName}");
                }

                streams += 1;
            }

            o.ParseErrors.Where(t => !t.Contains("truncated")).Should().BeEmpty(file);
        }

        streams.Should().BeGreaterThan(20000);
    }

    [Test]
    public void TruncatedJumpListGivesWhatOlefileGives()
    {
        var file = Path.Combine(CorpusPath(), "Tom", "AutomaticDestinations", "9b9cdc69c1c24e2b.automaticDestinations-ms");
        if (!File.Exists(file))
        {
            Assert.Ignore("Sample not in the corpus");
        }

        var o = OleCf.LoadFile(file);

        int Length(string name) => o.GetPayloadForDirectory(o.Directory.Single(t => t.DirectoryName == name)).Length;

        // olefile reads 2611, 320 and 128 bytes of these (sizes 2611, 504, 696)
        Length("1").Should().Be(2611);
        Length("DestList").Should().Be(320);
        Length("2").Should().Be(128);
        o.ParseErrors.Should().Contain("Stream 'DestList' is truncated: 320 of 504 bytes are in the file");
    }
}
