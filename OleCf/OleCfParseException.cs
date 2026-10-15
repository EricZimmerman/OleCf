using System;

namespace OleCf;

/// <summary>
///     Thrown when data is not a usable OLE compound file: a bad header, an allocation table or directory that can't
///     be read, or (from <see cref="OleCfFile.GetPayloadForDirectory" />) a stream that can't be read.
///     Problems with individual directory entries are reported in <see cref="OleCfFile.ParseErrors" /> instead.
/// </summary>
public class OleCfParseException : Exception
{
    public OleCfParseException(string message, long offset = -1, string field = null, Exception inner = null)
        : base(message, inner)
    {
        Offset = offset;
        Field = field;
    }

    /// <summary>
    ///     Byte offset in the file of the failing read, or -1 when unknown.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    ///     Name of the field or structure being read when parsing failed, if known.
    /// </summary>
    public string Field { get; }
}
