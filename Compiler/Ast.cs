namespace Compiler
{
    public readonly struct SourceSpan
    {
        public readonly string File;
        public readonly int Line;
        public readonly int Col;

        public SourceSpan(string file, int line, int col)
        {
            File = file;
            Line = line;
            Col = col;
        }

        public override string ToString()
            => $"{File}:{Line}:{Col}";
    }

    // ---------------------------------------------------------------- types (syntax)

    public abstract class TypeNode
    {
        public SourceSpan Span;
    }

    public sealed class PrimTypeNode : TypeNode
    {
        public string Name = "";
    }

    public sealed class NamedTypeNode : TypeNode
    {
        public string Name = "";
    }

    public sealed class PointerTypeNode : TypeNode
    {
        public TypeNode Inner = null!;
    }

    public sealed class ArrayTypeNode : TypeNode
    {
        public TypeNode Elem = null!;
        public long Length;
    }

    // ---------------------------------------------------------------- symbols

    public sealed class ParamInfo
    {
        public string Name = "";
        public Type Type = null!;
        public int Offset;
        public bool InRegister;
    }

    public sealed class FuncSymbol
    {
        public string Name = "";
        public Type Return = null!;
        public List<ParamInfo> Params = new();
        public string Label = "";
        public int RegParams;
        public int StackParams;
        public int FrameSize;
        public bool Used;
    }

    public sealed class Symbol
    {
        public string Name = "";
        public Type Type = null!;
        public bool IsGlobal;
        public int Offset;
        public bool IsStackParam;
        public string? GlobalLabel;
        public FuncSymbol? Func;
    }

    // ---------------------------------------------------------------- top level

    public abstract class TopLevel
    {
        public SourceSpan Span;
    }

    public sealed class StructDecl : TopLevel
    {
        public string Name = "";
        public List<(string Name, TypeNode Type)> Fields = new();
    }

    public sealed class GlobalVarDecl : TopLevel
    {
        public TypeNode Type = null!;
        public string Name = "";
        public Expr? Init;
    }

    public sealed class FuncDecl : TopLevel
    {
        public TypeNode Return = null!;
        public string Name = "";
        public List<(string Name, TypeNode Type)> Params = new();
        public BlockStmt Body = null!;
    }

    // ---------------------------------------------------------------- statements

    public abstract class Stmt
    {
        public SourceSpan Span;
    }

    public sealed class BlockStmt : Stmt
    {
        public List<Stmt> Stmts = new();
    }

    public sealed class ExprStmt : Stmt
    {
        public Expr Expr = null!;
    }

    public sealed class IfStmt : Stmt
    {
        public Expr Cond = null!;
        public Stmt Then = null!;
        public Stmt? Else;
    }

    public sealed class WhileStmt : Stmt
    {
        public Expr Cond = null!;
        public Stmt Body = null!;
    }

    public sealed class DoWhileStmt : Stmt
    {
        public Stmt Body = null!;
        public Expr Cond = null!;
    }

    public sealed class ForStmt : Stmt
    {
        public Stmt? Init;
        public Expr? Cond;
        public Expr? Inc;
        public Stmt Body = null!;
    }

    public sealed class ReturnStmt : Stmt
    {
        public Expr? Value;
    }

    public sealed class BreakStmt : Stmt { }

    public sealed class ContinueStmt : Stmt { }

    public sealed class DeclStmt : Stmt
    {
        public TypeNode Type = null!;
        public string Name = "";
        public Expr? Init;
        public Symbol? Symbol;
    }

    // ---------------------------------------------------------------- expressions

    public abstract class Expr
    {
        public SourceSpan Span;
        public Type? Type;
    }

    public sealed class IntExpr : Expr
    {
        public long Value;
    }

    public sealed class StrExpr : Expr
    {
        public string Value = "";
        public string Label = "";
    }

    public sealed class VarExpr : Expr
    {
        public string Name = "";
        public Symbol? Symbol;
    }

    public sealed class UnaryExpr : Expr
    {
        public string Op = "";
        public Expr Operand = null!;
    }

    public sealed class BinaryExpr : Expr
    {
        public string Op = "";
        public Expr Left = null!;
        public Expr Right = null!;
        public Type? PromotedOperand;
    }

    public sealed class AssignExpr : Expr
    {
        public Expr Target = null!;
        public Expr Value = null!;
    }

    public sealed class CallExpr : Expr
    {
        public string Name = "";
        public List<Expr> Args = new();
        public FuncSymbol? Symbol;
    }

    public sealed class IndexExpr : Expr
    {
        public Expr Base = null!;
        public Expr Index = null!;
    }

    public sealed class MemberExpr : Expr
    {
        public Expr Base = null!;
        public string Name = "";
        public bool Arrow;
    }

    public sealed class CastExpr : Expr
    {
        public TypeNode TargetType = null!;
        public Expr Operand = null!;
    }
}
