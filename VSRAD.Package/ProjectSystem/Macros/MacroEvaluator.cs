using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class MacroEvaluatorTransientValues
    {
        public string ActiveSourceFullPath { get; }
        public string ActiveSourceFile { get; }
        public string ActiveSourceDir { get; }
        public uint ActiveSourceLine { get; }
        public uint[] BreakLines { get; }
        public ReadOnlyCollection<string> Watches { get; }

        public MacroEvaluatorTransientValues(uint sourceLine, string sourcePath, uint[] breakLines, ReadOnlyCollection<string> watches, string sourceDir = null, string sourceFile = null)
        {
            ActiveSourceFullPath = sourcePath;
            ActiveSourceDir = sourceDir ?? Path.GetDirectoryName(sourcePath);
            ActiveSourceFile = sourceFile ?? Path.GetFileName(sourcePath);
            ActiveSourceLine = sourceLine;
            BreakLines = breakLines;
            Watches = watches;
        }
    }

    public static class CleanProfileMacros
    {
        public static readonly (string, string)[] Macros = new[]
        {
            (LocalWorkDir, LocalWorkDirValue),
            (RemoteWorkDir, RemoteWorkDirValue)
        };

        public const string LocalWorkDir = "RadLocalWorkDir";
        public const string LocalWorkDirValue = "$(ProjectDir)";

        public const string RemoteWorkDir = "RadRemoteWorkDir";
        public const string RemoteWorkDirValue = "";
    }

    public static class RadMacros
    {
        public const string ActiveSourceFullPath = "RadActiveSourceFullPath";
        public const string ActiveSourceDir = "RadActiveSourceDir";
        public const string ActiveSourceFile = "RadActiveSourceFile";
        public const string ActiveSourceFileLine = "RadActiveSourceFileLine";
        public const string Watches = "RadWatches";
        public const string AWatches = "RadAWatches";
        public const string BreakLine = "RadBreakLine";
        public const string DebugAppArgs = "RadDebugAppArgs";
        public const string DebugAppArgs2 = "RadDebugAppArgs2";
        public const string DebugAppArgs3 = "RadDebugAppArgs3";
        public const string Counter = "RadCounter";
        public const string NGroups = "RadNGroups";
        public const string GroupSize = "RadGroupSize";
    }

    public interface IMacroEvaluator
    {
        Task<Result<string>> GetMacroValueAsync(string name);
        Task<Result<string>> EvaluateAsync(string src);
    }

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

            // Predefined macros
            _macroCache = new Dictionary<string, string>
            {
                { RadMacros.ActiveSourceFullPath, values.ActiveSourceFullPath },
                { RadMacros.ActiveSourceDir, values.ActiveSourceDir },
                { RadMacros.ActiveSourceFile, values.ActiveSourceFile },
                { RadMacros.ActiveSourceFileLine, values.ActiveSourceLine.ToString() },
                { RadMacros.Watches, string.Join(":", values.Watches) },
                { RadMacros.AWatches, string.Join(":", debuggerOptions.GetAWatchSnapshot()) },
                { RadMacros.BreakLine, string.Join(":", values.BreakLines ?? new[] { 0u }) },
                { RadMacros.DebugAppArgs, debuggerOptions.AppArgs },
                { RadMacros.DebugAppArgs2, debuggerOptions.AppArgs2 },
                { RadMacros.DebugAppArgs3, debuggerOptions.AppArgs3 },
                { RadMacros.Counter, debuggerOptions.Counter.ToString() },
                { RadMacros.NGroups, debuggerOptions.NGroups.ToString() },
                { RadMacros.GroupSize, debuggerOptions.GroupSize.ToString() }
            };
        }

        public Task<Result<string>> GetMacroValueAsync(string name) => GetMacroValueAsync(name, new List<string>());

        private async Task<Result<string>> GetMacroValueAsync(string name, List<string> evaluationChain)
        {
            if (_macroCache.TryGetValue(name, out var value))
                return value;

            if (evaluationChain.Contains(name))
            {
                var chain = string.Join(" -> ", evaluationChain.Append(name).Select(n => "$(" + n + ")"));
                return new Error($"$({evaluationChain[0]}) contains a cycle: {chain}");
            }
            evaluationChain.Add(name);

            string unevaluated = null;
            foreach (var macro in _profileOptions.Macros)
            {
                if (macro.Name == name)
                {
                    unevaluated = macro.Value;
                    break;
                }
            }

            if (unevaluated != null)
            {
                var evalResult = await EvaluateAsync(unevaluated, evaluationChain);
                if (!evalResult.TryGetResult(out value, out var error))
                    return error;
            }
            else
            {
                value = await _projectProperties.GetEvaluatedPropertyValueAsync(name);
            }

            _macroCache.Add(name, value);
            return value;
        }

        public Task<Result<string>> EvaluateAsync(string src) => EvaluateAsync(src, new List<string>());

        private async Task<Result<string>> EvaluateAsync(string src, List<string> evaluationChain)
        {
            if (string.IsNullOrEmpty(src))
                return "";

            var evaluated = new StringBuilder();
            var posAfterLastMatch = 0;

            foreach (Match match in _macroRegex.Matches(src))
            {
                string macroValue;

                var macroName = match.Groups[2].Value;
                switch (match.Groups[1].Value)
                {
                    case "ENV":
                        macroValue = Environment.GetEnvironmentVariable(macroName);
                        break;
                    case "ENVR":
                        if (_remoteEnvironment == null)
                        {
                            macroValue = Environment.GetEnvironmentVariable(macroName);
                        }
                        else
                        {
                            var remoteEnv = await _remoteEnvironment.GetValueAsync();
                            if (!remoteEnv.TryGetValue(macroName, out macroValue))
                                macroValue = "";
                        }
                        break;
                    default:
                        var evalResult = await GetMacroValueAsync(macroName, evaluationChain);
                        if (!evalResult.TryGetResult(out macroValue, out var error))
                            return error;
                        break;
                }

                evaluated.Append(src, posAfterLastMatch, match.Index - posAfterLastMatch);
                evaluated.Append(macroValue);

                posAfterLastMatch = match.Index + match.Length;
            }

            evaluated.Append(src, posAfterLastMatch, src.Length - posAfterLastMatch);
            return evaluated.ToString();
        }
    }
}
