using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class ColumnStylingTests
    {
        private static List<DataGridViewColumn> GenerateTestColumns() =>
            Enumerable.Range(0, 512).Select(_ => new DataGridViewTextBoxColumn()).ToList<DataGridViewColumn>();

        private static FontAndColorState MakeColorState() => TestHelper.MakeWithReadOnlyProps<FontAndColorState>(
            (nameof(FontAndColorState.HighlightBackground), new Color[]
                { /* none */ Color.Empty, /* inactive */ Color.LightGray, /* highlight */ Color.Red, Color.Green, Color.Blue }),
            (nameof(FontAndColorState.HighlightForeground), new Color[]
                { /* none */ Color.Black, /* inactive */ default, /* highlight */ Color.DarkRed, Color.DarkGreen, Color.DarkBlue }),
            (nameof(FontAndColorState.HighlightBold), Enumerable.Repeat(false, 5).ToArray()));

        private static BreakState MakeBreakState(uint waveSize, uint groupSize) =>
            new BreakState(BreakTarget.Empty, new Dictionary<string, WatchMeta>(),
                new BreakStateDispatchParameters(waveSize: waveSize, gridX: groupSize, gridY: 1, gridZ: 1, groupX: groupSize, groupY: 1, groupZ: 1, ""),
                new Dictionary<uint, uint>(), dwordsPerLane: 1,
                new BreakStateOutputFile("", false, 0, default, dwordCount: (int)groupSize), checkMagicNumber: null);

        [Fact]
        public void VisibilityTest()
        {
            // '-' denotes a range, any other non-digit character is treated as a separator
            var options = new ColumnStylingOptions() { VisibleColumns = "0:2-6 11;14-20,33,666" }; // 666 will result in an IndexOutOfBounds if we don't validate indexes 
            var columns = GenerateTestColumns();

            var computedStyling = new ComputedColumnStyling();
            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance(), options, MakeBreakState(waveSize: 32, groupSize: 32));

            new ColumnStyling(new VisualizerAppearance(), options, computedStyling, MakeColorState()).Apply(columns);

            Assert.True(columns[0].Visible);
            Assert.False(columns[1].Visible);
            for (int i = 2; i <= 6; i++)
                Assert.True(columns[i].Visible);
            for (int i = 7; i <= 10; i++)
                Assert.False(columns[i].Visible);
            Assert.True(columns[11].Visible);
            for (int i = 12; i <= 13; i++)
                Assert.False(columns[i].Visible);
            for (int i = 14; i <= 20; i++)
                Assert.True(columns[i].Visible);
            for (int i = 21; i < columns.Count; i++)
                Assert.False(columns[i].Visible);
        }

        [Fact]
        public void HighlightColorTest()
        {
            var visualizerOptions = new VisualizerOptions();
            var appearance = new VisualizerAppearance();

            var columns = GenerateTestColumns();
            var computedStyling = new ComputedColumnStyling();
            var options = new ColumnStylingOptions { BackgroundColors = null, ForegroundColors = "" };

            computedStyling.Recompute(visualizerOptions, new VisualizerAppearance(), options, MakeBreakState(waveSize: 16, groupSize: 16));
            new ColumnStyling(appearance, options, computedStyling, MakeColorState()).Apply(columns);
            for (int i = 0; i < 16; ++i)
            {
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
                Assert.Equal(Color.Black, columns[i].DefaultCellStyle.ForeColor);
            }

            columns = GenerateTestColumns();
            options = new ColumnStylingOptions { BackgroundColors = null, ForegroundColors = "rgbJKFJ" + new string(' ', 512 - "rgbJKFJ".Length) };
            computedStyling.Recompute(visualizerOptions, new VisualizerAppearance(), options, MakeBreakState(waveSize: 16, groupSize: 16));
            new ColumnStyling(appearance, options, computedStyling, MakeColorState()).Apply(columns);

            for (int i = 0; i < 16; ++i)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
            Assert.Equal(Color.DarkRed, columns[0].DefaultCellStyle.ForeColor);
            Assert.Equal(Color.DarkGreen, columns[1].DefaultCellStyle.ForeColor);
            Assert.Equal(Color.DarkBlue, columns[2].DefaultCellStyle.ForeColor);
            for (int i = 3; i < 16; ++i)
                Assert.Equal(Color.Black, columns[i].DefaultCellStyle.ForeColor);

            string bgString = null;
            bgString = DataHighlightColors.UpdateColorStringRange(bgString, new[] { 0, 1, 2, 3, 4, 5 }, DataHighlightColor.Red, columns.Count); // rrrrrr
            bgString = DataHighlightColors.UpdateColorStringRange(bgString, new[] { 1, 2, 3 }, DataHighlightColor.Green, columns.Count);        // rgggrr
            bgString = DataHighlightColors.UpdateColorStringRange(bgString, new[] { 3, 4, 666, -1 }, DataHighlightColor.Blue, columns.Count);   // rggbbr, 666 (> 512) and -1 to trigger IndexOutOfBounds

            var fgString = DataHighlightColors.UpdateColorStringRange(bgString, Enumerable.Range(0, 512), DataHighlightColor.None, columns.Count);
            Assert.Equal("", fgString);

            options = new ColumnStylingOptions { BackgroundColors = bgString, ForegroundColors = fgString };

            computedStyling.Recompute(visualizerOptions, new VisualizerAppearance(), options, MakeBreakState(waveSize: 16, groupSize: 16));
            new ColumnStyling(appearance, options, computedStyling, MakeColorState()).Apply(columns);

            for (int i = 0; i < 16; ++i)
                Assert.Equal(Color.Black, columns[i].DefaultCellStyle.ForeColor);

            Assert.Equal(Color.Red, columns[0].DefaultCellStyle.BackColor);
            for (int i = 1; i <= 2; ++i)
                Assert.Equal(Color.Green, columns[i].DefaultCellStyle.BackColor);
            for (int i = 3; i <= 4; ++i)
                Assert.Equal(Color.Blue, columns[i].DefaultCellStyle.BackColor);
            Assert.Equal(Color.Red, columns[5].DefaultCellStyle.BackColor);
            Assert.Equal(Color.Empty, columns[6].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void LaneGroupingTest()
        {
            const int laneSep = 3;
            const int hiddenSep = 8;
            var appearance = new VisualizerAppearance { HiddenColumnSeparatorWidth = hiddenSep, LaneSeparatorWidth = laneSep };

            var options = new ColumnStylingOptions { VisibleColumns = "2-6:8-9:11-12:14-16" };
            var columns = GenerateTestColumns();

            var computedStyling = new ComputedColumnStyling();
            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 4 }, options, MakeBreakState(waveSize: 32, groupSize: 32));
            new ColumnStyling(appearance, options, computedStyling, MakeColorState()).Apply(columns);

            // 2 3 | 4 5 6 || 8 9 || 11 | 12 || 14 15 | 16

            Assert.Equal(0, columns[0].DividerWidth);
            Assert.Equal(0, columns[1].DividerWidth);
            Assert.Equal(0, columns[2].DividerWidth);
            Assert.Equal(laneSep, columns[3].DividerWidth);
            Assert.Equal(0, columns[4].DividerWidth);
            Assert.Equal(0, columns[5].DividerWidth);
            Assert.Equal(hiddenSep, columns[6].DividerWidth);
            Assert.Equal(0, columns[7].DividerWidth);
            Assert.Equal(0, columns[8].DividerWidth);
            Assert.Equal(hiddenSep, columns[9].DividerWidth);
            Assert.Equal(0, columns[10].DividerWidth);
            Assert.Equal(laneSep, columns[11].DividerWidth);
            Assert.Equal(hiddenSep, columns[12].DividerWidth);
            Assert.Equal(0, columns[13].DividerWidth);
            Assert.Equal(0, columns[14].DividerWidth);
            Assert.Equal(laneSep, columns[15].DividerWidth);

            options.VisibleColumns = "0-511";
            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 3}, options, MakeBreakState(waveSize: 32, groupSize: 512));
            new ColumnStyling(new VisualizerAppearance(), options, computedStyling, MakeColorState()).Apply(columns);
            // no assertions here, this is just to trigger an index out of bounds if we're not careful with grouping

            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 1 }, options, MakeBreakState(waveSize: 32, groupSize: 512));
            new ColumnStyling(new VisualizerAppearance(), options, computedStyling, MakeColorState()).Apply(columns);
            Assert.NotEqual(0, columns[0].DividerWidth); // 0 should be separated

            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 0 }, options, MakeBreakState(waveSize: 32, groupSize: 512));
            new ColumnStyling(new VisualizerAppearance(), options, computedStyling, MakeColorState()).Apply(columns);
            for (int i = 0; i < 256; i++)
                Assert.Equal(0, columns[i].DividerWidth);
        }

        [Fact]
        public void ParserIntOverflowTest()
        {
            var options = new ColumnStylingOptions();
            var columns = GenerateTestColumns();

            options.VisibleColumns = "0,111111111111111111111,34-111111111111111111111,111111111111111111111-34,1-2";

            var computedStyling = new ComputedColumnStyling();
            computedStyling.Recompute(new VisualizerOptions(), new VisualizerAppearance { LaneGrouping = 1 }, options, MakeBreakState(waveSize: 32, groupSize: 512));
            new ColumnStyling(new VisualizerAppearance(), options, computedStyling, MakeColorState()).Apply(columns);

            Assert.True(columns[0].Visible);
            Assert.True(columns[1].Visible);
            Assert.True(columns[2].Visible);
            for (int i = 3; i < 512; i++)
                Assert.False(columns[i].Visible);
        }
    }
}