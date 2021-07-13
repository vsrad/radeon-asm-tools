using System;

namespace VSRAD.Package.Utils
{
    static class BreakModeConverter
    {
        static public readonly string[] BreakModeOptions = new[] {  "Single active breakpoint, round-robin",
                                                                    "Single active breakpoint, rerun same line",
                                                                    "Multiple active breakpoints" };

        public static String BreakModeToString(Options.BreakMode value)
        {
            switch (value)
            {
                case Options.BreakMode.SingleRoundRobin: return BreakModeOptions[0];
                case Options.BreakMode.SingleRerun: return BreakModeOptions[1];
                case Options.BreakMode.Multiple: return BreakModeOptions[2];
                default: throw new ArgumentException($"Unknown break mode: {value}");
            }
        }

        public static Options.BreakMode FromString(string value)
        {
            if (value == BreakModeOptions[0]) return Options.BreakMode.SingleRoundRobin;
            if (value == BreakModeOptions[1]) return Options.BreakMode.SingleRerun;
            if (value == BreakModeOptions[2]) return Options.BreakMode.Multiple;
            throw new ArgumentException($"Unknown break mode description: {value}");
        }
    }
}
