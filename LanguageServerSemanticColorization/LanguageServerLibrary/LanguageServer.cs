namespace LanguageServer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using System.ComponentModel;
    using StreamJsonRpc;
    using LanguageServerLibrary.Documents;

    internal sealed class LanguageServer : INotifyPropertyChanged, IDisposable
    {
        private readonly JsonRpc rpc;
        private readonly LanguageServerTarget target;

        public LanguageServer(
            Stream sender,
            Stream reader,
            params IDocumentServiceFactory[] serviceFactories)
        {
            this.target = new LanguageServerTarget(this);
            this.rpc = JsonRpc.Attach(sender, reader, this.target);
            this.rpc.Disconnected += OnRpcDisconnected;

            this.DocumentManager = new DocumentManager(serviceFactories);
        }

        public event EventHandler Disconnected;
        public event PropertyChangedEventHandler PropertyChanged;

        public DocumentManager DocumentManager { get; }

        public Task HasCompleted => this.rpc.Completion;

        public void Dispose() => this.rpc.Dispose();

        public void Exit() => this.Disconnected?.Invoke(this, EventArgs.Empty);

        private void OnRpcDisconnected(object sender, JsonRpcDisconnectedEventArgs e) => Exit();
    }
}
