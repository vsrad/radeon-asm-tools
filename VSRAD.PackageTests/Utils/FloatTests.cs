using System;
using VSRAD.Package.Utils;
using Xunit;

namespace VSRAD.PackageTests.Utils
{
    public class FloatTests
    {
        [Fact]
        public void HalfFloatConversionRoundtripTest()
        {
            for (uint i = 0; i <= ushort.MaxValue; ++i)
            {
                var h = new F16((ushort)i);
                var f1 = (float)h;
                var f2 = (float)(F16)f1;

                var bits1 = BitConverter.ToUInt32(BitConverter.GetBytes(f1), 0);
                var bits2 = BitConverter.ToUInt32(BitConverter.GetBytes(f2), 0);

                Assert.Equal(bits1, bits2);
            }
        }

        [Fact]
        public void HalfStringConversionRoundtripTest()
        {
            for (uint i = 0; i <= ushort.MaxValue; ++i)
            {
                var h1 = new F16((ushort)i);
                var s = h1.ToString();
                Assert.True(F16.TryParse(s, out var h2));

                if (F16.IsNaN(h1))
                    Assert.True(F16.IsNaN(h2));
                else
                    Assert.Equal(h1.Bits, h2.Bits);
            }
        }
    }
}
