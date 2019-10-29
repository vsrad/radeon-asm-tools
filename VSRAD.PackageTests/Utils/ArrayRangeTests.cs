using VSRAD.Package.Utils;
using Xunit;

namespace VSRAD.PackageTests.Utils
{
    public class ArrayRangeTests
    {
        [Fact]
        public void AppendNewBrackets()
        {
            var name = "eva-syncro-levels[1][2]";
            var from = -3;
            var to = 4;
            var results = ArrayRange.FormatArrayRangeWatch(name, from, to);
            Assert.Equal("eva-syncro-levels[1][2][-3]", results[0]);
            Assert.Equal("eva-syncro-levels[1][2][-2]", results[1]);
            Assert.Equal("eva-syncro-levels[1][2][-1]", results[2]);
            Assert.Equal("eva-syncro-levels[1][2][0]", results[3]);
            Assert.Equal("eva-syncro-levels[1][2][1]", results[4]);
            Assert.Equal("eva-syncro-levels[1][2][2]", results[5]);
            Assert.Equal("eva-syncro-levels[1][2][3]", results[6]);
            Assert.Equal("eva-syncro-levels[1][2][4]", results[7]);

            name = "eva-internal-battery-countdown";
            results = ArrayRange.FormatArrayRangeWatch(name, from, to);
            Assert.Equal("eva-internal-battery-countdown[-3]", results[0]);
            Assert.Equal("eva-internal-battery-countdown[-2]", results[1]);
            Assert.Equal("eva-internal-battery-countdown[-1]", results[2]);
            Assert.Equal("eva-internal-battery-countdown[0]", results[3]);
            Assert.Equal("eva-internal-battery-countdown[1]", results[4]);
            Assert.Equal("eva-internal-battery-countdown[2]", results[5]);
            Assert.Equal("eva-internal-battery-countdown[3]", results[6]);
            Assert.Equal("eva-internal-battery-countdown[4]", results[7]);
        }

        [Fact]
        public void AppendWithOffset()
        {
            var name = "eva-pilot-pulse[offset]";
            var from = -3;
            var to = 4;
            var results = ArrayRange.FormatArrayRangeWatch(name, from, to);
            Assert.Equal("eva-pilot-pulse[offset-3]", results[0]);
            Assert.Equal("eva-pilot-pulse[offset-2]", results[1]);
            Assert.Equal("eva-pilot-pulse[offset-1]", results[2]);
            Assert.Equal("eva-pilot-pulse[offset+0]", results[3]);
            Assert.Equal("eva-pilot-pulse[offset+1]", results[4]);
            Assert.Equal("eva-pilot-pulse[offset+2]", results[5]);
            Assert.Equal("eva-pilot-pulse[offset+3]", results[6]);
            Assert.Equal("eva-pilot-pulse[offset+4]", results[7]);

            name = "eva-gun-voltage[0][offset]";
            results = ArrayRange.FormatArrayRangeWatch(name, from, to);
            Assert.Equal("eva-gun-voltage[0][offset-3]", results[0]);
            Assert.Equal("eva-gun-voltage[0][offset-2]", results[1]);
            Assert.Equal("eva-gun-voltage[0][offset-1]", results[2]);
            Assert.Equal("eva-gun-voltage[0][offset+0]", results[3]);
            Assert.Equal("eva-gun-voltage[0][offset+1]", results[4]);
            Assert.Equal("eva-gun-voltage[0][offset+2]", results[5]);
            Assert.Equal("eva-gun-voltage[0][offset+3]", results[6]);
            Assert.Equal("eva-gun-voltage[0][offset+4]", results[7]);
        }
    }
}
