using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace VSRAD.BuildTools
{
    public sealed class RemoteBuildTask : Task
    {
        [Required]
        public string ProjectDir { get; set; }

        public override bool Execute()
        {
            var bridge = new IPCBridge(ProjectDir);
            var result = bridge.Build();

            foreach (var message in RemoteBuildStderrParser.ExtractMessages(result.Stderr))
            {
                switch (message.Kind)
                {
                    case RemoteBuildStderrParser.MessageKind.Error:
                        Log.LogError(
                            subcategory: null, errorCode: null, helpKeyword: null,
                            message: message.Text, file: message.SourceFile,
                            lineNumber: message.Line, columnNumber: message.Column,
                            endLineNumber: 0, endColumnNumber: 0);
                        break;
                    case RemoteBuildStderrParser.MessageKind.Warning:
                        Log.LogWarning(
                            subcategory: null, warningCode: null, helpKeyword: null,
                            message: message.Text, file: message.SourceFile,
                            lineNumber: message.Line, columnNumber: message.Column,
                            endLineNumber: 0, endColumnNumber: 0);
                        break;
                }
            }

            Log.LogMessage(MessageImportance.High, $"Build finished with exit code {result.ExitCode}");

            return result.ExitCode == 0;
        }
    }
}