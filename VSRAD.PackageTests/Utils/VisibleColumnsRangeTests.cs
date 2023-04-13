using System.Collections.Generic;
using VSRAD.Package.Utils;
using Xunit;

namespace VSRAD.PackageTests.Utils
{
    public class VisibleColumnsRangeTests
    {
        [Fact]
        public void TestString()
        {
            var range = new VisibleColumnsRange(SelectorType.First, 16, 64);
            Assert.Equal("0-15:64-79:128-143:192-207:256-271:320-335:384-399:448-463:512-527:576-591:", range.GetRepresentation(640));

            range = new VisibleColumnsRange(SelectorType.Last, 16, 64);
            Assert.Equal("48-63:112-127:176-191:240-255:304-319:368-383:432-447:496-511:560-575:624-639:", range.GetRepresentation(640));
        }
    }
}
