namespace LanguageServerLibrary.Parser
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using LanguageServerLibrary.Documents;

    using static LanguageServerLibrary.Documents.TextBuffer;

    internal sealed class ParserService
    {
        private readonly Document document;

        private ImmutableList<Token?> lineTokens = ImmutableList<Token?>.Empty;
        private IncrementalUpdate update = IncrementalUpdate.Empty;
        private int version;

        public ParserService(Document document)
        {
            this.document = document;
            this.document.TextBuffer.Changed += this.OnTextBufferChanged;
        }

        public IReadOnlyList<Token?> LineTokens => this.lineTokens;

        public IncrementalUpdate QueuedUpdates
        {
            get
            {
                var update = this.update;
                this.update = IncrementalUpdate.Empty;

                return update;
            }

            set => this.update = value;
        }

        private void OnTextBufferChanged(object sender, TextBufferChangedEventArgs e)
            => this.QueueReparse(e.Snapshot, e.InvalidatedOrInsertedLines);

        private void QueueReparse(Snapshot snapshot, IReadOnlyList<int> invalidatedLines)
        {
            // For simplicity, we'll do the parse in direct response
            // to the edit but in a real language service, you'd want
            // to do the parse on a separate thread so as to not delay
            // the ACK message to the LSP client and block subseqeuent
            // edits.
            this.ParseLines(snapshot, invalidatedLines);
        }

        private void ParseLines(
            Snapshot snapshot,
            IReadOnlyList<int> invalidatedLines)
        {
            var lineTokens = this.lineTokens;

            var changeCollection = ImmutableArray.CreateBuilder<Change>();

            foreach (var lineNumber in invalidatedLines)
            {
                var line = snapshot.Lines[lineNumber];
                var newToken = this.ParseLine(lineNumber, line);

                // TODO: batching.
                // TODO: multiple lines?
                if (lineNumber < lineTokens.Count)
                {
                    lineTokens = lineTokens.SetItem(lineNumber, newToken);
                    changeCollection.Add(new Change(lineNumber, 1, ImmutableArray.Create(newToken)));
                }
                else
                {
                    lineTokens = lineTokens.Add(newToken);
                    changeCollection.Add(new Change(lineNumber, 0, ImmutableArray.Create(newToken)));
                }
            }

            // TODO: do we need to preserve old, not yet fetched edits?
            this.lineTokens = lineTokens;
            this.QueuedUpdates = new IncrementalUpdate(this.version, changeCollection.ToImmutable());
            this.version++;
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
                "-" => leftNumber - rightNumber,
                "*" => leftNumber * rightNumber,
                "/" => leftNumber / rightNumber,
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

        internal class IncrementalUpdate
        {
            public static readonly IncrementalUpdate Empty = new IncrementalUpdate(0, ImmutableArray<Change>.Empty);


            public IncrementalUpdate(int targetVersionNumber, ImmutableArray<Change> changes)
            {
                this.TargetVersionNumber = targetVersionNumber;
                this.Changes = changes;
            }

            public int TargetVersionNumber { get; }

            public ImmutableArray<Change> Changes { get; }
        }

        internal class Change
        {
            public Change(int start, int deleteCount, ImmutableArray<Token?> tokens)
            {
                this.Start = start;
                this.DeleteCount = deleteCount;
                this.Tokens = tokens;
            }

            public int Start { get; }

            public int DeleteCount { get; }

            public ImmutableArray<Token?> Tokens { get; }
        }
    }
}
