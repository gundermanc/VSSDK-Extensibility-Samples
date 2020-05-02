namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Generic;

    using static LanguageServerLibrary.Documents.TextBuffer;

    internal sealed class TextBufferChangedEventArgs : EventArgs
    {
        public TextBufferChangedEventArgs(Snapshot snapshot, IReadOnlyList<int> invalidatedLines)
        {
            this.Snapshot = snapshot;
            this.InvalidatedLines = invalidatedLines;
        }

        public Snapshot Snapshot { get; }

        public IReadOnlyList<int> InvalidatedLines { get; }

    }
}
