using System;
using System.Globalization;
using System.Numerics;
using System.Text;

/*
    Contains parts of https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib,
    distributed under the MIT License, reproduced below:

    The MIT License (MIT)

    Copyright (c) .NET Foundation and Contributors

    All rights reserved.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

/*
    FP to string conversion routines are based on https://arxiv.org/pdf/1310.8121.pdf
    (Easy Accurate Reading and Writing of Floating-Point Numbers).
*/

namespace VSRAD.Package.Utils
{
    public readonly struct F32 : IEquatable<F32>
    {
        public const uint SignMask = 0x8000_0000;
        public const int SignShift = 31;
        public const byte ShiftedSignMask = (byte)(SignMask >> SignShift);

        public const uint BiasedExpMask = 0x7F80_0000;
        public const int BiasedExpShift = 23;

        public const uint MantissaMask = 0x007F_FFFF;

        public const byte MinBiasedExponent = 0x00;
        public const byte MaxBiasedExponent = 0xFF;

        public const byte ExpBias = 127;

        public float Value { get; }

        public uint Bits => BitConverter.ToUInt32(BitConverter.GetBytes(Value), 0);

        public F32(float value)
        {
            Value = value;
        }

        public static implicit operator float(F32 value) => value.Value;

        public static implicit operator F32(float value) => new F32(value);

        public static F32 FromBits(uint bits) => new F32(BitConverter.ToSingle(BitConverter.GetBytes(bits), 0));

        public static bool TryParse(string s, out F32 result)
        {
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                result = f;
                if (s.StartsWith("-", StringComparison.Ordinal) && result >= 0.0) // Parse "-0" as negative zero
                    result = FromBits(result.Bits | SignMask);
                return true;
            }
            else
            {
                result = 0.0f;
                return false;
            }
        }

        public override string ToString()
        {
            if (Value == 0.0 && IsNegative(this))
                return "-" + Value.ToString("R", CultureInfo.InvariantCulture);
            else
                return Value.ToString("R", CultureInfo.InvariantCulture);
        }

        public string ToStringPrecise()
        {
            if (IsNaN(this) || this == float.PositiveInfinity || this == float.NegativeInfinity || this == 0.0f)
                return ToString();

            bool sign = IsNegative(this);
            int biasedExp = (int)((Bits & BiasedExpMask) >> BiasedExpShift);
            uint mantissa = Bits & MantissaMask;

            int exponent = biasedExp - ExpBias - BiasedExpShift;
            if (biasedExp != 0)
                mantissa |= 1 << BiasedExpShift;
            else // denorm
                exponent += 1;

            while ((mantissa & 1) == 0) // normalize to avoid trailing zeroes after the decimal point
            {
                mantissa /= 2;
                exponent++;
            }

            return FloatExtensions.FormatFloatPrecise(sign, exponent, mantissa);
        }

        public static bool IsNegative(F32 value) => (int)value.Bits < 0;

        public static bool IsNaN(F32 value) => float.IsNaN(value);

        public override bool Equals(object obj) => (obj is F32 other) && Equals(other);

        public bool Equals(F32 other) => Value.Equals(other.Value);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(F32 left, F32 right) => left.Value == right.Value;

