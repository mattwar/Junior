namespace Junior
{
    public enum TokenKind : byte
    {
        /// <summary>
        /// The token is not yet read.
        /// May occur before the first MoveNext
        /// </summary>
        Unknown,

        /// <summary>
        /// Not at token.
        /// May occur when there are no more tokens.
        /// </summary>
        None,

        /// <summary>
        /// The token is a open bracket '[' that starts a json list.
        /// </summary>
        ListStart,

        /// <summary>
        /// The token is a close bracket ']' that ends a json list.
        /// </summary>
        ListEnd,

        /// <summary>
        /// The token is an open brace '{' that starts a json object.
        /// </summary>
        ObjectStart,

        /// <summary>
        /// The token is a close brace '}' that ends a json object.
        /// </summary>
        ObjectEnd,

        /// <summary>
        /// The token is a number
        /// </summary>
        Number,

        /// <summary>
        /// The token is a string.
        /// </summary>
        String,

        /// <summary>
        /// The token is the word 'true'.
        /// </summary>
        True,

        /// <summary>
        /// The token is the word 'false'.
        /// </summary>
        False,

        /// <summary>
        /// The token is the word 'null'.
        /// </summary>
        Null,

        /// <summary>
        /// The token is a comma ',' that is found between items in a json list
        /// or properties in an json object.
        /// </summary>
        Comma,

        /// <summary>
        /// The token is a colon ':' that is found between a property name 
        /// and a property value.
        /// </summary>
        Colon,

        /// <summary>
        /// When treating whitespace as a token.
        /// </summary>
        Whitespace,

        /// <summary>
        /// The token is unexpected and considered an error.
        /// </summary>
        Error
    }


    public static class TokenKindExtensions
    {
        public static bool IsValueStart(this TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.ListStart:
                case TokenKind.ListEnd:
                case TokenKind.ObjectStart:
                case TokenKind.ObjectEnd:
                case TokenKind.Number:
                case TokenKind.String:
                case TokenKind.True:
                case TokenKind.False:
                case TokenKind.Null:
                    return true;
                default:
                    return false;
            }
        }
    }
}