using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.BuildTools.Errors
{
    public static class Parser
    {
        public static ICollection<Message> ParseStderr(IEnumerable<string> outputs)
        {
            var messages = new List<Message>();
            foreach (var output in outputs)
            {
                using (var reader = new StringReader(output))
                {
                    Message lastMessage = null;

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var message = ParseScriptMessage(line) ?? ParseKeywordMessage(line) ?? ParseClangMessage(line);
                        if (message != null)
                        {
                            lastMessage = message;
                            messages.Add(message);
                        }
                        else if (lastMessage != null)
                        {
                            lastMessage.Text += Environment.NewLine + line;
                        }
                    }
                }
            }
            return messages;
        }

        private static readonly Regex ClangErrorRegex = new Regex(
            @"(?<file>.+):(?<line>\d+):(?<col>\d+):\s*(?<kind>error|warning|note):\s(?<text>.+)", RegexOptions.Compiled);

        private static Message ParseClangMessage(string header)
        {
            var match = ClangErrorRegex.Match(header);
            if (!match.Success) return null;
            return new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value,
                SourceFile = match.Groups["file"].Value,
                Line = int.Parse(match.Groups["line"].Value),
                Column = int.Parse(match.Groups["col"].Value)
            };
        }

        private static readonly Regex ScriptErrorRegex = new Regex(
            @"\*(?<kind>[EW]),(?<code>[^:(]+)(?>\s\((?<file>.+):(?<line>\d+)\))?:\s(?<text>.+)", RegexOptions.Compiled);

        private static readonly Regex ScriptErrorLocationInTextRegex = new Regex(
            @"(?<text>.+)\s\((?<file>.+):(?<line>\d+)\)", RegexOptions.Compiled);

        private static Message ParseScriptMessage(string header)
        {
            var match = ScriptErrorRegex.Match(header);
            if (!match.Success) return null;

            var code = match.Groups["code"].Value;
            var textAndMaybeLocation = match.Groups["text"].Value;

            var message = new Message { Kind = ParseMessageKind(match.Groups["kind"].Value) };

            if (!match.Groups["file"].Success)
                match = ScriptErrorLocationInTextRegex.Match(textAndMaybeLocation);

            if (match.Success)
            {
                message.SourceFile = match.Groups["file"].Value;
                message.Line = int.Parse(match.Groups["line"].Value);
            }

            message.Text = code + ": " + (match.Success ? match.Groups["text"].Value : textAndMaybeLocation);

            return message;
        }

        private static readonly Regex KeywordErrorRegex = new Regex(
            @"(?<kind>ERROR|WARNING):\s*(?<text>.+)", RegexOptions.Compiled);

        private static Message ParseKeywordMessage(string header)
        {
            var match = KeywordErrorRegex.Match(header);
            if (!match.Success) return null;

            var message = new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value
            };

            return message;
        }

        private static MessageKind ParseMessageKind(string kind)
        {
            switch (kind)
            {
                case "E": case "error": case "ERROR": return MessageKind.Error;
                case "W": case "warning": case "WARNING": return MessageKind.Warning;
                case "note": return MessageKind.Note;
                default: throw new ArgumentException(kind, nameof(kind));
            }
        }
    }
}
