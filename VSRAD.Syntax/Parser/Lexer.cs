using System.Collections.Generic;
using System.IO;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.Parser
{
    public interface ILexer
    {
        IEnumerable<TokenSpan> Run(IEnumerable<string> textSegments, int offset);
        RadAsmTokenType LexerTokenToRadAsmToken(int type);
    }

    public class TextSegmentsCharStream : TextReader
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
