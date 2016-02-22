using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace OleCf.Test
{
    [TestFixture]

    public class TestMain
    {

        [Test]
        public void BaseTests()
        {
            var names = new List<string>();
            names.Add(@"C:\Users\e\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations\fe57f5df17b45fe.automaticDestinations-ms");
            names.Add(@"C:\Users\e\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations\5f7b5f1e01b83767.automaticDestinations-ms");
            names.Add(@"C:\Users\e\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations\b842d0b8aaf85331.automaticDestinations-ms");
            names.Add(@"C:\Users\e\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations\ecf56f5453131993.automaticDestinations-ms");

            foreach (var fname in names)
            {
                Debug.WriteLine(fname);
                var o = OleCf.LoadFile(fname);

                o.Header.Should().NotBeNull();

                Debug.WriteLine(o.Header);

                foreach (var directoryItem in o.DirectoryItems)
                {
                    Debug.WriteLine(directoryItem);
                }
            }


          

            
        }

        [Test]
        public void InvalidFileShouldThrowException()
        {
            var badFile = Path.Combine(@"C:\Temp\Logons1000.csv");
            Action action = () => OleCf.LoadFile(badFile);

            action.ShouldThrow<Exception>().WithMessage("Invalid signature!");
        }
    }
}
