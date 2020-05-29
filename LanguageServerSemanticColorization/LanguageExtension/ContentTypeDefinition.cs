namespace MockLanguageExtension
{
    using System.ComponentModel.Composition;

    using Microsoft.VisualStudio.LanguageServer.Client;
    using Microsoft.VisualStudio.Utilities;

    internal static class ContentDefinition
    {
        internal const string Name = "equation";

        [Export]
        [Name(Name)]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteTextMateColorizationTypeName)]
        internal static ContentTypeDefinition FooContentTypeDefinition = null;


        [Export]
        [FileExtension(".equation")]
        [ContentType(Name)]
        internal static FileExtensionToContentTypeDefinition FooFileExtensionDefinition = null;
    }
}
