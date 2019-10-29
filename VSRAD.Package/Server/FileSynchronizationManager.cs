using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Server
{
    public interface IFileSynchronizationManager
    {
        Task SynchronizeRemoteAsync();
        Task ClearSynchronizedFilesAsync();
    }

    [Export(typeof(IFileSynchronizationManager))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class FileSynchronizationManager : IFileSynchronizationManager
    {
        private readonly ICommunicationChannel _channel;
        private readonly IDeployFilePacker _filePacker;
        private readonly IProject _project;
        private readonly IProjectSourceManager _projectSourceManager;
        private readonly Dictionary<string, DeployItemsPack> _unsyncedFiles = new Dictionary<string, DeployItemsPack>();

        [ImportingConstructor]
        public FileSynchronizationManager(ICommunicationChannel channel,
            IDeployFilePacker filePacker,
            IProjectEvents projectEvents,
            IProject project,
            IProjectSourceManager projectSourceManager)
        {
            _channel = channel;
            _filePacker = filePacker;
            _project = project;
            _projectSourceManager = projectSourceManager;

            projectEvents.SourceFileChanged += SourceFileChanged;
        }

        private void SourceFileChanged(string file)
        {
            foreach (var items in _unsyncedFiles.Values)
            {
                var fileItem = new DeployItem();
                fileItem.ActualPath = file;
                fileItem.MakeArchivePath(_project.RootPath);
                items.UnsyncedProjectItems.Add(fileItem);
            }
        }

        public async Task ClearSynchronizedFilesAsync()
        {
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.General.EvaluateAsync(evaluator);

            if (string.IsNullOrWhiteSpace(options.DeployDirectory))
                return;

            if (_unsyncedFiles.TryGetValue(options.DeployDirectory, out _))
            {
                _unsyncedFiles.Remove(options.DeployDirectory);
            }
        }

        public async Task SynchronizeRemoteAsync()
        {
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.General.EvaluateAsync(evaluator);

            if (string.IsNullOrWhiteSpace(options.DeployDirectory))
                throw new Exception("The project cannot be deployed: remote directory is not specified. Set it in project properties.");

            await _projectSourceManager.SaveDocumentsAsync(options.AutosaveSource);

            if (!options.CopySources) return;

            var deployTasks = new List<Task>();
            byte[] projectSourceArchive;
            DeployItemsPack deployItemsPack;

            if (_unsyncedFiles.TryGetValue(options.DeployDirectory, out var unsynced))
            {
                if (unsynced.UnsyncedProjectItems.Count != 0)
                {
                    projectSourceArchive = _filePacker.PackItems(unsynced.UnsyncedProjectItems);
                    deployTasks.Add(_channel.SendAsync(new Deploy() { Data = projectSourceArchive, Destination = options.DeployDirectory }));
                }
                deployItemsPack = unsynced;
            }
            else
            {
                projectSourceArchive = _filePacker.PackDirectory(_project.RootPath);
                deployTasks.Add(_channel.SendAsync(new Deploy() { Data = projectSourceArchive, Destination = options.DeployDirectory }));

                deployItemsPack = new DeployItemsPack()
                {
                    DeployItemTracker = new DeployItemTracker(),
                    UnsyncedProjectItems = new HashSet<DeployItem>(),
                };
                _unsyncedFiles[options.DeployDirectory] = deployItemsPack;
            }

            var additionalPaths = options.AdditionalSources.GetPathsSemicolonSeparated();
            var additionalDeployItems = deployItemsPack.DeployItemTracker.GetDeployItems(additionalPaths, _project.RootPath);

            if (additionalDeployItems.Count() != 0)
            {
                byte[] additionalArchive = _filePacker.PackItems(additionalDeployItems);
                deployTasks.Add(_channel.SendAsync(new Deploy() { Data = additionalArchive, Destination = options.DeployDirectory }));
            }

            await Task.WhenAll(deployTasks);

            deployItemsPack.UnsyncedProjectItems.Clear();
        }

        private struct DeployItemsPack
        {
            public IDeployItemTracker DeployItemTracker { get; set; }
            public HashSet<DeployItem> UnsyncedProjectItems;
        }
    }
}