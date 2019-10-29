using System.Linq;
using Xunit;

namespace VSRAD.Package.DebugVisualizer.Tests
{
    public class GroupIndexSelectorTests
    {
        [Fact]
        public void DimensionClippingTest()
        {
            var options = new Options.VisualizerOptions() { NDRange3D = true };
            var selector = new GroupIndexSelector(options, getGroupCount: (_) => 0, groupSelectionChanged: (_, __) => { });

            var changedPropertyName = "";
            selector.PropertyChanged += (s, e) => changedPropertyName = e.PropertyName;

            selector.DimX = 2;
            Assert.Equal(nameof(selector.DimX), changedPropertyName);

            selector.X = 3;
            Assert.Equal(nameof(selector.X), changedPropertyName);
            Assert.Equal<uint>(1, selector.X); // clipped to DimX - 1
        }

        [Fact]
        public void ValidationErrorTest()
        {
            uint groupCount = 0;
            var options = new Options.VisualizerOptions() { NDRange3D = true };
            var selector = new GroupIndexSelector(options, getGroupCount: (_) => groupCount, (_, __) => { }) { DimX = 20, X = 19 };
            Assert.False(selector.HasErrors);

            selector.OnDataAvailable();
            Assert.True(selector.HasErrors);
            Assert.Equal("Invalid group index: 19 >= 0", selector.GetErrors("X").Cast<string>().First());
            Assert.Equal("Invalid group index: 19 >= 0", selector.GetErrors("Y").Cast<string>().First());
            Assert.Equal("Invalid group index: 19 >= 0", selector.GetErrors("Z").Cast<string>().First());
            Assert.Empty(selector.GetErrors("").Cast<string>());

            groupCount = 20;
            selector.OnDataAvailable();
            Assert.False(selector.HasErrors);
            Assert.Empty(selector.GetErrors("").Cast<string>());
        }

        [Fact]
        public void GroupIndexChangeOnlyRaisedWithDataTest()
        {
            var timesRaised = 0;
            var options = new Options.VisualizerOptions() { NDRange3D = true };
            var selector = new GroupIndexSelector(options,
                getGroupCount: (_) => 20,
                groupSelectionChanged: (_, __) => timesRaised++);

            selector.DimX = 10;
            selector.X = 5;
            Assert.Equal(0, timesRaised);

            selector.OnDataAvailable();
            Assert.Equal(1, timesRaised);

            selector.DimX = 30;
            selector.X = 6;
            Assert.False(selector.HasErrors);
            Assert.Equal(2, timesRaised);

            selector.X = 21;
            Assert.True(selector.HasErrors);
            Assert.Equal(2, timesRaised);
        }

        [Fact]
        public void GroupIndexChangeArgsTest()
        {
            (uint, string) eventArgs = default;

            var options = new Options.VisualizerOptions() { NDRange3D = true };
            var selector = new GroupIndexSelector(options,
                getGroupCount: (_) => 2000,
                groupSelectionChanged: (index, coordinates) => eventArgs = (index, coordinates));

            selector.OnDataAvailable();
            Assert.Equal<(uint, string)>((0, "(0; 0; 0)"), eventArgs);

            selector.DimX = 10;
            selector.DimY = 100;
            selector.DimZ = 1000;
            selector.X = 3;
            selector.Y = 2;
            selector.Z = 1;

            Assert.Equal<(uint, string)>((3 + 2 * 10 + 10 * 100, "(3; 2; 1)"), eventArgs);
        }
    }
}
