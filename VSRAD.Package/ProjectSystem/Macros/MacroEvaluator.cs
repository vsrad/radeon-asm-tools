using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
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
        public string DebugStartupPath { get; }
        public string TargetProcessor { get; }

        public MacroEvaluatorTransientValues(uint sourceLine, string sourcePath, string debugPath = "", string targetProcessor = "", string sourceDir = null, string sourceFile = null)
        {
            ActiveSourceFullPath = sourcePath;
            ActiveSourceDir = sourceDir ?? Path.GetDirectoryName(sourcePath);
            ActiveSourceFile = sourceFile ?? Path.GetFileName(sourcePath);
            ActiveSourceLine = sourceLine;
            DebugStartupPath = debugPath;
            TargetProcessor = targetProcessor;
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
        public const string DebugAppArgs = "RadDebugAppArgs";
        public const string DebugBreakArgs = "RadDebugBreakArgs";
        public const string DebugStartupPath = "RadDebugStartupPath";
        public const string TargetProcessor = "RadTargetProcessor";
    }

    public interface IMacroEvaluator
    {
        Task<Result<string>> GetMacroValueAsync(string name);
        Task<Result<string>> EvaluateAsync(string src);
    }

    public sealed class MacroEvaluationException : Exception { public MacroEvaluationException(string message) : base(message) { } }

    public sealed class MacroEvaluator : IMacroEvaluator
    {
        private static readonly Regex _macroRegex = new Regex(@"\$(ENVR?)?\(([^()]+)\)", RegexOptions.Compiled);

        private readonly IProjectProperties _projectProperties;
        private readonly MacroEvaluatorTransientValues _transientValues;
        private readonly AsyncLazy<IReadOnlyDictionary<string, string>> _remoteEnvironment;

        private readonly Options.DebuggerOptions _debuggerOptions;
        private readonly Options.ProfileOptions _profileOptions;

        private readonly Dictionary<string, string> _macroCache = new Dictionary<string, string>();

        public MacroEvaluator(
            IProjectProperties projectProperties,
            MacroEvaluatorTransientValues transientValues,
            AsyncLazy<IReadOnlyDictionary<string, string>> remoteEnvironment,
            Options.DebuggerOptions debuggerOptions,
            Options.ProfileOptions profileOptions)
        {
            _projectProperties = projectProperties;
            _transientValues = transientValues;
            _remoteEnvironment = remoteEnvironment;
            _debuggerOptions = debuggerOptions;
            _profileOptions = profileOptions;
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

            switch (name)
            {
                case RadMacros.ActiveSourceFullPath: value = _transientValues.ActiveSourceFullPath; break;
                case RadMacros.ActiveSourceDir: value = _transientValues.ActiveSourceDir; break;
                case RadMacros.ActiveSourceFile: value = _transientValues.ActiveSourceFile; break;
                case RadMacros.ActiveSourceFileLine: value = _transientValues.ActiveSourceLine.ToString(); break;
                case RadMacros.DebugAppArgs: value = _debuggerOptions.AppArgs; break;
                case RadMacros.DebugBreakArgs: value = _debuggerOptions.BreakArgs; break;
                case RadMacros.DebugStartupPath: value = _transientValues.DebugStartupPath; break;
                case RadMacros.TargetProcessor:
                    if (!(await EvaluateAsync(_transientValues.TargetProcessor, evaluationChain)).TryGetResult(out value, out var error))
                        return error;
                    break;
                default:
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
                        if (!evalResult.TryGetResult(out value, out error))
                            return error;
                    }
                    else
                    {
                        value = await _projectProperties.GetEvaluatedPropertyValueAsync(name);
                    }
                    break;
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
