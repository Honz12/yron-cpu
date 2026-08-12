using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Compiler
{
    /// <summary>
    /// Emits yron assembly (.yrn) for a resolved yrC program.
    ///
    /// ABI:
    ///   $10-$13  a0..a3   argument registers (caller-saved)
    ///   $0F      rv       return value register
    ///   $1F      fp       frame pointer (callee-saved)
    ///   $0E      t0       primary scratch / expression result
    ///   $03-$0D  t1..t11  scratch (caller-saved, may be clobbered by calls)
    ///
    /// Stack frame (callee, after prologue):
    ///   [fp+8..]      stack params (arg4 at fp+8)
    ///   [fp+4]        return address
    ///   [fp]          saved fp
    ///   [fp-4..fp-N]  register params and locals (offsets negative)
    /// </summary>
    public sealed class Codegen
    {
        private const int T0 = 0x0E;
        private const int T1 = 0x03;
        private const int T2 = 0x04;
        private const int A0 = 0x10;
        private const int A1 = 0x11;
        private const int A2 = 0x12;
        private const int RV = 0x0F;
        private const int FP = 0x1F;

        private readonly Resolver _resolver;
        private List<string> _lines = new();
        private readonly Dictionary<string, List<string>> _funcLines = new();
        private readonly HashSet<string> _helpers = new();
        private readonly List<(string Label, string Text)> _strings = new();
        private readonly Stack<LoopContext> _loops = new();
        private int _labelCounter;
        private int _stringCounter;

        private sealed class LoopContext
        {
            public string Break = "";
            public string Continue = "";
        }

        public Codegen(Resolver resolver) => _resolver = resolver;

        public string Emit()
        {
            // codegen every function reachable from main (BFS, deterministic order)
            Queue<string> pending = new();
            pending.Enqueue("main");
            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (_funcLines.ContainsKey(name)) continue;
                if (!_resolver.Funcs.TryGetValue(name, out FuncSymbol? fs)) continue;

                List<string> saved = _lines;
                _lines = new List<string>();
                EmitFunction(fs);
                _funcLines[name] = _lines;
                _lines = saved;

                FuncDecl? decl = _resolver.Items.OfType<FuncDecl>().FirstOrDefault(d => d.Name == name);
                if (decl != null)
                    foreach (string callee in WalkCalls(decl.Body))
                        if (!_funcLines.ContainsKey(callee))
                            pending.Enqueue(callee);
            }

            _lines = new List<string>();
            EmitEntry();
            EmitHelpers();
            foreach (string name in _funcLines.Keys)
                _lines.AddRange(_funcLines[name]);
            EmitData();
            return string.Join("\n", _lines) + "\n";
        }

        // ---------------------------------------------------------------- output helpers

        private void Emit(string line) => _lines.Add(line);

        private void EmitLabel(string label) => Emit($"{label}:");

        private static string Reg(int reg) => $"${reg:X2}";

        private string Nl() => $".l{_labelCounter++}";

        private void Ldi(int reg, long value)
        {
            if (value >= 0 && value <= 255)
                Emit($"ldib {Reg(reg)}, {value}");
            else if (value >= 0 && value <= 65535)
                Emit($"ldiw {Reg(reg)}, {value}");
            else
                Emit($"ldid {Reg(reg)}, {value}");
        }

        private void LdiLabel(int reg, string label) => Emit($"ldid {Reg(reg)}, {label}");

        // ---------------------------------------------------------------- entry + helpers

        private void EmitEntry()
        {
            EmitLabel("_start");
            Emit("call f__main");
            EmitLabel("__halt");
            Emit("jmp __halt");
        }

        private void EmitHelpers()
        {
            string[] order = { "__memcpy", "__shl", "__shr", "__sar", "__sext8", "__sext16", "__sdiv", "__smod" };
            foreach (string name in order)
                if (_helpers.Contains(name))
                    EmitBody(HelperBodies[name]);
        }

        private void EmitBody(string body)
        {
            foreach (string line in body.Split('\n'))
                _lines.Add(line.TrimEnd('\r'));
        }

        private static readonly Dictionary<string, string> HelperBodies = new()
        {
            ["__memcpy"] =
"__memcpy:\n" +
"\tldid $03, 0\n" +
"\teq $03, $12, $03\n" +
"\tjnz .mc_done, $03\n" +
".mc_loop:\n" +
"\tldb $04, $11\n" +
"\tstb $04, $10\n" +
"\tldid $03, 1\n" +
"\tadd $10, $03, $10\n" +
"\tadd $11, $03, $11\n" +
"\tldid $04, 1\n" +
"\tsub $12, $04, $12\n" +
"\tldid $05, 0\n" +
"\teq $05, $12, $05\n" +
"\tjz .mc_loop, $05\n" +
".mc_done:\n" +
"\tret",

            ["__shl"] =
"__shl:\n" +
"\tldid $03, 0\n" +
"\teq $03, $11, $03\n" +
"\tjnz .shl_done, $03\n" +
".shl_loop:\n" +
"\tadd $10, $10, $10\n" +
"\tldid $03, 1\n" +
"\tsub $11, $03, $11\n" +
"\tldid $04, 0\n" +
"\teq $04, $11, $04\n" +
"\tjz .shl_loop, $04\n" +
".shl_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__shr"] =
"__shr:\n" +
"\tldid $03, 0\n" +
"\teq $03, $11, $03\n" +
"\tjnz .shr_done, $03\n" +
".shr_loop:\n" +
"\tldid $04, 2\n" +
"\tdiv $10, $04, $10\n" +
"\tldid $03, 1\n" +
"\tsub $11, $03, $11\n" +
"\tldid $05, 0\n" +
"\teq $05, $11, $05\n" +
"\tjz .shr_loop, $05\n" +
".shr_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__sar"] =
"__sar:\n" +
"\tldid $03, 0\n" +
"\teq $03, $11, $03\n" +
"\tjnz .sar_done, $03\n" +
"\tldid $06, 0\n" +
"\tldid $04, 0x80000000\n" +
"\tand $10, $04, $04\n" +
"\tjz .sar_loop, $04\n" +
"\tldid $06, 1\n" +
"\tldid $04, 0xFFFFFFFF\n" +
"\txor $10, $04, $10\n" +
".sar_loop:\n" +
"\tldid $05, 2\n" +
"\tdiv $10, $05, $10\n" +
"\tldid $03, 1\n" +
"\tsub $11, $03, $11\n" +
"\tldid $04, 0\n" +
"\teq $04, $11, $04\n" +
"\tjz .sar_loop, $04\n" +
"\tldid $04, 0\n" +
"\teq $04, $06, $04\n" +
"\tjnz .sar_done, $04\n" +
"\tldid $04, 0xFFFFFFFF\n" +
"\txor $10, $04, $10\n" +
".sar_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__sext8"] =
"__sext8:\n" +
"\tldid $03, 0x80\n" +
"\tand $10, $03, $03\n" +
"\tjz .se8_done, $03\n" +
"\tldid $03, 0xFFFFFF00\n" +
"\tor $10, $03, $10\n" +
".se8_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__sext16"] =
"__sext16:\n" +
"\tldid $03, 0x8000\n" +
"\tand $10, $03, $03\n" +
"\tjz .se16_done, $03\n" +
"\tldid $03, 0xFFFF0000\n" +
"\tor $10, $03, $10\n" +
".se16_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__sdiv"] =
"__sdiv:\n" +
"\tldid $04, 0x80000000\n" +
"\tand $10, $04, $05\n" +
"\tand $11, $04, $06\n" +
"\txor $05, $06, $07\n" +
"\tjz .sd_na, $05\n" +
"\tldid $08, 0\n" +
"\tsub $08, $10, $10\n" +
".sd_na:\n" +
"\tjz .sd_nb, $06\n" +
"\tldid $08, 0\n" +
"\tsub $08, $11, $11\n" +
".sd_nb:\n" +
"\tdiv $10, $11, $10\n" +
"\tjz .sd_done, $07\n" +
"\tldid $08, 0\n" +
"\tsub $08, $10, $10\n" +
".sd_done:\n" +
"\tmov $0F, $10\n" +
"\tret",

            ["__smod"] =
