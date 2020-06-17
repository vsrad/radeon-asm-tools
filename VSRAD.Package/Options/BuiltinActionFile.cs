namespace VSRAD.Package.Options
{
    public sealed class BuiltinActionFile
    {
        public StepEnvironment Type { get; set; }

        public string Path { get; set; }

        public bool CheckTimestamp { get; set; }
    }
}
