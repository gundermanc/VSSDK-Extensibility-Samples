namespace MockLanguageExtension
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using StreamJsonRpc;
    using Microsoft.VisualStudio.LanguageServer.Client;
    using Microsoft.VisualStudio.Threading;
    using Microsoft.VisualStudio.Utilities;
    using Task = System.Threading.Tasks.Task;

    [ContentType(ContentDefinition.Name)]
    [Export(typeof(ILanguageClient))]
    internal sealed class FooLanguageClient : ILanguageClient
    {
        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        internal JsonRpc Rpc { get; set; }

        public string Name => "Foo Language Extension";

        public IEnumerable<string> ConfigurationSections => Array.Empty<string>();

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer => null;

        public object CustomMessageTarget => null;

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            var programPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Server", "LanguageServer.exe");
            info.FileName = programPath;
            info.WorkingDirectory = Path.GetDirectoryName(programPath);

            var stdInPipeName = "output";
            var stdOutPipeName = "input";

            var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var bufferSize = 256;
            var readerPipe = new NamedPipeServerStream(
                stdInPipeName,
                PipeDirection.InOut,
                4,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous,
                bufferSize,
                bufferSize,
                pipeSecurity);
            var writerPipe = new NamedPipeServerStream(
                stdOutPipeName,
                PipeDirection.InOut,
                4,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous,
                bufferSize,
                bufferSize,
                pipeSecurity);

            Process process = new Process();
            process.StartInfo = info;

            if (process.Start())
            {
                await readerPipe.WaitForConnectionAsync(token);
                await writerPipe.WaitForConnectionAsync(token);

                return new Connection(readerPipe, writerPipe);
            }

            return null;
        }

        public async Task OnLoadedAsync() => await StartAsync?.InvokeAsync(this, EventArgs.Empty);

        public Task OnServerInitializedAsync() => Task.CompletedTask;

        public Task OnServerInitializeFailedAsync(Exception e) => Task.CompletedTask;
    }
}
