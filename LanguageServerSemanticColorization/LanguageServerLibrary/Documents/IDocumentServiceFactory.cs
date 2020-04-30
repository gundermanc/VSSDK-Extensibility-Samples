namespace LanguageServerLibrary.Documents
{
    internal interface IDocumentServiceFactory
    {
        object CreateService(Document document);
    }
}
