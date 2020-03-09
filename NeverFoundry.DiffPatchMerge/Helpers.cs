using NeverFoundry.DiffPatchMerge;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NeverFoundry
{
    /// <summary>
    /// Helper methods.
    /// </summary>
    public static class DiffPatchMergeHelpers
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
            throw new ArgumentException(nameof(text));
        }

        /// <summary>
        /// Compresses a string into a base-64 encoded, deflate-compressed version of itself.
        /// </summary>
        /// <param name="str">The string to compress.</param>
        /// <returns>A compressed string.</returns>
        public static string Compress(this string str)
        {
            byte[] bytes;

            using (var uncompressed = new MemoryStream(Encoding.UTF8.GetBytes(str)))
            {
                using var compressed = new MemoryStream();
                using (var compressor = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                {
                    uncompressed.CopyTo(compressor);
                }
                bytes = compressed.ToArray();
            }

            return Convert.ToBase64String(bytes, Base64FormattingOptions.None);
        }

        /// <summary>
        /// Decompresses a string which has been compressed into a base-64 encoded,
        /// deflate-compressed version of itself via <see cref="Compress(string)"/>.
        /// </summary>
        /// <param name="str">The string to decompress.</param>
        /// <returns>The original string.</returns>
        public static string Decompress(this string str)
        {
            byte[] bytes;

            var compressed = new MemoryStream(Convert.FromBase64String(str));
            using (var decompressor = new DeflateStream(compressed, CompressionMode.Decompress))
            {
                using var decompressed = new MemoryStream();
                decompressor.CopyTo(decompressed);
                bytes = compressed.ToArray();
            }

            return Encoding.UTF8.GetString(bytes);
        }

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
