using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Options;
using VSRAD.PackageTests;
using Xunit;

namespace VSRAD.Package.DebugVisualizer.Tests
{
    public class RowStylingTests
    {
        private static List<DataGridViewRow> GenerateTestRows(int count) =>
            Enumerable.Range(0, count).Select(_ =>
            {
                var row = new DataGridViewRow();
                for (var i = 0; i < VisualizerTable.DataColumnCount + VisualizerTable.DataColumnOffset; ++i)
                    row.Cells.Add(new DataGridViewTextBoxCell());
                return row;
            }).ToList();

        private static FontAndColorState MakeColorState() => TestHelper.MakeWithReadOnlyProps<FontAndColorState>(
            (nameof(FontAndColorState.HighlightBackground), new Color[]
                { /* none */ Color.Empty, /* columns */ Color.Red, Color.Green, Color.Blue, /* rows */ default, default, default, /* inactive */ Color.LightGray }),
            (nameof(FontAndColorState.HighlightForeground), new Color[]
                { /* none */ Color.Black, /* columns */ Color.DarkRed, Color.DarkGreen, Color.DarkBlue, /* rows */ default, default, default, /* inactive */ default }),
            (nameof(FontAndColorState.HighlightBold), Enumerable.Repeat(false, 7).ToArray()));

        private static IEnumerable<DataGridViewCell> GetDataColumnCells(IEnumerable<DataGridViewRow> rows, int dataColumnIndex) =>
            rows.Select(r => r.Cells[VisualizerTable.DataColumnOffset + dataColumnIndex]);

        [Fact]
        public void LaneMaskingTest()
        {
            var maskLowBits = new bool[32];
            for (int i = 5; i < 23; i++)
                maskLowBits[i] = true;
            var maskHighBits = new bool[32];
            maskHighBits[13] = true;

            var system = new uint[64];

            var tmp = new int[1];
            new BitArray(maskLowBits).CopyTo(tmp, 0);
            system[8] = (uint)tmp[0];
            new BitArray(maskHighBits).CopyTo(tmp, 0);
            system[9] = (uint)tmp[0];

            var rows = GenerateTestRows(4);

            new RowStyling(rows, new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, MakeColorState())
                .Apply(groupSize: 64, system: system);

            for (int i = 0; i < 5; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.Equal(Color.LightGray, c.Style.BackColor);
            for (int i = 5; i < 23; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.False(c.HasStyle);
            for (int i = 24; i < 45; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.Equal(Color.LightGray, c.Style.BackColor);

            foreach (var c in GetDataColumnCells(rows, 45)) Assert.False(c.HasStyle);

            for (int i = 46; i < 64; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.Equal(Color.LightGray, c.Style.BackColor);

            new RowStyling(rows, new VisualizerOptions { MaskLanes = false, CheckMagicNumber = false }, MakeColorState())
                .Apply(groupSize: 64, system: system);

            for (int i = 0; i < 64; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.False(c.HasStyle);
        }

        [Fact]
        public void MagicNumberCheckTest()
        {
            var system = new uint[256];
            system[0] = 0x7;
            system[64] = 0x5;
            system[128] = 0x7;

            var rows = GenerateTestRows(4);

            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7 };
            new RowStyling(rows, visualizerOptions, MakeColorState())
                .Apply(groupSize: 256, system: system);

            for (int i = 0; i < 63; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.False(c.HasStyle);
            for (int i = 64; i < 128; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.Equal(Color.LightGray, c.Style.BackColor);
            for (int i = 128; i < 192; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.False(c.HasStyle);
            for (int i = 192; i < 256; i++)
                foreach (var c in GetDataColumnCells(rows, i)) Assert.Equal(Color.LightGray, c.Style.BackColor);
        }
    }
}
