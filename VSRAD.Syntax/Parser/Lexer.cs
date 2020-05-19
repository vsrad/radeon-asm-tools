using Antlr4.Runtime;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.Parser
{
    public interface IRadAsmLexer
    {
        IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset);
    }

    [Export(typeof(IRadAsmLexer))]
    public class Lexer : IRadAsmLexer
    {
        public IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset)
        {
            var lexer = new RadAsmLexer(new UnbufferedCharStream(new TextSegmentsCharStream(textSegments)));
            while (true)
            {
                IToken current = lexer.NextToken();
                if (current.Type == RadAsmLexer.Eof)
                    break;
                yield return new TokenSpan(current.Type, new Span(current.StartIndex + offset, current.StopIndex - current.StartIndex + 1));
            }
        }

        private class TextSegmentsCharStream : TextReader
        {
            private readonly IEnumerator<string> segments;
            int index;
            bool finished;

            public TextSegmentsCharStream(IEnumerable<string> segments)
            {
                this.segments = segments.GetEnumerator();
                this.segments.MoveNext();
            }

            public override int Read()
            {
                if (finished)
                    return -1;
                if (index >= segments.Current.Length)
                {
                    if (!segments.MoveNext())
                    {
                        finished = true;
                        return -1;
                    }
                    index = 0;
                }
                return segments.Current[index++];
            }

            public override int Peek()
            {
                if (finished)
                    return -1;
                return segments.Current[index];
            }
        }
    }
}
