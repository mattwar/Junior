using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{

    public class EnumeratorReader : TextReader
    {
        private readonly IEnumerator<string> _enumerator;
        private int _currentStringOffset; // offsent into current string

        public EnumeratorReader(IEnumerator<string> enumerator)
        {
            _enumerator = enumerator;
            _enumerator.MoveNext();
        }

        public override int Read(Span<char> buffer)
        {
            if (_enumerator.Current == null)
                return 0;

            while (_currentStringOffset >= _enumerator.Current.Length)
            {
                _currentStringOffset = 0;
                if (!_enumerator.MoveNext())
                    return 0;
            }

            var remaining = _enumerator.Current.Length - _currentStringOffset;
            var copyCount = Math.Min(buffer.Length, remaining);

            _enumerator.Current.AsSpan(_currentStringOffset, copyCount).CopyTo(buffer);
            _currentStringOffset += copyCount;
            return copyCount;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            return Read(buffer.AsSpan(index, count));
        }
    }
}
