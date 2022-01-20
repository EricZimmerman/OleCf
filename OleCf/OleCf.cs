using System.IO;

namespace OleCf;

public static class OleCf
{
    public static OleCfFile LoadFile(string filename)
    {
        var contents = File.ReadAllBytes(filename);

        var o = new OleCfFile(contents, filename);

        return o;
    }
}