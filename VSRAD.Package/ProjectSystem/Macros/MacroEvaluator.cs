using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public readonly struct MacroEvaluatorTransientValues
    {
        public (string filename, uint line) ActiveSourceFile { get; }
        public uint BreakLine { get; }
        public string[] WatchesOverride { get; }

        public MacroEvaluatorTransientValues((string, uint) activeSourceFile, uint breakLine = 0, string[] watchesOverride = null)
        {
            ActiveSourceFile = activeSourceFile;
            BreakLine = breakLine;
            WatchesOverride = watchesOverride;
        }
    }

    public static class RadMacros
    {
        public const string DeployDirectory = "RadDeployDir";

        public const string DebuggerExecutable = "RadDebugExe";
        public const string DebuggerArguments = "RadDebugArgs";
        public const string DebuggerWorkingDirectory = "RadDebugWorkDir";
        public const string DebuggerOutputPath = "RadDebugDataOutputPath";
        public const string DebuggerValidWatchesFilePath = "RadDebugValidWatchesFilePath";

        public const string DisassemblerExecutable = "RadDisasmExe";
        public const string DisassemblerArguments = "RadDisasmArgs";
        public const string DisassemblerWorkingDirectory = "RadDisasmWorkDir";
        public const string DisassemblerOutputPath = "RadDisasmOutputPath";
        public const string DisassemblerLocalPath = "RadDisasmLocalCopyPath";

        public const string ProfilerExecutable = "RadProfileExe";
        public const string ProfilerArguments = "RadProfileArgs";
        public const string ProfilerWorkingDirectory = "RadProfileWorkDir";
        public const string ProfilerOutputPath = "RadProfileOutputPath";
        public const string ProfilerViewerExecutable = "RadProfileViewerExe";
        public const string ProfilerViewerArguments = "RadProfileViewerArgs";
        public const string ProfilerLocalPath = "RadProfileLocalCopyPath";

        public const string ActiveSourceFile = "RadActiveSourceFile";
        public const string ActiveSourceFileLine = "RadActiveSourceFileLine";
        public const string Watches = "RadWatches";
        public const string AWatches = "RadAWatches";
        public const string BreakLine = "RadBreakLine";
        public const string DebugAppArgs = "RadDebugAppArgs";
        public const string DebugBreakArgs = "RadDebugBreakArgs";
        public const string Counter = "RadCounter";
    }

    public interface IMacroEvaluator
    {
        Task<string> GetMacroValueAsync(string name);
        Task<string> EvaluateAsync(string src);
    }

    public sealed class MacroEvaluationException : Exception { public MacroEvaluationException(string message) : base(message) { } }

    public sealed class MacroEvaluator : IMacroEvaluator
    {
        private readonly IProject _project;
        private readonly IProjectProperties _projectProperties;

        private readonly Options.ProfileOptions _profileOptions;
        private readonly Dictionary<string, string> _macroCache;

        public MacroEvaluator(
            IProject project,
            IProjectProperties projectProperties,
            MacroEvaluatorTransientValues values,
            Options.ProfileOptions profileOptionsOverride = null)
        {
            _project = project;
            _projectProperties = projectProperties;
            _profileOptions = profileOptionsOverride ?? _project.Options.Profile;

            // Properties that are macros but do not contain macros themselves
            _macroCache = new Dictionary<string, string>
            {
                { RadMacros.ActiveSourceFile, values.ActiveSourceFile.filename },
                { RadMacros.ActiveSourceFileLine, values.ActiveSourceFile.line.ToString() },
                { RadMacros.Watches, values.WatchesOverride != null
                    ? string.Join(":", values.WatchesOverride)
                    : string.Join(":", _project.Options.DebuggerOptions.GetWatchSnapshot()) },
                { RadMacros.AWatches, string.Join(":", _project.Options.DebuggerOptions.GetAWatchSnapshot()) },
                { RadMacros.BreakLine, values.BreakLine.ToString() },
                { RadMacros.DebugAppArgs, _project.Options.DebuggerOptions.AppArgs },
                { RadMacros.DebugBreakArgs, _project.Options.DebuggerOptions.BreakArgs },
                { RadMacros.Counter, _project.Options.DebuggerOptions.Counter.ToString() }
            };
        }

        public Task<string> GetMacroValueAsync(string name) => GetMacroValueAsync(name, null);

        private async Task<string> GetMacroValueAsync(string name, string recursionStartName)
        {
            if (_macroCache.TryGetValue(name, out var value))
                return value;

            if (recursionStartName == name)
                throw new MacroEvaluationException($"Unable to evaluate $({name}): the macro refers to itself.");
            if (recursionStartName == null)
                recursionStartName = name;

            // TODO: Replace with something less manual (reflection?)
            string unevaluated = null;
            switch (name)
            {
                case RadMacros.DeployDirectory: unevaluated = _profileOptions.General.DeployDirectory; break;

                case RadMacros.DebuggerExecutable: unevaluated = _profileOptions.Debugger.Executable; break;
                case RadMacros.DebuggerArguments: unevaluated = _profileOptions.Debugger.Arguments; break;
                case RadMacros.DebuggerWorkingDirectory: unevaluated = _profileOptions.Debugger.WorkingDirectory; break;
                case RadMacros.DebuggerOutputPath: unevaluated = _profileOptions.Debugger.OutputPath; break;

                case RadMacros.DisassemblerExecutable: unevaluated = _profileOptions.Disassembler.Executable; break;
                case RadMacros.DisassemblerArguments: unevaluated = _profileOptions.Disassembler.Arguments; break;
                case RadMacros.DisassemblerWorkingDirectory: unevaluated = _profileOptions.Disassembler.WorkingDirectory; break;
                case RadMacros.DisassemblerOutputPath: unevaluated = _profileOptions.Disassembler.OutputPath; break;
                case RadMacros.DisassemblerLocalPath: unevaluated = _profileOptions.Disassembler.LocalOutputCopyPath; break;

                case RadMacros.ProfilerExecutable: unevaluated = _profileOptions.Profiler.Executable; break;
                case RadMacros.ProfilerArguments: unevaluated = _profileOptions.Profiler.Arguments; break;
                case RadMacros.ProfilerWorkingDirectory: unevaluated = _profileOptions.Profiler.WorkingDirectory; break;
                case RadMacros.ProfilerOutputPath: unevaluated = _profileOptions.Profiler.OutputPath; break;
                case RadMacros.ProfilerViewerExecutable: unevaluated = _profileOptions.Profiler.ViewerExecutable; break;
                case RadMacros.ProfilerViewerArguments: unevaluated = _profileOptions.Profiler.ViewerArguments; break;
                case RadMacros.ProfilerLocalPath: unevaluated = _profileOptions.Profiler.LocalOutputCopyPath; break;
            }

            if (unevaluated != null)
                value = await EvaluateAsync(unevaluated, recursionStartName);
            else
                value = await _projectProperties.GetEvaluatedPropertyValueAsync(name);

            _macroCache.Add(name, value);
            return value;
        }

        public Task<string> EvaluateAsync(string src) => EvaluateAsync(src, null);

        public async Task<string> EvaluateAsync(string src, string recursionStartName)
        {
            var evaluated = new StringBuilder();

            // https://dotnetfiddle.net/4YG3Ox
            int c = 0;
            while (c < src.Length)
            {
                var macroStart = src.IndexOf('$', c, src.Length - c - 1);
                if (macroStart == -1 || src.Length - macroStart <= 3)
                {
                    evaluated.Append(src, c, src.Length - c);
                    break;
                }

                var macroEnd = src[macroStart + 1] == '(' ? src.IndexOf(')', macroStart + 3) : -1;
                if (macroEnd == -1)
                {
                    evaluated.Append(src, c, macroStart + 1 - c);
                    c = macroStart + 1;
                    continue;
                }

                var macroNameStart = macroStart + 2; // skip $(
                var macroNameLength = macroEnd - macroNameStart;

                var imbalancedMacroStart = src.IndexOf('$', macroNameStart, macroNameLength);
                if (imbalancedMacroStart != -1)
                {
                    evaluated.Append(src, c, imbalancedMacroStart - c);
                    c = imbalancedMacroStart;
                    continue;
                }

                var macroName = src.Substring(macroNameStart, macroNameLength);
                var macroValue = await GetMacroValueAsync(macroName, recursionStartName);
                evaluated.Append(src, c, macroStart - c);
                evaluated.Append(macroValue);
                c = macroEnd + 1;
            }

            return evaluated.ToString();
        }
    }
}
