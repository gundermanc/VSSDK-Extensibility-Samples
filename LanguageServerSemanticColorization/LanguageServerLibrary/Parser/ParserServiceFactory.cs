namespace LanguageServerLibrary.Parser
{
    using LanguageServerLibrary.Documents;

    internal sealed class ParserServiceFactory : IDocumentServiceFactory
    {
        public object CreateService(Document document)
        {
            return new ParserService(document);
        }
    }
}