        public static bool operator !=(F32 left, F32 right) => !(left == right);
    }

    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Half.cs
    public readonly struct F16 : IEquatable<F16>
    {
        public const ushort SignMask = 0x8000;
        public const int SignShift = 15;

        public const ushort BiasedExpMask = 0x7C00;
        public const int BiasedExpShift = 10;

        public const ushort MantissaMask = 0x03FF;

        public const byte MinBiasedExponent = 0x00;
        public const byte MaxBiasedExponent = 0x1F;

        public const byte ExpBias = 15;

        public const ushort PositiveInfinityBits = 0x7C00;
        public const ushort NegativeInfinityBits = 0xFC00;

        public const ushort MinValueBits = 0xFBFF;
        public const ushort MaxValueBits = 0x7BFF;

        public static F16 PositiveInfinity => new F16(PositiveInfinityBits);
        public static F16 NegativeInfinity => new F16(NegativeInfinityBits);
        public static F16 MinValue => new F16(MinValueBits);
        public static F16 MaxValue => new F16(MaxValueBits);

        public ushort Bits { get; }

        public F16(ushort value)
        {
            Bits = value;
        }
        public F16(bool sign, ushort exp, ushort sig)
        {
            Bits = (ushort)(((sign ? 1 : 0) << SignShift) + (exp << BiasedExpShift) + sig);
        }

        public string ToStringPrecise()
        {
            if (IsNaN(this) || this == PositiveInfinity || this == NegativeInfinity || IsPositiveOrNegativeZero(this))
                return new F32((float)this).ToString();

            bool sign = IsNegative(this);
            int biasedExp = (Bits & BiasedExpMask) >> BiasedExpShift;
            uint mantissa = (uint)Bits & MantissaMask;

            int exponent = biasedExp - ExpBias - BiasedExpShift;
            if (biasedExp != 0)
                mantissa |= 1 << BiasedExpShift;
            else // denorm
                exponent += 1;

            while ((mantissa & 1) == 0) // normalize to avoid trailing zeroes after the decimal point
            {
                mantissa /= 2;
                exponent++;
            }

            return FloatExtensions.FormatFloatPrecise(sign, exponent, mantissa);
        }

        public override string ToString()
        {
            if (IsNaN(this) || this == PositiveInfinity || this == NegativeInfinity)
                return new F32((float)this).ToString();
            if (IsPositiveOrNegativeZero(this))
                return IsNegative(this) ? "-0" : "0";

            bool sign = IsNegative(this);
            int biasedExp = (Bits & BiasedExpMask) >> BiasedExpShift;
            uint mantissa = (uint)Bits & MantissaMask;

            int exponent = biasedExp - ExpBias - BiasedExpShift;
            if (biasedExp != 0)
                mantissa |= 1 << BiasedExpShift;
            else // denorm
                exponent += 1;

            BigInteger lquo;
            int point = (int)Math.Ceiling(exponent * Math.Log10(2.0));
            if (exponent > 0)
            {
                var num = new BigInteger(mantissa) << (exponent - point);
                lquo = FloatExtensions.RoundQuotient(num, BigInteger.Pow(new BigInteger(5), point));

                var roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                if (roundtripped != this)
                {
                    num <<= 1;
                    point--;
                    lquo = FloatExtensions.RoundQuotient(num, BigInteger.Pow(new BigInteger(5), point));
                    if (mantissa == 1L << BiasedExpShift)
                    {
                        roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                        if (roundtripped != this)
                        {
                            lquo++;
                            roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                            if (roundtripped != this)
                            {
                                num <<= 1;
                                point--;
                                lquo = FloatExtensions.RoundQuotient(num, BigInteger.Pow(new BigInteger(5), point));
                            }
                        }
                    }
                }
            }
            else
            {
                var num = BigInteger.Multiply(BigInteger.Pow(new BigInteger(5), -point), new BigInteger(mantissa));
                var den = BigInteger.One << (point - exponent);
                lquo = FloatExtensions.RoundQuotient(num, den);

                var roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                if (roundtripped != this)
                {
                    num *= new BigInteger(10);
                    point--;
                    lquo = FloatExtensions.RoundQuotient(num, den);
                    if (mantissa == 1L << BiasedExpShift)
                    {
                        roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                        if (roundtripped != this)
                        {
                            lquo++;
                            roundtripped = (F16)float.Parse(FormatHalf(sign, lquo, point));
                            if (roundtripped != this)
                            {
                                num *= new BigInteger(10);
                                point--;
                                lquo = FloatExtensions.RoundQuotient(num, den);
                            }
                        }
                    }
                }
            }
            return FormatHalf(sign, lquo, point);
        }

        private static string FormatHalf(bool sign, BigInteger lquo, int point)
        {
            var sman = lquo.ToString();
            int len = sman.Length, lent = len;
            while (sman[lent - 1] == '0')
                lent--;
            int exp = point + len - 1;

            var str = new StringBuilder();
            if (sign)
                str.Append('-');
            if (exp >= 0 && exp < len)
            {
                exp++;
                str.Append(sman, 0, exp);
                if (lent > exp)
                {
                    str.Append('.');
                    str.Append(sman, exp, lent - exp);
                }
            }
            else if (exp < 0 && exp > -5)
            {
                int zs = point + len;
                str.Append("0.");
                while (zs++ < 0)
                    str.Append("0");
                str.Append(sman, 0, lent);
            }
            else
            {
                str.Append(sman, 0, 1);
                if (lent > 1)
                {
                    str.Append('.');
                    str.Append(sman, 1, lent - 1);
                }
                if (exp != 0)
                {
                    str.Append('E');
                    str.Append(exp);
                }
            }
            return str.ToString();
        }

        public static bool TryParse(string s, out F16 result)
        {
            if (F32.TryParse(s, out var f))
            {
                result = (F16)(float)f;
                return true;
            }
            else
            {
                result = new F16(0);
                return false;
            }
        }

        public static bool IsNegative(F16 value) => (short)value.Bits < 0;

        public static bool IsNaN(F16 value) => (ushort)(value.Bits & ~SignMask) > PositiveInfinityBits;

        public static bool IsPositiveOrNegativeZero(F16 value) => (value.Bits & ~SignMask) == 0;

        public override bool Equals(object obj) => (obj is F16 other) && Equals(other);

        public bool Equals(F16 other) => this == other;

        public static bool operator ==(F16 left, F16 right)
        {
            if (IsNaN(left) || IsNaN(right))
            {
                // IEEE defines that NaN is not equal to anything, including itself.
                return false;
            }

            // IEEE defines that positive and negative zero are equivalent.
            return (left.Bits == right.Bits) || (IsPositiveOrNegativeZero(left) && IsPositiveOrNegativeZero(right));
        }

        public static bool operator !=(F16 left, F16 right) => !(left == right);

        public override int GetHashCode()
        {
            if (IsNaN(this) || (Bits & ~SignMask) == 0)
            {
                // All NaNs should have the same hash code, as should both Zeros.
                return Bits & PositiveInfinityBits;
            }
            return Bits;
        }

        public static explicit operator float(F16 value)
        {
            bool sign = IsNegative(value);
            int exp = (value.Bits & BiasedExpMask) >> BiasedExpShift;
            uint sig = (uint)value.Bits & MantissaMask;

            if (exp == MaxBiasedExponent)
            {
                if (sig != 0)
                {
                    return CreateSingleNaN(sign, (ulong)sig << 54);
                }
                return sign ? float.NegativeInfinity : float.PositiveInfinity;
            }

            if (exp == 0)
            {
                if (sig == 0)
                {
                    return BitConverter.ToSingle(BitConverter.GetBytes(sign ? F32.SignMask : 0), 0); // Positive / Negative zero
                }
                int sigLeadingZeros32 = 32;
                for (uint s = sig; s != 0; s >>= 1)
                    sigLeadingZeros32--;
                int shiftDist = sigLeadingZeros32 - 16 - 5;
                sig <<= shiftDist;
                exp = -shiftDist;
            }

            return CreateSingle(sign, (byte)(exp + 0x70), sig << 13);
        }

        public static explicit operator F16(float value)
        {
            uint floatInt = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            bool sign = (int)floatInt < 0;
            int floatExp = (int)(floatInt & F32.BiasedExpMask) >> F32.BiasedExpShift;
            uint floatSig = floatInt & F32.MantissaMask;

            if (floatExp == F32.MaxBiasedExponent)
            {
                if (floatSig != 0) // NaN
                {
                    return CreateHalfNaN(sign, (ulong)floatSig << 41); // Shift the significand bits to the left end
                }
                return sign ? NegativeInfinity : PositiveInfinity;
            }

            uint sigHalf = (floatSig >> 9) | ((floatSig & 0x1FF) != 0 ? 1u : 0u); // RightShiftJam

            if ((floatExp | (int)sigHalf) == 0)
            {
                return new F16(sign, 0, 0);
            }

            return RoundPackToHalf(sign, (short)(floatExp - 0x71), (ushort)(sigHalf | 0x4000));
        }

        private static F16 RoundPackToHalf(bool sign, short exp, ushort sig)
        {
            const int RoundIncrement = 0x8; // Depends on rounding mode but it's always towards closest / ties to even
            int roundBits = sig & 0xF;

            if ((uint)exp >= 0x1D)
            {
                if (exp < 0)
                {
                    sig = (ushort)ShiftRightJam(sig, -exp);
                    exp = 0;
                    roundBits = sig & 0xF;
                }
                else if (exp > 0x1D || sig + RoundIncrement >= 0x8000) // Overflow
                {
                    return sign ? NegativeInfinity : PositiveInfinity;
                }
            }

            sig = (ushort)((sig + RoundIncrement) >> 4);
            sig &= (ushort)~(((roundBits ^ 8) != 0 ? 0 : 1) & 1);

            if (sig == 0)
            {
                exp = 0;
            }

            return new F16(sign, (ushort)exp, sig);
        }

        private static F16 CreateHalfNaN(bool sign, ulong significand)
        {
            const uint NaNBits = BiasedExpMask | 0x200; // Most significant significand bit

            uint signInt = (sign ? 1u : 0u) << SignShift;
            uint sigInt = (uint)(significand >> 54);

            return new F16((ushort)(signInt | NaNBits | sigInt));
        }

        // If any bits are lost by shifting, "jam" them into the LSB.
        // if dist > bit count, Will be 1 or 0 depending on i
        // (unlike bitwise operators that masks the lower 5 bits)
        private static uint ShiftRightJam(uint i, int dist) => dist < 31 ? (i >> dist) | (i << (-dist & 31) != 0 ? 1u : 0u) : (i != 0 ? 1u : 0u);

        private static float CreateSingleNaN(bool sign, ulong significand)
        {
            const uint NaNBits = F32.BiasedExpMask | 0x400000; // Most significant significand bit

            uint signInt = (sign ? 1u : 0u) << F32.SignShift;
            uint sigInt = (uint)(significand >> 41);

            return BitConverter.ToSingle(BitConverter.GetBytes(signInt | NaNBits | sigInt), 0);
        }

        private static float CreateSingle(bool sign, byte exp, uint sig) =>
            BitConverter.ToSingle(BitConverter.GetBytes(((sign ? 1u : 0u) << F32.SignShift) + ((uint)exp << F32.BiasedExpShift) + sig), 0);
    }

    public static class FloatExtensions
    {
        public static BigInteger RoundQuotient(BigInteger dividend, BigInteger divisor)
        {
            var quotient = BigInteger.DivRem(dividend, divisor, out var remainder);
            bool roundUp;
            if ((quotient & BigInteger.One) == BigInteger.Zero)
                roundUp = (remainder << 1) > divisor;
            else
                roundUp = (remainder << 1) >= divisor;
            return quotient + (roundUp ? 1 : 0);
        }

        public static string FormatFloatPrecise(bool sign, int exponent, uint mantissa)
        {
            BigInteger num;
            int numFracDigits;
            if (exponent > 0)
            {
                num = BigInteger.Multiply(BigInteger.Pow(new BigInteger(2), exponent), new BigInteger(mantissa));
                numFracDigits = 0;
            }
            else
            {
                num = BigInteger.Multiply(BigInteger.Pow(new BigInteger(5), -exponent), new BigInteger(mantissa));
                numFracDigits = -exponent;
            }

            var numStr = num.ToString();
            if (numFracDigits == 0)
            {
                return (sign ? "-" : "") + numStr;
            }
            else if (numStr.Length > numFracDigits)
            {
                var numIntDigits = numStr.Length - numFracDigits;
                return (sign ? "-" : "") + numStr.Substring(0, numIntDigits) + '.' + numStr.Substring(numIntDigits, numFracDigits);
            }
            else
            {
                var numLeadingZeroes = numFracDigits - numStr.Length;
                return (sign ? "-0." : "0.") + new string('0', numLeadingZeroes) + numStr;
            }
        }
    }
}
