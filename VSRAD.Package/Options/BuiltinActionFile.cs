namespace VSRAD.Package.Options
{
    public sealed class BuiltinActionFile
    {
        public ActionEnvironment Type { get; set; }

        public string Path { get; set; }

        public bool CheckTimestamp { get; set; }
    }
}
