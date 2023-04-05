using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Junior
{
    public struct JsonToken
    {
        private readonly JsonTokenReader _reader;
        private readonly long _position;

        internal JsonToken(JsonTokenReader reader)
        {
            _reader = reader;
            _position = reader.Position;
        }

        public TokenKind Kind => _reader.TokenKind;
        public bool InBuffer => _reader.TokenInBuffer;
        public bool HasDecimal => _reader.HasDecimal;
        public bool HasExponent => _reader.HasExponent;

        public TextFragmentEnumerator GetTextFragments() =>
            new TextFragmentEnumerator(_reader);

        public bool TryGetText(out string? value)
        {
            CheckPosition();
            return _reader.TryGetTokenText(out value);
        }

        public string GetText()
        {
            CheckPosition();
            return _reader.GetTokenText();
        }

        public ValueTask<string> GetTextAsync()
        {
            CheckPosition();
            return _reader.GetTokenTextAsync();
        }

        public bool TryGetValue(out string? value)
        {
            CheckPosition();
            return _reader.TryGetTokenValue(out value);
        }

        public string GetValue()
        {
            CheckPosition();
            return _reader.GetTokenValue();
        }

        public ValueTask<string> GetValueAsync()
        {
            CheckPosition();
            return _reader.GetTokenValueAsync();
        }

        public bool TryGetValueAs<TValue>([MaybeNullWhen(false)] out TValue value)
            where TValue : ISpanParsable<TValue>
        {
            CheckPosition();
            return _reader.TryGetTokenValueAs(out value);
        }

        private void CheckPosition()
        {
            if (_reader.Position != _position)
            {
                throw new InvalidOperationException("The token is no longer valid.");
            }
        }
    }

    public struct TextFragmentEnumerator 
        : IEnumerable<TextFragment>, 
          IEnumerator<TextFragment>, 
          IEnumerable, 
          IEnumerator,
          IAsyncEnumerable<TextFragment>, 
          IAsyncEnumerator<TextFragment>
    {
        private readonly JsonTokenReader _reader;

        internal TextFragmentEnumerator(JsonTokenReader reader)
        {
            _reader = reader;
        }

        public TextFragment Current => new TextFragment(_reader);

        public bool MoveNext()
        {
            return _reader.ReadNextTokenChars();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _reader.ReadNextTokenValueCharsAsync();
        }


        public TextFragmentEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this;
        }

        public TextFragmentEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<TextFragment> IEnumerable<TextFragment>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        object IEnumerator.Current => this.Current;

        void IEnumerator.Reset()
        {
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        IAsyncEnumerator<TextFragment> IAsyncEnumerable<TextFragment>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return this;
        }
    }

    public struct TextFragment
    {
        private readonly JsonTokenReader _reader;
        private readonly long _position;

        internal TextFragment(JsonTokenReader reader)
        {
            _reader = reader;
            _position = _reader.Position;
        }

        public ReadOnlySpan<char> Span
        {
            get
            {
                CheckPosition();
                return _reader.CurrentChars;
            }
        }

        private void CheckPosition()
        {
            if (_reader.Position != _position)
            {
                throw new InvalidOperationException("The text fragment is no longer valid.");
            }
        }
    }
}
