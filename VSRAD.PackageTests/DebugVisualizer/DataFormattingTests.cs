using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class DataFormattingTests
    {
        private static uint[] _data = new uint[]
        {
            0x_dead_beef,
            0x_c0ffee_00,
            0x_00_0ff1ce,
            0x_c001_f00d,
            0x_abba_abba,
            0x_0600d_bed,
            0x_added_bee,
            0x_caba66e_0,
            0x_dead_7ace,
            0x_000_ba6e1
        };

        [Fact]
        public void FormatDwordTests()
        {

        }
    }
}
