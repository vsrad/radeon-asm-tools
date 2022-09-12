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
                        var message = ParseKeywordMessage(line) ?? ParseClangMessage(line);
                        var scriptMessages = ParseScriptMessage(line);
                        if (message != null || scriptMessages != null)
                        {
                            if (message != null)
                            {
                                lastMessage = message;
                                messages.Add(message);
                            }
                            else
                            {
                                lastMessage = scriptMessages[scriptMessages.Count - 1];
                                messages.AddRange(scriptMessages);
                            }
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
            @"(?<file>.+):(?<line>\d+):(?<col>\d+):\s*(?<kind>error|warning|note|fatal error):\s(?<text>.+)", RegexOptions.Compiled);

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
            @"\*(?<kind>[EW]),(?<code>[^:(]+)(?>\s\((?<file>.+)\))?:\s(?<text>.+)", RegexOptions.Compiled);

        private static readonly Regex ScriptErrorTextRegex = new Regex(
            @"(?<text>.+)\s\((?<file>.+)\)", RegexOptions.Compiled);

        private static readonly Regex ScriptErrorLocationsRegex = new Regex(
            @"(?<file>.+):(?<line>\d+)", RegexOptions.Compiled);

        private static List<Message> ParseScriptMessage(string header)
        {
            var match = ScriptErrorRegex.Match(header);
            if (!match.Success) return null;

            var code = match.Groups["code"].Value;
            var textAndMaybeLocation = match.Groups["text"].Value;

            var kind = ParseMessageKind(match.Groups["kind"].Value);

            if (!match.Groups["file"].Success)
                match = ScriptErrorTextRegex.Match(textAndMaybeLocation);

            var messages = new List<Message>();

            if (match.Success)
            {
                foreach (var source in match.Groups["file"].Value.Split(','))
                {
                    var message = new Message { Kind = kind };
                    var locationMatch = ScriptErrorLocationsRegex.Match(source);
                    message.SourceFile = locationMatch.Groups["file"].Value.Trim();
                    message.Line = int.Parse(locationMatch.Groups["line"].Value);
                    message.Text = code + ": " + match.Groups["text"].Value;
                    messages.Add(message);
                }
            }
            else
            {
                messages.Add(new Message { Kind = kind, Text = code + ": " + textAndMaybeLocation });
            }

            return messages;
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
            switch (kind.ToUpperInvariant())
            {
                case "E": case "ERROR": case "FATAL ERROR": return MessageKind.Error;
                case "W": case "WARNING": return MessageKind.Warning;
                case "NOTE": return MessageKind.Note;
                default: throw new ArgumentException(kind, nameof(kind));
            }
        }
    }
}
