using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools
{
    public interface IBuildErrorProcessor
    {
        Task<IEnumerable<Message>> ExtractMessagesAsync(string output, string preprocessedSource);
    }

    public sealed class BuildErrorProcessor
    {
        private readonly IProjectSourceManager _sourceManager;

        [ImportingConstructor]
        public BuildErrorProcessor(IProjectSourceManager sourceManager)
        {
            _sourceManager = sourceManager;
        }

        public async Task<IEnumerable<Message>> ExtractMessagesAsync(string output, string preprocessedSource)
        {
            var messages = Errors.Parser.ParseStderr(output);
            if (messages.Count > 0)
            {
                var projectSources = await _sourceManager.ListProjectFilesAsync();
                UpdateErrorLocations(messages, preprocessedSource, projectSources);
            }
            return messages;

        }

        public static void UpdateErrorLocations(IEnumerable<Message> messages, string preprocessedSource, IEnumerable<string> projectSources)
        {
            if (!string.IsNullOrEmpty(preprocessedSource))
            {
                var ppLines = Errors.LineMapper.MapLines(preprocessedSource);
                foreach (var message in messages)
                {
                    var messageLine = message.Line;
                    foreach (var marker in ppLines)
                    {
                        if (marker.PpLine > messageLine) break;

                        message.Line = marker.SourceLine + messageLine - marker.PpLine;
                        message.SourceFile = marker.SourceFile;
                    }
                }
            }

            foreach (var message in messages)
                if (message.SourceFile != null)
                    message.SourceFile = Errors.LineMapper.MapSourceToHost(message.SourceFile, projectSources);
        }
    }
}
