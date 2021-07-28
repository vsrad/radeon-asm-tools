﻿using System;

namespace VSRAD.Package.Utils
{
    class BreakModeConverter
    {
        static public readonly string[] BreakModeOptions = new[] {  "Round-robin, single active breakpoint",
                                                                    "Rerun line, single active breakpoint",
                                                                    "Multiple active breakpoints" };

        static public readonly string[] ShortBreakModeOptions = new[] { "Round-robin", "Rerun line", "Multiple" };

        public static String BreakModeToString(Options.BreakMode value, bool shortForm = false)
                                => shortForm ? ShortBreakModeOptions[(int)value] : BreakModeOptions[(int)value];

        public static Options.BreakMode FromString(string value)
        {
            if (value == BreakModeOptions[0]) return Options.BreakMode.SingleRoundRobin;
            if (value == BreakModeOptions[1]) return Options.BreakMode.SingleRerun;
            if (value == BreakModeOptions[2]) return Options.BreakMode.Multiple;
            throw new ArgumentException($"Unknown break mode description: {value}");
        }
    }
}