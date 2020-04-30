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
        public delegate void ErrorsUpdateDelegate(IReadOnlyList<string> instructions);
        public event ErrorsUpdateDelegate InstructionUpdated;

        public List<string> InstructionList { get; }

        [ImportingConstructor]
        public InstructionListManager()
        {
            InstructionList = new List<string>();
        }

        public Task LoadInstructionsFromFilesAsync(string pathsString)
        {
            if (string.IsNullOrWhiteSpace(pathsString))
                return Task.CompletedTask;

            var paths = pathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            InstructionList.Clear();
            foreach (var path in paths)
            {
                LoadInstructionsFromFile(path);
            }

            InstructionUpdated?.Invoke(InstructionList);
            return Task.CompletedTask;
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
