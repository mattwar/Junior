using System;
using System.Collections.Immutable;
using System.Text;

namespace Junior
{
    /// <summary>
    /// Represents a string that is stored in chunks instead of a single contiguous block of characters.
    /// </summary>
    public class LargeString 
        : IComparable<LargeString>, 
          IEquatable<LargeString>
    {
        private readonly ImmutableList<string> _segments;
        private readonly long _length;

        private LargeString(ImmutableList<string> segments, long length)
        {
            _segments = segments;
            _length = length;
        }

        public LargeString(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _segments = ImmutableList<string>.Empty.Add(text);
                _length = text.Length;
            }
            else
            {
                _segments = ImmutableList<string>.Empty;
                _length = 0;
            }
        }

        public LargeString()
            : this("")
        {
        }


        public long Length => _length;

        public static readonly LargeString Empty =
            new LargeString(ImmutableList<string>.Empty, 0);

#if false
        public LargeString Append(string text)
        {
            return new LargeString(
                _segments.Add(text), 
                this.Length + text.Length);
        }

        public LargeString Append(LargeString text)
        {
            return new LargeString(
                _segments.AddRange(text._segments), 
                this.Length + text.Length);
        }

        public LargeString Append(char[] buffer, int start, int length)
        {
            return Append(new String(buffer, start, length));
        }
#endif

        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(LargeString? other) =>
             other != null ? Compare(this, other) == 0 : false;

        public override int GetHashCode()
        {
            return unchecked((int)_length);
        }

        public int CompareTo(LargeString? other) =>
            (other == null) ? 1 : Compare(this, other);

        public static int Compare(
            LargeString largeA, 
            LargeString largeB, 
            StringComparison? comparison = null) =>
            Compare(largeA, 0, largeB, 0, Math.Max(largeA.Length, largeB.Length));

        public static int Compare(
            LargeString largeA, long startA, 
            LargeString largeB, long startB, 
            long length,
            StringComparison? comparison = null)
        {
            comparison ??= StringComparison.CurrentCulture;
            long endA = startA + length;
            long endB = startB + length;
            long segmentStartA = 0;
            long segmentEndA = 0;
            long segmentStartB = 0;
            long segmentEndB = 0;
            string? segmentA = null;
            string? segmentB = null;

            // find starting segment for A
            int indexA = 0;
            for (; indexA < largeA._segments.Count; indexA++)
            {
                segmentA = largeA._segments[indexA];
                segmentEndA = segmentStartA + segmentA.Length;
                if (startA >= segmentStartA && startA < segmentEndA)
                    break;
            }

            // find starting segment for B
            int indexB = 0;
            for (; indexB < largeB._segments.Count; indexB++)
            {
                segmentB = largeB._segments[indexB];
                segmentEndB = segmentStartB + segmentB.Length;
                if (startB >= segmentStartB && startB < segmentEndB)
                    break;
            }

            var sectionStartA = (int)Math.Max(startA - segmentStartA, 0);
            var sectionEndA = (int)Math.Min(endA - segmentEndA, segmentEndA);
            var sectionLengthA = sectionEndA - sectionStartA;
            var sectionStartB = (int)Math.Max(startB - segmentStartB, 0);
            var sectionEndB = (int)Math.Min(endB - segmentEndB, segmentEndB);
            var sectionLengthB = sectionEndB - sectionStartB;

            while (segmentA != null && segmentB != null)
            {
                var compareLength = Math.Min(sectionLengthA, sectionLengthB);
                var result = string.Compare(segmentA, sectionStartA, segmentB, sectionStartB, compareLength);
                if (result != 0)
                    return result;

                sectionStartA += compareLength;
                if (sectionStartA >= sectionEndA)
                {
                    indexA++;
                    if (indexA < largeA._segments.Count)
                    {
                        segmentA = largeA._segments[indexA];
                        segmentStartA = segmentEndA;
                        segmentEndA = segmentStartA + segmentA.Length;
                    }
                    else
                    {
                        segmentA = null;
                    }
                }

                sectionStartA += compareLength;
                if (sectionStartA >= sectionEndA)
                {
                    indexA++;
                    if (indexA < largeA._segments.Count)
                    {
                        segmentA = largeA._segments[indexA];
                        segmentStartA = segmentEndA;
                        segmentEndA = segmentStartA + segmentA.Length;
                        sectionStartA = 0;
                        sectionEndA = (int)Math.Min(endA - segmentEndA, segmentEndA);
                        sectionLengthA = sectionEndA - sectionStartA;
                    }
                    else
                    {
                        segmentA = null;
                    }
                }

                sectionStartB += compareLength;
                if (sectionStartB >= sectionEndB)
                {
                    indexB++;
                    if (indexB < largeB._segments.Count)
                    {
                        segmentB = largeB._segments[indexB];
                        segmentStartB = segmentEndB;
                        segmentEndB = segmentStartB + segmentB.Length;
                        sectionStartB = 0;
                        sectionEndB = (int)Math.Min(endB - segmentEndB, segmentEndB);
                        sectionLengthB = sectionEndB - sectionStartB;
                    }
                    else
                    {
                        segmentB = null;
                    }
                }
            }

            if (segmentA == null && segmentB == null)
                return 0;
            else if (segmentA == null)
                return -1;
            else
                return 1;
        }

