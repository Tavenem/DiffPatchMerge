using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace NeverFoundry.DiffPatchMerge
{
    /// <summary>
    /// Represents a change from one version of a text to another.
    /// </summary>
    public class Revision
    {
        /// <summary>
        /// The collection of patches represented by this revision.
        /// </summary>
        public IReadOnlyList<Patch> Patches { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="Revision"/>.
        /// </summary>
        /// <param name="patches">
        /// The collection of patches represented by this revision.
        /// </param>
        public Revision(List<Patch> patches) => Patches = patches.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of <see cref="Revision"/>.
        /// </summary>
        /// <param name="diffs">
        /// The diffs represented by this revision.
        /// </param>
        public Revision(IEnumerable<Diff> diffs) : this(diffs.Select(x => new Patch(x)).ToList()) { }

        /// <summary>
        /// Given two texts, compute the revision represeting the changes from one to the other.
        /// </summary>
        /// <param name="text1">The original text.</param>
        /// <param name="text2">The altered text.</param>
        /// <returns>
        /// The <see cref="Revision"/> representing the change from <paramref name="text1"/> to
        /// <paramref name="text2"/>.
        /// </returns>
        public static Revision GetRevison(string text1, string text2) => new Revision(Diff.GetDiff(text1, text2));

        /// <summary>
        /// Attempt to parse a delta-formatted <see cref="Revision"/> string.
        /// </summary>
        /// <param name="text">A delta-formatted <see cref="Revision"/> string.</param>
        /// <param name="revision">
        /// If this method returns <see langword="true"/>, will contain a <see cref="Revision"/>
        /// object.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the given string could be parsed; otherwise <see
        /// langword="false"/>.
        /// </returns>
        public static bool TryParse(string text, [NotNullWhen(true)] out Revision? revision)
        {
            revision = null;
            var patches = new List<Patch>();
            foreach (var token in text.Split('\t', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Patch.TryParse(token, out var patch))
                {
                    return false;
                }
                patches.Add(patch);
            }
            revision = new Revision(patches);
            return true;
        }

        /// <summary>
        /// Get the version of a text after applying this revision.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <returns>
        /// The result of applying this revision to the given <paramref name="text"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="text"/> is not the original text from which this revision was
        /// calculated; or, one or more of the <see cref="Patch"/> objects in this instance is
        /// incorrectly formed.
        /// </exception>
        public string Apply(string text)
        {
            if (TryApplying(text, out var result))
            {
                return result;
            }
            throw new ArgumentException(nameof(text));
        }

        /// <summary>
        /// Convert this instance into a collection of <see cref="Diff"/> objects, given a source
        /// <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <returns>
        /// A <see cref="List{T}"/> of <see cref="Diff"/> objects representing this instance.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="text"/> is not the original text from which this revision was
        /// calculated; or, one or more of the <see cref="Patch"/> objects in this instance is
        /// incorrectly formed.
        /// </exception>
        public List<Diff> GetDiffs(string text)
        {
            if (TryGetDiffs(text, out var result))
            {
                return result;
            }
            throw new ArgumentException(nameof(text));
        }

        /// <summary>
        /// Attempt to get the version of a text after applying this revision.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <param name="result">
        /// If this method returns <see langword="true"/>, will contain the result of applying this
        /// revision to the given <paramref name="text"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if this instance could be applied to the given
        /// <paramref name="text"/>; otherwise <see langword="false"/>.
        /// </returns>
        public bool TryApplying(string text, out string result)
        {
            if (!TryGetDiffs(text, out var diffs))
            {
                result = string.Empty;
                return false;
            }
            result = Diff.GetNewVersion(diffs);
            return true;
        }

        /// <summary>
        /// <para>
        /// Returns a string that represents the current object.
        /// </para>
        /// <para>
        /// Renders a compact, encoded string which describes the revision. The first character is
        /// '=' for unchanged text, '+' for an insertion, and '-' for deletion. Unchanged text and
        /// deletions are followed by their length only; insertions are followed by a compressed
        /// version of their full text. Each patch is separated from the next with a tab character
        /// ('\t').
        /// </para>
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (var patch in Patches)
            {
                text.Append(patch.ToString());
            }
            return text.ToString();
        }

        /// <summary>
        /// Attempt to convert this instance into a collection of <see cref="Diff"/> objects, given
        /// a source <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The original text.</param>
        /// <param name="diffs">
        /// If this method returns <see langword="true"/>, will contain a <see cref="List{T}"/> of
        /// <see cref="Diff"/> objects representing this instance.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if this instance could be represented as diffs for the given
        /// <paramref name="text"/>; otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// A failure state occurs when the result of applying the <see cref="Patches"/> of this
        /// instance either reaches the end of the given <paramref name="text"/> before all patches
        /// have been applied, or if all patches are applied but the given <paramref name="text"/>
        /// does not yet have all its content accounted for (via deletions or equalities). It is
        /// possible, therefore, for this method to succeed if the given <paramref name="text"/> is
        /// not, in fact, the original text from which this revision was calculated, provided that
        /// it is exactly the same length.
        /// </remarks>
        public bool TryGetDiffs(string text, out List<Diff> diffs)
        {
            diffs = new List<Diff>();
            var empty = string.IsNullOrEmpty(text);
            if ((Patches.Count == 0 && !empty)
                || (Patches.Count != 0 && empty))
            {
                return false;
            }
            var index = 0;
            foreach (var patch in Patches)
            {
                switch (patch.Operation)
                {
                    case DiffOperation.Inserted:
                        if (patch.Text is null)
                        {
                            return false;
                        }
                        string t;
                        try
                        {
                            t = patch.Text.Decompress();
                        }
                        catch
                        {
                            return false;
                        }
                        diffs.Add(new Diff(DiffOperation.Inserted, t));
                        break;
                    case DiffOperation.Deleted:
                    case DiffOperation.Unchanged:
                        if (patch.Length < 0)
                        {
                            return false;
                        }
                        if (patch.Length > 0)
                        {
                            if (index + patch.Length >= text.Length)
                            {
                                return false;
                            }
                            diffs.Add(new Diff(patch.Operation, text.Substring(index, patch.Length)));
                            index += patch.Length;
                        }
                        break;
                }
            }
            return index == text.Length;
        }
    }
}
