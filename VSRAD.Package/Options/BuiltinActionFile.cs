using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class BuiltinActionFile : DefaultNotifyPropertyChanged
    {
        private StepEnvironment _location = StepEnvironment.Remote;
        public StepEnvironment Location { get => _location; set => SetField(ref _location, value); }

        private string _path = "";
        public string Path { get => _path; set => SetField(ref _path, value); }

        private bool _checkTimestamp = true;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public async Task<BuiltinActionFile> EvaluateAsync(IMacroEvaluator evaluator) =>
            new BuiltinActionFile
            {
                Location = Location,
                Path = await evaluator.EvaluateAsync(Path),
                CheckTimestamp = CheckTimestamp
            };
    }
}
