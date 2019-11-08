using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace VSRAD.BuildTools
{
    public static class RemoteBuildStderrParser
    {
        private static readonly Regex ClangErrorRegex = new Regex(
            @"(?<file>[^:]+):(?<line>\d+):(?<col>\d+):\s*(?<type>error|warning):\s(?<text>.+)", RegexOptions.Compiled);

        public enum MessageKind { Error, Warning }

        public sealed class Message
        {
            public MessageKind Kind { get; set; }
            public string Text { get; set; }
            public string SourceFile { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
        }

        public static IEnumerable<Message> ExtractMessages(string stderr)
        {
            var messages = new LinkedList<Message>();
            using (var reader = new StringReader(stderr))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = ClangErrorRegex.Match(line);
                    if (match.Success)
                        messages.AddLast(new Message
                        {
                            Kind = match.Groups["type"].Value == "error" ? MessageKind.Error : MessageKind.Warning,
                            Text = match.Groups["text"].Value,
                            SourceFile = match.Groups["file"].Value,
                            Line = int.Parse(match.Groups["line"].Value),
                            Column = int.Parse(match.Groups["col"].Value)
                        });
                    else if (messages.Last != null)
                        messages.Last.Value.Text += Environment.NewLine + line;
                }
            }
            return messages;
        }
    }
}
