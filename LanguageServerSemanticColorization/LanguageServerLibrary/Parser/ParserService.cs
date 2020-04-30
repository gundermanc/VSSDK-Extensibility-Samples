namespace LanguageServerLibrary.Parser
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using LanguageServerLibrary.Documents;

    internal sealed class ParserService
    {
        private readonly Document document;

        public ParserService(Document document)
        {
            this.document = document;
            //this.document.TextBuffer.ChangedAsync += this.OnTextChangedAsync;
        }

        //private Task OnTextChangedAsync(object sender, EventArgs args)
        //{

        //}
    }
}
