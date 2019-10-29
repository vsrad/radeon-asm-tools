using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VSRAD.BuildTools
{
    public class SourceValidationBuild : Task
    {
        [Required]
        public string ProjectRoot { get; set; }
        [Required]
        public string StartFile { get; set; }
        public string BuildToolExe { get; set; }
        public string BuildToolExeArguments { get; set; }
        public string BuildToolEnvironmentVariables { get; set; }

        public override bool Execute()
        {
            if (BuildToolExe == null || BuildToolExeArguments == null || BuildToolEnvironmentVariables == null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append((BuildToolExe == null) ? $"{nameof(BuildToolExe)} " : null);
                stringBuilder.Append((BuildToolExeArguments == null) ? $"{nameof(BuildToolExeArguments)} " : null);
                stringBuilder.Append((BuildToolEnvironmentVariables == null) ? $"{nameof(BuildToolEnvironmentVariables)} " : null);
                Log.LogMessage(MessageImportance.High, $"Build did not start: one or several properties is not set - {stringBuilder.ToString()}");
                return true;
            }
            string args = BuildToolExeArguments.Replace("${SRC_PATH}", Path.Combine(ProjectRoot, StartFile));
            Log.LogMessage(MessageImportance.High, $"Executing {BuildToolExe} {args}");

            var buildProcess = new SourceValidationProcess(BuildToolExe, args,
                (stdoutMessage) => Log.LogMessage(MessageImportance.High, "[stdout] " + stdoutMessage),
                (stderrMessage) => Log.LogMessage(MessageImportance.High, "[stderr] " + stderrMessage),
                BuildToolEnvironmentVariables);

            int exitCode = buildProcess.WaitForExit();

            var errorBuffer = new StringBuilder();
            using (var reader = new StringReader(buildProcess.GetBufferedStderr()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("*"))
                    {
                        if (errorBuffer.Length != 0)
                        {
                            PrintMessages(errorBuffer.ToString());
                            errorBuffer.Clear();
                        }
                        errorBuffer.AppendLine(line.TrimStart(new[] { '*' }));
                    }
                    else if (errorBuffer.Length != 0 && line.StartsWith("    "))
                    {
                        errorBuffer.AppendLine(line);
                    }
                }
            }
            if (errorBuffer.Length != 0)
            {
                PrintMessages(errorBuffer.ToString());
            }

            Log.LogMessage(MessageImportance.High, $"Build finished with exit code {exitCode}");

            return exitCode == 0;
        }

        private void PrintMessages(string errorString)
        {
            var defaultFile = Path.Combine(ProjectRoot, StartFile);
            var messages = SourceValidationMessageParser.ExtractMessages(ProjectRoot, defaultFile, errorString);
            foreach (var message in messages)
            {
                switch (message.Kind)
                {
                    case SourceValidationMessageParser.MessageKind.Error:
                        Log.LogError(subcategory: null, errorCode: null, helpKeyword: null,
                           message: message.Text, file: message.SourceFile, lineNumber: message.LineNumber,
                           columnNumber: 0, endLineNumber: 0, endColumnNumber: 0);
                        break;
                    case SourceValidationMessageParser.MessageKind.Warning:
                        Log.LogWarning(subcategory: null, warningCode: null, helpKeyword: null,
                           message: message.Text, file: message.SourceFile, lineNumber: message.LineNumber,
                           columnNumber: 0, endLineNumber: 0, endColumnNumber: 0);
                        break;
                }
            }
        }
    }
}