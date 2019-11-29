using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace VSRAD.BuildTools
{
    public sealed class RemoteBuildTask : Task
    {
        public const string TimeoutError = "Failed to connect to the IDE build server. Ensure the project you're attempting to build is open in Visual Studio.";
        public const string ServerErrorPrefix = "Build server error: ";

        [Required]
        public string ProjectDir { get; set; }

        public override bool Execute()
        {
            var bridge = new IPCBridge(ProjectDir);

            try
            {
                var result = bridge.Build();
                return CheckBuildResult(result);
            }
            catch (TimeoutException)
            {
                Log.LogError(TimeoutError);
                return false;
            }
        }

        private bool CheckBuildResult(IPCBuildResult result)
        {
            if (result.Skipped)
            {
                Log.LogMessage(MessageImportance.High, "Build skipped (executable is not set)");
                return true;
            }
            if (!string.IsNullOrEmpty(result.ServerError))
            {
                Log.LogError(ServerErrorPrefix + result.ServerError);
                return false;
            }

            foreach (var message in result.ErrorMessages)
                switch (message.Kind)
                {
                    case IPCBuildResult.MessageKind.Error:
                        Log.LogError(
                            subcategory: null, errorCode: null, helpKeyword: null,
                            message: message.Text, file: message.SourceFile,
                            lineNumber: message.Line, columnNumber: message.Column,
                            endLineNumber: 0, endColumnNumber: 0);
                        break;
                    case IPCBuildResult.MessageKind.Warning:
                        Log.LogWarning(
                            subcategory: null, warningCode: null, helpKeyword: null,
                            message: message.Text, file: message.SourceFile,
                            lineNumber: message.Line, columnNumber: message.Column,
                            endLineNumber: 0, endColumnNumber: 0);
                        break;
                    case IPCBuildResult.MessageKind.Note:
                        Log.LogWarning(
                            subcategory: null, warningCode: null, helpKeyword: null,
                            message: "note: " + message.Text, file: message.SourceFile,
                            lineNumber: message.Line, columnNumber: message.Column,
                            endLineNumber: 0, endColumnNumber: 0);
                        break;
                }

            Log.LogMessage(MessageImportance.High, $"Build finished with exit code {result.ExitCode}");
            return result.Successful;
        }
    }
}