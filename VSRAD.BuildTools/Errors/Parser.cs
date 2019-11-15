using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace VSRAD.BuildTools.Errors
{
    public static class Parser
    {
        public static IEnumerable<Message> ExtractMessages(string stderr, string preprocessed)
        {
            var messages = ParseStderr(stderr);

            if (messages.Count > 0 && !string.IsNullOrEmpty(preprocessed))
            {
                var ppLines = LineMapper.MapLines(preprocessed);

                foreach (var message in messages)
                    message.Line = ppLines[message.Line - 1];
            }

            return messages;
        }

        private enum ErrorFormat { Clang, Script, Undefined };

        private static ICollection<Message> ParseStderr(string stderr)
        {
            var messages = new LinkedList<Message>();
            var format = ErrorFormat.Undefined;
            using (var reader = new StringReader(stderr))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Message message;
                    switch (format)
                    {
                        case ErrorFormat.Clang: message = ParseClangMessage(line); break;
                        case ErrorFormat.Script: message = ParseScriptMessage(line); break;
                        default:
                            if ((message = ParseClangMessage(line)) != null)
                                format = ErrorFormat.Clang;
                            else if ((message = ParseScriptMessage(line)) != null)
                                format = ErrorFormat.Script;
                            break;
                    }
                    if (message != null)
                        messages.AddLast(message);
                    else if (messages.Last != null)
                        messages.Last.Value.Text += Environment.NewLine + line;
                }
            }
            return messages;
        }

        private static readonly Regex ClangErrorRegex = new Regex(
            @"(?<file>[^:]+):(?<line>\d+):(?<col>\d+):\s*(?<kind>error|warning|note):\s(?<text>.+)", RegexOptions.Compiled);

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
            @"\*(?<kind>[EW]),(?<code>\w+)(?>\s\((?<file>[\w<>]+):(?<line>\d+)\))?:\s(?<text>.+)", RegexOptions.Compiled);

        private static readonly Regex ScriptErrorLocationInTextRegex = new Regex(
            @"\((?<file>[\w<>]+):(?<line>\d+)\)", RegexOptions.Compiled);

        private static Message ParseScriptMessage(string header)
        {
            var match = ScriptErrorRegex.Match(header);
            if (!match.Success) return null;
            var message = new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value
            };
            if (match.Groups["file"].Success)
            {
                message.SourceFile = match.Groups["file"].Value;
                message.Line = int.Parse(match.Groups["line"].Value);
            }
            else
            {
                match = ScriptErrorLocationInTextRegex.Match(message.Text);
                if (match.Success)
                {
                    message.SourceFile = match.Groups["file"].Value;
                    message.Line = int.Parse(match.Groups["line"].Value);
                    message.Text = match.Groups["text"].Value;
                }
            }
            return message;
        }

        private static MessageKind ParseMessageKind(string kind)
        {
            switch (kind)
            {
                case "E": case "error": return MessageKind.Error;
                case "W": case "warning": return MessageKind.Warning;
                case "note": return MessageKind.Note;
                default: throw new ArgumentException();
            }
        }
    }
}
