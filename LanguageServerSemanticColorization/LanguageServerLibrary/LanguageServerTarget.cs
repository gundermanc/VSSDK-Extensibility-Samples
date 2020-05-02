namespace LanguageServer
{

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using LanguageServerLibrary.Parser;

    using Microsoft.VisualStudio.LanguageServer.Protocol;
    using StreamJsonRpc;

    using static LanguageServerLibrary.Parser.ParserService;

    public sealed class LanguageServerTarget
    {
        private readonly LanguageServer server;

        internal LanguageServerTarget(LanguageServer server)
        {
            this.server = server;
        }

        public event EventHandler Initialized;

        [JsonRpcMethod(Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
        public object Initialize(InitializeParams arg)
        {
            var capabilities = new ServerCapabilities()
            {
                TextDocumentSync = new TextDocumentSyncOptions()
                {
                    OpenClose = true,
                    Change = TextDocumentSyncKind.Incremental
                },
                SemanticTokensOptions = new SemanticTokensOptions()
                {
                    DocumentProvider = new SumType<bool, DocumentProviderOptions>(true)
                }
            };

            var result = new InitializeResult
            {
                Capabilities = capabilities
            };

            Initialized?.Invoke(this, new EventArgs());

            return result;
        }

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

        [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentDidClose(DidCloseTextDocumentParams didCloseParams)
            => this.server.DocumentManager.RemoveDocumentIfExists(didCloseParams.TextDocument.Uri);

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

        [JsonRpcMethod(Methods.TextDocumentSemanticTokensName, UseSingleObjectParameterDeserialization = true)]
        public SemanticTokens OnTextDocumentSemanticTokens(SemanticTokensParams semanticTokensParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(semanticTokensParams.TextDocument.Uri);

            if (document.TextBuffer.Length < 5)
            {
                return null;
            }

            var parserService = document.GetService<ParserService>();

            // TODO: there's a race condition here.
            // User makes an edit, parse starts, then some time later
            // we complete the parse, but the LSP client is going to
            // ask for new tokens immediately. We need to await the
            // in progress parse here.
            var dataBuilder = new List<double>();
            Token previousToken = new Token(0, 0, 0, false);
            foreach (var token in parserService.LineTokens)
            {
                if (token != null)
                {
                    // Data are delta encoded, as the difference between the location of the previous
                    // token and the next one.
                    dataBuilder.Add(token.Value.Line - previousToken.Line);
                    dataBuilder.Add(token.Value.StartOffset - previousToken.StartOffset);
                    dataBuilder.Add(token.Value.Length - previousToken.Length);

                    // TODO: 
                    dataBuilder.Add(0);
                    dataBuilder.Add(0);

                    previousToken = token.Value;
                }
            }

            // Highlight first 5 characters.
            return new SemanticTokens()
            {
                Data = dataBuilder.ToArray()
            };
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
    }
}
