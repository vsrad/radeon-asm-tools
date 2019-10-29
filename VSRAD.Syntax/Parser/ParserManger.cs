using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace VSRAD.Syntax.Parser
{
    public interface IParserManager
    {
        IBaseParser ActualParser { get; }
        event EventHandler UpdateParserHandler;
        string[] KeyWordStartPatterns { get; }
        string[] KeyWordEndPatterns { get; }
        string[] KeyWordMiddlePatterns { get; }
        string KeyWordFunctionPattern { get; }
        Regex FunctionNameRegular { get; }
        string ManyLineCommentStartPattern { get; }
        string ManyLineCommentEndPattern { get; }
        string OneLineCommentPattern { get; }
        string DeclorationStartPattern { get; }
        string DeclorationEndPattern { get; }
        bool EnableManyLineDecloration { get; }
        Dictionary<string, Regex> VariableDefinitionRegulars { get; }

        void UpdateParser(IBaseParser parser);
        void ParseSync();
    }

    internal class ParserManger : IParserManager
    {
        private ITextBuffer _textBuffer;
        private bool _initialized;
        private object _updateLock;
        private ITextSnapshot _actualSnapshot;
        private CancellationTokenSource _lastCancellationTokenSource;

        public ParserManger()
        {
            this._updateLock = new object();
            this._lastCancellationTokenSource = new CancellationTokenSource();
        }

        public string[] KeyWordStartPatterns { get; private set; }
        public string[] KeyWordEndPatterns { get; private set; }
        public string[] KeyWordMiddlePatterns { get; private set; }
        public string KeyWordFunctionPattern { get; private set; }
        public Regex FunctionNameRegular { get; private set; }
        public string ManyLineCommentStartPattern { get; private set; }
        public string ManyLineCommentEndPattern { get; private set; }
        public string OneLineCommentPattern { get; private set; }
        public string DeclorationStartPattern { get; private set; }
        public string DeclorationEndPattern { get; private set; }
        public bool EnableManyLineDecloration { get; private set; }
        public Dictionary<string, Regex> VariableDefinitionRegulars { get; private set; }
        public IBaseParser ActualParser { get; private set; }
        public event EventHandler UpdateParserHandler;


        public void Initialize(
            ITextBuffer textBuffer,
            string[] keyWordStartPatterns,
            string[] keyWordEndPatterns,
            string[] keyWordMiddlePatterns,
            string keyWordFunctionPattern,
            Regex functionNameRegular,
            string manyLineCommentStartPattern,
            string manyLineCommentEndPattern,
            string oneLineCommentPattern,
            string declorationStartPattern,
            string declorationEndPattern,
            bool enableManyLineDecloration,
            Dictionary<string, Regex> variableDefinitRegex)
        {
            this._textBuffer = textBuffer;
            this.KeyWordStartPatterns = keyWordStartPatterns;
            this.KeyWordEndPatterns = keyWordEndPatterns;
            this.KeyWordMiddlePatterns = keyWordMiddlePatterns;
            this.KeyWordFunctionPattern = keyWordFunctionPattern;
            this.FunctionNameRegular = functionNameRegular;
            this.ManyLineCommentStartPattern = manyLineCommentStartPattern;
            this.ManyLineCommentEndPattern = manyLineCommentEndPattern;
            this.OneLineCommentPattern = oneLineCommentPattern;
            this.DeclorationEndPattern = declorationEndPattern;
            this.DeclorationStartPattern = declorationStartPattern;
            this.EnableManyLineDecloration = enableManyLineDecloration;
            this.VariableDefinitionRegulars = variableDefinitRegex;
            _initialized = true;
            _textBuffer.Changed += OnBufferChanged;

            this.OnBufferChanged(null, null);
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (!_initialized || _textBuffer.CurrentSnapshot == _actualSnapshot)
                return;

            _lastCancellationTokenSource.Cancel();
            _actualSnapshot = _textBuffer.CurrentSnapshot;

            _lastCancellationTokenSource = new CancellationTokenSource();
            ThreadPool.QueueUserWorkItem(new BaseParser(this, _actualSnapshot).Parse, _lastCancellationTokenSource.Token);
        }

        public void UpdateParser(IBaseParser parser)
        {
            lock (_updateLock)
            {
                if (parser.CurrentSnapshot != _actualSnapshot)
                    return;

                ActualParser = parser;
                UpdateParserHandler?.Invoke(ActualParser, null);
            }
        }

        public void ParseSync()
        {
            var actualSnapshot = _textBuffer.CurrentSnapshot;
            var cancellationTokenSource = new CancellationTokenSource();
            var parser = new BaseParser(this, actualSnapshot);

            parser.Parse(cancellationTokenSource.Token);
            UpdateParser(parser);
        }
    }
}
