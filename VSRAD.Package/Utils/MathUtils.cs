namespace VSRAD.Package.Utils
{
    public static class MathUtils
    {
        public static int RoundUpQuotient(int dividend, int divisor) =>
            (dividend + (divisor - 1)) / divisor;

        public static uint RoundUpQuotient(uint dividend, uint divisor) =>
            (dividend + (divisor - 1)) / divisor;

        public static int RoundUpToMultiple(int roundee, int alignment) =>
            (roundee + (alignment - 1)) / alignment * alignment;

        public static uint RoundUpToMultiple(uint roundee, uint alignment) =>
            (roundee + (alignment - 1)) / alignment * alignment;
    }
}
