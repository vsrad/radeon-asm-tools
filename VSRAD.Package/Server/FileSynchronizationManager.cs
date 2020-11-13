using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Server
{
    public interface IFileSynchronizationManager
    {
        Task SynchronizeRemoteAsync(IMacroEvaluator evaluator);
    }

    [Export(typeof(IFileSynchronizationManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class FileSynchronizationManager : IFileSynchronizationManager
    {
        private readonly ICommunicationChannel _channel;
        private readonly IProject _project;
        private readonly IProjectSourceManager _projectSourceManager;

        private readonly Dictionary<string, DateTime> _fileTracker = new Dictionary<string, DateTime>();

        [ImportingConstructor]
        public FileSynchronizationManager(
            ICommunicationChannel channel,
            IProject project,
            IProjectSourceManager projectSourceManager)
        {
            _channel = channel;
            _project = project;
            _projectSourceManager = projectSourceManager;

            // Redeploy all files on connection state change
            _channel.ConnectionStateChanged += _fileTracker.Clear;
        }

        public async Task SynchronizeRemoteAsync(IMacroEvaluator evaluator)
        {
            var optionsResult = await _project.Options.Profile.General.EvaluateAsync(evaluator);
            if (!optionsResult.TryGetResult(out var options, out var error))
                throw new Exception(error.Message);

            var mode = _project.Options.DebuggerOptions.Autosave ? DocumentSaveType.OpenDocuments : DocumentSaveType.None;

            await _projectSourceManager.SaveDocumentsAsync(mode);
            _project.SaveOptions();

            if (!options.CopySources)
                return;
            if (string.IsNullOrWhiteSpace(options.DeployDirectory))
                throw new Exception("The project cannot be deployed: remote directory is not specified. Set it in project properties.");

            var deployItems = await ListDeployItemsAsync(additionalSources: options.AdditionalSources);
            if (!deployItems.Any())
                return;

            var archive = PackDeployItems(deployItems);
            await _channel.SendAsync(new Deploy { Data = archive, Destination = options.DeployDirectory });

            foreach (var (path, _, lastWrite) in deployItems)
                _fileTracker[path] = lastWrite;
        }

        private async Task<IEnumerable<(string path, string name, DateTime lastWrite)>> ListDeployItemsAsync(string additionalSources)
        {
            var projectItems = await _projectSourceManager.ListProjectFilesAsync();

            var items = new List<(string path, string name, DateTime lastWrite)>();
            foreach (var (absolutePath, relativePath) in projectItems)
                items.Add((absolutePath, relativePath, File.GetLastWriteTime(absolutePath)));

            foreach (var path in additionalSources.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var directory = new DirectoryInfo(path);
                    foreach (var file in directory.EnumerateFiles("*.*", SearchOption.AllDirectories))
                        items.Add((file.FullName, file.FullName.Substring(directory.FullName.Length + 1), file.LastWriteTime));
                }
                else
                {
                    items.Add((path, Path.GetFileName(path), File.GetLastWriteTime(path)));
                }
            }
            items.RemoveAll(i => _fileTracker.TryGetValue(i.path, out var lastWrite) && lastWrite == i.lastWrite);
            return items;
        }

        private static byte[] PackDeployItems(IEnumerable<(string path, string name, DateTime lastWrite)> items)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                    foreach (var (path, name, _) in items)
                        archive.CreateEntryFromFile(path, name.Replace('\\', '/'), CompressionLevel.Optimal);

                return memStream.ToArray();
            }
        }
    }
}