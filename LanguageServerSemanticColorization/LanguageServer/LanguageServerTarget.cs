namespace LanguageServer
{

    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.RegularExpressions;
    using LanguageServerLibrary.Parser;
    using StreamJsonRpc;
    using Microsoft.VisualStudio.LanguageServer.Protocol;

    using static LanguageServerLibrary.Parser.ParserService;
    using System.Threading.Tasks;

    public sealed class LanguageServerTarget
    {
        private readonly LanguageServer server;
        private ImmutableDictionary<string, int> tokenTypesLegend = ImmutableDictionary<string, int>.Empty;

        internal LanguageServerTarget(LanguageServer server)
        {
            this.server = server;

            // Create token type look up 'legend'.
            int i = 0;
            var legendBuilder = ImmutableDictionary.CreateBuilder<string, int>();
            foreach (var tokenType in SemanticTokenTypes.AllTypes)
            {
                legendBuilder.Add(tokenType, i++);
            }

            this.tokenTypesLegend = legendBuilder.ToImmutable();
        }

        public event EventHandler Initialized;

        /// <summary>
        /// Called by Visual Studio to initialize language server and agree on
        /// supported capabilities.
        /// </summary>
        [JsonRpcMethod(Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
        public object Initialize(InitializeParams arg)
        {
            var capabilities = new VSServerCapabilities()
            {
                TextDocumentSync = new TextDocumentSyncOptions()
                {
                    OpenClose = true,
                    Change = TextDocumentSyncKind.Incremental
                },
                SemanticTokensOptions = new SemanticTokensOptions
                {
                    // 'true' indicates that we support semantic colorization of the document.
                    DocumentProvider = new SumType<bool, DocumentProviderOptions>(new DocumentProviderOptions()
                    {
                        Edits = true
                    }),

                    // Defines the mapping of tokenTypes and TokenModifiers used
                    // by Visual Studio to colorize the text.
                    Legend = new SemanticTokensLegend
                    {
                        TokenModifiers = SemanticTokenModifiers.AllModifiers.ToArray(),
                        TokenTypes = SemanticTokenTypes.AllTypes.ToArray()
                    },

                    // Indicates that we support sending ranges of token updates
                    // instead of updating the entire document. The Visual Studio
                    // language client will always prefer asking for ranges, if
                    // possible, but you can comment out to test the full document
                    // path instead.
                    RangeProvider = true
                }
            };

            var result = new InitializeResult
            {
                Capabilities = capabilities
            };

            Initialized?.Invoke(this, new EventArgs());

            return result;
        }

        /// <summary>
        /// Sent by Visual Studio to alert server that a file was opened.
        /// </summary>
        [JsonRpcMethod(Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentOpened(DidOpenTextDocumentParams didOpenParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(didOpenParams.TextDocument.Uri);

            var lines = Regex.Split(didOpenParams.TextDocument.Text, "\r\n|\r|\n");

            int lineNumber = 0;
            foreach (var line in lines)
            {
                document.TextBuffer.Replace((lineNumber++, 0, 0, line));
            }
        }

        /// <summary>
        /// Sent by Visual Studio to alert server that a file was closed.
        /// </summary>
        /// <param name="didCloseParams"></param>
        [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentDidClose(DidCloseTextDocumentParams didCloseParams)
            => this.server.DocumentManager.RemoveDocumentIfExists(didCloseParams.TextDocument.Uri);

        /// <summary>
        /// Sent by server to synchronize text changes between the client and server.
        /// </summary>
        [JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentChanged(DidChangeTextDocumentParams didChangeParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(didChangeParams.TextDocument.Uri);

            // TODO: this is a really simple language server that isn't smart enough
            // to do deletes across multiple lines yet.
            document.TextBuffer.Replace(
                didChangeParams.ContentChanges.Select(
                    change => (
                    change.Range.Start.Line,
                    change.Range.Start.Character,
                    change.Range.End.Character - change.Range.Start.Character, change.Text)));
        }

        /// <summary>
        /// Gets ALL semantic tokens for colorizing the entire document at the current point
        /// in time.
        /// </summary>
        /// <remarks>
        /// This method is likely used only by language server clients that don't support
        /// range requests. Visual Studio, for example, will never call this if RangeProvider
        /// capability is <c>true</c>.
        /// </remarks>
        [JsonRpcMethod(SemanticTokensMethods.TextDocumentSemanticTokensName, UseSingleObjectParameterDeserialization = true)]
        public SemanticTokens OnTextDocumentSemanticTokens(SemanticTokensParams semanticTokensParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(semanticTokensParams.TextDocument.Uri);

            var parserService = document.GetService<ParserService>();

            // TODO: there's a race condition here.
            // User makes an edit, parse starts, then some time later
            // we complete the parse, but the LSP client is going to
            // ask for new tokens immediately. We need to await the
            // in progress parse here.
            var dataBuilder = new List<int>();
            Token previousToken = new Token(0, 0, 0, false);
            foreach (var token in parserService.LineTokens)
            {
                // If PartialResultToken was given, we can send progressive
                // updates instead of processing the whole thing.
                // It's not really necessary for this trivial sample, but a
                // a more complex language service might benefit.
                if (semanticTokensParams.PartialResultToken != null)
                {
                    // When doing progressive updates, specify first relative to the start of the doc.
                    previousToken = new Token(0, 0, 0, false);

                    List<int> batchTokensBuilder = new List<int>();
                    this.EncodeToken(batchTokensBuilder, token, ref previousToken);
                    semanticTokensParams.PartialResultToken.Report(new SemanticTokens { Data = batchTokensBuilder.ToArray() });

                    // Uncomment to simulate per-result processing delay and to see line by line updates.
                    // Thread.Sleep(1000);
                }
                else
                {
                    this.EncodeToken(dataBuilder, token, ref previousToken);
                }
            }

            return new SemanticTokens()
            {
                Data = dataBuilder.ToArray()
            };
        }

        /// <summary>
        /// Gets a range of semantic tokens for colorizing part of a document at the current
        /// point in time.
        /// </summary>
        /// <remarks>
        /// This method is probably used by most clients for colorizing spans of text that
        /// are currently visible.
        /// </remarks>
        [JsonRpcMethod(SemanticTokensMethods.TextDocumentSemanticTokensRangeName, UseSingleObjectParameterDeserialization = true)]
        public SemanticTokens OnTextDocumentSemanticTokensRange(SemanticTokensRangeParams semanticTokensParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(semanticTokensParams.TextDocument.Uri);

            var parserService = document.GetService<ParserService>();

            // Ensure we parse a minimum of 1 line.
            var endLine = Math.Max(semanticTokensParams.Range.End.Line, semanticTokensParams.Range.Start.Line + 1);

            // TODO: there's a race condition here.
            // User makes an edit, parse starts, then some time later
            // we complete the parse, but the LSP client is going to
            // ask for new tokens immediately. We need to await the
            // in progress parse here.
            var dataBuilder = new List<int>();
            Token previousToken = new Token(0, 0, 0, false);
            for (int lineNumber = semanticTokensParams.Range.Start.Line; lineNumber < endLine; lineNumber++)
            {
                var lineToken = parserService.LineTokens[lineNumber];

                // If PartialResultToken was given, we can send progressive
                // updates instead of processing the whole thing.
                // It's not really necessary for this trivial sample, but a
                // a more complex language service might benefit.
                if (semanticTokensParams.PartialResultToken != null)
                {
                    // When doing progressive updates, specify first relative to the start of the doc.
                    previousToken = new Token(0, 0, 0, false);

                    List<int> batchTokensBuilder = new List<int>();
                    this.EncodeToken(batchTokensBuilder, lineToken, ref previousToken);
                    semanticTokensParams.PartialResultToken.Report(new SemanticTokens { Data = batchTokensBuilder.ToArray() });

                    // Uncomment to simulate per-result processing delay and to see line by line updates.
                    //Thread.Sleep(100);
                }
                else
                {
                    this.EncodeToken(dataBuilder, lineToken, ref previousToken);
                }
            }

            return new SemanticTokens()
            {
                Data = dataBuilder.ToArray()
            };
        }

        [JsonRpcMethod(SemanticTokensMethods.TextDocumentSemanticTokensEditsName, UseSingleObjectParameterDeserialization = true)]
        public SumType<SemanticTokens, SemanticTokensEdits> OnTextDocumentSemanticTokensEdits(SemanticTokensEditsParams semanticTokensParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(semanticTokensParams.TextDocument.Uri);

            var parserService = document.GetService<ParserService>();

            var queuedUpdates = parserService.QueuedUpdates;

            return EncodeEdits(queuedUpdates);
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
        {
            return null;
        }

        [JsonRpcMethod(Methods.ExitName)]
        public void Exit()
        {
            server.Exit();
        }

        private SemanticTokensEdits EncodeEdits(IncrementalUpdate update)
        {
            var edits = new List<SemanticTokensEdit>();

            foreach (var change in update.Changes)
            {
                edits.Add(EncodeChange(change));
            }

            return new SemanticTokensEdits
            {
                Edits = edits.ToArray(),
                ResultId = update.TargetVersionNumber.ToString()
            };
        }

        private SemanticTokensEdit EncodeChange(Change change)
        {
            var tokensBuilder = new List<int>();
            Token previousToken = new Token(0, 0, 0, false);
            foreach (var token in change.Tokens)
            {
                previousToken = new Token(0, 0, 0, false);
                EncodeToken(tokensBuilder, token, ref previousToken);
            }

            return new SemanticTokensEdit
            {
                Start = change.Start * 5,
                DeleteCount = change.DeleteCount * 5,
                Data = tokensBuilder.ToArray()
            };
        }

        private void EncodeToken(List<int> dataBuilder, Token? token, ref Token previousToken)
        {
            if (token.HasValue)
            {
                // Reset the start offset when the line changes.
                var previousTokenOnThisLineOffset = previousToken.Line == token.Value.Line ?
                    previousToken.StartOffset :
                    0;

                // Data are delta encoded, as the difference between the location of the previous
                // token and the next one.
                dataBuilder.Add(token.Value.Line - previousToken.Line);
                dataBuilder.Add(token.Value.StartOffset - previousTokenOnThisLineOffset);
                dataBuilder.Add(token.Value.Length);

                // Lookup the token type's integer representation.
                dataBuilder.Add(
                    this.tokenTypesLegend[
                        token.Value.IsMatch ?
                        SemanticTokenTypes.Keyword :
                        SemanticTokenTypes.String]);

                // TODO: token modifiers.
                dataBuilder.Add(0);

                previousToken = token.Value;
            }
        }
    }
}
