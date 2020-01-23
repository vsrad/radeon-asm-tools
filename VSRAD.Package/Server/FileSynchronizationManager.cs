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

namespace VSRAD.Package.Server
{
    public interface IFileSynchronizationManager
    {
        Task SynchronizeRemoteAsync();
    }

    [Export(typeof(IFileSynchronizationManager))]
    [AppliesTo(Constants.ProjectCapability)]
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

        public async Task SynchronizeRemoteAsync()
        {
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.General.EvaluateAsync(evaluator);

            await _projectSourceManager.SaveDocumentsAsync(options.AutosaveSource);
            _project.SaveOptions();

            if (!options.CopySources)
                return;
            if (string.IsNullOrWhiteSpace(options.DeployDirectory))
                throw new Exception("The project cannot be deployed: remote directory is not specified. Set it in project properties.");

            var deployItems = EnumerateDeployItems(options);
            if (!deployItems.Any())
                return;

            var archive = PackDeployItems(deployItems);
            await _channel.SendAsync(new Deploy { Data = archive, Destination = options.DeployDirectory });

            foreach (var (localPath, _) in deployItems)
                _fileTracker[localPath] = File.GetLastWriteTime(localPath);
        }

        private IEnumerable<(string localPath, string archivePath)> EnumerateDeployItems(Options.GeneralProfileOptions profile)
        {
            foreach (var path in profile.AdditionalSources.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Append(_project.RootPath))
            {
                foreach (var (localPath, archivePath) in EnumerateFilePaths(path, path))
                {
                    // Skip project settings
                    if (localPath.Contains(".radproj"))
                        continue;

                    // Skip files that haven't been modified since the last deployment
                    if (_fileTracker.TryGetValue(localPath, out var lastWriteTime))
                        if (lastWriteTime == File.GetLastWriteTime(localPath))
                            continue;

                    yield return (localPath, archivePath);
                }
            }
        }

        private static IEnumerable<(string localPath, string archivePath)> EnumerateFilePaths(string path, string rootPath)
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                foreach (var file in Directory.EnumerateFiles(path))
                    yield return (file, MakeArchivePath(file, rootPath));

                foreach (var directory in Directory.EnumerateDirectories(path))
                    foreach (var file in EnumerateFilePaths(directory, rootPath))
                        yield return file;
            }
            else
            {
                yield return (path, MakeArchivePath(path, rootPath));
            }
        }

        private static string MakeArchivePath(string file, string rootPath)
        {
            if (file == rootPath)
                return Path.GetFileName(file);
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                rootPath += Path.DirectorySeparatorChar;
            return file.Replace(rootPath, "");
        }

        private static byte[] PackDeployItems(IEnumerable<(string localPath, string archivePath)> items)
        {
            using (var memStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, false))
                    foreach (var (localPath, archivePath) in items)
                        archive.CreateEntryFromFile(localPath, archivePath.Replace('\\', '/'), CompressionLevel.Optimal);

                return memStream.ToArray();
            }
        }
    }
}