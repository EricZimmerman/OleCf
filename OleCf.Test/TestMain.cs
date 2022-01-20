using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test;

[TestFixture]
public class TestMain
{
    public static string BasePath = @"..\..\TestFiles";
    public static string Win10Path = Path.Combine(BasePath, "Win10");
    public static string Win2K3Path = Path.Combine(BasePath, "Win2k3");
    public static string Win7Path = Path.Combine(BasePath, "Win7");
    public static string Win80Path = Path.Combine(BasePath, "Win80");
    public static string Win81Path = Path.Combine(BasePath, "Win81");
    public static string Win2012Path = Path.Combine(BasePath, "Win2012");
    public static string Win2012R2Path = Path.Combine(BasePath, "Win2012R2");

    public static string MiscPath = Path.Combine(BasePath, "Misc");
    public static string BadPath = Path.Combine(BasePath, "Bad");

    // A bunch of good jump lists that I don't want to share =)
    public static string LocalPath = @"C:\Users\e\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations";

    private readonly List<string> _allPaths = new List<string>
    {
        //MiscPath,
        //WinXpPath,
        Win10Path,
        //Win2K3Path,
        Win7Path,
        Win80Path,
        Win81Path,
        LocalPath
        //Win2012Path,
        //Win2012R2Path,
    };


    [Test]
    public void BaseTests()
    {
        foreach (var allPath in _allPaths)
        {
            foreach (var fname in Directory.GetFiles(allPath))
            {
                Debug.WriteLine(fname);
                var o = OleCf.LoadFile(fname);

                o.Header.Should().NotBeNull();

                Debug.WriteLine(o.Header);

                Debug.WriteLine($"Directory items. total: {o.Directory.Count}");
                foreach (var directoryItem in o.Directory)
                {
                    directoryItem.DirectoryName.Should().NotBeNullOrEmpty();

                    Debug.WriteLine(
                        $"Name: {directoryItem.DirectoryName}, Size: {directoryItem.DirectorySize}, Type: {directoryItem.DirectoryType}");
                }

                Debug.WriteLine("");
            }
        }
    }

    [Test]
    public void OneOff()
    {
        var o =OleCf.LoadFile(@"C:\Temp\f01b4d95cf55d32a.automaticDestinations-ms");
        //   var o =OleCf.LoadFile(@"C:\Temp\5f7b5f1e01b83767.automaticDestinations-ms");


        var destList = o.Directory.SingleOrDefault(t => t.DirectoryName.ToLowerInvariant() == "destlist");
        var foo = o.GetPayloadForDirectory(destList);

        Debug.WriteLine(o.Directory.Count);
    }

    [Test]
    public void InvalidFileShouldThrowException()
    {
        var badFile = Path.Combine(BadPath, @"CALC.EXE-3FBEF7FD.pf");
        Action action = () => OleCf.LoadFile(badFile);

        action.Should().Throw<Exception>().WithMessage("Invalid signature!");
    }
}