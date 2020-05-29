namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class DocumentManager
    {
        private readonly IReadOnlyList<IDocumentServiceFactory> serviceFactories;
        private ImmutableDictionary<Uri, Document> documents = ImmutableDictionary<Uri, Document>.Empty;

        public DocumentManager(IReadOnlyList<IDocumentServiceFactory> serviceFactories)
        {
            this.serviceFactories = serviceFactories;
        }

        public Document GetOrAddDocument(Uri documentUri)
            => ImmutableInterlocked.GetOrAdd(ref documents, documentUri, uri => OpenDocument(uri));

        public void RemoveDocumentIfExists(Uri uri)
            => ImmutableInterlocked.TryRemove(ref this.documents, uri, out _);

        private Document OpenDocument(Uri documentUri)
        {
            var document = new Document(documentUri);
            
            foreach (var factory in this.serviceFactories)
            {
                document.AddService(factory);
            }

            return document;
        }
    }
}
