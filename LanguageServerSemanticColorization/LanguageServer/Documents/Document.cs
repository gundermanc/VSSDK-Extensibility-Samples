namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Immutable;

    internal sealed class Document
    {
        private ImmutableDictionary<object, object> services = ImmutableDictionary<object, object>.Empty;

        public Document(Uri uri)
        {
            this.Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        public Uri Uri { get; }

        public TextBuffer TextBuffer { get; } = new TextBuffer();

        public void AddService(IDocumentServiceFactory serviceFactory)
        {
            if (!ImmutableInterlocked.TryAdd(
                ref this.services,
                serviceFactory.ServiceType,
                serviceFactory.CreateService(this)))
            {
                throw new InvalidOperationException("Duplicate service");
            }
        }

        public T GetService<T>() 
            where T : class 
            => this.services[typeof(T)] as T;
    }
}
