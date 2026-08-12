namespace Compiler
{
    public sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public Parser(List<Token> tokens) => _tokens = tokens;

        private Token Current => _tokens[_pos];
        private Token Peek(int n = 1) => _tokens[Math.Min(_pos + n, _tokens.Count - 1)];

        private Token Advance()
        {
            Token t = Current;
            if (_pos < _tokens.Count - 1) _pos++;
            return t;
        }

        private bool IsPunct(string text) => Current.Kind == TokenKind.Punct && Current.Text == text;

        private bool Match(string text)
        {
            if (IsPunct(text))
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool IsIdent(string text) => Current.Kind == TokenKind.Ident && Current.Text == text;

        private bool MatchIdent(string text)
        {
            if (IsIdent(text))
            {
                Advance();
                return true;
            }
            return false;
        }

        private Token ExpectPunct(string text)
        {
            if (!IsPunct(text))
                throw new CompileError(Current.Span, $"expected '{text}', got '{Current.Text}'");
            return Advance();
        }

        private Token ExpectIdent(string what = "identifier")
        {
            if (Current.Kind != TokenKind.Ident)
                throw new CompileError(Current.Span, $"expected {what}, got '{Current.Text}'");
            return Advance();
        }

        // ---------------------------------------------------------------- program

        public List<TopLevel> ParseProgram()
        {
            List<TopLevel> items = new();
            while (Current.Kind != TokenKind.Eof)
            {
                if (MatchIdent("struct"))
                {
                    items.Add(ParseStructDecl());
                }
                else
                {
                    items.Add(ParseGlobalDecl());
                }
            }
            return items;
        }

        private StructDecl ParseStructDecl()
        {
            SourceSpan span = _tokens[_pos - 1].Span;
            Token name = ExpectIdent("struct name");
            ExpectPunct("{");
            StructDecl decl = new() { Span = span, Name = name.Text };
            while (!IsPunct("}"))
            {
                if (Current.Kind == TokenKind.Eof)
                    throw new CompileError(Current.Span, "unterminated struct definition");
                (TypeNode type, string fieldName) = ParseDeclarator(span);
                ExpectPunct(";");
                decl.Fields.Add((fieldName, type));
            }
            ExpectPunct("}");
            ExpectPunct(";");
            return decl;
        }

        private TopLevel ParseGlobalDecl()
        {
            SourceSpan span = Current.Span;
            TypeNode baseType = ParseBaseType();

            if (Current.Kind == TokenKind.Ident && Peek().Kind == TokenKind.Punct && Peek().Text == "(")
            {
                Token name = Advance();
                ExpectPunct("(");
                FuncDecl fn = new() { Span = span, Return = baseType, Name = name.Text };
                if (!IsPunct(")"))
                {
                    while (true)
                    {
                        (TypeNode ptype, string pname) = ParseDeclarator(span);
                        fn.Params.Add((pname, ptype));
                        if (!Match(",")) break;
                    }
                }
                ExpectPunct(")");
                fn.Body = ParseBlock();
                return fn;
            }

            (TypeNode type, string varName) = ParseDeclaratorTail(baseType, span);
            GlobalVarDecl decl = new() { Span = span, Type = type, Name = varName };
            if (Match("="))
                decl.Init = ParseAssign();
            ExpectPunct(";");
            return decl;
        }

        // Parses "baseType", then expects an identifier, then optional array
        // dimensions. Returns the built type and the name.
        private (TypeNode Type, string Name) ParseDeclarator(SourceSpan span)
        {
            TypeNode baseType = ParseBaseType();
            return ParseDeclaratorTail(baseType, span);
        }

        private (TypeNode Type, string Name) ParseDeclaratorTail(TypeNode baseType, SourceSpan span)
        {
            while (Match("*"))
                baseType = new PointerTypeNode { Span = span, Inner = baseType };

            Token name = ExpectIdent("name");

            while (IsPunct("["))
            {
                Advance();
                long len = ParseConstInt();
                ExpectPunct("]");
                if (len <= 0)
                    throw new CompileError(span, $"array size must be positive, got {len}");
                baseType = new ArrayTypeNode { Span = span, Elem = baseType, Length = len };
            }

            return (baseType, name.Text);
        }

        // base type: primitive keyword or 'struct Name'
        private TypeNode ParseBaseType()
        {
            SourceSpan span = Current.Span;
            if (Current.Kind == TokenKind.Ident && IsPrimKeyword(Current.Text))
            {
                return new PrimTypeNode { Span = span, Name = Advance().Text };
            }
            if (IsIdent("struct"))
            {
                Advance();
                Token name = ExpectIdent("struct name");
                return new NamedTypeNode { Span = span, Name = name.Text };
            }
            throw new CompileError(span, $"expected a type, got '{Current.Text}'");
        }

        private static bool IsPrimKeyword(string text)
            => text is "void" or "u8" or "u16" or "u32" or "i8" or "i16" or "i32" or "char";

        private long ParseConstInt()
        {
            if (Current.Kind != TokenKind.Number)
                throw new CompileError(Current.Span, "expected a constant integer");
            return Advance().Value;
        }

        // ---------------------------------------------------------------- statements

        private BlockStmt ParseBlock()
        {
            SourceSpan span = Current.Span;
            ExpectPunct("{");
            BlockStmt block = new() { Span = span };
            while (!IsPunct("}"))
            {
                if (Current.Kind == TokenKind.Eof)
                    throw new CompileError(Current.Span, "unterminated block");
                block.Stmts.Add(ParseStmt());
            }
            ExpectPunct("}");
            return block;
        }

        private Stmt ParseStmt()
        {
            SourceSpan span = Current.Span;

            if (IsPunct("{")) return ParseBlock();
            if (MatchIdent("if")) return ParseIf();
            if (MatchIdent("while")) return ParseWhile();
            if (MatchIdent("do")) return ParseDoWhile();
            if (MatchIdent("for")) return ParseFor();
            if (MatchIdent("return"))
            {
                ReturnStmt ret = new() { Span = span };
                if (!IsPunct(";"))
                    ret.Value = ParseAssign();
                ExpectPunct(";");
                return ret;
            }
            if (MatchIdent("break"))
            {
                ExpectPunct(";");
                return new BreakStmt { Span = span };
            }
            if (MatchIdent("continue"))
            {
                ExpectPunct(";");
                return new ContinueStmt { Span = span };
            }
            if (IsPunct(";"))
            {
                Advance();
                return new BlockStmt { Span = span };
            }
            if (IsTypeStart())
            {
                (TypeNode type, string name) = ParseDeclarator(span);
                DeclStmt decl = new() { Span = span, Type = type, Name = name };
                if (Match("="))
                    decl.Init = ParseAssign();
                ExpectPunct(";");
                return decl;
            }
            // expression statement
            Expr e = ParseAssign();
            ExpectPunct(";");
            return new ExprStmt { Span = span, Expr = e };
        }

        private bool IsTypeStart()
        {
            if (Current.Kind != TokenKind.Ident) return false;
            if (IsPrimKeyword(Current.Text) || Current.Text == "struct") return true;
            // bare struct name? not supported; treat as expression
            return false;
        }

        private Stmt ParseIf()
        {
            SourceSpan span = _tokens[_pos - 1].Span;
            ExpectPunct("(");
            Expr cond = ParseAssign();
            ExpectPunct(")");
            Stmt then = ParseStmt();
            Stmt? els = null;
            if (MatchIdent("else"))
                els = ParseStmt();
            return new IfStmt { Span = span, Cond = cond, Then = then, Else = els };
        }

        private Stmt ParseWhile()
        {
            SourceSpan span = _tokens[_pos - 1].Span;
            ExpectPunct("(");
            Expr cond = ParseAssign();
            ExpectPunct(")");
            Stmt body = ParseStmt();
            return new WhileStmt { Span = span, Cond = cond, Body = body };
        }

        private Stmt ParseDoWhile()
        {
            SourceSpan span = _tokens[_pos - 1].Span;
            Stmt body = ParseStmt();
            if (!MatchIdent("while"))
                throw new CompileError(Current.Span, "expected 'while' after do-block");
            ExpectPunct("(");
            Expr cond = ParseAssign();
            ExpectPunct(")");
            ExpectPunct(";");
            return new DoWhileStmt { Span = span, Body = body, Cond = cond };
        }

        private Stmt ParseFor()
        {
            SourceSpan span = _tokens[_pos - 1].Span;
            ExpectPunct("(");
            Stmt? init = null;
            if (!IsPunct(";"))
            {
                if (IsTypeStart())
                {
                    (TypeNode type, string name) = ParseDeclarator(span);
                    DeclStmt decl = new() { Span = span, Type = type, Name = name };
                    if (Match("="))
                        decl.Init = ParseAssign();
                    init = decl;
                }
                else
                {
                    init = new ExprStmt { Span = span, Expr = ParseAssign() };
                }
            }
            ExpectPunct(";");
            Expr? cond = null;
            if (!IsPunct(";"))
                cond = ParseAssign();
            ExpectPunct(";");
            Expr? inc = null;
            if (!IsPunct(")"))
                inc = ParseAssign();
            ExpectPunct(")");
            Stmt body = ParseStmt();
            return new ForStmt { Span = span, Init = init, Cond = cond, Inc = inc, Body = body };
        }

        // ---------------------------------------------------------------- expressions

        public Expr ParseAssign()
        {
            Expr left = ParseOr();
            if (Match("="))
            {
                SourceSpan span = left.Span;
                Expr value = ParseAssign();
                return new AssignExpr { Span = span, Target = left, Value = value };
            }
            return left;
        }

        private Expr ParseOr()
        {
            Expr left = ParseAnd();
            while (IsPunct("||"))
            {
                SourceSpan span = Advance().Span;
                Expr right = ParseAnd();
                left = new BinaryExpr { Span = span, Op = "||", Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseAnd()
        {
            Expr left = ParseBitOr();
            while (IsPunct("&&"))
            {
                SourceSpan span = Advance().Span;
                Expr right = ParseBitOr();
                left = new BinaryExpr { Span = span, Op = "&&", Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseBitOr()
        {
            Expr left = ParseBitXor();
            while (IsPunct("|"))
            {
                SourceSpan span = Advance().Span;
                Expr right = ParseBitXor();
                left = new BinaryExpr { Span = span, Op = "|", Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseBitXor()
        {
            Expr left = ParseBitAnd();
            while (IsPunct("^"))
            {
                SourceSpan span = Advance().Span;
                Expr right = ParseBitAnd();
                left = new BinaryExpr { Span = span, Op = "^", Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseBitAnd()
        {
            Expr left = ParseEquality();
            while (IsPunct("&"))
            {
                SourceSpan span = Advance().Span;
                Expr right = ParseEquality();
                left = new BinaryExpr { Span = span, Op = "&", Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseEquality()
        {
            Expr left = ParseRelational();
            while (IsPunct("==") || IsPunct("!="))
            {
                string op = Advance().Text;
                SourceSpan span = left.Span;
                Expr right = ParseRelational();
                left = new BinaryExpr { Span = span, Op = op, Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseRelational()
        {
            Expr left = ParseShift();
            while (IsPunct("<") || IsPunct("<=") || IsPunct(">") || IsPunct(">="))
            {
                string op = Advance().Text;
                SourceSpan span = left.Span;
                Expr right = ParseShift();
                left = new BinaryExpr { Span = span, Op = op, Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseShift()
        {
            Expr left = ParseAdditive();
            while (IsPunct("<<") || IsPunct(">>"))
            {
                string op = Advance().Text;
                SourceSpan span = left.Span;
                Expr right = ParseAdditive();
                left = new BinaryExpr { Span = span, Op = op, Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseAdditive()
        {
            Expr left = ParseMultiplicative();
            while (IsPunct("+") || IsPunct("-"))
            {
                string op = Advance().Text;
                SourceSpan span = left.Span;
                Expr right = ParseMultiplicative();
                left = new BinaryExpr { Span = span, Op = op, Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseMultiplicative()
        {
            Expr left = ParseUnary();
            while (IsPunct("*") || IsPunct("/") || IsPunct("%"))
            {
                string op = Advance().Text;
                SourceSpan span = left.Span;
                Expr right = ParseUnary();
                left = new BinaryExpr { Span = span, Op = op, Left = left, Right = right };
            }
            return left;
        }

        private Expr ParseUnary()
        {
            SourceSpan span = Current.Span;
            if (IsPunct("-") || IsPunct("~") || IsPunct("!"))
            {
                string op = Advance().Text;
                Expr operand = ParseUnary();
                return new UnaryExpr { Span = span, Op = op, Operand = operand };
            }
            if (IsPunct("*"))
            {
                Advance();
                Expr operand = ParseUnary();
                return new UnaryExpr { Span = span, Op = "*", Operand = operand };
            }
            if (IsPunct("&"))
            {
                Advance();
                Expr operand = ParseUnary();
                return new UnaryExpr { Span = span, Op = "&", Operand = operand };
            }
            return ParsePostfix();
        }

        private Expr ParsePostfix()
        {
            Expr e = ParsePrimary();
            while (true)
            {
                if (IsPunct("("))
                {
                    Advance();
                    CallExpr call = new() { Span = e.Span };
                    if (e is not VarExpr v)
                        throw new CompileError(e.Span, "only named functions can be called");
                    call.Name = v.Name;
                    if (!IsPunct(")"))
                    {
                        while (true)
                        {
                            call.Args.Add(ParseAssign());
                            if (!Match(",")) break;
                        }
                    }
                    ExpectPunct(")");
                    e = call;
                }
                else if (IsPunct("["))
                {
                    Advance();
                    Expr idx = ParseAssign();
                    ExpectPunct("]");
                    e = new IndexExpr { Span = e.Span, Base = e, Index = idx };
                }
                else if (IsPunct(".") || IsPunct("->"))
                {
                    bool arrow = Advance().Text == "->";
                    Token field = ExpectIdent("field name");
                    e = new MemberExpr { Span = e.Span, Base = e, Name = field.Text, Arrow = arrow };
                }
                else
                {
                    break;
                }
            }
            return e;
        }

        private Expr ParsePrimary()
        {
            SourceSpan span = Current.Span;

            if (Current.Kind == TokenKind.Number)
                return new IntExpr { Span = span, Value = Advance().Value };

            if (Current.Kind == TokenKind.Char)
                return new IntExpr { Span = span, Value = Advance().Value };

            if (Current.Kind == TokenKind.Str)
                return new StrExpr { Span = span, Value = Advance().Text };

            if (Current.Kind == TokenKind.Ident)
                return new VarExpr { Span = span, Name = Advance().Text };

            if (IsPunct("("))
            {
                Advance();
                // cast?
                if (IsTypeStart() && Peek().Kind == TokenKind.Punct && Peek().Text == ")")
                {
                    Token saved = Current;
                    if (saved.Text == "struct")
                    {
                        // struct cast not supported
                        throw new CompileError(span, "unsupported cast");
                    }
                    Advance();
                    ExpectPunct(")");
                    CastExpr cast = new() { Span = span, TargetType = new PrimTypeNode { Span = saved.Span, Name = saved.Text } };
                    cast.Operand = ParseUnary();
                    return cast;
                }
                Expr inner = ParseAssign();
                ExpectPunct(")");
                return inner;
            }

            throw new CompileError(span, $"expected an expression, got '{Current.Text}'");
        }
    }
}
