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

        public async Task<Result<BuiltinActionFile>> EvaluateAsync(IMacroEvaluator evaluator)
        {
            var pathResult = await evaluator.EvaluateAsync(Path);
            if (!pathResult.TryGetResult(out var evaluatedPath, out var error))
                return error;

            return new BuiltinActionFile { Location = Location, Path = evaluatedPath, CheckTimestamp = CheckTimestamp };
        }

        public override bool Equals(object obj) =>
            obj is BuiltinActionFile file &&
            Location == file.Location &&
            Path == file.Path &&
            CheckTimestamp == file.CheckTimestamp;

        public override int GetHashCode() =>
            (Location, Path, CheckTimestamp).GetHashCode();
    }
}
