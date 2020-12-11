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
        Task<IEnumerable<Message>> ExtractMessagesAsync(IEnumerable<string> outputs);
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

        public async Task<IEnumerable<Message>> ExtractMessagesAsync(IEnumerable<string> outputs)
        {
            var messages = Errors.Parser.ParseStderr(outputs);
            if (messages.Count > 0)
            {
                var projectSources = (await _sourceManager.ListProjectFilesAsync()).Select(f => f.relativePath);
                UpdateErrorLocations(messages, projectSources);
            }
            return messages;

        }

        public static void UpdateErrorLocations(IEnumerable<Message> messages, IEnumerable<string> projectSources)
        {
            foreach (var message in messages)
                if (message.SourceFile != null)
                    message.SourceFile = Errors.LineMapper.MapSourceToHost(message.SourceFile, projectSources);
        }
    }
}
