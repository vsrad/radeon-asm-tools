using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(InstructionListManager))]
    internal sealed class InstructionListManager
    {
        public delegate void InstructionsUpdateDelegate(IReadOnlyList<string> instructions);
        public event InstructionsUpdateDelegate InstructionUpdated;

        public List<string> InstructionList { get; }

        [ImportingConstructor]
        public InstructionListManager(OptionsProvider optionsEventProvider)
        {
            InstructionList = new List<string>();
            optionsEventProvider.OptionsUpdated += InstructionPathsUpdated;
        }

        private void InstructionPathsUpdated(OptionsProvider provider) =>
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(() => LoadInstructionsFromDirectoriesAsync(provider.InstructionsPaths));

        public Task LoadInstructionsFromDirectoriesAsync(string dirPathsString)
        {
            InstructionList.Clear();
            if (string.IsNullOrWhiteSpace(dirPathsString))
                return Task.CompletedTask;

            var paths = dirPathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var path in paths)
            {
                LoadInstructionsFromDirectory(path);
            }

            InstructionUpdated?.Invoke(InstructionList);
            return Task.CompletedTask;
        }

        private void LoadInstructionsFromDirectory(string path)
        {
            try
            {
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.InstructionsFileExtension)
                        LoadInstructionsFromFile(filepath);
                }
            }catch (Exception e)
            {
                Error.ShowError(e, "instrunction folder paths");
            }
        }

        private void LoadInstructionsFromFile(string path)
        {
            try
            {
                using (var fileStream = File.OpenRead(path))
                using (var streamReader = new StreamReader(fileStream))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        InstructionList.Add(line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                Error.ShowError(e, "instrunction file paths");
            }
        }
    }
}
