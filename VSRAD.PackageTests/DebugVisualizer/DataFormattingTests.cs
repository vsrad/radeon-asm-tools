﻿using System;
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

        static readonly string[] Hex32LZs = new string[]
        {
            "0xdeadbeef", "0xc0ffee00", "0x000ff1ce", "0xc001f00d", "0xabbaabba",
            "0x0600dbed", "0xaddedbee", "0xcaba66e0", "0xdead7ace", "0x000ba6e1"
        };

        static readonly string[] Hex32LZsSep3 = new string[]
        {
            "0xde adb eef", "0xc0 ffe e00", "0x00 0ff 1ce", "0xc0 01f 00d", "0xab baa bba",
            "0x06 00d bed", "0xad ded bee", "0xca ba6 6e0", "0xde ad7 ace", "0x00 0ba 6e1"
        };

        static readonly string[] Hex32 = new string[]
        {
            "0xdeadbeef", "0xc0ffee00", "0xff1ce",    "0xc001f00d", "0xabbaabba",
            "0x600dbed",  "0xaddedbee", "0xcaba66e0", "0xdead7ace", "0xba6e1"
        };

        /* TODO:
         * would be nice to test all possible formats using the above approach
         * in the scope of #346 i'll focus on newly added packed integer types
         */

        static readonly string[] UInt32 = new string[]
        {
            "3735928559", "3237998080", "1044942",    "3221352461", "2881137594",
            "100719597",  "2917063662", "3401213664", "3735911118", "763617"
        };

        static readonly string[] UInt16 = new string[]
        {
            "57005; 48879", "49407; 60928", "15; 61902", "49153; 61453", "43962; 43962",
            "1536; 56301", "44510; 56302", "51898; 26336", "57005; 31438", "11; 42721"
        };

        static readonly string[] UInt8 = new string[]
        {
            "222; 173; 190; 239", "192; 255; 238; 0", "0; 15; 241; 206",
            "192; 1; 240; 13", "171; 186; 171; 186", "6; 0; 219; 237",
            "173; 222; 219; 238", "202; 186; 102; 224", "222; 173; 122; 206",
            "0; 11; 166; 225"
        };

        public static IEnumerable<object[]> ProvideTestData =>
            new List<object[]>
            {
                new object[] { new VariableInfo(VariableType.Hex, 32),  0, 0, true,  Hex32LZs     },
                new object[] { new VariableInfo(VariableType.Hex, 32),  3, 0, true,  Hex32LZsSep3 },
                new object[] { new VariableInfo(VariableType.Hex, 32),  0, 0, false, Hex32        },
                new object[] { new VariableInfo(VariableType.Uint, 32), 0, 0, false, UInt32       },
                new object[] { new VariableInfo(VariableType.Uint, 16), 0, 0, false, UInt16       },
                new object[] { new VariableInfo(VariableType.Uint, 8),  0, 0, false, UInt8        },
            };

        [Theory, MemberData(nameof(ProvideTestData))]
        public void FormatDwordTests(VariableInfo info, uint binHexSeparator, uint intSeparator,
            bool leadingZeros, string[] expected) =>
                Assert.Equal(_data.Select(d => DataFormatter.FormatDword(info, d, binHexSeparator,
                    intSeparator, leadingZeros)), expected);
    }
}
