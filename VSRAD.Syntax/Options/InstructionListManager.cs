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
        private readonly List<string> _instructionList;

        public delegate void ErrorsUpdateDelegate(IReadOnlyList<string> instructions);
        public event ErrorsUpdateDelegate InstructionUpdated;

        public List<string> InstructionList 
        {
            get { return _instructionList; }
        }

        [ImportingConstructor]
        public InstructionListManager()
        {
            _instructionList = new List<string>();
        }

        public Task LoadInstructionsFromFilesAsync(string pathsString)
        {
            if (string.IsNullOrWhiteSpace(pathsString))
                return Task.CompletedTask;

            var paths = pathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            _instructionList.Clear();
            foreach (var path in paths)
            {
                LoadInstructionsFromFile(path);
            }

            InstructionUpdated?.Invoke(_instructionList);
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
                        _instructionList.Add(line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                Error.ShowError(e);
            }
        }
    }
}
