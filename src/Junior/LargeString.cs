using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Junior.Helpers;

namespace Junior
{
    /// <summary>
    /// Represents a string that is stored in multiple segments 
    /// instead of a single contiguous block of characters.
    /// </summary>
    public class LargeString 
        : IComparable<LargeString>, 
          IEquatable<LargeString>
    {
        private readonly ImmutableList<Segment> _segments;
        private readonly long _length;
        private readonly int _segmentSize;

        private struct Segment
        {
            public string Text;
            public long Start;

            public int Length => this.Text.Length;
            public long End => this.Start + this.Length;

            public Segment(string text, long start)
            {
                this.Text = text;
                this.Start = start;
            }
        }

        private static readonly int DefaultSegmentSize = 4096;

        private LargeString(
            ImmutableList<Segment> segments, long length, int segmentLength)
        {
            _segments = segments;
            _length = length;
            _segmentSize = segmentLength;
        }

        /// <summary>
        /// Constructs a new <see cref="LargeString"/> from the specified string.
        /// Since the specified string is already allocated, the <see cref="LargeString"/>
        /// will not break it down into segments.
        /// </summary>
        public LargeString(string text, int segmentSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                _segments = ImmutableList<Segment>.Empty;
                _length = 0;
                _segmentSize = segmentSize;
            }
            else
            {
                _segments = ImmutableList<Segment>.Empty.Add(new Segment(text, 0));
                _length = text.Length;
                _segmentSize = segmentSize;
            }
        }

        /// <summary>
        /// Constructs a new <see cref="LargeString"/> from the specified string.
        /// Since the specified string is already allocated, the <see cref="LargeString"/>
        /// will not break it down into segments.
        /// </summary>
        public LargeString(string text)
            : this(text, DefaultSegmentSize)
        {
        }

        /// <summary>
        /// Constructs a new empty <see cref="LargeString"/>
        /// with the specified <see cref="SegmentSize"/>.
        /// </summary>
        public LargeString(int segmentSize)
            : this("", segmentSize)
        {
        }

        /// <summary>
        /// Constructs a new empty <see cref="LargeString"/>.
        /// </summary>
        public LargeString()
            : this("")
        {
        }

        /// <summary>
        /// A singleton empty <see cref="LargeString"/>
        /// </summary>
        public static readonly LargeString Empty =
            new LargeString();

        /// <summary>
        /// The current count of characters in the <see cref="LargeString"/>
        /// </summary>
        public long Length => _length;

        /// <summary>
        /// The segment size (in characters) used by the <see cref="LargeString"/> 
        /// to limit contiguous memory allocations.
        /// </summary>
        public int SegmentSize => _segmentSize;

