namespace LanguageServer
{

    using System;
    using System.Linq;
    using Microsoft.VisualStudio.LanguageServer.Protocol;
    using StreamJsonRpc;

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
            document.TextBuffer.Replace((0, 0, didOpenParams.TextDocument.Text));
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentDidClose(DidCloseTextDocumentParams didCloseParams)
            => this.server.DocumentManager.RemoveDocumentIfExists(didCloseParams.TextDocument.Uri);

        [JsonRpcMethod(Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
        public void OnTextDocumentChanged(DidChangeTextDocumentParams didChangeParams)
        {
            var document = this.server.DocumentManager.GetOrAddDocument(didChangeParams.TextDocument.Uri);

            document.TextBuffer.Replace(
                didChangeParams.ContentChanges.Select(
                    change => (change.Range.Start.Character, change.RangeLength, change.Text)));
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
