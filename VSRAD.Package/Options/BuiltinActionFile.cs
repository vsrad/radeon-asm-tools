using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Options
{
    public sealed class BuiltinActionFile
    {
        public StepEnvironment Location { get; set; } = StepEnvironment.Remote;

        public string Path { get; set; } = "";

        public bool CheckTimestamp { get; set; } = true;

        public async Task<BuiltinActionFile> EvaluateAsync(IMacroEvaluator evaluator) =>
            new BuiltinActionFile
            {
                Location = Location,
                Path = await evaluator.EvaluateAsync(Path),
                CheckTimestamp = CheckTimestamp
            };
    }
}
