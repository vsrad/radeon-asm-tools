using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace VSRAD.Package.DebugVisualizer.Tests
{
    public class ColumnStylingTests
    {
        public static List<DataGridViewColumn> GenerateTestColumns()
        {
            var columns = new List<DataGridViewColumn>();
            for (int i = 0; i < 512; i++)
                columns.Add(new DataGridViewTextBoxColumn());
            return columns;
        }

        [Fact]
        public void VisibilityTest()
        {
            // '-' denotes a range, any other non-digit character is treated as a separator
            var options = new ColumnStylingOptions() { VisibleColumns = "0:2-6 11;14-20,33,666" }; // 666 will result in an IndexOutOfBounds if we don't validate indexes 
            var columns = GenerateTestColumns();
            options.Computed.Apply(columns, groupSize: 32, laneGrouping: 0);

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
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = ColumnHighlightColor.Red, Selector = "0|1|2-5" });
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = ColumnHighlightColor.Green, Selector = "1-3" });
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = ColumnHighlightColor.Blue, Selector = "3-4,666-667" }); // 666 (> 512) to trigger IndexOutOfBounds
            var columns = GenerateTestColumns();
            options.Computed.Apply(columns, groupSize: 8, laneGrouping: 0);

            Assert.Equal(Colors.RedHighlight, columns[0].DefaultCellStyle.BackColor);
            Assert.Equal(Colors.GreenHighlight, columns[1].DefaultCellStyle.BackColor);
            Assert.Equal(Colors.GreenHighlight, columns[2].DefaultCellStyle.BackColor);
            Assert.Equal(Colors.BlueHighlight, columns[3].DefaultCellStyle.BackColor);
            Assert.Equal(Colors.BlueHighlight, columns[4].DefaultCellStyle.BackColor);
            Assert.Equal(Colors.RedHighlight, columns[5].DefaultCellStyle.BackColor);
            Assert.Equal(Color.Empty, columns[6].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void LaneGroupingTest()
        {
            var options = new ColumnStylingOptions() { VisibleColumns = "2-6:8:11:14" };
            var columns = GenerateTestColumns();

            // 2,3|4,5,6|8,11|14|

            options.Computed.Apply(columns, groupSize: 16, laneGrouping: 4);
            
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
            options.Computed.Apply(columns, groupSize: 512, laneGrouping: 3);
            // no assertions here, this is just to trigger an index out of bounds if we're not careful with grouping

            options.Computed.Apply(columns, groupSize: 512, laneGrouping: 1);
            Assert.NotEqual(0, columns[0].DividerWidth); // 0 should be separated

            options.Computed.Apply(columns, groupSize: 512, laneGrouping: 0);
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
            ColumnStyling.ApplyLaneMask(columns, groupSize: 64, system: system);

            for (int i = 0; i < 5; i++)
                Assert.Equal(Color.LightGray, columns[i].DefaultCellStyle.BackColor);
            for (int i = 5; i < 23; i++)
                Assert.Equal(Color.Empty, columns[i].DefaultCellStyle.BackColor);
            for (int i = 24; i < 45; i++)
                Assert.Equal(Color.LightGray, columns[i].DefaultCellStyle.BackColor);
            Assert.Equal(Color.Empty, columns[45].DefaultCellStyle.BackColor);
            for (int i = 46; i < 64; i++)
                Assert.Equal(Color.LightGray, columns[i].DefaultCellStyle.BackColor);
        }

        [Fact]
        public void GrayOutColumnsTest()
        {
            var columns = GenerateTestColumns();
            ColumnStyling.GrayOutColumns(columns, groupSize: 512);
            for (int i = 0; i < 512; i++)
            {
                Assert.Equal(Color.LightGray, columns[i].DefaultCellStyle.BackColor);
            }
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

            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = ColumnHighlightColor.Red, Selector = "1" });
            Assert.Equal(2, eventsRaised);
            options.HighlightRegions[0].Selector = "2";
            Assert.Equal(3, eventsRaised);
            options.HighlightRegions[0].Color = ColumnHighlightColor.Blue;
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
            options.HighlightRegions.Add(new ColumnHighlightRegion { Color = ColumnHighlightColor.Red, Selector = null });
            Assert.Equal("", options.HighlightRegions[0].Selector);
            Assert.Equal("", options.HighlightRegions[1].Selector);
        }

        [Fact]
        public void ParserIntOverflowTest()
        {
            var options = new ColumnStylingOptions();
            var columns = GenerateTestColumns();

            options.VisibleColumns = "0,111111111111111111111,34-111111111111111111111,111111111111111111111-34,1-2";
            options.Computed.Apply(columns, groupSize: 512, laneGrouping: 1);

            Assert.True(columns[0].Visible);
            Assert.True(columns[1].Visible);
            Assert.True(columns[2].Visible);
            for (int i = 3; i < 512; i++)
                Assert.False(columns[i].Visible);
        }
    }
}