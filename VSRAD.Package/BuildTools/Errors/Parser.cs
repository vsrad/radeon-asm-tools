using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
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
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var currentLineParsed = 
                            ParseAsmMessages(line, messages) ||
                            ParseScriptMessages(line, messages) ||
                            ParseClangMessages(line, messages);
                        if (!currentLineParsed && !string.IsNullOrWhiteSpace(line)
                                && messages.Count > 0)
                            messages.Last().Text += Environment.NewLine + line;
                    }
                }
            }
            return messages;
        }

        private static readonly Regex ClangErrorRegex = new Regex(
            @"(?<file>.+):(?<line>\d+):(?<col>\d+):\s*(?<kind>error|warning|note|fatal error):\s(?<text>.+)", RegexOptions.Compiled);

        private static bool ParseClangMessages(string header, List<Message> messages)
        {
            var match = ClangErrorRegex.Match(header);
            if (!match.Success) return false;
            messages.Add(new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value,
                SourceFile = match.Groups["file"].Value,
                Line = int.Parse(match.Groups["line"].Value),
                Column = int.Parse(match.Groups["col"].Value)
            });
            return true;
        }

        private static readonly Regex AsmErrorRegex = new Regex(
            @"\*(?<kind>[EW]),(?<code>[^:(]+)(?>\s\((?<file>.+)\))?:\s(?<text>.+)", RegexOptions.Compiled);

        private static readonly Regex AsmErrorTextRegex = new Regex(
            @"(?<text>.+)\s\((?<file>.+)\)", RegexOptions.Compiled);

        private static readonly Regex AsmErrorLocationsRegex = new Regex(
            @"(?<fileOrText>[^,]+):(?<line>\d+)", RegexOptions.Compiled);

        private static bool ParseAsmMessages(string header, List<Message> messages)
        {
            var match = AsmErrorRegex.Match(header);
            if (!match.Success) return false;

            var code = match.Groups["code"].Value;
            var textAndMaybeLocation = match.Groups["text"].Value;

            var kind = ParseMessageKind(match.Groups["kind"].Value);

            if (!match.Groups["file"].Success)
                match = AsmErrorTextRegex.Match(textAndMaybeLocation);

            if (match.Success)
            {
                var locationMatch = AsmErrorLocationsRegex.Matches(match.Groups["file"].Value);
                if (locationMatch.Count == 0)
                {
                    messages.Add(new Message { Kind = kind, Text = code + ": " + textAndMaybeLocation });
                    return true;
                }
                foreach (Match lMatch in locationMatch)
                {
                    if (!lMatch.Success)
                    {
                        messages.Add(new Message { Kind = kind, Text = code + ": " + textAndMaybeLocation });
                    }
                    else
                    {
                        messages.Add(new Message
                        {
                            Kind = kind,
                            SourceFile = lMatch.Groups["fileOrText"].Value.Trim(),
                            Line = int.Parse(lMatch.Groups["line"].Value),
                            Text = code + ": " + match.Groups["text"].Value
                        });
                    }
                }
            }
            else
            {
                messages.Add(new Message { Kind = kind, Text = code + ": " + textAndMaybeLocation });
            }
            return true;
        }

        private static readonly Regex ScriptErrorRegex = new Regex(
            @"(?<kind>ERROR|WARNING):\s*(?<text>.+)", RegexOptions.Compiled);

        private static bool ParseScriptMessages(string header, List<Message> messages)
        {
            var match = ScriptErrorRegex.Match(header);
            if (!match.Success) return false;

            messages.Add(new Message
            {
                Kind = ParseMessageKind(match.Groups["kind"].Value),
                Text = match.Groups["text"].Value
            });
            return true;
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