        /// <summary>
        /// returns the character at the specified index
        /// </summary>
        public char this[long index]
        {
            get
            {
                if (TryGetSegmentIndex(index, out var segmentIndex)
                    && TryGetSegmentRange(index, 1, segmentIndex, out var segment, out var segmentOffset, out _))
                {
                    return segment.Text[segmentOffset];
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        /// <summary>
        /// Returns a new instance of this <see cref="LargeString"/>
        /// with the <see cref="SegmentSize"/> changed to the specified value.
        /// This size will not affect the existing chunks.
        /// </summary>
        public LargeString WithSegmentSize(int size)
        {
            return new LargeString(_segments, _length, size);
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified span of characters appended.
        /// </summary>
        public LargeString Append(ReadOnlySpan<char> text)
        {
            return Append(new string(text));
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified string appended.
        /// </summary>
        public LargeString Append(string text)
        {
            if (string.IsNullOrEmpty(text))
                return this;

            var lastIndex = _segments.Count - 1;

            if (text.Length < _segmentSize
                && _segments.Count > 0
                && _segments[lastIndex].Length < _segmentSize)
            {
                var last = _segments[lastIndex];
                return new LargeString(_segmentSize)
                    .Append(_segments.GetItemsBefore(lastIndex))
                    .Append(last.Text + text);
            }
            else
            {
                return new LargeString(
                    _segments.Add(
                        new Segment(text, _length)), 
                        _length + text.Length,
                        _segmentSize);
            }
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified range of the string appended.
        /// </summary>
        public LargeString Append(string text, int start, int length) =>
            Append(text.AsSpan().Slice(start, length));

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the text from the specified <see cref="LargeString"/> appended.
        /// </summary>
        public LargeString Append(LargeString text)
        {
            if (text._segments.Count == 0)
                return text;
            else if (text._segments.Count == 1)
                return Append(text._segments[0].Text);

            return Append(text._segments);
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified characters appended.
        /// </summary>
        public LargeString Append(char[] buffer) =>
            Append(buffer.AsSpan());

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified range of characters appended.
        /// </summary>
        public LargeString Append(char[] buffer, int start, int length) =>
            Append(buffer.AsSpan().Slice(start, length));

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified <see cref="Segment"/>'s remapped and appended.
        /// </summary>
        private LargeString Append(ImmutableList<Segment> segments)
        {
            if (segments.Count == 0)
                return this;

            var newSegments = Remap(_length, segments, out var newLength);
            return new LargeString(
                _segments.AddRange(newSegments),
                newLength,
                _segmentSize);
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified string inserted 
        /// </summary>
        public LargeString Insert(long startIndex, string value)
        {
            if (startIndex >= _length)
            {
                return Append(value);
            }
            else if (startIndex == 0 && value.Length >= _segmentSize)
            {
                return new LargeString(value).Append(_segments);
            }
            else if (TryGetSegment(startIndex, out var index, out var segment))
            {
                var offset = (int)(startIndex - segment.Start);
                var newMiddleText = segment.Text.Insert(offset, value);
                var segmentsBefore = _segments.GetItemsBefore(index);
                var segmentsAfter = _segments.GetItemsAfter(index);
                var newString =
                    new LargeString(_segmentSize)
                    .Append(segmentsBefore)
                    .Append(newMiddleText)
                    .Append(segmentsAfter);
                return newString;
            }
            else
            {
                throw new InvalidOperationException("Could not find insertion segment");
            }
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified string inserted 
        /// </summary>
        public LargeString Insert(long startIndex, LargeString value)
        {
            if (startIndex >= _length)
            {
                return Append(value);
            }
            else if (startIndex == 0 && value.Length >= _segmentSize)
            {
                return new LargeString(_segmentSize).Append(value).Append(_segments);
            }
            else if (TryGetSegment(startIndex, out var index, out var segment))
            {
                var offset = (int)(startIndex - segment.Start);
                var textBefore = segment.Text.Substring(0, offset);
                var textAfter = segment.Text.Substring(offset);
                var segmentsBefore = _segments.GetItemsBefore(index);
                var segmentsAfter = _segments.GetItemsAfter(index);
                var newString =
                    new LargeString(_segmentSize)
                    .Append(segmentsBefore)
                    .Append(textBefore)
                    .Append(value._segments)
                    .Append(textAfter)
                    .Append(segmentsAfter);
                return newString;
            }
            else
            {
                throw new InvalidOperationException("Could not find insertion segment");
            }
        }


        /// <summary>
        /// Returns a new <see cref="LargeString"/> with the specified range of characters removed.
        /// </summary>
        public LargeString Remove(long start, long length)
        {
            var end = start + length;
            if (_segments.Count == 0)
            {
                return this;
            }
            else if (start == 0 && length == _length)
            {
                return new LargeString(_segmentSize);
            }
            else if (_segments.Count == 1)
            {
                var newText = _segments[0].Text.Remove((int)start, (int)length);
                return new LargeString(newText, _segmentSize);
            }
            else if (start == 0
                && TryGetSegmentIndex(end, out var index)
                && TryGetSegmentRange(start, length, index, out var segment, out var segmentOffset, out var segmentLength))
            {
                var newText = segment.Text.Remove(segmentOffset, segmentLength);
                var segmentsAfter = _segments.GetItemsAfter(index);
                return new LargeString(_segmentSize)
                    .Append(newText)
                    .Append(segmentsAfter);
            }
            else if (end == _length
                && TryGetSegmentIndex(start, out index)
                && TryGetSegmentRange(start, length, index, out segment, out segmentOffset, out segmentLength))
            {
                var newText = segment.Text.Remove(segmentOffset, segmentLength);
                var segmentsBefore = _segments.GetItemsBefore(index);
                return new LargeString(_segmentSize)
                    .Append(segmentsBefore)
                    .Append(newText);                   
            }
            else if (TryGetSegmentIndex(start, out var firstIndex)
                && TryGetSegmentRange(start, length, firstIndex, out var firstSegment, out var firstOffset, out var firstLength)
                && TryGetSegmentIndex(end, out var lastIndex)
                && TryGetSegmentRange(start, length, lastIndex, out var lastSegment, out var lastOffset, out var lastLength))
            {
                var newMiddleText =
                    (firstIndex == lastIndex)
                        ? firstSegment.Text.Remove(firstOffset, firstLength)
                        : firstSegment.Text.Remove(firstOffset, firstLength)
                            + lastSegment.Text.Remove(lastOffset, lastLength);
                var segmentsBefore = _segments.GetItemsBefore(firstIndex);
                var segmentsAfter = _segments.GetItemsAfter(lastIndex);
                return new LargeString(_segmentSize)
                    .Append(segmentsBefore)
                    .Append(newMiddleText)
                    .Append(segmentsAfter);
            }
            else
            {
                return this;
            }
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> with only the specified range of characters remaining.
        /// </summary>
        public LargeString Substring(long start, long length)
        {
            var end = start + length;
            if (start == 0 && length == _length)
            {
                return this;
            }
            else if (_length == 0)
            {
                return new LargeString(_segmentSize);
            }
            else if (_segments.Count == 1)
            {
                var newText = _segments[0].Text.Substring((int)start, (int)length);
                return new LargeString(newText, _segmentSize);
            }
            else if (start == 0
                && TryGetSegmentIndex(end, out var index)
                && TryGetSegmentRange(start, length, index, out var segment, out var segmentOffset, out var segmentLength))
            {
                var newText = segment.Text.Substring(segmentOffset, segmentLength);
                var segmentsBefore = _segments.GetItemsBefore(index);
                return new LargeString(_segmentSize)
                    .Append(segmentsBefore)
                    .Append(newText);
            }
            else if (end == _length
                && TryGetSegmentIndex(start, out index)
                && TryGetSegmentRange(start, length, index, out segment, out segmentOffset, out segmentLength))
            {
                var newText = segment.Text.Substring(segmentOffset, segmentLength);
                var segmentsAfter = _segments.GetItemsAfter(index);
                return new LargeString(_segmentSize)
                    .Append(newText)
                    .Append(segmentsAfter);
            }
            else if (TryGetSegmentIndex(start, out var firstIndex)
                && TryGetSegmentRange(start, length, firstIndex, out var firstSegment, out var firstOffset, out var firstLength)
                && TryGetSegmentIndex(end, out var lastIndex)
                && TryGetSegmentRange(start, length, lastIndex, out var lastSegment, out var lastOffset, out var lastLength))
            {
                if (firstIndex == lastIndex)
                {
                    var newText = firstSegment.Text.Substring(firstOffset, firstLength);
                    return new LargeString(newText, _segmentSize);
                }
                else
                {
                    var newFirstText = firstSegment.Text.Substring(firstOffset, firstLength);
                    var newLastText = lastSegment.Text.Substring(lastOffset, lastLength);
                    var newMiddleSegments = _segments
                        .RemoveItemsAfter(lastIndex - 1)
                        .RemoveItemsBefore(firstIndex + 1);
                    return new LargeString(_segmentSize)
                        .Append(newFirstText)
                        .Append(newMiddleSegments)
                        .Append(newLastText);
                }
            }
            else
            {
                return Empty;
            }
        }

        /// <summary>
        /// Returns a new <see cref="LargeString"/> without the characters before the start index.
        /// </summary>
        public LargeString Substring(long startIndex) =>
            Substring(startIndex, _length - startIndex);

        /// <summary>
        /// Copies the character starting at the specified index to the span.
        /// </summary>
        public void CopyTo(long startIndex, Span<char> span)
        {
            var hasSegment = TryGetFirstSegmentRange(
                startIndex, span.Length,
                out var segmentIndex,
                out var segment,
                out var segmentOffset,
                out var segmentLength);

            var spanOffset = 0;
            var spanLength = span.Length;
 
            while (hasSegment && spanLength > 0)
            {
                var copyLength = Math.Min(segmentLength, span.Length);
                
                segment.Text.AsSpan(segmentOffset, copyLength)
                    .CopyTo(span.Slice(spanOffset, spanLength));

                segmentOffset += copyLength;
                segmentLength -= copyLength;
                spanOffset += copyLength;
                spanLength -= copyLength;

                if (segmentLength == 0)
                {
                    segmentIndex++;
                    hasSegment = TryGetSegmentRange(
                        startIndex, span.Length, segmentIndex,
                        out segment, out segmentOffset, out segmentLength);
                }

                if (spanLength == 0)
                    break;
            }
        }

        /// <summary>
        /// Copies the character starting at the specified index to the destination
        /// buffer starting at the destination index.
        /// </summary>
        public void CopyTo(long startIndex, char[] destination, int destinationIndex, int length) =>
            CopyTo(startIndex, destination.AsSpan(destinationIndex, length));

        private static List<Segment> Remap(
            long start,
            IReadOnlyList<Segment> segments,
            out long length)
        {
            var list = new List<Segment>(segments.Count);

            foreach (var seg in segments)
            {
                if (seg.Length > 0)
                    list.Add(new Segment(seg.Text, start));
                start += seg.Length;
            }

            length = start;
            return list;
        }

        private bool TryGetSegmentIndex(
            long position, 
            out int index)
        {
            index = _segments.BinarySearch(
                position,
                (pos, seg) =>
                    pos < seg.Start ? 1 // look lower
                    : pos >= seg.End ? -1 // look higher
                    : 0);

            return index >= 0;
        }

        private bool TryGetSegment(
            long position,
            out int index,
            out Segment segment)
        {
            if (TryGetSegmentIndex(position, out index))
            {
                segment = _segments[index];
                return true;
            }
            else
            {
                segment = default;
                return false;
            }
        }

        /// <summary>
        /// Gets the first segment in a range.
        /// </summary>
        private bool TryGetFirstSegmentRange(
            long rangeStart,
            long rangeLength,
            out int index,
            out Segment segment,
            out int segmentOffset,
            out int segmentLength)
        {
            if (TryGetSegmentIndex(rangeStart, out index))
            {
                return TryGetSegmentRange(
                    rangeStart,
                    rangeLength,
                    index,
                    out segment,
                    out segmentOffset,
                    out segmentLength);
            }
            else
            {
                segment = default;
                segmentOffset = 0;
                segmentLength = 0;
                return false;
            }
        }

        private bool TryGetSegmentRange(
            long rangeStart,
            long rangeLength,
            int index,
            out Segment segment,
            out int segmentOffset, 
            out int segmentLength)
        {
            var rangeEnd = rangeStart + rangeLength;

            if (index < _segments.Count)
            {
                segment = _segments[index];
                if (rangeStart < segment.End && rangeEnd >= segment.Start)
                {
                    var segmentRangeStart = Math.Min(Math.Max(rangeStart, segment.Start), Math.Max(rangeEnd, segment.End));
                    var segmentRangeEnd = Math.Max(Math.Min(rangeStart, segment.Start), Math.Min(rangeEnd, segment.End));
                    segmentOffset = (int)(segmentRangeStart - segment.Start);
                    segmentLength = (int)(segmentRangeEnd - segmentRangeStart);
                    return true;
                }
            }

            segment = default;
            segmentOffset = 0;
            segmentLength = 0;
            return false;
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(LargeString? other) =>
             other != null ? Compare(this, other) == 0 : false;

        public bool Equals(string? other) =>
            other != null ? Compare(this, other) == 0 : false;

        public override int GetHashCode()
        {
            // just use the length as the hash code..
            // instead of iterating over the entire text.
            return unchecked((int)_length);
        }

        public int CompareTo(LargeString? other) =>
            (other == null) ? 1 : Compare(this, other);

        public static int Compare(
            LargeString largeA, 
            LargeString largeB) =>
            Compare(largeA, 0, largeB, 0, Math.Max(largeA.Length, largeB.Length));

        public static int Compare(
            LargeString largeA, long startA, 
            LargeString largeB, long startB, 
            long length,
            StringComparison comparison = StringComparison.CurrentCulture)
        {
            bool hasSegmentA = largeA
                .TryGetFirstSegmentRange(
                    startA, length,
                    out var indexA, 
                    out var segmentA,
                    out var segmentOffsetA,
                    out var segmentLengthA);

            bool hasSegmentB = largeB
                .TryGetFirstSegmentRange(
                    startB, length,
                    out var indexB, 
                    out var segmentB,
                    out var segmentOffsetB,
                    out var segmentLengthB);

            while (hasSegmentA && hasSegmentB)
            {
                var compareLength = Math.Min(segmentLengthA, segmentLengthB);
                var result = string.Compare(
                    segmentA.Text, segmentOffsetA, 
                    segmentB.Text, segmentOffsetB,
                    compareLength,
                    comparison);

                if (result != 0)
                    return result;

                segmentOffsetA += compareLength;
                segmentLengthA -= compareLength;
                segmentOffsetB += compareLength;
                segmentLengthB -= compareLength;

                if (segmentLengthA == 0)
                {
                    indexA++;
                    hasSegmentA = largeA.TryGetSegmentRange(
                        startA, length, indexA,
                        out segmentA, out segmentOffsetA, out segmentLengthA);
                }

                if (segmentLengthB == 0)
                {
                    indexB++;
                    hasSegmentB = largeB.TryGetSegmentRange(
                        startB, length, indexB,
                        out segmentB, out segmentOffsetB, out segmentLengthB);
                }
            }

            if (!hasSegmentA && !hasSegmentB)
                return 0;
            else if (!hasSegmentA)
                return -1;
            else
                return 1;
        }

        public static int Compare(
            LargeString largeA, 
            string stringB) =>
            Compare(largeA, 0, stringB, 0, Math.Max(largeA.Length, stringB.Length));

        public static int Compare(
            LargeString largeA, long startA,
            string? stringB, int startB,
            long length,
            StringComparison comparison = StringComparison.CurrentCulture)
        {
            var endA = startA + length;

            // find starting segment for A
            var hasSegmentA = largeA
                .TryGetFirstSegmentRange(
                    startA, length,
                    out var indexA,
                    out var segmentA,
                    out var segmentOffsetA,
                    out var segmentLengthA);

            var stringOffsetB = startB;
            var stringLengthB = stringB != null ? stringB.Length : 0;

            if (hasSegmentA && stringB != null)
            {
                while (hasSegmentA && stringB != null)
                {
                    var compareLength = Math.Min(segmentLengthA, stringLengthB);
                    var result = string.Compare(
                        segmentA.Text, segmentOffsetA, 
                        stringB, stringOffsetB, 
                        compareLength,
                        comparison);
                    if (result != 0)
                        return result;

                    segmentOffsetA += compareLength;
                    segmentLengthA -= compareLength;
                    stringOffsetB += compareLength;
                    stringLengthB -= compareLength;

                    if (segmentLengthA == 0)
                    {
                        indexA++;
                        hasSegmentA = largeA.TryGetSegmentRange(
                            startA, length, indexA,
                            out segmentA, out segmentOffsetA, out segmentLengthA);
                    }

                    if (stringLengthB == 0)
                    {
                        stringB = null;
                        break;
                    }
                }
            }

            if (!hasSegmentA && stringB == null)
                return 0;
            else if (!hasSegmentA)
                return -1;
            else
                return 1;
        }

        public void WriteTo(TextWriter writer) =>
            WriteTo(writer, 0, _length);

        public void WriteTo(TextWriter writer, long start, long length)
        {
            if (TryGetSegmentIndex(start, out var index))
            {
                for (; TryGetSegmentRange(
                        start, length, index,
                        out var segment, out var segmentOffset, out var segmentLength);
                        index++)
                {
                    writer.Write(segment.Text.AsSpan().Slice(segmentOffset, segmentLength));
                }
            }
        }

        public override string ToString() =>
            ToString(0, _length);

        public string ToString(long start, long length)
        {
            if (_segments.Count == 0)
            {
                return "";
            }
            else if (_segments.Count == 1)
            {
                if (start == 0 && length == _length)
                {
                    return _segments[0].Text;
                }
                else
                {
                    var actualStart = (int)Math.Min(start, _length);
                    var actualEnd = (int)Math.Min(start + length, _length);
                    var actualLength = actualEnd - actualStart;
                    return _segments[0].Text.Substring(actualStart, actualLength);
                }
            }
            else
            {
                var writer = new StringWriter();
                WriteTo(writer, start, length);
                return writer.ToString();
            }
        }

        public Builder ToBuilder()
        {
            return new Builder(this);
        }

        public class Builder
        {
            private readonly ImmutableList<Segment>.Builder _listBuilder;
            private readonly StringBuilder _stringBuilder;
            private long _length;
            private int _segmentSize;

            internal Builder(LargeString large)
            {
                _listBuilder = large._segments.ToBuilder();
                _stringBuilder = new StringBuilder();
                _length = large._length;
                _segmentSize = large._segmentSize;
            }

            public void Add(ReadOnlySpan<char> span)
            {
                _stringBuilder.Append(span);
                FlushStringBuilder(force: false);
            }

            public void Add(string text)
            {
                if (text.Length >= _segmentSize
                    && _stringBuilder.Length == 0)
                {
                    _listBuilder.Add(new Segment(text, _length));
                    _length += text.Length;
                }
                else
                {
                    Add(text.AsSpan());
                }
            }

            public void Add(string text, int start, int length)
            {
                if (start == 0 && length == text.Length)
                {
                    Add(text);
                }
                else
                {
                    Add(text.AsSpan().Slice(start, length));
                }
            }

            public void Add(char[] buffer) =>
                Add(buffer.AsSpan());

            public void Add(char[] buffer, int start, int length) =>
                Add(buffer.AsSpan().Slice(start, length));

            public void Add(LargeString text) =>
                Add(text, 0, text.Length);

            public void Add(LargeString text, long start, long length)
            {
                if (text.TryGetSegmentIndex(start, out var index))
                {
                    for (; text.TryGetSegmentRange(
                            start, length, index,
                            out var segment, out var segmentOffset, out var segmentLength);
                            index++)
                    {
                        Add(segment.Text, segmentOffset, segmentLength);
                    }
                }
            }

            private void FlushStringBuilder(bool force)
            {
                if (_stringBuilder.Length > _segmentSize
                    || (force && _stringBuilder.Length > 0))
                {
                    _listBuilder.Add(new Segment(_stringBuilder.ToString(), _length));
                    _length += _stringBuilder.Length;
                    _stringBuilder.Length = 0;
                }
            }

            public LargeString ToLargeString()
            {
                FlushStringBuilder(force: true);
                return new LargeString(_listBuilder.ToImmutableList(), _length, _segmentSize);
            }
        }
    }
}
