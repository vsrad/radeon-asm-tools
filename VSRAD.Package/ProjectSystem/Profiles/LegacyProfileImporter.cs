using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class LegacyProfileImporter
    {
        public static Dictionary<string, ProfileOptions> ReadProfiles(JObject projectOptions) =>
            ((JObject)projectOptions["Profiles"]).Properties().ToDictionary(p => p.Name, p => ReadProfile((JObject)p.Value));

        private static ProfileOptions ReadProfile(JObject conf)
        {
            var profile = new ProfileOptions();

            ReadGeneralOptions(profile.General, (JObject)conf["General"]);
            ReadDebuggerOptions(profile.Debugger, (JObject)conf["Debugger"]);

            if (ReadPreprocessDisassembleAction("Preprocess", (JObject)conf["Preprocessor"]) is ActionProfileOptions pp)
                profile.Actions.Add(pp);
            if (ReadPreprocessDisassembleAction("Disassemble", (JObject)conf["Disassembler"]) is ActionProfileOptions disasm)
                profile.Actions.Add(disasm);
            if (ReadProfileAction((JObject)conf["Profiler"]) is ActionProfileOptions prof)
                profile.Actions.Add(prof);
            if (ReadBuildAction((JObject)conf["Build"]) is ActionProfileOptions build)
                profile.Actions.Add(build);

            TransferHardcodedMacros(profile, conf);
            return profile;
        }

        private static void ReadGeneralOptions(GeneralProfileOptions opts, JObject conf)
        {
            opts.RemoteMachine = (string)conf["RemoteMachine"];
            opts.Port = (int)conf["Port"];
            opts.CopySources = (bool)conf["CopySources"];
            opts.DeployDirectory = (string)conf["DeployDirectory"];
            opts.AdditionalSources = (string)conf["AdditionalSources"];
        }

        private static void ReadDebuggerOptions(DebuggerProfileOptions opts, JObject conf)
        {
            opts.BinaryOutput = (bool)conf["BinaryOutput"];
            opts.OutputOffset = (int)conf["OutputOffset"];

            opts.OutputFile.Location = StepEnvironment.Remote;
            opts.OutputFile.CheckTimestamp = true;
            opts.OutputFile.Path = (string)conf["OutputPath"];

            opts.WatchesFile.Location = StepEnvironment.Remote;
            opts.WatchesFile.CheckTimestamp = true;
            opts.WatchesFile.Path = (string)conf["ValidWatchesFilePath"];

            opts.StatusFile.Location = StepEnvironment.Remote;
            opts.StatusFile.CheckTimestamp = true;
            opts.StatusFile.Path = (string)conf["StatusStringFilePath"];

            opts.Steps.Add(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = (string)conf["Executable"],
                Arguments = (string)conf["Arguments"],
                WorkingDirectory = (string)conf["WorkingDirectory"],
                RunAsAdmin = (bool)conf["RunAsAdmin"],
                WaitForCompletion = true,
                TimeoutSecs = (int)conf["TimeoutSecs"]
            });
        }

        private static ActionProfileOptions ReadPreprocessDisassembleAction(string name, JObject conf)
        {
            if (string.IsNullOrEmpty((string)conf["Executable"]))
                return null;

            var action = new ActionProfileOptions { Name = name };
            action.Steps.Add(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = (string)conf["Executable"],
                Arguments = (string)conf["Arguments"],
                WorkingDirectory = (string)conf["WorkingDirectory"],
                WaitForCompletion = true
            });
            action.Steps.Add(new CopyFileStep
            {
                Direction = FileCopyDirection.RemoteToLocal,
                SourcePath = (string)conf["OutputPath"],
                TargetPath = (string)conf["LocalOutputCopyPath"],
                CheckTimestamp = true
            });
            action.Steps.Add(new OpenInEditorStep
            {
                Path = (string)conf["LocalOutputCopyPath"],
                LineMarker = (string)conf["LineMarker"]
            });
            return action;
        }

        private static ActionProfileOptions ReadProfileAction(JObject conf)
        {
            if (string.IsNullOrEmpty((string)conf["Executable"]))
                return null;

            var action = new ActionProfileOptions { Name = "Profile" };
            action.Steps.Add(new ExecuteStep
            {
                Environment = StepEnvironment.Remote,
                Executable = (string)conf["Executable"],
                Arguments = (string)conf["Arguments"],
                WorkingDirectory = (string)conf["WorkingDirectory"],
                RunAsAdmin = true,
                WaitForCompletion = true
            });
            action.Steps.Add(new CopyFileStep
            {
                Direction = FileCopyDirection.RemoteToLocal,
                SourcePath = (string)conf["OutputPath"],
                TargetPath = (string)conf["LocalOutputCopyPath"],
                CheckTimestamp = true
            });
            action.Steps.Add(new ExecuteStep
            {
                Environment = StepEnvironment.Local,
                Executable = (string)conf["ViewerExecutable"],
                Arguments = (string)conf["ViewerArguments"],
                WaitForCompletion = false
            });
            return action;
        }

        private static ActionProfileOptions ReadBuildAction(JObject conf)
        {
            var action = new ActionProfileOptions { Name = "Build" };

            if ((bool)conf["RunPreprocessor"])
                action.Steps.Add(new RunActionStep { Name = "Preprocess" });
            if ((bool)conf["RunDisassembler"])
                action.Steps.Add(new RunActionStep { Name = "Disassemble" });
            if (!string.IsNullOrEmpty((string)conf["Executable"]))
                action.Steps.Add(new ExecuteStep
                {
                    Environment = StepEnvironment.Local,
                    Executable = (string)conf["Executable"],
                    Arguments = (string)conf["Arguments"],
                    WorkingDirectory = (string)conf["WorkingDirectory"],
                    WaitForCompletion = true
                });

            return (action.Steps.Count > 0) ? action : null;
        }

        private static void TransferHardcodedMacros(ProfileOptions profile, JObject conf)
        {
            var macroRegex = new Regex(@"\$\(([^()]+)\)", RegexOptions.Compiled);
            var macros = new Dictionary<string, string>();

            foreach (var objProp in conf.Properties())
            {
                foreach (var prop in ((JObject)objProp.Value).Properties())
                {
                    foreach (Match match in macroRegex.Matches((string)prop.Value))
                    {
                        var macroName = match.Groups[1].Value;
                        if (macros.ContainsKey(macroName))
                            continue;
                        if (ExtractMacro(profile, macroName) is string macroValue)
                            macros.Add(macroName, macroValue);
                    }
                }
            }

            foreach (var macroPair in macros)
                profile.Macros.Add(new MacroItem(macroPair.Key, macroPair.Value, userDefined: true));
        }

        private static string ExchangeValueWithMacro(object obj, string propertyName, string macro)
        {
            var macroValue = (string)obj.GetType().GetProperty(propertyName).GetValue(obj);
            obj.GetType().GetProperty(propertyName).SetValue(obj, "$(" + macro + ")");
            return macroValue;
        }

        private static string ExtractMacro(ProfileOptions profile, string macroName)
        {
            string WithAction(string name, Func<ActionProfileOptions, string> edit)
            {
                var action = profile.Actions.FirstOrDefault(a => a.Name == name);
                if (action != null)
                    return edit(action);
                return null;
            }

            switch (macroName)
            {
                case RadMacros.DebuggerExecutable:
                    return ExchangeValueWithMacro(profile.Debugger.Steps[0], nameof(ExecuteStep.Executable), RadMacros.DebuggerExecutable);
                case RadMacros.DebuggerArguments:
                    return ExchangeValueWithMacro(profile.Debugger.Steps[0], nameof(ExecuteStep.Arguments), RadMacros.DebuggerArguments);
                case RadMacros.DebuggerWorkingDirectory:
                    return ExchangeValueWithMacro(profile.Debugger.Steps[0], nameof(ExecuteStep.WorkingDirectory), RadMacros.DebuggerWorkingDirectory);
                case RadMacros.DebuggerOutputPath:
                    return ExchangeValueWithMacro(profile.Debugger.OutputFile, nameof(BuiltinActionFile.Path), RadMacros.DebuggerOutputPath);

                case RadMacros.PreprocessorExecutable:
                    return WithAction("Preprocess", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Executable), RadMacros.PreprocessorExecutable));
                case RadMacros.PreprocessorArguments:
                    return WithAction("Preprocess", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Arguments), RadMacros.PreprocessorArguments));
                case RadMacros.PreprocessorWorkingDirectory:
                    return WithAction("Preprocess", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.WorkingDirectory), RadMacros.PreprocessorWorkingDirectory));
                case RadMacros.PreprocessorOutputPath:
                    return WithAction("Preprocess", a => ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.SourcePath), RadMacros.PreprocessorOutputPath));
                case RadMacros.PreprocessorLocalPath:
                    return WithAction("Preprocess", a =>
                    {
                        ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.TargetPath), RadMacros.PreprocessorLocalPath);
                        return ExchangeValueWithMacro(a.Steps[2], nameof(OpenInEditorStep.Path), RadMacros.PreprocessorLocalPath);
                    });

                case RadMacros.DisassemblerExecutable:
                    return WithAction("Disassemble", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Executable), RadMacros.DisassemblerExecutable));
                case RadMacros.DisassemblerArguments:
                    return WithAction("Disassemble", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Arguments), RadMacros.DisassemblerArguments));
                case RadMacros.DisassemblerWorkingDirectory:
                    return WithAction("Disassemble", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.WorkingDirectory), RadMacros.DisassemblerWorkingDirectory));
                case RadMacros.DisassemblerOutputPath:
                    return WithAction("Disassemble", a => ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.SourcePath), RadMacros.DisassemblerOutputPath));
                case RadMacros.DisassemblerLocalPath:
                    return WithAction("Disassemble", a =>
                    {
                        ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.TargetPath), RadMacros.DisassemblerLocalPath);
                        return ExchangeValueWithMacro(a.Steps[2], nameof(OpenInEditorStep.Path), RadMacros.DisassemblerLocalPath);
                    });

                case RadMacros.ProfilerExecutable:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Executable), RadMacros.ProfilerExecutable));
                case RadMacros.ProfilerArguments:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.Arguments), RadMacros.ProfilerArguments));
                case RadMacros.ProfilerWorkingDirectory:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[0], nameof(ExecuteStep.WorkingDirectory), RadMacros.ProfilerWorkingDirectory));
                case RadMacros.ProfilerOutputPath:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.SourcePath), RadMacros.ProfilerOutputPath));
                case RadMacros.ProfilerViewerExecutable:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[2], nameof(ExecuteStep.Executable), RadMacros.ProfilerViewerExecutable));
                case RadMacros.ProfilerViewerArguments:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[2], nameof(ExecuteStep.Arguments), RadMacros.ProfilerViewerArguments));
                case RadMacros.ProfilerLocalPath:
                    return WithAction("Profile", a => ExchangeValueWithMacro(a.Steps[1], nameof(CopyFileStep.TargetPath), RadMacros.ProfilerLocalPath));

                case RadMacros.BuildExecutable:
                    return WithAction("Build", a =>
                    {
                        if (a.Steps.FirstOrDefault(s => s is ExecuteStep) is ExecuteStep exec)
                            return ExchangeValueWithMacro(exec, nameof(ExecuteStep.Executable), RadMacros.BuildExecutable);
                        return null;
                    });
                case RadMacros.BuildArguments:
                    return WithAction("Build", a =>
                    {
                        if (a.Steps.FirstOrDefault(s => s is ExecuteStep) is ExecuteStep exec)
                            return ExchangeValueWithMacro(exec, nameof(ExecuteStep.Arguments), RadMacros.BuildArguments);
                        return null;
                    });
                case RadMacros.BuildWorkingDirectory:
                    return WithAction("Build", a =>
                    {
                        if (a.Steps.FirstOrDefault(s => s is ExecuteStep) is ExecuteStep exec)
                            return ExchangeValueWithMacro(exec, nameof(ExecuteStep.WorkingDirectory), RadMacros.BuildWorkingDirectory);
                        return null;
                    });
            }
            return null;
        }
    }
}