using System.Collections.Generic;
using VSRAD.Package.DebugVisualizer;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class ColumnSelectorTests
    {
        [Fact]
        public void FromIndexesTest()
        {
            var selector = ColumnSelector.FromIndexes(new[] { 35, 2, 31, 33, 34, 40, 56, 10, 10 });
            Assert.Equal("2:10:31:33-35:40:56", selector);

            selector = ColumnSelector.FromIndexes(new[] { 35, 34, 33 });
            Assert.Equal("33-35", selector);
        }

        [Fact]
        public void PartialSubgroupsTest()
        {
            var keepFirst = ColumnSelector.PartialSubgroups(groupSize: 256, subgroupSize: 32, displayedCount: 8, displayLast: false);
            Assert.Equal("0-7:32-39:64-71:96-103:128-135:160-167:192-199:224-231", keepFirst);

            var keepLast = ColumnSelector.PartialSubgroups(groupSize: 256, subgroupSize: 32, displayedCount: 8, displayLast: true);
            Assert.Equal("24-31:56-63:88-95:120-127:152-159:184-191:216-223:248-255", keepLast);
        }

        [Fact]
        public void RemoveIndexesTest()
        {
            var highlightRegions = new List<ColumnHighlightRegion>()
            {
                new ColumnHighlightRegion() { Selector = "0-5:10", Color = ColumnHighlightColor.Blue },
                new ColumnHighlightRegion() { Selector = "15:17:22-25", Color = ColumnHighlightColor.Green },
                new ColumnHighlightRegion() { Selector = "30-45:51-55", Color = ColumnHighlightColor.Red },
                new ColumnHighlightRegion() { Selector = "66-69", Color = ColumnHighlightColor.Blue }
            };
            var selectedIndexes = new List<int>() { 3, 10, 16, 23, 24, 33, 37, 42, 52, 66, 67, 68, 69 };
            ColumnSelector.RemoveIndexes(selectedIndexes, highlightRegions);

            Assert.Equal("0-2:4-5", highlightRegions[0].Selector);
            Assert.Equal("15:17:22:25", highlightRegions[1].Selector);
            Assert.Equal("30-32:34-36:38-41:43-45:51:53-55", highlightRegions[2].Selector);
            Assert.Equal(3, highlightRegions.Count);
        }

        [Fact]
        public void SelectorsMultiplicationTest()
        {
            var currentSelector = "0-511";

            // keep first
            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-255");
            Assert.Equal("0-255", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-127:256-384");
            Assert.Equal("0-127", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-63:128-191:256-319:384-447");
            Assert.Equal("0-63", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-127");
            Assert.Equal("0-127", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-255");
            Assert.Equal("0-255", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-511");
            Assert.Equal("0-511", currentSelector);

            //keep last
            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "256-511");
            Assert.Equal("256-511", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "128-255:384-511");
            Assert.Equal("384-511", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "64-127:192-255:320-383:448-511");
            Assert.Equal("448-511", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "384-511");
            Assert.Equal("384-511", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "256-511");
            Assert.Equal("256-511", currentSelector);

            currentSelector = ColumnSelector.GetSelectorMultiplication(currentSelector, "0-511");
            Assert.Equal("0-511", currentSelector);
        }
    }
}
