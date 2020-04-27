using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public interface IErrorListManager
    {
        Task AddToErrorListAsync(string contents);
    }

    [Export(typeof(IErrorListManager))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class ErrorListManager : IErrorListManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private static readonly Regex ScriptErrorRegex = new Regex(
            @"(?<file>[^:]+):\sline\s(?<line>\d+):\s(?<text>.+)", RegexOptions.Compiled);

        [ImportingConstructor]
        public ErrorListManager(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _errorListProvider = new ErrorListProvider(_serviceProvider);
        }

        public Task AddToErrorListAsync(string stderr)
        {
            if (stderr == null) return Task.CompletedTask;

            _errorListProvider.Tasks.Clear();
            using (var reader = new StringReader(stderr))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = ScriptErrorRegex.Match(line);
                    var task = new ErrorTask
                    {
                        Text = match.Success ? match.Groups["text"].Value : line,
                        Document = match.Success ? match.Groups["file"].Value : string.Empty,
                        Line = match.Success ? int.Parse(match.Groups["line"].Value) : 0,
                        ErrorCategory = TaskErrorCategory.Error
                    };
                    _errorListProvider.Tasks.Add(task);
                }
            }
            return Task.CompletedTask;
        }
    }
}
