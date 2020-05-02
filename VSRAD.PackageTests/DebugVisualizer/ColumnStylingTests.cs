using Moq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VSRAD.Package.Options;
using Xunit;

namespace VSRAD.Package.DebugVisualizer.Tests
{
    public class ColumnStylingTests
    {
        private static List<DataGridViewColumn> GenerateTestColumns() =>
            Enumerable.Range(0, 512).Select(_ => new DataGridViewTextBoxColumn()).ToList<DataGridViewColumn>();

        [Fact]
        public void VisibilityTest()
        {
            // '-' denotes a range, any other non-digit character is treated as a separator
            var options = new ColumnStylingOptions() { VisibleColumns = "0:2-6 11;14-20,33,666" }; // 666 will result in an IndexOutOfBounds if we don't validate indexes 
            var columns = GenerateTestColumns();

            new ColumnStyling(new VisualizerOptions(), new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 32);

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
            for (int i = 21; i <= 32; i++)
                Assert.False(columns[i].Visible);
            for (int i = 33; i <= 511; i++)
                Assert.False(columns[i].Visible);
        }

        [Fact]
        public void HighlightColorTest()
        {
            var options = new ColumnStylingOptions();
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = DataHighlightColor.ColumnRed, Selector = "0|1|2-5" });
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = DataHighlightColor.ColumnGreen, Selector = "1-3" });
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = DataHighlightColor.ColumnBlue, Selector = "3-4,666-667" }); // 666 (> 512) to trigger IndexOutOfBounds
            var columns = GenerateTestColumns();

            var colors = new Mock<IFontAndColorProvider>();
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.ColumnRed)).Returns((fg: Color.Red, bg: Color.DarkRed, bold: false));
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.ColumnGreen)).Returns((fg: Color.Green, bg: Color.DarkGreen, bold: false));
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.ColumnBlue)).Returns((fg: Color.Blue, bg: Color.DarkBlue, bold: false));
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.None)).Returns((fg: Color.Black, bg: Color.Empty, bold: false));

            new ColumnStyling(new VisualizerOptions(), new VisualizerAppearance(), options, new DataFontAndColor(colors.Object))
                .Apply(columns, groupSize: 8);

            Assert.Equal(Color.Red, columns[0].DefaultCellStyle.ForeColor);
            Assert.Equal(Color.DarkRed, columns[0].DefaultCellStyle.BackColor);
            for (int i = 1; i <= 2; ++i)
            {
                Assert.Equal(Color.Green, columns[i].DefaultCellStyle.ForeColor);
                Assert.Equal(Color.DarkGreen, columns[i].DefaultCellStyle.BackColor);
            }
            for (int i = 3; i <= 4; ++i)
            {
                Assert.Equal(Color.Blue, columns[i].DefaultCellStyle.ForeColor);
                Assert.Equal(Color.DarkBlue, columns[i].DefaultCellStyle.BackColor);
            }
            Assert.Equal(Color.Red, columns[5].DefaultCellStyle.ForeColor);
            Assert.Equal(Color.DarkRed, columns[5].DefaultCellStyle.BackColor);

            Assert.Equal(Color.Black, columns[6].DefaultCellStyle.ForeColor);
            Assert.Equal(Color.Empty, columns[6].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void LaneGroupingTest()
        {
            var options = new ColumnStylingOptions() { VisibleColumns = "2-6:8:11:14" };
            var columns = GenerateTestColumns();

            // 2,3|4,5,6|8,11|14|

            new ColumnStyling(new VisualizerOptions { LaneGrouping = 4 }, new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 16);

            Assert.Equal(0, columns[0].DividerWidth);
            Assert.Equal(8, columns[1].DividerWidth);
            Assert.Equal(0, columns[2].DividerWidth);
            Assert.NotEqual(0, columns[3].DividerWidth);
            Assert.Equal(0, columns[4].DividerWidth);
            Assert.Equal(0, columns[5].DividerWidth);
            Assert.NotEqual(0, columns[6].DividerWidth);
            Assert.Equal(8, columns[7].DividerWidth);
            Assert.Equal(8, columns[8].DividerWidth);
            Assert.Equal(0, columns[9].DividerWidth);
            Assert.Equal(8, columns[10].DividerWidth);
            Assert.NotEqual(0, columns[11].DividerWidth);
            Assert.Equal(0, columns[12].DividerWidth);
            Assert.Equal(8, columns[13].DividerWidth);
            Assert.NotEqual(0, columns[14].DividerWidth);
            Assert.Equal(0, columns[15].DividerWidth);

            options.VisibleColumns = "0-511";
            new ColumnStyling(new VisualizerOptions { LaneGrouping = 3 }, new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 512);
            // no assertions here, this is just to trigger an index out of bounds if we're not careful with grouping

            new ColumnStyling(new VisualizerOptions { LaneGrouping = 1 }, new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 512);
            Assert.NotEqual(0, columns[0].DividerWidth); // 0 should be separated

            new ColumnStyling(new VisualizerOptions { LaneGrouping = 0 }, new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 512);
            for (int i = 0; i < 256; i++)
                Assert.Equal(0, columns[i].DividerWidth);
        }

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

            var columns = GenerateTestColumns();
            var colors = new Mock<IFontAndColorProvider>();
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.Inactive)).Returns((fg: Color.Empty, bg: Color.Gray, bold: false));
            new ColumnStyling(new VisualizerOptions { MaskLanes = true, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(), new DataFontAndColor(colors.Object))
                .Apply(columns, groupSize: 64, system: system);

            for (int i = 0; i < 5; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);
            for (int i = 5; i < 23; i++)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
            for (int i = 24; i < 45; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);
            Assert.Equal(Color.Empty, columns[45].DefaultCellStyle.BackColor);
            for (int i = 46; i < 64; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);

            new ColumnStyling(new VisualizerOptions { MaskLanes = false, CheckMagicNumber = false }, new VisualizerAppearance(), new ColumnStylingOptions(), new DataFontAndColor(colors.Object))
                .Apply(columns, groupSize: 64, system: system);
            for (int i = 0; i < 64; i++)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void MagicNumberCheckTest()
        {
            var system = new uint[256];
            system[0] = 0x7;
            system[64] = 0x5;
            system[128] = 0x7;

            var columns = GenerateTestColumns();
            var colors = new Mock<IFontAndColorProvider>();
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.Inactive)).Returns((fg: Color.Empty, bg: Color.Gray, bold: false));
            var visualizerOptions = new VisualizerOptions { MaskLanes = false, CheckMagicNumber = true, MagicNumber = 0x7 };
            new ColumnStyling(visualizerOptions, new VisualizerAppearance(), new ColumnStylingOptions(), new DataFontAndColor(colors.Object))
                .Apply(columns, groupSize: 256, system: system);

            for (int i = 0; i < 63; i++)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
            for (int i = 64; i < 128; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);
            for (int i = 128; i < 192; i++)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
            for (int i = 192; i < 256; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void GrayOutColumnsTest()
        {
            var columns = GenerateTestColumns();
            var colors = new Mock<IFontAndColorProvider>();
            colors.Setup(c => c.GetHighlightInfo(DataHighlightColor.Inactive)).Returns((fg: Color.Empty, bg: Color.Gray, bold: false));
            ColumnStyling.GrayOutColumns(new DataFontAndColor(colors.Object), columns, groupSize: 512);
            for (int i = 0; i < 512; i++)
                Assert.Equal(Color.Gray, columns[i].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void StylingChangedEventTest()
        {
            int eventsRaised = 0;

            var options = new ColumnStylingOptions();
            options.StylingChanged += () => ++eventsRaised;

            Assert.Equal(0, eventsRaised);
            options.VisibleColumns = "1";
            Assert.Equal(1, eventsRaised);

            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = DataHighlightColor.ColumnRed, Selector = "1" });
            Assert.Equal(2, eventsRaised);
            options.HighlightRegions[0].Selector = "2";
            Assert.Equal(3, eventsRaised);
            options.HighlightRegions[0].Color = DataHighlightColor.ColumnBlue;
            Assert.Equal(4, eventsRaised);
            options.HighlightRegions.RemoveAt(0);
            Assert.Equal(5, eventsRaised);
        }

        [Fact]
        public void HighlightRangeNeverNullTest()
        {
            /* DataGridView, which we use to present highlight regions, treats an empty string as null.
             * Region selectors, however, must _not_ be null */

            var options = new ColumnStylingOptions();
            options.HighlightRegions.Add(new ColumnHighlightRegion());
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = DataHighlightColor.ColumnRed, Selector = null });
            Assert.Equal("", options.HighlightRegions[0].Selector);
            Assert.Equal("", options.HighlightRegions[1].Selector);
        }

        [Fact]
        public void ParserIntOverflowTest()
        {
            var options = new ColumnStylingOptions();
            var columns = GenerateTestColumns();

            options.VisibleColumns = "0,111111111111111111111,34-111111111111111111111,111111111111111111111-34,1-2";
            new ColumnStyling(new VisualizerOptions { LaneGrouping = 1 }, new VisualizerAppearance(), options, new DataFontAndColor(new Mock<IFontAndColorProvider>().Object))
                .Apply(columns, groupSize: 512);

            Assert.True(columns[0].Visible);
            Assert.True(columns[1].Visible);
            Assert.True(columns[2].Visible);
            for (int i = 3; i < 512; i++)
                Assert.False(columns[i].Visible);
        }
    }
}