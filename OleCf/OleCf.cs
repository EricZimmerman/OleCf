using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace OleCf
{
    public static class OleCf
    {
        public static OleCfFile LoadFile(string filename)
        {
            var contents = File.ReadAllBytes(filename);

            var o = new OleCfFile(contents);

            return o;
        }
    }
}
