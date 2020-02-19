using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// https://android.googlesource.com/platform/frameworks/base/+/master/core/java/android/util/Half.java
namespace VSRAD.Package.Utils
{
    class Half
    {
        private const int FP16_SIGN_MASK = 0x8000;
        private const int FP16_EXPONENT_SHIFT = 10;
        private const int FP16_EXPONENT_MASK = 0x1f;
        private const int FP16_SIGNIFICAND_MASK = 0x3ff;
        private const int FP16_EXPONENT_BIAS = 15;
        private const int FP32_EXPONENT_SHIFT = 23;
        private const int FP32_EXPONENT_BIAS = 127;
        private const int FP32_QNAN_MASK = 0x400000;
        private const uint FP32_DENORMAL_MAGIC = 126 << 23;
        private static readonly float FP32_DENORMAL_FLOAT = UintBitsToFloat(FP32_DENORMAL_MAGIC);

        public static float ToFloat(ushort h)
        {
            uint bits = (uint) h & 0xffff;
            uint s = bits & FP16_SIGN_MASK;
            uint e = (bits >> FP16_EXPONENT_SHIFT) & FP16_EXPONENT_MASK;
            uint m = (bits) & FP16_SIGNIFICAND_MASK;
            uint outE = 0;
            uint outM = 0;
            if (e == 0)
            { // Denormal or 0
                if (m != 0)
                {
                    // Convert denorm fp16 into normalized fp32
                    float o = UintBitsToFloat(FP32_DENORMAL_MAGIC + m);
                    o -= FP32_DENORMAL_FLOAT;
                    return s == 0 ? o : -o;
                }
            }
            else
            {
                outM = m << 13;
                if (e == 0x1f)
                { // Infinite or NaN
                    outE = 0xff;
                    if (outM != 0)
                    { // SNaNs are quieted
                        outM |= FP32_QNAN_MASK;
                    }
                }
                else
                {
                    outE = e - FP16_EXPONENT_BIAS + FP32_EXPONENT_BIAS;
                }
            }
            uint res = (s << 16) | (outE << FP32_EXPONENT_SHIFT) | outM;
            return UintBitsToFloat(res);
        }

        private static float UintBitsToFloat(uint num)
        {
            byte[] bytes = BitConverter.GetBytes(num);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
