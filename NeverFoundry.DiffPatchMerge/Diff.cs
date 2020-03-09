using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NeverFoundry.DiffPatchMerge
{
    /// <summary>
    /// Represents a change within a text.
    /// </summary>
    public class Diff : IEquatable<Diff>
    {
        private static readonly Regex _BlankLineEnd = new Regex("\\n\\r?\\n\\Z", RegexOptions.Compiled);
        private static readonly Regex _BlankLineStart = new Regex("\\A\\r?\\n\\r?\\n", RegexOptions.Compiled);

        /// <summary>
        /// The "cost" of an empty edit in characters.
        /// </summary>
        private const short EditCost = 4;

        /// <summary>
        /// The number of seconds to map a diff before giving up.
        /// </summary>
        public static float Timeout { get; set; }

        /// <summary>
        /// The operation represented by this change.
        /// </summary>
        public DiffOperation Operation { get; private set; }

        /// <summary>
        /// The text involved in this change.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Initializes a new instance of <see cref="Diff"/>.
        /// </summary>
        /// <param name="operation">The operation represented by this change.</param>
        /// <param name="text">The text involved in this change.</param>
        public Diff(DiffOperation operation, string text)
        {
            Operation = operation;
            Text = text;
        }

        /// <summary>
        /// Gets a diff between the two strings.
        /// </summary>
        /// <param name="text1">The first string.</param>
        /// <param name="text2">The second string.</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="Diff"/> objects.</returns>
        public static List<Diff> GetDiff(string text1, string text2)
        {
            var deadline = Timeout <= 0 || float.IsInfinity(Timeout) || float.IsNaN(Timeout)
                ? DateTime.MaxValue
                : DateTime.Now + TimeSpan.FromSeconds(Timeout);

            var diffs = ComputeDiff(text1, text2, deadline);
            if (diffs.Count > 2)
            {
                CleanupSemantic(diffs);
                CleanupEfficiency(diffs);
            }
            return diffs;
        }

        /// <summary>
        /// Get the version of a text after applying all given diffs (all equalities and
        /// insertions).
        /// </summary>
        /// <returns>The result text.</returns>
        public static string GetNewVersion(IList<Diff> diffs)
        {
            var text = new StringBuilder();
            foreach (var diff in diffs)
            {
                if (diff.Operation != DiffOperation.Deleted)
                {
                    text.Append(diff.Text);
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Get the version of a text prior to applying all given diffs (all equalities and
        /// deletions).
        /// </summary>
        /// <returns>The source text.</returns>
        public static string GetPreviousVersion(IList<Diff> diffs)
        {
            var text = new StringBuilder();
            foreach (var diff in diffs)
            {
                if (diff.Operation != DiffOperation.Inserted)
                {
                    text.Append(diff.Text);
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Gets a word diff between the two strings.
        /// </summary>
        /// <param name="text1">The first string.</param>
        /// <param name="text2">The second string.</param>
        /// <returns>A <see cref="List{T}"/> of <see cref="Diff"/> objects.</returns>
        public static List<Diff> GetWordDiff(string text1, string text2)
        {
            var deadline = Timeout <= 0 || float.IsInfinity(Timeout) || float.IsNaN(Timeout)
                ? DateTime.MaxValue
                : DateTime.Now + TimeSpan.FromSeconds(Timeout);

            return DiffWords(text1, text2, deadline);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// <see langword="true" /> if the current object is equal to the <paramref name="other" />
        /// parameter; otherwise, <see langword="false" />.
        /// </returns>
        public bool Equals(Diff other) => Operation == other.Operation && string.Equals(Text, other.Text);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>
        /// <see langword="true" /> if the specified object is equal to the current object;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public override bool Equals(object obj) => obj is Diff other && Equals(other);

        /// <summary>Serves as the default hash function.</summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => HashCode.Combine(Operation, Text);

        /// <summary>
        /// <para>
        /// Returns a string that represents the current object.
        /// </para>
        /// <para>
        /// Renders a compact, encoded string which describes the diff operation. The first
        /// character is '=' for unchanged text, '+' for an insertion, and '-' for deletion.
        /// Unchanged text and deletions are followed by their length only; insertions are followed
        /// by a compressed version of their full <see cref="Text"/>.
        /// </para>
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() => Operation switch
        {
            DiffOperation.Deleted => $"-{Text.Length}",
            DiffOperation.Inserted => $"+{Text.Compress()}",
            _ => $"={Text.Length}"
        };

        /// <summary>Returns a string that represents the current object.</summary>
        /// <param name="format">
        /// <para>
        /// The format used.
        /// </para>
        /// <para>
        /// Can be either "delta" (the default), "gnu", "md", or "html" (case insensitive).
        /// </para>
        /// <para>
        /// The "delta" format (the default, used if an empty string or whitespace is passed)
        /// renders a compact, encoded string which describes the diff operation. The first
        /// character is '=' for unchanged text, '+' for an insertion, and '-' for deletion.
        /// Unchanged text and deletions are followed by their length only; insertions are followed
        /// by a compressed version of their full <see cref="Text"/>.
        /// </para>
        /// <para>
        /// The "gnu" format renders the <see cref="Text"/> preceded by "- " for deletion, "+ " for
        /// addition, or nothing if the text was unchanged.
        /// </para>
        /// <para>
        /// The "md" format renders the <see cref="Text"/> surrounded by "~~" for deletion, "++" for
        /// addition, or nothing if the text was unchanged.
        /// </para>
        /// <para>
        /// The "html" format renders the <see cref="Text"/> surrounded by a span with class
        /// "diff-deleted" for deletion, "diff-inserted" for addition, or without a wrapping span if
        /// the text was unchanged.
        /// </para>
        /// </param>
        /// <returns>A string that represents the current object.</returns>
        public string ToString(string format)
        {
            if (string.IsNullOrWhiteSpace(format)
                || string.Equals(format, "delta", StringComparison.OrdinalIgnoreCase))
            {
                return ToString();
            }
            else if (string.Equals(format, "gnu", StringComparison.OrdinalIgnoreCase))
            {
                return Operation switch
                {
                    DiffOperation.Deleted => "- ",
                    DiffOperation.Inserted => "+ ",
                    _ => string.Empty
                } + Text;
            }
            else if (string.Equals(format, "md", StringComparison.OrdinalIgnoreCase))
            {
                return Operation switch
                {
                    DiffOperation.Deleted => $"~~{Text}~~",
                    DiffOperation.Inserted => $"++{Text}++",
                    _ => Text,
                };
            }
            else if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            {
                return Operation switch
                {
                    DiffOperation.Deleted => $"<span class=\"diff-deleted\">{Text}</span>",
                    DiffOperation.Inserted => $"<span class=\"diff-inserted\">{Text}</span>",
                    _ => Text,
                };
            }
            else
            {
                throw new ArgumentException($"Format \"{format}\" is unrecognized.", nameof(format));
            }
        }

        /// <summary>
        /// Indicates whether two objects are equal.
        /// </summary>
        /// <param name="left">The first object.</param>
        /// <param name="right">The first object.</param>
        /// <returns>
        /// <see langword="true" /> if the objects are equal; otherwise, <see langword="false" />.
        /// </returns>
        public static bool operator ==(Diff left, Diff right) => EqualityComparer<Diff>.Default.Equals(left, right);

        /// <summary>
        /// Indicates whether two objects are unequal.
        /// </summary>
        /// <param name="left">The first object.</param>
        /// <param name="right">The first object.</param>
        /// <returns>
        /// <see langword="true" /> if the objects are unequal; otherwise, <see langword="false" />.
        /// </returns>
        public static bool operator !=(Diff left, Diff right) => !(left == right);

        private static void CharsToWords(ICollection<Diff> diffs, IList<string> words)
        {
            var text = new StringBuilder();
            foreach (var diff in diffs)
            {
                text.Clear();
                for (var i = 0; i < diff.Text.Length; i++)
                {
                    text.Append(words[diff.Text[i]]);
                }
                diff.Text = text.ToString();
            }
        }

        private static void CleanupEfficiency(List<Diff> diffs)
        {
            var changes = false;
            var equalities = new Stack<int>();
            string? lastEquality = null;
            var index = 0;
            var preInsertion = false;
            var preDeletion = false;
            var postInsertion = false;
            var postDeletion = false;
            while (index < diffs.Count)
            {
                if (diffs[index].Operation == DiffOperation.Unchanged)
                {
                    if (diffs[index].Text.Length < EditCost
                        && (postInsertion || postDeletion))
                    {
                        equalities.Push(index);
                        preInsertion = postInsertion;
                        preDeletion = postDeletion;
                        lastEquality = diffs[index].Text;
                    }
                    else
                    {
                        equalities.Clear();
                        lastEquality = null;
                    }
                    postInsertion = postDeletion = false;
                }
                else
                {
                    if (diffs[index].Operation == DiffOperation.Deleted)
                    {
                        postDeletion = true;
                    }
                    else
                    {
                        postInsertion = true;
                    }
                    if (lastEquality != null
                        && ((preInsertion && preDeletion && postInsertion && postDeletion)
                        || ((lastEquality.Length < EditCost / 2)
                        && ((preInsertion ? 1 : 0) + (preDeletion ? 1 : 0) + (postInsertion ? 1 : 0) + (postDeletion ? 1 : 0)) == 3)))
                    {
                        diffs.Insert(equalities.Peek(), new Diff(DiffOperation.Deleted, lastEquality));
                        diffs[equalities.Peek() + 1].Operation = DiffOperation.Inserted;
                        equalities.Pop();
                        lastEquality = null;
                        if (preInsertion && preDeletion)
                        {
                            postInsertion = postDeletion = true;
                            equalities.Clear();
                        }
                        else
                        {
                            if (equalities.Count > 0)
                            {
                                equalities.Pop();
                            }
                            index = equalities.Count > 0 ? equalities.Peek() : -1;
                            postInsertion = postDeletion = false;
                        }
                        changes = true;
                    }
                }
                index++;
            }

            if (changes)
            {
                Merge(diffs);
            }
        }

        private static void CleanupSemantic(List<Diff> diffs)
        {
            var changes = false;
            var equalities = new Stack<int>();
            string? lastEquality = null;
            var index = 0;
            var insertions1Length = 0;
            var deletions1Length = 0;
            var insertions2Length = 0;
            var deletions2Length = 0;
            while (index < diffs.Count)
            {
                if (diffs[index].Operation == DiffOperation.Unchanged)
                {
                    equalities.Push(index);
                    insertions1Length = insertions2Length;
                    deletions1Length = deletions2Length;
                    insertions2Length = 0;
                    deletions2Length = 0;
                    lastEquality = diffs[index].Text;
                }
                else
                {
                    if (diffs[index].Operation == DiffOperation.Inserted)
                    {
                        insertions2Length += diffs[index].Text.Length;
                    }
                    else
                    {
                        deletions2Length += diffs[index].Text.Length;
                    }
                    if (lastEquality != null
                        && lastEquality.Length <= Math.Max(insertions1Length, deletions1Length)
                        && lastEquality.Length <= Math.Max(insertions2Length, deletions2Length))
                    {
                        diffs.Insert(equalities.Peek(), new Diff(DiffOperation.Deleted, lastEquality));
                        diffs[equalities.Peek() + 1].Operation = DiffOperation.Inserted;
                        equalities.Pop();
                        if (equalities.Count > 0)
                        {
                            equalities.Pop();
                        }
                        index = equalities.Count > 0 ? equalities.Peek() : -1;
                        insertions1Length = 0;
                        deletions1Length = 0;
                        insertions2Length = 0;
                        deletions2Length = 0;
                        lastEquality = null;
                        changes = true;
                    }
                }
                index++;
            }

            if (changes)
            {
                Merge(diffs);
            }

            index = 1;
            while (index < diffs.Count - 1)
            {
                if (diffs[index - 1].Operation == DiffOperation.Unchanged
                    && diffs[index + 1].Operation == DiffOperation.Unchanged)
                {
                    var equality1 = diffs[index - 1].Text;
                    var edit = diffs[index].Text;
                    var equality2 = diffs[index + 1].Text;
                    var commonOffset = GetCommonSuffixLength(equality1, edit, out var commonString);
                    if (commonOffset > 0)
                    {
                        equality1 = equality1.Substring(0, equality1.Length - commonOffset);
                        edit = commonString + edit.Substring(0, edit.Length - commonOffset);
                        equality2 = commonString + equality2;
                    }

                    var bestEquality1 = equality1;
                    var bestEdit = edit;
                    var bestEquality2 = equality2;
                    var bestScore = GetSemanticScore(equality1, edit) + GetSemanticScore(edit, equality2);
                    while (edit.Length != 0 && equality2.Length != 0 && edit[0] == equality2[0])
                    {
                        equality1 += edit[0];
                        edit = edit.Substring(1) + equality2[0].ToString();
                        equality2 = equality2.Substring(1);
                        var score = GetSemanticScore(equality1, edit) + GetSemanticScore(edit, equality2);
                        if (score >= bestScore)
                        {
                            bestScore = score;
                            bestEquality1 = equality1;
                            bestEdit = edit;
                            bestEquality2 = equality2;
                        }
                    }

                    if (diffs[index - 1].Text != bestEquality1)
                    {
                        if (bestEquality1.Length != 0)
                        {
                            diffs[index - 1].Text = bestEquality1;
                        }
                        else
                        {
                            diffs.RemoveAt(index - 1);
                            index--;
                        }
                        diffs[index].Text = bestEdit;
                        if (bestEquality2.Length != 0)
                        {
                            diffs[index + 1].Text = bestEquality2;
                        }
                        else
                        {
                            diffs.RemoveAt(index + 1);
                            index--;
                        }
                    }
                }
                index++;
            }

            index = 1;
            while (index < diffs.Count)
            {
                if (diffs[index - 1].Operation == DiffOperation.Deleted
                    && diffs[index].Operation == DiffOperation.Inserted)
                {
                    var deletion = diffs[index - 1].Text;
                    var insertion = diffs[index].Text;
                    var overlapLength1 = GetCommonOverlap(deletion, insertion);
                    var overlapLength2 = GetCommonOverlap(insertion, deletion);
                    if (overlapLength1 >= overlapLength2)
                    {
                        if (overlapLength1 >= deletion.Length / 2.0
                            || overlapLength1 >= insertion.Length / 2.0)
                        {
                            diffs.Insert(index, new Diff(DiffOperation.Unchanged, insertion.Substring(0, overlapLength1)));
                            diffs[index - 1].Text = deletion.Substring(0, deletion.Length - overlapLength1);
                            diffs[index + 1].Text = insertion.Substring(overlapLength1);
                            index++;
                        }
                    }
                    else if (overlapLength2 >= deletion.Length / 2.0
                        || overlapLength2 >= insertion.Length / 2.0)
                    {
                        diffs.Insert(index, new Diff(DiffOperation.Unchanged, deletion.Substring(0, overlapLength2)));
                        diffs[index - 1].Operation = DiffOperation.Inserted;
                        diffs[index - 1].Text = insertion.Substring(0, insertion.Length - overlapLength2);
                        diffs[index + 1].Operation = DiffOperation.Deleted;
                        diffs[index + 1].Text = deletion.Substring(overlapLength2);
                        index++;
                    }
                    index++;
                }
                index++;
            }
        }

        private static List<Diff> ComputeDiff(string text1, string text2, DateTime deadline, bool lines = false)
        {
            if (text1 == text2)
            {
                return new List<Diff> { new Diff(DiffOperation.Unchanged, text1) };
            }

            var commonLength = GetCommonPrefixLength(text1, text2, out var commonPrefix);
            text1 = text1.Substring(commonLength);
            text2 = text2.Substring(commonLength);

            commonLength = GetCommonSuffixLength(text1, text2, out var commonSuffix);
            text1 = text1.Substring(0, text1.Length - commonLength);
            text2 = text2.Substring(0, text2.Length - commonLength);

            var diffs = ComputeDiffMain(text1, text2, deadline, lines);

            if (commonPrefix.Length > 0)
            {
                diffs.Insert(0, new Diff(DiffOperation.Unchanged, commonPrefix));
            }
            if (commonSuffix.Length > 0)
            {
                diffs.Add(new Diff(DiffOperation.Unchanged, commonSuffix));
            }

            Merge(diffs);

            return diffs;
        }

        private static List<Diff> ComputeDiffMain(string text1, string text2, DateTime deadline, bool lines)
        {
            var text1Length = text1.Length;
            if (text1Length == 0)
            {
                return new List<Diff> { new Diff(DiffOperation.Inserted, text2) };
            }

            var text2Length = text2.Length;
            if (text2Length == 0)
            {
                return new List<Diff> { new Diff(DiffOperation.Deleted, text1) };
            }

            var isText1Longer = text1Length > text2Length;
            var longText = isText1Longer ? text1 : text2;
            var shortText = isText1Longer ? text2 : text1;

            var index = longText.IndexOf(shortText, StringComparison.Ordinal);
            if (index != -1)
            {
                var operation = isText1Longer ? DiffOperation.Deleted : DiffOperation.Inserted;
                return new List<Diff>
                {
                    new Diff(operation, longText.Substring(0, index)),
                    new Diff(DiffOperation.Unchanged, shortText),
                    new Diff(operation, longText.Substring(index + shortText.Length)),
                };
            }

            if (shortText.Length == 1)
            {
                return new List<Diff>
                {
                    new Diff(DiffOperation.Deleted, text1),
                    new Diff(DiffOperation.Inserted, text2),
                };
            }

            var halfMatch = GetHalfMatch(text1, text2);
            if (halfMatch.HasValue)
            {
                var diffs1 = ComputeDiff(halfMatch.Value.text1Prefix, halfMatch.Value.text2Prefix, deadline, lines);
                var diffs2 = ComputeDiff(halfMatch.Value.text1Suffix, halfMatch.Value.text2Suffix, deadline, lines);
                diffs1.Add(new Diff(DiffOperation.Unchanged, halfMatch.Value.common));
                diffs1.AddRange(diffs2);
                return diffs1;
            }

            if (lines && text1Length > 100 && text2Length > 100)
            {
                return DiffLines(text1, text2, deadline);
            }

            var maxD = (text1Length + text2Length + 1) / 2;
            var vOffset = maxD;
            var vLength = 2 * maxD;
            var v1 = new int[vLength];
            var v2 = new int[vLength];
            for (var i = 0; i < vLength; i++)
            {
                v1[i] = -1;
                v2[i] = -1;
            }
            v1[vOffset + 1] = 0;
            v2[vOffset + 1] = 0;
            var delta = text1Length - text2Length;
            var front = delta % 2 != 0;
            var k1Start = 0;
            var k1End = 0;
            var k2Start = 0;
            var k2End = 0;
            for (var d = 0; d < maxD; d++)
            {
                if (DateTime.Now > deadline)
                {
                    break;
                }

                for (var k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
                {
                    var k1Offset = vOffset + k1;
                    var x1 = k1 == -d || (k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1])
                        ? v1[k1Offset + 1]
                        : v1[k1Offset - 1] + 1;
                    var y1 = x1 - k1;
                    while (x1 < text1Length && y1 < text2Length && text1[x1] == text2[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1Offset] = x1;
                    if (x1 > text1Length)
                    {
                        k1End += 2;
                    }
                    else if (y1 > text2Length)
                    {
                        k1Start += 2;
                    }
                    else if (front)
                    {
                        var k2Offset = vOffset + delta - k1;
                        if (k2Offset >= 0 && k2Offset < vLength && v2[k2Offset] != -1)
                        {
                            var x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                return DiffBisect(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }

                for (var k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
                {
                    var k2Offset = vOffset + k2;
                    var x2 = k2 == -d || (k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1])
                        ? v2[k2Offset + 1]
                        : v2[k2Offset - 1] + 1;
                    var y2 = x2 - k2;
                    while (x2 < text1Length && y2 < text2Length && text1[text1Length - x2 - 1] == text2[text2Length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2Offset] = x2;
                    if (x2 > text1Length)
                    {
                        k2End += 2;
                    }
                    else if (y2 > text2Length)
                    {
                        k2Start += 2;
                    }
                    else if (!front)
                    {
                        var k1Offset = vOffset + delta - k2;
                        if (k1Offset >= 0 && k1Offset < vLength && v1[k1Offset] != -1)
                        {
                            var x1 = v1[k1Offset];
                            var y1 = vOffset + x1 - k1Offset;
                            x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                return DiffBisect(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }
            }
            return new List<Diff>
            {
                new Diff(DiffOperation.Deleted, text1),
                new Diff(DiffOperation.Inserted, text2),
            };
        }

        private static List<Diff> DiffBisect(string text1, string text2, int x, int y, DateTime deadline)
        {
            var text1a = text1.Substring(0, x);
            var text2a = text2.Substring(0, y);
            var text1b = text1.Substring(x);
            var text2b = text2.Substring(y);

            var diffs = ComputeDiff(text1a, text2a, deadline);
            var diffsB = ComputeDiff(text1b, text2b, deadline);

            diffs.AddRange(diffsB);

            return diffs;
        }

        private static List<Diff> DiffLines(string text1, string text2, DateTime deadline)
        {
            var (chars1, chars2, lines) = WordsToChars(text1, text2);
            text1 = chars1;
            text2 = chars2;

            var diffs = ComputeDiff(text1, text2, deadline);

            CharsToWords(diffs, lines);
            CleanupSemantic(diffs);

            ReDiffByCharacter(diffs, deadline);

            return diffs;
        }

        private static List<Diff> DiffWords(string text1, string text2, DateTime deadline)
        {
            var (chars1, chars2, words) = WordsToChars(text1, text2);
            text1 = chars1;
            text2 = chars2;

            var diffs = ComputeDiff(text1, text2, deadline);

            CharsToWords(diffs, words);
            CleanupSemantic(diffs);

            ReDiffByCharacter(diffs, deadline);

            return diffs;
        }

        private static int GetCommonOverlap(string text1, string text2)
        {
            var text1Length = text1.Length;
            var text2Length = text2.Length;
            if (text1Length == 0 || text2Length == 0)
            {
                return 0;
            }

            if (text1Length > text2Length)
            {
                text1 = text1.Substring(text1Length - text2Length);
            }
            else if (text1Length < text2Length)
            {
                text2 = text2.Substring(0, text1Length);
            }
            var textLength = Math.Min(text1Length, text2Length);
            if (text1 == text2)
            {
                return textLength;
            }

            var best = 0;
            var length = 1;
            while (true)
            {
                var pattern = text1.Substring(textLength - length);
                var found = text2.IndexOf(pattern, StringComparison.Ordinal);
                if (found == -1)
                {
                    return best;
                }
                length += found;
                if (found == 0 || text1.Substring(textLength - length) == text2.Substring(0, length))
                {
                    best = length;
                    length++;
                }
            }
        }

        private static int GetCommonPrefixLength(string text1, string text2, out string commonPrefix)
        {
            var minLength = Math.Min(text1.Length, text2.Length);
            var index = minLength;
            for (var i = 0; i < minLength; i++)
            {
                if (text1[i] != text2[i])
                {
                    index = i;
                    break;
                }
            }
            commonPrefix = text1.Substring(0, index);
            return index;
        }

        private static int GetCommonSuffixLength(string text1, string text2, out string commonSuffix)
        {
            var text1Length = text1.Length;
            var text2Length = text2.Length;
            var minLength = Math.Min(text1.Length, text2.Length);
            var index = minLength;
            for (var i = 1; i <= minLength; i++)
            {
                if (text1[text1Length - i] != text2[text2Length - i])
                {
                    index = i - 1;
                    break;
                }
            }
            commonSuffix = text1.Substring(text1Length - index);
            return index;
        }

        private static (string text1Prefix, string text1Suffix, string text2Prefix, string text2Suffix, string common)? GetHalfMatch(string text1, string text2)
        {
            // This is a time optimization which can produce suboptimal results.
            // Skip if there is no time limit.
            if (Timeout <= 0 || float.IsInfinity(Timeout) || float.IsNaN(Timeout))
            {
                return null;
            }

            var text1Longer = text1.Length > text2.Length;
            var longText = text1Longer ? text1 : text2;
            var shortText = text1Longer ? text2 : text1;
            // No gain.
            if (longText.Length < 4 || shortText.Length * 2 < longText.Length)
            {
                return null;
            }

            var hm1 = GetHalfMatch(longText, shortText, (longText.Length + 3) / 4);
            var hm2 = GetHalfMatch(longText, shortText, (longText.Length + 1) / 2);
            if (!hm1.HasValue && !hm2.HasValue)
            {
                return null;
            }
            (string longTextPrefix, string longTextSuffix, string shortTextPrefix, string shortTextSuffix, string common) hm;
            if (!hm2.HasValue)
            {
                hm = hm1!.Value;
            }
            else if (!hm1.HasValue)
            {
                hm = hm2.Value;
            }
            else
            {
                hm = hm1.Value.common.Length > hm2.Value.common.Length
                    ? hm1.Value
                    : hm2.Value;
            }

            return text1Longer
                ? hm
                : (hm.shortTextPrefix, hm.shortTextSuffix, hm.longTextPrefix, hm.longTextSuffix, hm.common);
        }

        private static (string longTextPrefix, string longTextSuffix, string shortTextPrefix, string shortTextSuffix, string common)? GetHalfMatch(string longText, string shortText, int index)
        {
            var seed = longText.Substring(index, longText.Length / 4);
            var j = -1;
            var bestCommon = string.Empty;
            var bestLongTextPrefix = string.Empty;
            var bestLongTextSuffix = string.Empty;
            var bestShortTextPrefix = string.Empty;
            var bestShortTextSuffix = string.Empty;
            while (j < shortText.Length && (j = shortText.IndexOf(seed, j + 1, StringComparison.Ordinal)) != -1)
            {
                var prefixLength = GetCommonPrefixLength(longText.Substring(index), shortText.Substring(j), out var c1);
                var suffixLength = GetCommonSuffixLength(longText.Substring(0, index), shortText.Substring(0, j), out var c2);
                if (bestCommon.Length < suffixLength + prefixLength)
                {
                    bestCommon = c2 + c1;
                    bestLongTextPrefix = longText.Substring(0, index - suffixLength);
                    bestLongTextSuffix = longText.Substring(index + prefixLength);
                    bestShortTextPrefix = longText.Substring(0, j - suffixLength);
                    bestShortTextSuffix = longText.Substring(j + prefixLength);
                }
            }
            return bestCommon.Length * 2 >= longText.Length
                ? (bestLongTextPrefix, bestLongTextSuffix, bestShortTextPrefix, bestShortTextSuffix, bestCommon)
                : ((string, string, string, string, string)?)null;
        }

        private static int GetSemanticScore(string first, string second)
        {
            if (first.Length == 0 || second.Length == 0)
            {
                return 6; // best
            }

            var char1 = first[^1];
            var char2 = second[0];
            var nonAlphaNumeric1 = !char.IsLetterOrDigit(char1);
            var nonAlphaNumeric2 = !char.IsLetterOrDigit(char2);
            var whitespace1 = nonAlphaNumeric1 && char.IsWhiteSpace(char1);
            var whitespace2 = nonAlphaNumeric2 && char.IsWhiteSpace(char2);
            var lineBreak1 = whitespace1 && char.IsControl(char1);
            var lineBreak2 = whitespace2 && char.IsControl(char2);
            var blankLine1 = lineBreak1 && _BlankLineEnd.IsMatch(first);
            var blankLine2 = lineBreak2 && _BlankLineStart.IsMatch(second);

            if (blankLine1 || blankLine2)
            {
                return 5;
            }
            if (lineBreak1 || lineBreak2)
            {
                return 4;
            }
            if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
            {
                return 3; // end of sentence
            }
            if (whitespace1 || whitespace2)
            {
                return 2;
            }
            if (nonAlphaNumeric1 || nonAlphaNumeric2)
            {
                return 1;
            }
            return 0;
        }

        private static (string chars1, string chars2, List<string> lines) LinesToChars(string text1, string text2)
        {
            var lines = new List<string>();
            var lineHash = new Dictionary<string, int>();

            // avoid null character
            lines.Add(string.Empty);

            var chars1 = LinesToChars(text1, lines, lineHash, 40000);
            var chars2 = LinesToChars(text2, lines, lineHash, 65535);
            return (chars1, chars2, lines);
        }

        private static string LinesToChars(string text, List<string> lines, Dictionary<string, int> lineHash, int maxLines)
        {
            var lineStart = 0;
            var lineEnd = -1;
            string line;
            var chars = new StringBuilder();
            while (lineEnd < text.Length - 1)
            {
                lineEnd = text.IndexOf("\n", lineStart);
                if (lineEnd == -1)
                {
                    lineEnd = text.Length - 1;
                }
                line = text[lineStart..(lineEnd + 1)];
                if (lineHash.ContainsKey(line))
                {
                    chars.Append((char)lineHash[line]);
                }
                else
                {
                    if (lines.Count == maxLines)
                    {
                        line = text.Substring(lineStart);
                        lineEnd = text.Length;
                    }
                    lines.Add(line);
                    lineHash.Add(line, lines.Count - 1);
                    chars.Append((char)(lines.Count - 1));
                }
                lineStart = lineEnd + 1;
            }
            return chars.ToString();
        }

        private static void Merge(List<Diff> diffs)
        {
            diffs.Add(new Diff(DiffOperation.Unchanged, string.Empty));
            var index = 0;
            var deleteCount = 0;
            var insertCount = 0;
            var deleteText = string.Empty;
            var insertText = string.Empty;
            int commonLength;
            while (index < diffs.Count)
            {
                switch (diffs[index].Operation)
                {
                    case DiffOperation.Deleted:
                        deleteCount++;
                        deleteText += diffs[index].Text;
                        index++;
                        break;
                    case DiffOperation.Inserted:
                        insertCount++;
                        insertText += diffs[index].Text;
                        index++;
                        break;
                    default:
                        if (deleteCount + insertCount > 1)
                        {
                            if (deleteCount != 0 && insertCount != 0)
                            {
                                commonLength = GetCommonPrefixLength(insertText, deleteText, out var commonString);
                                if (commonLength != 0)
                                {
                                    if (index - deleteCount - insertCount > 0
                                        && diffs[index - deleteCount - insertCount - 1].Operation == DiffOperation.Unchanged)
                                    {
                                        diffs[index - deleteCount - insertCount - 1].Text += commonString;
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new Diff(DiffOperation.Unchanged, commonString));
                                        index++;
                                    }
                                    insertText = insertText.Substring(commonLength);
                                    deleteText = deleteText.Substring(commonLength);
                                }
                                commonLength = GetCommonSuffixLength(insertText, deleteText, out commonString);
                                if (commonLength != 0)
                                {
                                    diffs[index].Text = commonString + diffs[index].Text;
                                    insertText = insertText.Substring(0, insertText.Length - commonLength);
                                    deleteText = deleteText.Substring(0, deleteText.Length - commonLength);
                                }
                            }
                            index -= deleteCount + insertCount;
                            diffs.RemoveRange(index, deleteCount + insertCount);
                            if (deleteText.Length != 0)
                            {
                                diffs.Insert(index, new Diff(DiffOperation.Deleted, deleteText));
                                index++;
                            }
                            if (insertText.Length != 0)
                            {
                                diffs.Insert(index, new Diff(DiffOperation.Inserted, insertText));
                                index++;
                            }
                            index++;
                        }
                        else if (index != 0 && diffs[index - 1].Operation == DiffOperation.Unchanged)
                        {
                            diffs[index - 1].Text += diffs[index].Text;
                            diffs.RemoveAt(index);
                        }
                        else
                        {
                            index++;
                        }
                        insertCount = 0;
                        deleteCount = 0;
                        deleteText = string.Empty;
                        insertText = string.Empty;
                        break;
                }
            }
            if (diffs[^1].Text.Length == 0)
            {
                diffs.RemoveAt(diffs.Count - 1);
            }

            var changes = false;
            index = 1;
            while (index < diffs.Count - 1)
            {
                if (diffs[index - 1].Operation == DiffOperation.Unchanged
                    && diffs[index + 1].Operation == DiffOperation.Unchanged)
                {
                    if (diffs[index].Text.EndsWith(diffs[index - 1].Text, StringComparison.Ordinal))
                    {
                        diffs[index].Text = diffs[index - 1].Text + diffs[index].Text.Substring(0, diffs[index].Text.Length - diffs[index - 1].Text.Length);
                        diffs[index + 1].Text = diffs[index - 1].Text + diffs[index + 1].Text;
                        diffs.RemoveAt(index - 1);
                        changes = true;
                    }
                    else if (diffs[index].Text.StartsWith(diffs[index + 1].Text, StringComparison.Ordinal))
                    {
                        diffs[index - 1].Text += diffs[index + 1].Text;
                        diffs[index].Text = diffs[index].Text.Substring(diffs[index + 1].Text.Length) + diffs[index + 1].Text;
                        diffs.RemoveAt(index + 1);
                        changes = true;
                    }
                }
                index++;
            }
            if (changes)
            {
                Merge(diffs);
            }
        }

        private static void ReDiffByCharacter(List<Diff> diffs, DateTime deadline)
        {
            diffs.Add(new Diff(DiffOperation.Unchanged, string.Empty));
            var diffIndex = 0;
            var deleteCount = 0;
            var insertCount = 0;
            var deleteText = string.Empty;
            var insertText = string.Empty;
            while (diffIndex < diffs.Count)
            {
                switch (diffs[diffIndex].Operation)
                {
                    case DiffOperation.Deleted:
                        deleteCount++;
                        deleteText = diffs[diffIndex].Text;
                        break;
                    case DiffOperation.Inserted:
                        insertCount++;
                        insertText = diffs[diffIndex].Text;
                        break;
                    default:
                        if (deleteCount >= 1 && insertCount >= 1)
                        {
                            diffs.RemoveRange(diffIndex - deleteCount - insertCount, deleteCount + insertCount);
                            diffIndex = diffIndex - deleteCount - insertCount;
                            var subDiff = ComputeDiff(deleteText, insertText, deadline);
                            diffs.InsertRange(diffIndex, subDiff);
                            diffIndex += subDiff.Count;
                        }
                        insertCount = 0;
                        insertText = string.Empty;
                        deleteCount = 0;
                        deleteText = string.Empty;
                        break;
                }
                diffIndex++;
            }
            diffs.RemoveAt(diffs.Count - 1);
        }

        private static (string chars1, string chars2, List<string> words) WordsToChars(string text1, string text2)
        {
            var words = new List<string>();
            var wordHash = new Dictionary<string, int>();

            // avoid null character
            words.Add(string.Empty);

            var chars1 = WordsToChars(text1, words, wordHash, 40000);
            var chars2 = WordsToChars(text2, words, wordHash, 65535);
            return (chars1, chars2, words);
        }

        private static string WordsToChars(string text, List<string> words, Dictionary<string, int> wordHash, int maxWords)
        {
            var wordStart = 0;
            var wordEnd = -1;
            string word;
            var chars = new StringBuilder();
            while (wordEnd < text.Length - 1)
            {
                var firstChar = text[wordStart];
                wordEnd = char.IsWhiteSpace(firstChar)
                    ? text.Find(x => x != firstChar, wordStart)
                    : text.Find(char.IsWhiteSpace, wordStart);
                if (wordEnd == -1)
                {
                    wordEnd = text.Length - 1;
                }
                word = text[wordStart..(wordEnd + 1)];
                if (wordHash.ContainsKey(word))
                {
                    chars.Append((char)wordHash[word]);
                }
                else
                {
                    if (words.Count == maxWords)
                    {
                        word = text.Substring(wordStart);
                        wordEnd = text.Length;
                    }
                    words.Add(word);
                    wordHash.Add(word, words.Count - 1);
                    chars.Append((char)(words.Count - 1));
                }
                wordStart = wordEnd + 1;
            }
            return chars.ToString();
        }
    }
}
