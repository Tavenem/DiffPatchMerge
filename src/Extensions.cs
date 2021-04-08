using System;
using System.Collections.Generic;
using System.Text;

namespace Tavenem.DiffPatchMerge
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class DiffPatchMergeExtensions
    {
        /// <summary>
        /// Gets the result of applying a sequence of revisions to a source <paramref name="text"/>.
        /// </summary>
        /// <param name="revisions">
        /// A sequence of <see cref="Revision"/> objects, in the order they should be applied.
        /// </param>
        /// <param name="text">The original text.</param>
        /// <returns>
        /// The result of applying all <paramref name="revisions"/> to the original <paramref
        /// name="text"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="text"/> is not the original text from which this revision was
        /// calculated; or, one or more of the <see cref="Revision"/> objects contains an
        /// incorrectly formed <see cref="Patch"/> instance.
        /// </exception>
        public static string Apply(this IEnumerable<Revision> revisions, string text)
        {
            if (TryApplying(revisions, text, out var result))
            {
                return result;
            }
            throw new ArgumentException(null, nameof(text));
        }

        /// <summary>
        /// Compresses a string into an encoded version of itself.
        /// </summary>
        /// <param name="str">The string to compress.</param>
        /// <returns>A compressed string.</returns>
        /// <remarks>
        /// Uses URL encoding, but undoes certain encodings which are not strictly necessary and
        /// make the result unnecessarily longer.
        /// </remarks>
        public static string Compress(this string str)
            => new StringBuilder(System.Web.HttpUtility.UrlEncode(str))
            .Replace('+', ' ').Replace("%20", " ").Replace("%21", "!")
            .Replace("%2a", "*").Replace("%27", "'").Replace("%28", "(")
            .Replace("%29", ")").Replace("%3b", ";").Replace("%2f", "/")
            .Replace("%3f", "?").Replace("%3a", ":").Replace("%40", "@")
            .Replace("%26", "&").Replace("%3d", "=").Replace("%2b", "+")
            .Replace("%24", "$").Replace("%2c", ",").Replace("%23", "#")
            .Replace("%7e", "~")
            .ToString();

        /// <summary>
        /// Decompresses a string which has been compressed via <see cref="Compress(string)"/>.
        /// </summary>
        /// <param name="str">The string to decompress.</param>
        /// <returns>The original string.</returns>
        public static string Decompress(this string str) => System.Web.HttpUtility.UrlDecode(str);

        /// <summary>
        /// Gets the index of the first character in this <see cref="string"/> which satisfies the
        /// given <paramref name="condition"/>.
        /// </summary>
        /// <param name="str">This <see cref="string"/>.</param>
        /// <param name="condition">A condition which a character in the <see cref="string"/> must
        /// satisfy.</param>
        /// <param name="startIndex">The index at which to begin searching.</param>
        /// <returns>
        /// The index of the first character in the <see cref="string"/> which satisfies the given
        /// <paramref name="condition"/>; or -1 if no characters satisfy the condition.
        /// </returns>
        public static int Find(this string str, Func<char, bool> condition, int startIndex = 0)
        {
            for (var i = startIndex; i < str.Length; i++)
            {
                if (condition(str[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns a string that represents the current collection of <see cref="Diff"/> objects.
        /// </summary>
        /// <param name="diffs">A collection of <see cref="Diff"/> objects.</param>
        /// <param name="format">
        /// <para>
        /// The format used.
        /// </para>
        /// <para>
        /// Can be either "delta" (the default), "gnu", "md", or "html" (case insensitive).
        /// </para>
        /// <para>
        /// The "delta" format (the default, used if an empty string or whitespace is passed)
        /// renders a compact, encoded string which describes each diff operation. The first
        /// character is '=' for unchanged text, '+' for an insertion, and '-' for deletion.
        /// Unchanged text and deletions are followed by their length only; insertions are followed
        /// by a compressed version of their full text. Each diff is separated by a tab character
        /// ('\t').
        /// </para>
        /// <para>
        /// The "gnu" format renders the text preceded by "- " for deletion, "+ " for addition, or
        /// nothing if the text was unchanged. Each diff is separated by a newline.
        /// </para>
        /// <para>
        /// The "md" format renders the text surrounded by "~~" for deletion, "++" for addition, or
        /// nothing if the text was unchanged. Diffs are concatenated without separators.
        /// </para>
        /// <para>
        /// The "html" format renders the text surrounded by a span with class "diff-deleted" for
        /// deletion, "diff-inserted" for addition, or without a wrapping span if the text was
        /// unchanged. Diffs are concatenated without separators.
        /// </para>
        /// </param>
        /// <returns>A string that represents the current object.</returns>
        public static string ToString(this IEnumerable<Diff> diffs, string format)
        {
            var isDelta = string.IsNullOrWhiteSpace(format)
                || string.Equals(format, "delta", StringComparison.OrdinalIgnoreCase);
            var isGnu = !isDelta && string.Equals(format, "gnu", StringComparison.OrdinalIgnoreCase);
            if (!isDelta
                && !isGnu
                && !string.Equals(format, "md", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Format \"{format}\" is unrecognized.", nameof(format));
            }
            var text = new StringBuilder();
            foreach (var diff in diffs)
            {
                text.Append(diff.ToString(format));
                if (isDelta)
                {
                    text.Append('\t');
                }
                else if (isGnu)
                {
                    text.AppendLine();
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Attempts to get the result of applying a sequence of revisions to a source <paramref
        /// name="text"/>.
        /// </summary>
        /// <param name="revisions">
        /// A sequence of <see cref="Revision"/> objects, in the order they should be applied.
        /// </param>
        /// <param name="text">The original text.</param>
        /// <param name="result">
        /// If this method returns <see langword="true"/>, this will be set to the result of
        /// applying all <paramref name="revisions"/> to the original <paramref name="text"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the <paramref name="revisions"/> were applied successfully;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public static bool TryApplying(this IEnumerable<Revision> revisions, string text, out string result)
        {
            result = text;
            foreach (var revision in revisions)
            {
                if (revision.TryApplying(result, out var step))
                {
                    result = step;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
    }
}
