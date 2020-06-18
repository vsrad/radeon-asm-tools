using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Options
{
    public sealed class BuiltinActionFile
    {
        public StepEnvironment Type { get; set; }

        public string Path { get; set; }

        public bool CheckTimestamp { get; set; }

        public async Task<BuiltinActionFile> EvaluateAsync(IMacroEvaluator evaluator) =>
            new BuiltinActionFile
            {
                Type = Type,
                Path = await evaluator.EvaluateAsync(Path),
                CheckTimestamp = CheckTimestamp
            };
    }
}
