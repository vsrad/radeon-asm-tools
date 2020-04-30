using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Parser.Blocks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    internal class Classifier : IClassifier
    {
        public IClassificationTypeRegistryService _classificationTypeRegistry;
        private readonly ITextBuffer _textBuffer;
        private readonly IParserManager _parserManager;
        private readonly Regex _stringPattern;
        private IEnumerable<IBaseBlock> _multiLineComment;
        private IReadOnlyList<string> _instructions;

        public List<string> Keywords { get; protected set; }
        public List<string> ExtraKeywords { get; protected set; }

        public Classifier(IClassificationTypeRegistryService registry, ITextBuffer textBuffer, InstructionListManager instructionListManager)
        {
            this._classificationTypeRegistry = registry;
            this._textBuffer = textBuffer;
            this._parserManager = _textBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
            _parserManager.ParserUpdatedEvent += OnParserComplete;
            this._stringPattern = new Regex("\\\"(.*?)\\\"");
            this._multiLineComment = new List<IBaseBlock>();
            this.ExtraKeywords = Constants.preprocessorStart
                .Concat(Constants.preprocessorMiddle)
                .Concat(Constants.preprocessorEnd)
                .Concat(Constants.preprocessorKeywords)
                .ToList();

            _instructions = instructionListManager.InstructionList;
            instructionListManager.InstructionUpdated += InstructionUpdatedEvent;
        }

        #region Public Events
#pragma warning disable 67
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67
        #endregion // Public Events

        #region Public Methods
        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            ITextSnapshot snapshot = span.Snapshot;
            List<ClassificationSpan> spans = new List<ClassificationSpan>();

            if (snapshot.Length == 0)
                return spans;

            string classificationTypeName;
            int startno = span.Start.GetContainingLine().LineNumber,  //number of start line
                endno = (span.End - 1).GetContainingLine().LineNumber;  //number of end line

            bool isNeedGetFunction = true;
            FunctionBlock currentFunction = null;
            var funcArguments = new List<string>();
            int functionEndPoint = 0;

            var _parser = _parserManager.ActualParser;

            foreach (var commentSpan in _multiLineComment)
            {
                var classificationSpan = AddClassificationSpan(PredefinedClassificationTypeNames.Comments, snapshot, commentSpan.BlockSpan.Span, spans);
                if (classificationSpan != null && classificationSpan.Span.Contains(span))
                    return spans;
            }

            for (int i = startno; i <= endno; i++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);

                var textLine = line.GetText();
                if (textLine.Contains("//"))
                {
                    var index = textLine.IndexOf("//", StringComparison.Ordinal);
                    var indexStart = line.Start + index;

                    AddClassificationSpan(PredefinedClassificationTypeNames.Comments, snapshot, indexStart, line.End - indexStart, spans);
                    textLine = textLine.Substring(0, index);
                }

                var match = _stringPattern.Match(textLine);
                if (match.Success)
                {
                    var indexStart = line.Start + match.Index;

                    AddClassificationSpan(PredefinedClassificationTypeNames.Strings, snapshot, indexStart, match.Length, spans);
                    textLine = textLine.Substring(0, match.Index);
                }


                String[] words = textLine.Split(new char[] { ' ', '\t', '(', ')', '+', '-', '=', '[', ']', ',', '!' }, StringSplitOptions.RemoveEmptyEntries);

                if (!isNeedGetFunction && line.Start > functionEndPoint)
                    isNeedGetFunction = true;

                if (isNeedGetFunction)
                    currentFunction = _parser?.GetFunctionByLine(line);

                if (currentFunction != null)
                {
                    funcArguments = currentFunction.GetArgumentTokens().Select(token => token.TokenName).ToList();
                    isNeedGetFunction = false;
                    functionEndPoint = currentFunction.BlockSpan.End;
                }

                int wordStart = line.Start;

                for (int ii = 0; ii <= words.Length - 1; ii++)
                {
                    classificationTypeName = null;
                    string word = words[ii];
                    int pos = textLine.IndexOf(word, StringComparison.Ordinal);
                    wordStart += pos;

                    if (words.Length == 1 && word.EndsWith(":", StringComparison.Ordinal))
                    {
                        classificationTypeName = PredefinedClassificationTypeNames.Labels;
                        AddClassificationSpan(classificationTypeName, snapshot, wordStart, word.Length, spans);
                        break;
                    }

                    if (char.IsDigit(word[0]))
                        classificationTypeName = PredefinedClassificationTypeNames.Numbers;
                    else if (ExtraKeywords.Contains(word))
                        classificationTypeName = PredefinedClassificationTypeNames.ExtraKeywords;
                    else if (_instructions.Contains(word))
                        classificationTypeName = PredefinedClassificationTypeNames.Instructions;
                    else
                        classificationTypeName = GetClassificationTypeNameOfWord(word, funcArguments);

                    if (classificationTypeName != null)
                        AddClassificationSpan(classificationTypeName, snapshot, wordStart, word.Length, spans);

                    textLine = textLine.Substring(pos + word.Length);
                    wordStart += word.Length;
                }
                if (currentFunction != null)
                    AddClassificationSpan(PredefinedClassificationTypeNames.Functions, snapshot, currentFunction.FunctionToken.SymbolSpan.Span, spans);
            }
            return spans;
        }

        #endregion

        public virtual string GetClassificationTypeNameOfWord(string word, IList<string> functionArgs)
        {
            return null;
        }

        private void OnParserComplete(object actualParser, object _)
        {
            try
            {
                var parser = actualParser as IBaseParser;
                _multiLineComment = parser.ListBlock.Where(b => b.BlockType == BlockType.Comment);
                ClassificationChanged.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, 0)));
            }
            catch (Exception e)
            {
                Microsoft.VisualStudio.Shell.ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private void InstructionUpdatedEvent(IReadOnlyList<string> instructions)
        {
            _instructions = instructions;
            ClassificationChanged.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, 0, _textBuffer.CurrentSnapshot.Length)));
        }

        private ClassificationSpan AddClassificationSpan(string classificationTypeName, ITextSnapshot snapshot, int start, int length, IList<ClassificationSpan> spans)
        {
            var type = _classificationTypeRegistry.GetClassificationType(classificationTypeName);

            ClassificationSpan classificationSpan = null;
            try
            {
                var span = new SnapshotSpan(snapshot, start, length);
                classificationSpan = new ClassificationSpan(span, type);
                spans.Add(classificationSpan);
            }
            catch (Exception e)
            {
                Microsoft.VisualStudio.Shell.ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
            return classificationSpan;
        }

        private ClassificationSpan AddClassificationSpan(string classificationTypeName, ITextSnapshot snapshot, Span span, IList<ClassificationSpan> spans)
            => AddClassificationSpan(classificationTypeName, snapshot, span.Start, span.Length, spans);
    }

    internal class Asm2Classifier : Classifier
    {
        public Asm2Classifier(
            IClassificationTypeRegistryService registry,
            ITextBuffer textBuffer,
            InstructionListManager instructionListManager) : base(registry, textBuffer, instructionListManager)
        {
            base.Keywords = Constants.asm2Start
                .Concat(Constants.asm2Middle)
                .Concat(Constants.asm2End)
                .Concat(Constants.asm2Keywords)
                .ToList();
            base.Keywords.Add(Constants.asm2FunctionKeyword);
        }

        public override string GetClassificationTypeNameOfWord(string word, IList<string> functionArgs)
        {
            if (Keywords.Contains(word))
            {
                return PredefinedClassificationTypeNames.Keywords;
            }
            else if (functionArgs.Contains(word))
            {
                return PredefinedClassificationTypeNames.Arguments;
            }
            return null;
        }
    }

    internal class Asm1Classifier : Classifier
    {
        public Asm1Classifier(
            IClassificationTypeRegistryService registry,
            ITextBuffer textBuffer,
            InstructionListManager instructionListManager) : base(registry, textBuffer, instructionListManager)
        {
            base.Keywords = Constants.asm1Start
                .Concat(Constants.asm1Middle)
                .Concat(Constants.asm1End)
                .Concat(Constants.asm1Keywords)
                .ToList();
            base.Keywords.Add(Constants.asm1FunctionKeyword);
            base.ExtraKeywords.AddRange(Constants.asm1ExtraKeywords);
        }

        public override string GetClassificationTypeNameOfWord(string word, IList<string> functionArgs)
        {
            if (Keywords.Contains(word))
            {
                return PredefinedClassificationTypeNames.Keywords;
            }
            else if (functionArgs.Contains(word))
            {
                return PredefinedClassificationTypeNames.Arguments;
            }
            else if (word.StartsWith("\\", StringComparison.Ordinal) && functionArgs.Contains(word.Substring(1)))
            {
                return PredefinedClassificationTypeNames.Arguments;
            }
            return null;
        }
    }
}
