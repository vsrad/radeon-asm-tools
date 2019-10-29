using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VSRAD.BuildTools
{
    class SourceValidationMessageParser
    {
        private static readonly Regex SourceFileRegex = new Regex(@"[ ]*\(((\w|\\\s)*.s:\d+,?\s?)+\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public enum MessageKind
        {
            Error,
            Warning
        }

        public struct Message
        {
            public MessageKind Kind;
            public string Text;
            public string SourceFile;
            public int LineNumber;
        }

        public static Message[] ExtractMessages(string projectRoot, string defaultFile, string errorString)
        {
            var messages = new List<Message>();

            MessageKind kind = errorString[0] == 'W' ? MessageKind.Warning : MessageKind.Error;
            string text = errorString.Substring(2 /* E or W and a comma */, errorString.Length - 1 - Environment.NewLine.Length);

            var fileMatch = SourceFileRegex.Match(text);
            if (fileMatch.Success)
            {
                var fileGroup = fileMatch.Groups[0];

                text = text.Remove(fileGroup.Index, fileGroup.Length);

                var files = fileGroup.Value
                    .Trim(new[] { ' ', '(', ')' })
                    .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var file in files)
                {
                    var pathAndLineNumber = file.Split(':');
                    string path = System.IO.Path.Combine(projectRoot, pathAndLineNumber[0]);
                    int lineNumber = int.Parse(pathAndLineNumber[1]);

                    messages.Add(new Message { Text = text, Kind = kind, SourceFile = path, LineNumber = lineNumber });
                }
            }
            else
            {
                messages.Add(new Message { Text = text, Kind = kind, SourceFile = defaultFile });
            }

            return messages.ToArray();
        }
    }
}