"__smod:\n" +
"\tldid $04, 0x80000000\n" +
"\tand $10, $04, $05\n" +
"\tand $11, $04, $06\n" +
"\tjz .sm_na, $05\n" +
"\tldid $08, 0\n" +
"\tsub $08, $10, $10\n" +
".sm_na:\n" +
"\tjz .sm_nb, $06\n" +
"\tldid $08, 0\n" +
"\tsub $08, $11, $11\n" +
".sm_nb:\n" +
"\tmod $10, $11, $10\n" +
"\tjz .sm_done, $05\n" +
"\tldid $08, 0\n" +
"\tsub $08, $10, $10\n" +
".sm_done:\n" +
"\tmov $0F, $10\n" +
"\tret",
        };

        // ---------------------------------------------------------------- functions

        private void EmitFunction(FuncSymbol fs)
        {
            _labelCounter = 0;
            EmitLabel(fs.Label);
            Emit("push $1F, DWORD");
            Emit("mov $1F, $02");
            Ldi(T0, fs.FrameSize);
            Emit($"sub $02, {Reg(T0)}, $02");
            SaveRegisterParams(fs);
            FuncDecl? decl = _resolver.Items.OfType<FuncDecl>().FirstOrDefault(d => d.Name == fs.Name);
            EmitStmt(decl!.Body);
            EmitEpilogue();
        }

        private void SaveRegisterParams(FuncSymbol fs)
        {
            for (int i = 0; i < fs.RegParams; i++)
            {
                Emit($"mov {Reg(T0)}, $1F");
                Ldi(T1, -fs.Params[i].Offset);
                Emit($"add {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                Emit($"std {Reg(A0 + i)}, {Reg(T0)}");
            }
        }

        private void EmitEpilogue()
        {
            Emit("mov $02, $1F");
            Emit("pop $1F, DWORD");
            Emit("ret");
        }

        private void EmitCallHelper(string label, int a0, int a1, int a2)
        {
            if (a0 >= 0) Emit($"mov {Reg(A0)}, {Reg(a0)}");
            if (a1 >= 0) Emit($"mov {Reg(A1)}, {Reg(a1)}");
            if (a2 >= 0) Emit($"mov {Reg(A2)}, {Reg(a2)}");
            Emit($"call {label}");
            Emit($"mov {Reg(T0)}, $0F");
            _helpers.Add(label);
        }

        // ---------------------------------------------------------------- statements

        private void EmitStmt(Stmt s)
        {
            switch (s)
            {
                case BlockStmt block:
                    foreach (Stmt child in block.Stmts)
                        EmitStmt(child);
                    break;

                case ExprStmt es:
                    if (es.Expr.Type is StructType or ArrayType)
                        EmitAddress(es.Expr);
                    else
                        EmitValue(es.Expr);
                    break;

                case IfStmt ifs:
                {
                    EmitCond(ifs.Cond);
                    string end = Nl();
                    if (ifs.Else != null)
                    {
                        string els = Nl();
                        Emit($"jz {els}, {Reg(T0)}");
                        EmitStmt(ifs.Then);
                        Emit($"jmp {end}");
                        EmitLabel(els);
                        EmitStmt(ifs.Else);
                        EmitLabel(end);
                    }
                    else
                    {
                        Emit($"jz {end}, {Reg(T0)}");
                        EmitStmt(ifs.Then);
                        EmitLabel(end);
                    }
                    break;
                }

                case WhileStmt ws:
                {
                    string start = Nl();
                    string end = Nl();
                    _loops.Push(new LoopContext { Break = end, Continue = start });
                    EmitLabel(start);
                    EmitCond(ws.Cond);
                    Emit($"jz {end}, {Reg(T0)}");
                    EmitStmt(ws.Body);
                    Emit($"jmp {start}");
                    _loops.Pop();
                    EmitLabel(end);
                    break;
                }

                case DoWhileStmt ds:
                {
                    string start = Nl();
                    string end = Nl();
                    _loops.Push(new LoopContext { Break = end, Continue = start });
                    EmitLabel(start);
                    EmitStmt(ds.Body);
                    EmitCond(ds.Cond);
                    Emit($"jnz {start}, {Reg(T0)}");
                    _loops.Pop();
                    EmitLabel(end);
                    break;
                }

                case ForStmt fs:
                {
                    if (fs.Init != null) EmitStmt(fs.Init);
                    string start = Nl();
                    string inc = Nl();
                    string end = Nl();
                    _loops.Push(new LoopContext { Break = end, Continue = inc });
                    EmitLabel(start);
                    if (fs.Cond != null)
                    {
                        EmitCond(fs.Cond);
                        Emit($"jz {end}, {Reg(T0)}");
                    }
                    EmitStmt(fs.Body);
                    EmitLabel(inc);
                    if (fs.Inc != null)
                    {
                        if (fs.Inc.Type is StructType or ArrayType)
                            EmitAddress(fs.Inc);
                        else
                            EmitValue(fs.Inc);
                    }
                    Emit($"jmp {start}");
                    _loops.Pop();
                    EmitLabel(end);
                    break;
                }

                case ReturnStmt rs:
                    if (rs.Value != null)
                    {
                        EmitValue(rs.Value);
                        Emit($"mov {Reg(RV)}, {Reg(T0)}");
                    }
                    EmitEpilogue();
                    break;

                case BreakStmt:
                    Emit($"jmp {_loops.Peek().Break}");
                    break;

                case ContinueStmt:
                    Emit($"jmp {_loops.Peek().Continue}");
                    break;

                case DeclStmt ds:
                    EmitDecl(ds);
                    break;
            }
        }

        private void EmitDecl(DeclStmt ds)
        {
            if (ds.Init == null) return;
            if (ds.Symbol!.Type is ArrayType && ds.Init is StrExpr str)
            {
                EmitStringCopy(ds.Symbol, EnsureStringLabel(str), str.Value.Length);
                return;
            }
            EmitValue(ds.Init);
            Emit($"push {Reg(T0)}, DWORD");
            EmitAddressOfSymbol(ds.Symbol);
            Emit($"pop {Reg(T1)}, DWORD");
            EmitStoreReg(T0, T1, ds.Symbol.Type);
        }

        private void EmitCond(Expr e) => EmitValue(e);

        // ---------------------------------------------------------------- expressions

        private void EmitValue(Expr e)
        {
            switch (e)
            {
                case IntExpr ie:
                    Ldi(T0, ie.Value);
                    break;

                case StrExpr se:
                    LdiLabel(T0, EnsureStringLabel(se));
                    break;

                case VarExpr ve:
                    if (ve.Type is ArrayType or StructType)
                    {
                        EmitAddress(ve);
                        break;
                    }
                    EmitAddress(ve);
                    EmitLoadReg(T0, ve.Type!);
                    break;

                case UnaryExpr ue:
                    EmitUnary(ue);
                    break;

                case BinaryExpr be:
                    EmitBinary(be);
                    break;

                case AssignExpr ae:
                    EmitAssign(ae);
                    break;

                case CallExpr ce:
                    EmitCall(ce);
                    break;

                case IndexExpr ie:
                    EmitAddress(ie);
                    if (ie.Type is not StructType && ie.Type is not ArrayType)
                        EmitLoadReg(T0, ie.Type!);
                    break;

                case MemberExpr me:
                    EmitAddress(me);
                    if (me.Type is not StructType && me.Type is not ArrayType)
                        EmitLoadReg(T0, me.Type!);
                    break;

                case CastExpr ce:
                    EmitValue(ce.Operand);
                    NarrowValue(ce.Type!);
                    break;
            }
        }

        private void EmitUnary(UnaryExpr ue)
        {
            switch (ue.Op)
            {
                case "&":
                    EmitAddress(ue.Operand);
                    break;

                case "*":
                    EmitValue(ue.Operand);
                    if (ue.Type is not StructType && ue.Type is not ArrayType)
                        EmitLoadReg(T0, ue.Type!);
                    break;

                case "!":
                    EmitValue(ue.Operand);
                    Ldi(T1, 0);
                    Emit($"eq {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                    break;

                case "-":
                    EmitValue(ue.Operand);
                    Ldi(T1, 0);
                    Emit($"sub {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    break;

                case "~":
                    EmitValue(ue.Operand);
                    Ldi(T1, 0xFFFFFFFF);
                    Emit($"xor {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                    break;
            }
        }

        private void EmitBinary(BinaryExpr be)
        {
            switch (be.Op)
            {
                case "&&":
                {
                    EmitValue(be.Left);
                    string els = Nl();
                    string done = Nl();
                    Emit($"jz {els}, {Reg(T0)}");
                    EmitValue(be.Right);
                    Emit($"jz {els}, {Reg(T0)}");
                    Ldi(T0, 1);
                    Emit($"jmp {done}");
                    EmitLabel(els);
                    Ldi(T0, 0);
                    EmitLabel(done);
                    break;
                }

                case "||":
                {
                    EmitValue(be.Left);
                    string tr = Nl();
                    string done = Nl();
                    Emit($"jnz {tr}, {Reg(T0)}");
                    EmitValue(be.Right);
                    Emit($"jnz {tr}, {Reg(T0)}");
                    Ldi(T0, 0);
                    Emit($"jmp {done}");
                    EmitLabel(tr);
                    Ldi(T0, 1);
                    EmitLabel(done);
                    break;
                }

                case "<<":
                case ">>":
                    EmitShift(be);
                    break;

                default:
                    EmitArith(be);
                    break;
            }
        }

        private void EmitArith(BinaryExpr be)
        {
            if ((be.Op == "+" || be.Op == "-") && be.Left.Type is PtrType ptr)
            {
                EmitValue(be.Left);
                Emit($"push {Reg(T0)}, DWORD");
                EmitValue(be.Right);
                if (ptr.Pointee.Size > 1)
                {
                    Ldi(T1, ptr.Pointee.Size);
                    Emit($"mul {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                }
                Emit($"pop {Reg(T1)}, DWORD");
                Emit(be.Op == "+"
                    ? $"add {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"
                    : $"sub {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                return;
            }

            if (be.Op == "+" && be.Right.Type is PtrType ptr2)
            {
                EmitValue(be.Left);
                Emit($"push {Reg(T0)}, DWORD");
                EmitValue(be.Right);
                Emit($"pop {Reg(T1)}, DWORD");
                if (ptr2.Pointee.Size > 1)
                {
                    Ldi(T2, ptr2.Pointee.Size);
                    Emit($"mul {Reg(T1)}, {Reg(T2)}, {Reg(T1)}");
                }
                Emit($"add {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                return;
            }

            // generic integer binary: left -> T0 (pushed), right -> T0, left -> T1
            EmitValue(be.Left);
            Emit($"push {Reg(T0)}, DWORD");
            EmitValue(be.Right);
            Emit($"pop {Reg(T1)}, DWORD");

            bool isSigned = be.PromotedOperand?.IsSigned ?? false;

            switch (be.Op)
            {
                case "+": Emit($"add {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "-": Emit($"sub {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "*": Emit($"mul {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "/":
                    if (isSigned) EmitCallHelper("__sdiv", T1, T0, -1);
                    else Emit($"div {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    break;
                case "%":
                    if (isSigned) EmitCallHelper("__smod", T1, T0, -1);
                    else Emit($"mod {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    break;
                case "&": Emit($"and {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "|": Emit($"or {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "^": Emit($"xor {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;

                case "==": Emit($"eq {Reg(T1)}, {Reg(T0)}, {Reg(T0)}"); break;
                case "!=":
                    Emit($"eq {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    Ldi(T1, 1);
                    Emit($"xor {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                    break;

                case "<":
                case "<=":
                case ">":
                case ">=":
                {
                    string alu = be.Op switch
                    {
                        "<" => "lt",
                        "<=" => "lte",
                        ">" => "gt",
                        _ => "gte"
                    };
                    if (isSigned)
                    {
                        Ldi(T2, 0x80000000);
                        Emit($"xor {Reg(T1)}, {Reg(T2)}, {Reg(T1)}");
                        Emit($"xor {Reg(T0)}, {Reg(T2)}, {Reg(T0)}");
                    }
                    Emit($"{alu} {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    break;
                }
            }
        }

        private void EmitShift(BinaryExpr be)
        {
            if (be.Right is IntExpr cnt)
            {
                EmitValue(be.Left);
                EmitConstShift(be, cnt.Value);
                return;
            }

            EmitValue(be.Left);
            Emit($"push {Reg(T0)}, DWORD");
            EmitValue(be.Right);
            Emit($"pop {Reg(T1)}, DWORD");

            if (be.Op == "<<")
                EmitCallHelper("__shl", T1, T0, -1);
            else if (be.PromotedOperand?.IsSigned == true)
                EmitCallHelper("__sar", T1, T0, -1);
            else
                EmitCallHelper("__shr", T1, T0, -1);
        }

        private void EmitConstShift(BinaryExpr be, long count)
        {
            if (count < 0)
                throw new CompileError(be.Span, "negative shift count");
            if (count >= 32)
            {
                if (be.Op == ">>" && be.PromotedOperand?.IsSigned == true)
                {
                    Ldi(T1, 32);
                    EmitCallHelper("__sar", T0, T1, -1);
                }
                else
                {
                    Ldi(T0, 0);
                }
                return;
            }
            Ldi(T1, 1L << (int) count);
            if (be.Op == "<<")
                Emit($"mul {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
            else if (be.PromotedOperand?.IsSigned == true)
            {
                Ldi(T1, count);
                EmitCallHelper("__sar", T0, T1, -1);
            }
            else
                Emit($"div {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
        }

        private void EmitAssign(AssignExpr ae)
        {
            if (ae.Type is StructType st)
            {
                EmitAddress(ae.Target);
                Emit($"mov {Reg(A0)}, {Reg(T0)}");
                Emit($"push {Reg(A0)}, DWORD");
                EmitAddress(ae.Value);
                Emit($"mov {Reg(A1)}, {Reg(T0)}");
                Emit($"pop {Reg(A0)}, DWORD");
                Ldi(T0, st.Size);
                Emit($"mov {Reg(A2)}, {Reg(T0)}");
                Emit("call __memcpy");
                _helpers.Add("__memcpy");
                return;
            }

            EmitAddress(ae.Target);
            Emit($"push {Reg(T0)}, DWORD");
            EmitValue(ae.Value);
            NarrowValue(ae.Type!);
            Emit($"mov {Reg(T1)}, {Reg(T0)}");
            Emit($"pop {Reg(T0)}, DWORD");
            EmitStoreReg(T0, T1, ae.Type!);
            Emit($"mov {Reg(T0)}, {Reg(T1)}");
        }

        private void EmitCall(CallExpr ce)
        {
            FuncSymbol fs = ce.Symbol!;
            int n = ce.Args.Count;

            for (int i = n - 1; i >= 4; i--)
            {
                EmitValue(ce.Args[i]);
                Emit($"push {Reg(T0)}, DWORD");
            }

            int regN = Math.Min(4, n);
            for (int i = 0; i < regN; i++)
            {
                EmitValue(ce.Args[i]);
                Emit($"push {Reg(T0)}, DWORD");
            }

            for (int i = regN - 1; i >= 0; i--)
                Emit($"pop {Reg(A0 + i)}, DWORD");

            Emit($"call {fs.Label}");
            Emit($"mov {Reg(T0)}, $0F");
        }

        private void NarrowValue(Type t)
        {
            if (t is not PrimType p) return;
            switch (p.Size)
            {
                case 1:
                    Ldi(T2, 0xFF);
                    Emit($"and {Reg(T0)}, {Reg(T2)}, {Reg(T0)}");
                    if (p.IsSigned) EmitCallHelper("__sext8", T0, -1, -1);
                    break;
                case 2:
                    Ldi(T2, 0xFFFF);
                    Emit($"and {Reg(T0)}, {Reg(T2)}, {Reg(T0)}");
                    if (p.IsSigned) EmitCallHelper("__sext16", T0, -1, -1);
                    break;
            }
        }

        // ---------------------------------------------------------------- addresses

        private void EmitAddress(Expr e)
        {
            switch (e)
            {
                case VarExpr ve:
                    EmitAddressOfSymbol(ve.Symbol!);
                    break;

                case UnaryExpr ue when ue.Op == "*":
                    EmitValue(ue.Operand);
                    break;

                case IndexExpr ie:
                {
                    if (ie.Base.Type is ArrayType)
                        EmitAddress(ie.Base);
                    else
                        EmitValue(ie.Base);
                    Emit($"push {Reg(T0)}, DWORD");
                    EmitValue(ie.Index);
                    if (ie.Type!.Size > 1)
                    {
                        Ldi(T1, ie.Type.Size);
                        Emit($"mul {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                    }
                    Emit($"pop {Reg(T1)}, DWORD");
                    Emit($"add {Reg(T1)}, {Reg(T0)}, {Reg(T0)}");
                    break;
                }

                case MemberExpr me:
                {
                    if (me.Arrow)
                        EmitValue(me.Base);
                    else
                        EmitAddress(me.Base);
                    int offset = FieldOffset(me);
                    if (offset != 0)
                    {
                        Ldi(T1, offset);
                        Emit($"add {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
                    }
                    break;
                }

                case CastExpr ce:
                    EmitValue(ce.Operand);
                    break;

                default:
                    throw new CompileError(e.Span, "expression is not addressable");
            }
        }

        private void EmitAddressOfSymbol(Symbol sym)
        {
            if (sym.IsGlobal)
            {
                LdiLabel(T0, sym.GlobalLabel!);
                return;
            }
            Emit($"mov {Reg(T0)}, $1F");
            if (sym.Offset != 0)
            {
                Ldi(T1, sym.Offset);
                Emit($"add {Reg(T0)}, {Reg(T1)}, {Reg(T0)}");
            }
        }

        private static int FieldOffset(MemberExpr me)
        {
            Type structType = me.Arrow
                ? ((PtrType) me.Base.Type!).Pointee
                : me.Base.Type!;
            StructLayout layout = ((StructType) structType).Layout!;
            foreach (StructField f in layout.Fields)
                if (f.Name == me.Name)
                    return f.Offset;
            throw new CompileError(me.Span, $"struct '{layout.Name}' has no field '{me.Name}'");
        }

        // ---------------------------------------------------------------- loads / stores

        private void EmitLoadReg(int addrReg, Type t)
        {
            switch (t)
            {
                case PrimType p:
                    switch (p.Size)
                    {
                        case 1:
                            Emit($"ldb {Reg(T0)}, {Reg(addrReg)}");
                            if (p.IsSigned) EmitCallHelper("__sext8", T0, -1, -1);
                            break;
                        case 2:
                            Emit($"ldw {Reg(T0)}, {Reg(addrReg)}");
                            if (p.IsSigned) EmitCallHelper("__sext16", T0, -1, -1);
                            break;
                        default:
                            Emit($"ldd {Reg(T0)}, {Reg(addrReg)}");
                            break;
                    }
                    break;

                case PtrType:
                    Emit($"ldd {Reg(T0)}, {Reg(addrReg)}");
                    break;

                default:
                    throw new CompileError(new SourceSpan("", 0, 0), "cannot load a non-scalar value");
            }
        }

        private void EmitStoreReg(int addrReg, int valueReg, Type t)
        {
            switch (t.Size)
            {
                case 1: Emit($"stb {Reg(valueReg)}, {Reg(addrReg)}"); break;
                case 2: Emit($"stw {Reg(valueReg)}, {Reg(addrReg)}"); break;
                default: Emit($"std {Reg(valueReg)}, {Reg(addrReg)}"); break;
            }
        }

        private string EnsureStringLabel(StrExpr se)
        {
            if (se.Label.Length == 0)
            {
                se.Label = $"s__{_stringCounter}";
                _strings.Add((se.Label, se.Value));
                _stringCounter++;
            }
            return se.Label;
        }

        private void EmitStringCopy(Symbol sym, string label, int length)
        {
            EmitAddressOfSymbol(sym);
            Emit($"push {Reg(T0)}, DWORD");
            LdiLabel(T0, label);
            Emit($"push {Reg(T0)}, DWORD");
            Ldi(T0, length + 1);
            Emit($"pop {Reg(A1)}, DWORD");
            Emit($"pop {Reg(A0)}, DWORD");
            Emit($"mov {Reg(A2)}, {Reg(T0)}");
            Emit("call __memcpy");
            _helpers.Add("__memcpy");
        }

        // ---------------------------------------------------------------- data

        private void EmitData()
        {
            foreach ((string label, string text) in _strings)
            {
                EmitLabel(label);
                Emit($"%asciz \"{EscapeAsmString(text)}\"");
            }

            foreach (TopLevel item in _resolver.Items)
            {
                if (item is not GlobalVarDecl gd) continue;
                Symbol sym = _resolver.Globals[gd.Name];
                if (sym.Type.Align > 1)
                    Emit($"%align {sym.Type.Align}");
                EmitLabel(sym.GlobalLabel!);

                if (sym.Type is ArrayType at)
                {
                    if (gd.Init is StrExpr str)
                    {
                        Emit($"%asciz \"{EscapeAsmString(str.Value)}\"");
                        int used = str.Value.Length + 1;
                        if (at.Length > used)
                            Emit($"%fill {at.Length - used}");
                    }
                    else
                    {
                        Emit($"%fill {at.Size}");
                    }
                }
                else if (sym.Type is StructType)
                {
                    Emit($"%fill {sym.Type.Size}");
                }
                else
                {
                    long value = gd.Init != null ? _resolver.ConstValue(gd.Init) : 0;
                    Emit($"{DataDirective(sym.Type.Size)} {value}");
                }
            }
        }

        private static string DataDirective(int size)
            => size switch { 1 => "byte", 2 => "word", _ => "dword" };

        private static string EscapeAsmString(string s)
        {
            StringBuilder sb = new();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\0': sb.Append("\\0"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- call walker

        private static IEnumerable<string> WalkCalls(Stmt s)
        {
            List<string> calls = new();
            WalkStmt(s, calls);
            return calls;
        }

        private static void WalkStmt(Stmt s, List<string> calls)
        {
            switch (s)
            {
                case BlockStmt b:
                    foreach (Stmt c in b.Stmts) WalkStmt(c, calls);
                    break;
                case ExprStmt es:
                    WalkExpr(es.Expr, calls);
                    break;
                case IfStmt i:
                    WalkExpr(i.Cond, calls);
                    WalkStmt(i.Then, calls);
                    if (i.Else != null) WalkStmt(i.Else, calls);
                    break;
                case WhileStmt w:
                    WalkExpr(w.Cond, calls);
                    WalkStmt(w.Body, calls);
                    break;
                case DoWhileStmt d:
                    WalkStmt(d.Body, calls);
                    WalkExpr(d.Cond, calls);
                    break;
                case ForStmt f:
                    if (f.Init != null) WalkStmt(f.Init, calls);
                    if (f.Cond != null) WalkExpr(f.Cond, calls);
                    WalkStmt(f.Body, calls);
                    if (f.Inc != null) WalkExpr(f.Inc, calls);
                    break;
                case ReturnStmt r:
                    if (r.Value != null) WalkExpr(r.Value, calls);
                    break;
                case DeclStmt ds:
                    if (ds.Init != null) WalkExpr(ds.Init, calls);
                    break;
            }
        }

        private static void WalkExpr(Expr e, List<string> calls)
        {
            switch (e)
            {
                case UnaryExpr u:
                    WalkExpr(u.Operand, calls);
                    break;
                case BinaryExpr b:
                    WalkExpr(b.Left, calls);
                    WalkExpr(b.Right, calls);
                    break;
                case AssignExpr a:
                    WalkExpr(a.Target, calls);
                    WalkExpr(a.Value, calls);
                    break;
                case CallExpr c:
                    calls.Add(c.Name);
                    foreach (Expr arg in c.Args) WalkExpr(arg, calls);
                    break;
                case IndexExpr ix:
                    WalkExpr(ix.Base, calls);
                    WalkExpr(ix.Index, calls);
                    break;
                case MemberExpr m:
                    WalkExpr(m.Base, calls);
                    break;
                case CastExpr c:
                    WalkExpr(c.Operand, calls);
                    break;
            }
        }
    }
}
