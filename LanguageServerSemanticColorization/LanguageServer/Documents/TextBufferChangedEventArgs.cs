namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Generic;

    using static LanguageServerLibrary.Documents.TextBuffer;

    internal sealed class TextBufferChangedEventArgs : EventArgs
    {
        public TextBufferChangedEventArgs(Snapshot snapshot, IReadOnlyList<int> invalidatedOrInsertedLines)
        {
            this.Snapshot = snapshot;
            this.InvalidatedOrInsertedLines = invalidatedOrInsertedLines;
        }

        public Snapshot Snapshot { get; }

        public IReadOnlyList<int> InvalidatedOrInsertedLines { get; }
    }
}
