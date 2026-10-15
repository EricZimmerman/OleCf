# OleCf
Library to process the OLE compound file format ([MS-CFB]). It was initially written for jump list parsing (`*.automaticDestinations-ms`), and is used by [JumpList](https://github.com/EricZimmerman/JumpList).

```csharp
var ole = OleCf.OleCf.LoadFile(@"C:\Temp\f01b4d95cf55d32a.automaticDestinations-ms");
foreach (var entry in ole.Directory)
{
    var bytes = ole.GetPayloadForDirectory(entry);
}
```

- Sector sizes of 512 bytes (version 3) and 4096 bytes (version 4), and files large enough to need MSAT sectors.
- `OleCfParseException` (with `Offset` and `Field`) is thrown when the header, allocation tables or directory can't be read, and by `GetPayloadForDirectory` when a stream's sectors can't be read (loops, ids outside the table, data past the end of the file). Problems with single directory entries, such as an invalid name length or an out-of-range time, are listed in `ParseErrors`.
- `DirectoryEntry.Id` is the entry's index in the directory stream, which `PreviousDirectoryId`, `NextDirectoryId` and `SubDirectoryId` refer to.

[MS-CFB]: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/
