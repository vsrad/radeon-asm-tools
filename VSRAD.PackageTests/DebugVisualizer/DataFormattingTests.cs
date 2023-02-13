using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Utils;
using Xunit;
using Xunit.Extensions;

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

        static readonly string[] ExpectedHex32 = new string[]
        {
            "0xdeadbeef", "0xc0ffee00", "0x000ff1ce", "0xc001f00d", "0xabbaabba",
            "0x0600dbed", "0xaddedbee", "0xcaba66e0", "0xdead7ace", "0x000ba6e1"
        };

        public static IEnumerable<object[]> ProvideTestData =>
            new List<object[]>
            {
                new object[] { new VariableInfo(VariableType.Hex, 32), ExpectedHex32 },
            };

        [Theory, MemberData(nameof(ProvideTestData))]
        public void FormatDwordTests(VariableInfo info, string[] expected) =>
            Assert.Equal(_data.Select(d => DataFormatter.FormatDword(info, d, 0, 0, true)), expected);
    }
}
