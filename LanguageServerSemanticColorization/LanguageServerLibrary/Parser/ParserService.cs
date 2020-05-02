namespace LanguageServerLibrary.Parser
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using LanguageServerLibrary.Documents;

    using Microsoft.VisualStudio.Threading;

    using static LanguageServerLibrary.Documents.TextBuffer;

    internal sealed class ParserService
    {
        private readonly Document document;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private ImmutableList<Token?> lineTokens = ImmutableList<Token?>.Empty;

        public ParserService(Document document)
        {
            this.document = document;
            this.document.TextBuffer.Changed += OnTextBufferChanged;
        }

        public IReadOnlyList<Token?> LineTokens => this.lineTokens;

        private void OnTextBufferChanged(object sender, TextBufferChangedEventArgs e) => this.QueueReparse(e.Snapshot, e.InvalidatedLines);

        private void QueueReparse(Snapshot snapshot, IReadOnlyList<int> invalidatedLines)
        {
            // Cancel any previous parses.
            var newCancellationTokenSource = new CancellationTokenSource();
            var oldCancellationTokenSource = Interlocked.Exchange(
                ref this.cancellationTokenSource,
                new CancellationTokenSource());
            if (oldCancellationTokenSource != null)
            {
                oldCancellationTokenSource.Cancel();
                oldCancellationTokenSource.Dispose();
            }

            // Start new parse.
            Task.Run(() => this.ParseLines(snapshot, invalidatedLines, newCancellationTokenSource.Token)).Forget();
        }

        private void ParseLines(
            Snapshot snapshot,
            IReadOnlyList<int> invalidatedLines,
            CancellationToken cancellationToken)
        {
            var lineTokens = this.lineTokens;

            foreach (var lineNumber in invalidatedLines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = snapshot.Lines[lineNumber];
                var newToken = this.ParseLine(lineNumber, line);

                lineTokens = lineNumber < lineTokens.Count ?
                    lineTokens.SetItem(lineNumber, newToken) :
                    lineTokens.Add(newToken);
            }

            this.lineTokens = lineTokens;
        }

        private Token? ParseLine(
            int lineNumber,
            ImmutableList<char> line)
        {
            // Tokenizes each line into an array of numbers and operators.
            var segments = Regex.Matches(new string(line.ToArray()), "([0-9]+(\\.([0-9]+))*)|\\+|-|/|\\*|EQUALS|NOT_EQUALS")
                .Cast<Match>()
                .Select(m => (text: m.Value, start: m.Index))
                .ToArray();

            try
            {
                return this.ParseLineTokens(lineNumber, segments);
            }
            catch
            {
                // Evaluation of our simple language failed, return nothing.
                return null;
            }
        }

        private Token? ParseLineTokens(
            int lineNumber,
            IReadOnlyList<(string text, int start)> tokens)
        {
            int i = 0;

            var leftExpressionValue = ParseExpressionTokens(tokens, ref i);
            var (comparisonText, comparisonStart) = tokens[i++];
            var rightExpressionValue = ParseExpressionTokens(tokens, ref i);

            return comparisonText switch
            {
                "EQUALS" => new Token(
                    lineNumber,
                    comparisonStart,
                    comparisonText.Length,
                    leftExpressionValue == rightExpressionValue),
                "NOT_EQUALS" => new Token(
                    lineNumber,
                    comparisonStart,
                    comparisonText.Length,
                    leftExpressionValue != rightExpressionValue),
                _ => throw new InvalidDataException("Unknown operator")
            };
        }

        private double ParseExpressionTokens(IReadOnlyList<(string text, int start)> tokens, ref int i)
        {
            var leftSide = tokens[i++].text;
            var leftNumber = double.Parse(leftSide);

            var @operator = tokens[i++].text;

            var rightSide = tokens[i++].text;
            var rightNumber = double.Parse(rightSide);

            return @operator switch
            {
                "+" => leftNumber + rightNumber,
                "-" => leftNumber + rightNumber,
                "*" => leftNumber + rightNumber,
                "/" => leftNumber + rightNumber,
                _ => throw new InvalidDataException("Invalid syntax"),
            };
        }

        internal struct Token
        {
            public Token(int line, int startOffset, int length, bool isMatch)
            {
                this.Line = line;
                this.StartOffset = startOffset;
                this.Length = length;
                this.IsMatch = isMatch;
            }

            public int Line { get; }

            public int StartOffset { get; }

            public int Length { get; }

            public bool IsMatch { get; }
        }
    }
}
