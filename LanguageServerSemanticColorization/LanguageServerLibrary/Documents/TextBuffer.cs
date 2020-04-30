namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// Holds text from an open text document.
    /// </summary>
    /// <remarks>
    /// This is not the "smartest" TextBuffer, something akin to the
    /// Visual Studio TextBuffer is more flexible.
    /// https://github.com/microsoft/vs-editor-api/blob/master/src/Editor/Text/Impl/TextModel/TextBuffer.cs.
    /// </remarks>
    internal sealed class TextBuffer
    {
        // For this sample, we're using ImmutableList, which is de facto a balanced binary tree,
        // making mutation actions for arbitrarily large files generally a logarithmic time action.
        private ImmutableList<char> text = ImmutableList<char>.Empty;

        public event AsyncEventHandler ChangedAsync;

        public int Length => this.text.Count;

        public void Replace(params (int start, int length, string newText)[] changes)
            => Replace((IEnumerable<(int start, int length, string newText)>)changes);

        public void Replace(IEnumerable<(int start, int length, string newText)> changes)
        {
            var text = this.text;

            bool changed = false;

            foreach (var (start, length, newText) in changes.OrderByDescending(change => change.start))
            {
                if (start > text.Count || (start + length) > text.Count)
                {
                    throw new ArgumentOutOfRangeException("Attempted to replace text beyond end of document");
                }

                changed |= length > 0 || newText.Length > 0;

                text = text.RemoveRange(start, length).InsertRange(start, newText);
            }

            if (changed)
            {
                this.Changed?.Invoke(this, EventArgs.Empty);
            }

            this.text = text;
        }

        public string GetText(int start = 0, int length = -1)
        {
            var text = this.text;

            if (length == -1)
            {
                length = text.Count;
            }

            if (start > text.Count || start + length > text.Count)
            {
                throw new ArgumentOutOfRangeException($"{nameof(start)} and/or {nameof(length)} is beyond length of document");
            }

            var builder = new StringBuilder();
            for (int i = start; i < start + length; i++)
            {
                builder.Append(text[i]);
            }
            return builder.ToString();
        }

        public override string ToString()
        {
            return this.GetText();
        }
    }
}
