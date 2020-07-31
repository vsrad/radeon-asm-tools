using Microsoft.VisualStudio.ProjectSystem;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools
{
    public interface IBuildErrorProcessor
    {
        Task<IEnumerable<Message>> ExtractMessagesAsync(IEnumerable<string> outputs, string preprocessedSource);
    }

    [Export(typeof(IBuildErrorProcessor))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BuildErrorProcessor : IBuildErrorProcessor
    {
        private readonly IProjectSourceManager _sourceManager;

        [ImportingConstructor]
        public BuildErrorProcessor(IProjectSourceManager sourceManager)
        {
            _sourceManager = sourceManager;
        }

        public async Task<IEnumerable<Message>> ExtractMessagesAsync(IEnumerable<string> outputs, string preprocessedSource)
        {
            var messages = Errors.Parser.ParseStderr(outputs);
            if (messages.Count > 0)
            {
                var projectSources = (await _sourceManager.ListProjectFilesAsync()).Select(f => f.relativePath);
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
                    // if message do not contain line than do
                    // not try to map it with markers
                    if (messageLine == 0)
                        continue;
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
