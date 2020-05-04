namespace LanguageServerLibrary.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Holds text from an open text document.
    /// </summary>
    /// <remarks>
    /// This is not the "smartest" TextBuffer but it attempts to support logarithmic time updates
    /// anywhere in the document. 
    /// </remarks>
    internal sealed class TextBuffer
    {
        // For this sample, we're using ImmutableList, which is de facto a balanced binary tree,
        // making mutation actions for arbitrarily large files generally a logarithmic time action.
        private Snapshot snapshot = new Snapshot(length: 0, ImmutableList<ImmutableList<char>>.Empty);

        public event EventHandler<TextBufferChangedEventArgs> Changed;

        public int Length => this.snapshot.Length;

        public void Replace(params (int line, int start, int length, string newText)[] changes)
            => Replace((IEnumerable<(int line, int start, int length, string newText)>)changes);

        public void Replace(IEnumerable<(int line, int start, int length, string newText)> changes)
        {
            bool changed;

            Snapshot oldSnapshot = null;
            Snapshot newSnapshot = null;

            do
            {
                oldSnapshot = this.snapshot;
                newSnapshot = this.CreateNewSnapshotForReplace(oldSnapshot, changes, out changed);
            }
            while (Interlocked.CompareExchange(ref this.snapshot, newSnapshot, oldSnapshot) != oldSnapshot);

            // Notify subscribers that the text changed.
            if (changed)
            {
                this.Changed?.Invoke(
                    this,
                    new TextBufferChangedEventArgs(
                        newSnapshot,
                        changes.Select(change => change.line).ToArray()));
            }
        }

        public string GetText(int start = 0, int length = -1)
        {
            var lines = this.snapshot.Lines;

            if (length == -1)
            {
                length = this.Length;
            }

            if (start > this.Length || start + length > this.Length)
            {
                throw new ArgumentOutOfRangeException($"{nameof(start)} and/or {nameof(length)} is beyond length of document");
            }

            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                for (int i = start; i < start + length; i++)
                {
                    builder.Append(line[i]);
                }

                builder.AppendLine();
            }
            return builder.ToString();
        }

        public override string ToString() => this.GetText();

        private Snapshot CreateNewSnapshotForReplace(
            Snapshot oldSnapshot,
            IEnumerable<(int line, int start, int length, string newText)> changes, out bool changed)
        {
            var lines = oldSnapshot.Lines;

            var changeInLength = 0;
            changed = false;
            foreach (var (line, start, length, newText) in changes.OrderByDescending(change => change.start))
            {
                // Check bounds.
                if (start > oldSnapshot.Length || (start + length) > oldSnapshot.Length)
                {
                    throw new ArgumentOutOfRangeException("Attempted to replace text beyond end of document");
                }

                // Add or update line text.
                if (line < lines.Count)
                {
                    lines = lines.SetItem(line, lines[line].RemoveRange(start, length).InsertRange(start, newText));
                    changed |= length > 0 || newText.Length > 0;
                }
                else
                {
                    lines = lines.Add(ImmutableList<char>.Empty.AddRange(newText));

                    // We inserted a (possibly empty) line.
                    changed |= true;
                }

                // Update length.
                changeInLength += newText.Length - length;
            }

            // Update document snapshot.
            return new Snapshot(
                length: Math.Max(0, (int)(oldSnapshot.Length + changeInLength)),
                lines);
        }

        internal sealed class Snapshot
        {
            public Snapshot(
                int length,
                ImmutableList<ImmutableList<char>> lines)
            {
                // In debug configuration, check our length calculation.
                Debug.Assert(lines.Sum(line => line.Count) == length);

                this.Length = length;
                this.Lines = lines;
            }

            public int Length { get; }

            public ImmutableList<ImmutableList<char>> Lines { get; }
        }
    }
}
