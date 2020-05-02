namespace LanguageServerLibrary.Documents
{
    using System;

    internal interface IDocumentServiceFactory
    {
        Type ServiceType { get; }

        object CreateService(Document document);
    }
}
