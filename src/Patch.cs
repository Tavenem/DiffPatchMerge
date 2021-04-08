using System;
using System.Diagnostics.CodeAnalysis;

namespace Tavenem.DiffPatchMerge
{
    /// <summary>
    /// Represents a change within a text from one version to another.
    /// </summary>
    public class Patch : IEquatable<Patch>
    {
        /// <summary>
        /// The length of this change in the original text, for deletions and equalities.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// The operation represented by this change.
        /// </summary>
        public DiffOperation Operation { get; }

        /// <summary>
        /// A compressed version of the text involved in an addition.
        /// </summary>
        public string? Text { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="Patch"/>.
        /// </summary>
        /// <param name="operation">The operation represented by this change.</param>
        /// <param name="length">
        /// The length of this change in the original text, for deletions and equalities.
        /// </param>
        public Patch(DiffOperation operation, int length)
        {
            Operation = operation;
            Length = length;
        }

        /// <summary>
        /// Initializes a new instance of an additive <see cref="Patch"/>.
        /// </summary>
        /// <param name="text">
        /// A compressed version of the text involved in an addition.
        /// </param>
        public Patch(string text)
        {
            Operation = DiffOperation.Inserted;
            Text = text.Compress();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Patch"/>.
        /// </summary>
        /// <param name="diff">A <see cref="Diff"/> which represents this change.</param>
        public Patch(Diff diff)
        {
            Operation = diff.Operation;
            switch (diff.Operation)
            {
                case DiffOperation.Inserted:
                    Text = diff.Text.Compress();
                    break;
                case DiffOperation.Deleted:
                case DiffOperation.Unchanged:
                    Length = diff.Text.Length;
                    break;
            }
        }

        /// <summary>
        /// Attempt to parse a delta-formatted <see cref="Patch"/> string.
        /// </summary>
        /// <param name="text">A delta-formatted <see cref="Patch"/> string.</param>
        /// <param name="patch">
        /// If this method returns <see langword="true"/>, will contain a <see cref="Patch"/>
        /// object.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the given string could be parsed; otherwise <see
        /// langword="false"/>.
        /// </returns>
        public static bool TryParse(string text, [NotNullWhen(true)] out Patch? patch)
        {
            patch = null;
            if (string.IsNullOrEmpty(text)
                || text.Length < 2)
            {
                return false;
            }
            var param = text[1..];
            switch (text[0])
            {
                case '+':
                    try
                    {
                        param = param.Decompress();
                    }
                    catch
                    {
                        return false;
                    }
                    patch = new Patch(param);
                    return true;
                case '-':
                case '=':
                    if (!int.TryParse(param, out var n)
                        || n < 1)
                    {
                        return false;
                    }
                    patch = new Patch(text[0] == '-' ? DiffOperation.Deleted : DiffOperation.Unchanged, n);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// <see langword="true" /> if the current object is equal to the <paramref name="other" />
        /// parameter; otherwise, <see langword="false" />.
        /// </returns>
        public bool Equals(Patch? other) => other is not null
            && Length == other.Length
            && Operation == other.Operation
            && (Operation != DiffOperation.Inserted
            || string.Equals(Text, other.Text));

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>
        /// <see langword="true" /> if the specified object is equal to the current object;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public override bool Equals(object? obj) => obj is Patch other && Equals(other);

        /// <summary>Serves as the default hash function.</summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => HashCode.Combine(Length, Operation, Text);

        /// <summary>
        /// <para>
        /// Returns a string that represents the current object.
        /// </para>
        /// <para>
        /// Renders a compact, encoded string which describes the diff operation. The first
        /// character is '=' for unchanged text, '+' for an insertion, and '-' for deletion.
        /// Unchanged text and deletions are followed by their length only; insertions are followed
        /// by a compressed version of their full <see cref="Text"/>. Each diff is separated from
        /// the next with a tab character ('\t').
        /// </para>
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() => Operation switch
        {
            DiffOperation.Deleted => $"-{Length}",
            DiffOperation.Inserted => $"+{Text}",
            _ => $"={Length}"
        };
    }
}
