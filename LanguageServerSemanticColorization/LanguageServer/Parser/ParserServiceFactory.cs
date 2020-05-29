namespace LanguageServerLibrary.Parser
{
    using System;
    using LanguageServerLibrary.Documents;

    internal sealed class ParserServiceFactory : IDocumentServiceFactory
    {
        public Type ServiceType => typeof(ParserService);

        public object CreateService(Document document)
        {
            return new ParserService(document);
        }
    }
}
