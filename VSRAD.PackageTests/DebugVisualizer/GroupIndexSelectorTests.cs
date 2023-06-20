using System.Linq;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Options;
using Xunit;

namespace VSRAD.PackageTests.DebugVisualizer
{
    public class GroupIndexSelectorTests
    {
#if false // FIXME: broken
        [Fact]
        public void DimensionClippingTest()
        {
            var options = new ProjectOptions();
            options.VisualizerOptions.NDRange3D = true;
            var selector = new GroupIndexSelector(options);

            var changedPropertyName = "";
            selector.PropertyChanged += (s, e) => changedPropertyName = e.PropertyName;

            selector.DimX = 2;
            Assert.Equal(nameof(selector.DimX), changedPropertyName);

            selector.X = 3;
            Assert.Equal(nameof(selector.X), changedPropertyName);
            Assert.Equal<uint>(1, selector.X); // clipped to DimX - 1
        }
#endif

        [Fact]
        public void ValidationErrorTest()
        {
            var options = new ProjectOptions();
            options.VisualizerOptions.NDRange3D = true;
            var selector = new GroupIndexSelector(options) { DimX = 20, X = 19 };

            Assert.False(selector.HasErrors);

            selector.IndexChanged += (s, e) =>
            {
                e.DataGroupCount = 1;
                e.IsValid = false;
            };
            selector.Update();
            Assert.True(selector.HasErrors);
            Assert.Equal("Invalid group index: 19 >= 1", selector.GetErrors("X").Cast<string>().First());
            Assert.Equal("Invalid group index: 19 >= 1", selector.GetErrors("Y").Cast<string>().First());
            Assert.Equal("Invalid group index: 19 >= 1", selector.GetErrors("Z").Cast<string>().First());
            Assert.Empty(selector.GetErrors("").Cast<string>());

            selector.IndexChanged += (s, e) =>
            {
                e.DataGroupCount = 20;
                e.IsValid = true;
            };
            selector.Update();
            Assert.False(selector.HasErrors);
            Assert.Empty(selector.GetErrors("").Cast<string>());
        }

        [Fact]
        public void GroupIndexChangeArgsTest()
        {
            GroupIndexChangedEventArgs eventArgs = null;

            var options = new ProjectOptions();
            options.VisualizerOptions.NDRange3D = true;
            var selector = new GroupIndexSelector(options);

            selector.IndexChanged += (s, e) => { eventArgs = e; e.IsValid = true; };

            selector.DimX = 10;
            selector.DimY = 100;
            selector.DimZ = 1000;
            selector.X = 3;
            selector.Y = 2;
            selector.Z = 1;

            Assert.NotNull(eventArgs);
            Assert.Equal((uint)(3 + 2 * 10 + 10 * 100), eventArgs.GroupIndex);
            Assert.Equal("(3; 2; 1)", eventArgs.Coordinates);
        }
    }
}
