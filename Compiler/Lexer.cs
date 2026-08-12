namespace Compiler
{
    public enum TokenKind
    {
        Ident,
        Number,
        Char,
        Str,
        Punct,
        Eof
    }

    public readonly struct Token
    {
        public readonly TokenKind Kind;
        public readonly string Text;
        public readonly long Value;
        public readonly SourceSpan Span;

        public Token(TokenKind kind, string text, long value, SourceSpan span)
        {
            Kind = kind;
            Text = text;
            Value = value;
            Span = span;
        }

        public override string ToString() => $"{Kind} '{Text}'";
    }

    public sealed class CompileError : Exception
    {
        public CompileError(SourceSpan span, string message)
            : base($"{span}: {message}")
        { }

        public CompileError(string message)
            : base(message)
        { }
    }

    public static class Lexer
    {
        private static readonly string[] Multi = { "->", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||" };

        public static List<Token> Tokenize(string source, string path)
        {
            List<Token> tokens = new();
            int i = 0;
            int line = 1;
            int col = 1;

            void Advance(int n = 1)
            {
                for (int k = 0; k < n; k++)
                {
                    if (i < source.Length && source[i] == '\n')
                    {
                        line++;
                        col = 1;
                    }
                    else
                    {
                        col++;
                    }
                    i++;
                }
            }

            SourceSpan Here() => new(path, line, col);

            while (i < source.Length)
            {
                char c = source[i];

                if (char.IsWhiteSpace(c))
                {
                    Advance();
                    continue;
                }

                // comments
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') Advance();
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    SourceSpan start = Here();
                    Advance(2);
                    bool closed = false;
                    while (i < source.Length)
                    {
                        if (source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/')
                        {
                            Advance(2);
                            closed = true;
                            break;
                        }
                        Advance();
                    }
                    if (!closed)
                        throw new CompileError(start, "unterminated block comment");
                    continue;
                }

                SourceSpan span = Here();

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) Advance();
                    tokens.Add(new Token(TokenKind.Ident, source.Substring(start, i - start), 0, span));
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int start = i;
                    long value;
                    if (c == '0' && i + 1 < source.Length && (source[i + 1] == 'x' || source[i + 1] == 'X'))
                    {
                        Advance(2);
                        int digits = i;
                        while (i < source.Length && Uri.IsHexDigit(source[i])) Advance();
                        if (i == digits)
                            throw new CompileError(span, "invalid hex literal");
                        string text = source.Substring(digits, i - digits);
                        try
                        {
                            value = Convert.ToInt64(text, 16);
                        }
                        catch (Exception)
                        {
                            throw new CompileError(span, $"hex literal '0x{text}' out of range");
                        }
                    }
                    else if (c == '0' && i + 1 < source.Length && (source[i + 1] == 'b' || source[i + 1] == 'B'))
                    {
                        Advance(2);
                        int digits = i;
                        while (i < source.Length && (source[i] == '0' || source[i] == '1')) Advance();
                        if (i == digits)
                            throw new CompileError(span, "invalid binary literal");
                        value = 0;
                        for (int k = digits; k < i; k++)
                        {
                            value = (value << 1) | (source[k] == '1' ? 1L : 0L);
                            if (value < 0)
                                throw new CompileError(span, $"binary literal out of range");
                        }
                    }
                    else
                    {
                        while (i < source.Length && char.IsDigit(source[i])) Advance();
                        string text = source.Substring(start, i - start);
                        try
                        {
                            value = long.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch (Exception)
                        {
                            throw new CompileError(span, $"integer literal '{text}' out of range");
                        }
                    }
                    tokens.Add(new Token(TokenKind.Number, source.Substring(start, i - start), value, span));
                    continue;
                }

                if (c == '\'')
                {
                    Advance();
                    long value;
                    if (i >= source.Length)
                        throw new CompileError(span, "unterminated character literal");
                    char ch = source[i];
                    if (ch == '\\')
                    {
                        Advance();
                        if (i >= source.Length)
                            throw new CompileError(span, "unterminated escape in character literal");
                        value = ParseEscape(source[i], span);
                        Advance();
                    }
                    else
                    {
                        value = ch;
                        Advance();
                    }
                    if (i >= source.Length || source[i] != '\'')
                        throw new CompileError(span, "character literal must contain a single character");
                    Advance();
                    tokens.Add(new Token(TokenKind.Char, $"'{value}'", value, span));
                    continue;
                }

                if (c == '"')
                {
                    Advance();
                    System.Text.StringBuilder sb = new();
                    bool closed = false;
                    while (i < source.Length)
                    {
                        if (source[i] == '"')
                        {
                            Advance();
                            closed = true;
                            break;
                        }
                        if (source[i] == '\\')
                        {
                            Advance();
                            if (i >= source.Length)
                                throw new CompileError(span, "unterminated escape in string literal");
                            sb.Append((char) ParseEscape(source[i], span));
                            Advance();
                        }
                        else
                        {
                            sb.Append(source[i]);
                            Advance();
                        }
                    }
                    if (!closed)
                        throw new CompileError(span, "unterminated string literal");
                    tokens.Add(new Token(TokenKind.Str, sb.ToString(), 0, span));
                    continue;
                }

                // punctuation (longest match)
                bool matched = false;
                foreach (string op in Multi)
                {
                    if (i + op.Length <= source.Length && source.Substring(i, op.Length) == op)
                    {
                        tokens.Add(new Token(TokenKind.Punct, op, 0, span));
                        Advance(op.Length);
                        matched = true;
                        break;
                    }
                }
                if (matched) continue;

                if ("(){}[];,.*&!~+-/%^<>=:|".IndexOf(c) >= 0)
                {
                    tokens.Add(new Token(TokenKind.Punct, c.ToString(), 0, span));
                    Advance();
                    continue;
                }

                throw new CompileError(span, $"unexpected character '{c}'");
            }

            tokens.Add(new Token(TokenKind.Eof, "", 0, new SourceSpan(path, line, col)));
            return tokens;
        }

        private static long ParseEscape(char c, SourceSpan span)
        {
            return c switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '0' => '\0',
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                _ => throw new CompileError(span, $"invalid escape '\\{c}'")
            };
        }
    }
}
