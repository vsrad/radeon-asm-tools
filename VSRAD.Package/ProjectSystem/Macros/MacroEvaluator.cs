using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
#pragma warning disable CA1815 // Not overriding equals because the struct is only used to pass arguments
    public readonly struct MacroEvaluatorTransientValues
#pragma warning restore CA1815
    {
        public (string filename, uint line) ActiveSourceFile { get; }
        public uint[] BreakLines { get; }
        public string[] WatchesOverride { get; }

        public MacroEvaluatorTransientValues((string, uint) activeSourceFile, uint[] breakLines = null, string[] watchesOverride = null)
        {
            ActiveSourceFile = activeSourceFile;
            BreakLines = breakLines;
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

        public const string BuildExecutable = "RadBuildExe";
        public const string BuildArguments = "RadBuildArgs";
        public const string BuildWorkingDirectory = "RadBuildWorkDir";

        public const string PreprocessorExecutable = "RadPpExe";
        public const string PreprocessorArguments = "RadPpArgs";
        public const string PreprocessorWorkingDirectory = "RadPpDir";
        public const string PreprocessorOutputPath = "RadPpOutputPath";
        public const string PreprocessorLocalPath = "RadPpLocalCopyPath";
        public const string PreprocessorLineMarker = "RadPpLineMarker";
    }

    public interface IMacroEvaluator
    {
        Task<string> GetMacroValueAsync(string name);
        Task<string> EvaluateAsync(string src);
    }

    public sealed class MacroEvaluationException : Exception { public MacroEvaluationException(string message) : base(message) { } }

    public sealed class MacroEvaluator : IMacroEvaluator
    {
        private static readonly Regex _macroRegex = new Regex(@"\$(ENVR?)?\(([^()]+)\)", RegexOptions.Compiled);

        private readonly IProjectProperties _projectProperties;
        private readonly AsyncLazy<IReadOnlyDictionary<string, string>> _remoteEnvironment;

        private readonly Options.ProfileOptions _profileOptions;
        private readonly Dictionary<string, string> _macroCache;

        public MacroEvaluator(
            IProjectProperties projectProperties,
            MacroEvaluatorTransientValues values,
            AsyncLazy<IReadOnlyDictionary<string, string>> remoteEnvironment,
            Options.DebuggerOptions debuggerOptions,
            Options.ProfileOptions profileOptions)
        {
            _projectProperties = projectProperties;
            _remoteEnvironment = remoteEnvironment;
            _profileOptions = profileOptions;

            // Properties that are macros but do not contain macros themselves
            _macroCache = new Dictionary<string, string>
            {
                { RadMacros.ActiveSourceFile, values.ActiveSourceFile.filename },
                { RadMacros.ActiveSourceFileLine, values.ActiveSourceFile.line.ToString() },
                { RadMacros.Watches, values.WatchesOverride != null
                    ? string.Join(":", values.WatchesOverride)
                    : string.Join(":", debuggerOptions.GetWatchSnapshot()) },
                { RadMacros.AWatches, string.Join(":", debuggerOptions.GetAWatchSnapshot()) },
                { RadMacros.BreakLine, string.Join(":", values.BreakLines ?? new[] { 0u }) },
                { RadMacros.DebugAppArgs, debuggerOptions.AppArgs },
                { RadMacros.DebugBreakArgs, debuggerOptions.BreakArgs },
                { RadMacros.Counter, debuggerOptions.Counter.ToString() }
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

                case RadMacros.BuildExecutable: unevaluated = _profileOptions.Build.Executable; break;
                case RadMacros.BuildArguments: unevaluated = _profileOptions.Build.Arguments; break;
                case RadMacros.BuildWorkingDirectory: unevaluated = _profileOptions.Build.WorkingDirectory; break;

                case RadMacros.PreprocessorExecutable: unevaluated = _profileOptions.Preprocessor.Executable; break;
                case RadMacros.PreprocessorArguments: unevaluated = _profileOptions.Preprocessor.Arguments; break;
                case RadMacros.PreprocessorWorkingDirectory: unevaluated = _profileOptions.Preprocessor.WorkingDirectory; break;
                case RadMacros.PreprocessorOutputPath: unevaluated = _profileOptions.Preprocessor.OutputPath; break;
                case RadMacros.PreprocessorLocalPath: unevaluated = _profileOptions.Preprocessor.LocalOutputCopyPath; break;
                case RadMacros.PreprocessorLineMarker: unevaluated = _profileOptions.Preprocessor.LineMarker; break;
            }

            if (unevaluated != null)
                value = await EvaluateAsync(unevaluated, recursionStartName);
            else
                value = await _projectProperties.GetEvaluatedPropertyValueAsync(name);

            _macroCache.Add(name, value);
            return value;
        }

        public Task<string> EvaluateAsync(string src) => EvaluateAsync(src, null);

        private Task<string> EvaluateAsync(string src, string recursionStartName) =>
            _macroRegex.ReplaceAsync(src, (m) => ReplaceMacroMatchAsync(m, recursionStartName));

        private async Task<string> ReplaceMacroMatchAsync(Match macroMatch, string recursionStartName)
        {
            var macro = macroMatch.Groups[2].Value;
            switch (macroMatch.Groups[1].Value)
            {
                case "ENV":
                    return Environment.GetEnvironmentVariable(macro);
                case "ENVR":
                    var env = await _remoteEnvironment.GetValueAsync();
                    return env.TryGetValue(macro, out var value) ? value : "";
                default:
                    return await GetMacroValueAsync(macro, recursionStartName);
            }
        }
    }
}
