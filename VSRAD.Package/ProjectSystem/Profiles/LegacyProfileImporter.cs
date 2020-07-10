using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class LegacyProfileImporter
    {
        public static Dictionary<string, ProfileOptions> ReadProfiles(JObject projectOptions) =>
            ((JObject)projectOptions["Profiles"]).Properties().ToDictionary(p => p.Name, p => ReadProfile((JObject)p.Value));

        private static ProfileOptions ReadProfile(JObject profile)
        {
            var opts = new ProfileOptions();

            ReadGeneralOptions(opts.General, (JObject)profile["General"]);
            ReadDebuggerOptions(opts.Debugger, (JObject)profile["Debugger"]);

            if (ReadPreprocessDisassembleAction("Preprocess", (JObject)profile["Preprocessor"]) is ActionProfileOptions pp)
                opts.Actions.Add(pp);
            if (ReadPreprocessDisassembleAction("Disassemble", (JObject)profile["Disassembler"]) is ActionProfileOptions disasm)
                opts.Actions.Add(disasm);
            if (ReadProfileAction((JObject)profile["Profiler"]) is ActionProfileOptions prof)
                opts.Actions.Add(prof);
            if (ReadBuildAction((JObject)profile["Build"]) is ActionProfileOptions build)
                opts.Actions.Add(build);

            return opts;
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
    }
}