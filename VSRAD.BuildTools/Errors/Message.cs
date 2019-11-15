namespace VSRAD.BuildTools.Errors
{
    public enum MessageKind { Error, Warning, Note }

    public sealed class Message
    {
        public MessageKind Kind { get; set; }
        public string Text { get; set; }
        public string SourceFile { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
