# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A `netstandard2.0` NuGet library that parses OLE compound files ([MS-CFB]). Its main consumer is the JumpList library (EricZimmerman/JumpList, used by JLECmd and JumpListExplorer), which uses `OleCfFile`, `Directory`, `DirectoryName`, `DirectorySize` and `GetPayloadForDirectory`. Keep those compatible. Input may be corrupt or hostile.

## Commands

The SDK is pinned in `global.json` (.NET 10). The library targets `netstandard2.0`; tests target `net10.0`. Both use `LangVersion 10`. Tests use NUnit 5 + FluentAssertions 7 (pinned to `[7.0.0,8.0.0)`, since 8.x requires a commercial license).

```bash
dotnet build                                                  # builds OleCf.slnx
dotnet test                                                   # CI runs: dotnet test --configuration Release
dotnet test --filter "FullyQualifiedName~RobustnessTests"     # one fixture
dotnet test --filter "Name=InvalidFileShouldThrowException"   # one test
dotnet pack --configuration Release                           # package includes README.md and icon.png from the repo root
```

## Architecture

- **`Header`**: the 512 byte header. It validates the signature, byte order (little endian only), sector shifts and the SAT sector count. The internal constructor checks the count against the file length; the public one caps it. `SectorOffset(id)` is `(id + 1) * sectorSize`: the header occupies the whole first sector, which is 4096 bytes in version 4. `SATSectors` holds absolute offsets of SAT sectors (the first 109 come from the header, the rest from the MSAT chain).
- **`OleCfFile`**: reads MSAT sectors when there are more than 109 SAT sectors, then the SAT, SSAT, directory and mini stream (the root entry's chain, cut into 64 byte short sectors). `GetPayloadForDirectory` reads a stream from the SAT, or from the SSAT when it is below `MinimumStandardStreamSize`. With no SSAT it returns null for small streams, as before. Callers such as JumpList skip `DirectorySize == 0`.
- **`FollowChain`** is the only way chains are walked. It stops at the first negative id, and throws on an id outside the table or a loop. `GetDataFromSat` reads only the sectors that start inside the file (so buffers are never bigger than the file). A truncated file gives the bytes that are there, and sets `truncated`. The mini stream is kept as one byte array of what is in the file.
- **`DirectoryEntry`**: a 128 byte entry. Fields: name (64 bytes, length at 64 includes the NUL), type 66, color 67, left/right/child 68/72/76, CLSID 80, state bits 96, created 100, modified 108, start sector 116, size 120. `Id` is the index in the directory stream (empty entries are left out of `Directory`, so use `Id` to resolve sibling and child ids). Times of 0 or in 1601 are null; out-of-range times are null and reported.

### Error model
- `OleCfParseException` (derives from `Exception`; `Offset`, `Field`) is thrown for unusable structure: header, MSAT, a chain loop or a sector id outside its table. `GetPayloadForDirectory` throws it per stream, so one bad stream doesn't stop the others. Keep the message `"Invalid signature!"`, because callers have matched on it.
- **Truncation is not an error.** When the file ends before a stream does (or a chain is shorter than the stream's size), `GetPayloadForDirectory` returns the bytes that are there, so the result is shorter than `DirectorySize`, and adds "Stream 'X' is truncated: N of M bytes are in the file" to `ParseErrors`. This matches Python's olefile; the old code padded with zeros instead. A truncated mini stream, SSAT or directory is also reported there.
- Problems that don't stop parsing go to `OleCfFile.ParseErrors` (truncation, a missing SAT sector, which is treated as free) and `DirectoryEntry.ParseErrors` (copied to the file's list with the entry id).

## Tests

- `CorpusTests`: every automatic destination in a local collection of real jump lists that is not in the repo: the `Jump lists and lnks` folder in `Corpus/` next to the repository (or directly next to it), or `JUMPLIST_CORPUS`. Skipped when it is missing (as in CI). Every stream in it (24,866) was checked against olefile, including a truncated file (`Tom/.../9b9cdc69c1c24e2b`).

- `TestMain`: real jump lists in `OleCf.Test/TestFiles/<OS>/` (copied to the test output). Expected names, sizes and MD5s come from Python's `olefile`.
- `CfbBuilder`: writes compound files (version 3/4, mini stream, MSAT) for layouts the samples don't have. Its output was checked with `olefile`. `StructureTests` uses it for 4096 byte sectors, 1/2/3 MSAT sectors (10-26 MB) and directory entry offsets. `RobustnessTests` uses it for corruption (loops, bad ids, truncation, bad sizes, random corruption), with a 10 second guard against endless loops. Add regression tests here.

## Versioning and release

- MinVer sets the version from the git tag (bare tags like `2026.10.0`); there is no `<Version>` in the csproj. Untagged commits build as `x.y.z-alpha.0.N`.
- To release: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`.
- `.github/workflows/publish.yml` (identical to GuidMapping's) packs, validates and tests on PRs and pushes to `master`. When a GitHub Release is published, it pushes to NuGet.org using trusted publishing (`NuGet/login` with the `NUGET_USER` repo variable).
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.

[MS-CFB]: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/