        public static int Compare(
            LargeString largeA, 
            string stringB, 
            StringComparison? comparison = null) =>
            Compare(largeA, 0, stringB, 0, Math.Max(largeA.Length, stringB.Length), comparison);

        public static int Compare(
            LargeString largeA, long startA,
            string stringB, int startB,
            long length,
            StringComparison? comparison = null)
        {
            comparison ??= StringComparison.CurrentCulture;
            var endA = startA + length;
            long segmentStartA = 0;
            long segmentEndA = 0;
            string? segmentA = null;

            // find starting segment for A
            int indexA = 0;
            for (; indexA < largeA._segments.Count; indexA++)
            {
                segmentA = largeA._segments[indexA];
                segmentEndA = segmentStartA + segmentA.Length;
                if (startA >= segmentStartA && startA < segmentEndA)
                    break;
            }

            var segmentB = stringB;
            var sectionStartB = startB;

            if (segmentA != null && segmentB != null)
            {
                var sectionStartA = (int)Math.Max(startA - segmentStartA, 0);
                var sectionEndA = (int)Math.Min(endA - segmentEndA, segmentA!.Length);
                var sectionLengthA = sectionEndA - sectionStartA;
                var sectionEndB = (int)Math.Min(sectionStartB + length, segmentB.Length);
                var sectionLengthB = sectionEndB - sectionStartB;

                while (segmentA != null && segmentB != null)
                {
                    var compareLength = Math.Min(sectionLengthA, sectionLengthB);
                    var result = string.Compare(segmentA, sectionStartA, segmentB, sectionStartB, compareLength);
                    if (result != 0)
                        return result;

                    sectionStartA += compareLength;
                    if (sectionStartA >= sectionEndA)
                    {
                        indexA++;
                        if (indexA < largeA._segments.Count)
                        {
                            segmentA = largeA._segments[indexA];
                            segmentStartA = segmentEndA;
                            segmentEndA = segmentStartA + segmentA.Length;
                        }
                        else
                        {
                            segmentA = null;
                        }
                    }

                    sectionStartB += compareLength;
                    if (sectionStartB >= sectionLengthB)
                    {
                        segmentB = null;
                    }
                }
            }

            if (segmentA == null && segmentB == null)
                return 0;
            else if (segmentA == null)
                return -1;
            else
                return 1;
        }

        public void WriteTo(TextWriter writer) =>
            WriteTo(writer, 0, _length);

        public void WriteTo(TextWriter writer, long start, long length)
        {
            long segmentStart = 0;
            var end = start + length;

            foreach (var segment in _segments)
            {
                var segmentLength = segment.Length;
                var segmentEnd = segmentStart + segmentLength;
                
                if (start >= segmentEnd)
                    break;
                
                if (end >= segmentStart)
                {
                    var writeStart = Math.Max(start - segmentStart, 0);
                    var writeEnd = Math.Min(end - segmentEnd, segmentLength);
                    writer.Write(segment, writeStart, segmentLength - writeEnd);
                }

                segmentStart = segmentEnd;
            }
        }

        public override string ToString()
        {
            if (_segments.Count == 0)
            {
                return "";
            }
            else if (_segments.Count == 1)
            {
                return _segments[0];
            }
            else
            {
                return string.Concat(_segments);
            }
        }

        public string ToString(long start, long length)
        {
            if (_segments.Count == 0)
            {
                return "";
            }
            else if (start == 0L && length == this.Length)
            {
                return ToString();
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

        private static int IdealStringSegmentSize = 4096;

        public class Builder
        {
            private readonly ImmutableList<string>.Builder _listBuilder;
            private readonly StringBuilder _stringBuilder;
            private long _length;

            internal Builder(LargeString large)
            {
                _listBuilder = large._segments.ToBuilder();
                _stringBuilder = new StringBuilder();
                _length = large._length;
            }

            public void Add(ReadOnlySpan<char> span)
            {
                _stringBuilder.Append(span);
                FlushStringBuilder(force: false);
            }

            public void Add(string text)
            {
                if (text.Length >= IdealStringSegmentSize
                    && _stringBuilder.Length == 0)
                {
                    FlushStringBuilder(force: true);
                    _listBuilder.Add(text);
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
                long segmentStart = 0;
                var end = start + length;

                foreach (var segment in text._segments)
                {
                    var segmentLength = segment.Length;
                    var segmentEnd = segmentStart + segmentLength;

                    if (start >= segmentEnd)
                        break;

                    if (end >= segmentStart)
                    {
                        var writeStart = (int)Math.Max(start - segmentStart, 0);
                        var writeEnd = (int)Math.Min(end - segmentEnd, segmentLength);
                        var writeLength = segmentLength - writeEnd;
                        Add(segment, writeStart, writeLength);
                    }

                    segmentStart = segmentEnd;
                }
            }

            private void FlushStringBuilder(bool force)
            {
                if (_stringBuilder.Length > IdealStringSegmentSize
                    || force)
                {
                    _length += _stringBuilder.Length;
                    _listBuilder.Add(_stringBuilder.ToString());
                    _stringBuilder.Length = 0;
                }
            }

            public LargeString ToLargeString()
            {
                FlushStringBuilder(force: true);
                return new LargeString(_listBuilder.ToImmutableList(), _length);
            }
        }
    }
}
