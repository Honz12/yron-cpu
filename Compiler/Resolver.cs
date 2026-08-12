namespace Compiler
{
    public sealed class Scope
    {
        public Dictionary<string, Symbol> Map = new();
        public Scope? Parent;

        public bool TryGet(string name, out Symbol symbol)
        {
            for (Scope? s = this; s != null; s = s.Parent)
            {
                if (s.Map.TryGetValue(name, out symbol!))
                    return true;
            }
            symbol = null!;
            return false;
        }
    }

    public sealed class Resolver
    {
        private readonly List<TopLevel> _items;
        private readonly Dictionary<string, StructLayout> _structs = new();
        private readonly Dictionary<string, FuncSymbol> _funcs = new();
        private readonly Dictionary<string, Symbol> _globals = new();
        private FuncSymbol? _currentFunc;
        private int _nextLocalOffset;

        public IReadOnlyDictionary<string, StructLayout> Structs => _structs;
        public IReadOnlyDictionary<string, FuncSymbol> Funcs => _funcs;
        public IReadOnlyDictionary<string, Symbol> Globals => _globals;
        public IReadOnlyList<TopLevel> Items => _items;
        public FuncSymbol? Main { get; private set; }

        public Resolver(List<TopLevel> items) => _items = items;

        public void Resolve()
        {
            RegisterStructs();
            LayoutStructs();
            CollectFunctionsAndGlobals();
            ValidateMain();
            ResolveGlobals();
            foreach (FuncSymbol fs in _funcs.Values)
                ResolveFunctionBody(fs);
        }

        public long ConstValue(Expr e) => EvaluateConst(e, new SourceSpan("", 0, 0));

        // ---------------------------------------------------------------- structs

        private void RegisterStructs()
        {
            foreach (TopLevel item in _items)
            {
                if (item is StructDecl sd)
                {
                    if (_structs.ContainsKey(sd.Name) || _funcs.ContainsKey(sd.Name) || _globals.ContainsKey(sd.Name))
                        throw new CompileError(sd.Span, $"duplicate symbol '{sd.Name}'");
                    _structs[sd.Name] = new StructLayout { Name = sd.Name };
                }
            }
        }

        private void LayoutStructs()
        {
            foreach (TopLevel item in _items)
            {
                if (item is not StructDecl sd) continue;
                StructLayout layout = _structs[sd.Name];
                int offset = 0;
                int maxAlign = 1;
                foreach ((string fieldName, TypeNode fieldTypeNode) in sd.Fields)
                {
                    if (layout.Fields.Any(f => f.Name == fieldName))
                        throw new CompileError(sd.Span, $"duplicate field '{fieldName}' in struct '{sd.Name}'");
                    Type ft = ResolveType(fieldTypeNode, sd.Span, allowVoid: false, structByValue: true);
                    if (ft is StructType st && st.Layout == null)
                        throw new CompileError(sd.Span, $"struct '{st.Name}' used by value before it is defined (declare it earlier or use a pointer)");
                    offset = AlignUp(offset, ft.Align);
                    layout.Fields.Add(new StructField { Name = fieldName, Type = ft, Offset = offset });
                    offset += ft.Size;
                    maxAlign = Math.Max(maxAlign, ft.Align);
                }
                layout.Align = maxAlign;
                layout.Size = Math.Max(1, AlignUp(offset, maxAlign));
            }
        }

        private static int AlignUp(int value, int align) => (value + align - 1) / align * align;

        // ---------------------------------------------------------------- top level collection

        private void CollectFunctionsAndGlobals()
        {
            foreach (TopLevel item in _items)
            {
                if (item is FuncDecl fd)
                {
                    if (_structs.ContainsKey(fd.Name) || _funcs.ContainsKey(fd.Name) || _globals.ContainsKey(fd.Name))
                        throw new CompileError(fd.Span, $"duplicate symbol '{fd.Name}'");
                    Type ret = ResolveType(fd.Return, fd.Span, allowVoid: true, structByValue: false);
                    if (ret is StructType)
                        throw new CompileError(fd.Span, $"function '{fd.Name}' cannot return a struct by value (return a pointer instead)");
                    if (ret is ArrayType)
                        throw new CompileError(fd.Span, $"function '{fd.Name}' cannot return an array");

                    FuncSymbol fs = new() { Name = fd.Name, Return = ret, Label = "f__" + fd.Name };
                    foreach ((string pname, TypeNode ptype) in fd.Params)
                    {
                        Type pt = ResolveType(ptype, fd.Span, allowVoid: false, structByValue: false);
                        if (pt is StructType)
                            throw new CompileError(fd.Span, $"parameter '{pname}' of '{fd.Name}' must be a pointer (structs are passed by reference)");
                        if (fs.Params.Any(p => p.Name == pname))
                            throw new CompileError(fd.Span, $"duplicate parameter '{pname}' in function '{fd.Name}'");
                        fs.Params.Add(new ParamInfo { Name = pname, Type = pt });
                    }
                    _funcs[fd.Name] = fs;
                }
                else if (item is GlobalVarDecl gd)
                {
                    if (_structs.ContainsKey(gd.Name) || _funcs.ContainsKey(gd.Name) || _globals.ContainsKey(gd.Name))
                        throw new CompileError(gd.Span, $"duplicate symbol '{gd.Name}'");
                    Type t = ResolveType(gd.Type, gd.Span, allowVoid: false, structByValue: true);
                    _globals[gd.Name] = new Symbol { Name = gd.Name, Type = t, IsGlobal = true, GlobalLabel = "g__" + gd.Name };
                }
            }
        }

        private void ValidateMain()
        {
            if (!_funcs.TryGetValue("main", out FuncSymbol? main))
                throw new CompileError(new SourceSpan("", 0, 0), "no 'main' function defined");
            if (main.Params.Count != 0)
                throw new CompileError(new SourceSpan("", 0, 0), "main must not take parameters");
            if (main.Return is not PrimType)
                throw new CompileError(new SourceSpan("", 0, 0), "main must return an integer type");
            Main = main;
        }

        // ---------------------------------------------------------------- type resolution

        private Type ResolveType(TypeNode node, SourceSpan span, bool allowVoid, bool structByValue)
        {
            switch (node)
            {
                case PrimTypeNode pt:
                    if (pt.Name == "void")
                    {
                        if (allowVoid) return PrimType.Void;
                        throw new CompileError(span, "'void' is not a valid variable type");
                    }
                    return pt.Name switch
                    {
                        "u8" => PrimType.U8,
                        "u16" => PrimType.U16,
                        "u32" => PrimType.U32,
                        "i8" => PrimType.I8,
                        "i16" => PrimType.I16,
                        "i32" => PrimType.I32,
                        "char" => PrimType.I8,
                        _ => throw new CompileError(span, $"unknown type '{pt.Name}'")
                    };

                case NamedTypeNode nt:
                    if (_structs.TryGetValue(nt.Name, out StructLayout? layout))
                        return new StructType(nt.Name) { Layout = layout };
                    throw new CompileError(span, $"unknown type '{nt.Name}'");

                case PointerTypeNode pn:
                    return new PtrType(ResolveType(pn.Inner, span, allowVoid: false, structByValue: false));

                case ArrayTypeNode an:
                    Type elem = ResolveType(an.Elem, span, allowVoid: false, structByValue: false);
                    if (elem is ArrayType)
                        throw new CompileError(span, "multidimensional arrays are not supported");
                    if (an.Length > int.MaxValue)
                        throw new CompileError(span, "array too large");
                    try
                    {
                        return new ArrayType(elem, (int) an.Length);
                    }
                    catch (OverflowException)
                    {
                        throw new CompileError(span, "array too large");
                    }

                default:
                    throw new CompileError(span, "invalid type");
            }
        }

        // ---------------------------------------------------------------- globals

        private void ResolveGlobals()
        {
            foreach (TopLevel item in _items)
            {
                if (item is not GlobalVarDecl gd) continue;
                Symbol g = _globals[gd.Name];
                if (gd.Init == null) continue;
                ResolveGlobalInit(gd, g);
            }
        }

        private void ResolveGlobalInit(GlobalVarDecl gd, Symbol g)
        {
            if (g.Type is ArrayType at)
            {
                if (at.Elem != PrimType.U8 || gd.Init is not StrExpr str)
                    throw new CompileError(gd.Span, "only 'u8 name[N] = \"...\"' global initializers are supported");
                int need = str.Value.Length + 1;
                if (at.Length < need)
                    throw new CompileError(gd.Span, $"string initializer needs {need} bytes but array is only {at.Length}");
                return;
            }
            if (g.Type is StructType)
                throw new CompileError(gd.Span, "struct globals cannot have initializers (assign fields in main)");

            Type t = ResolveExpr(gd.Init!, new Scope());
            CheckConvertible(gd.Init!, t, g.Type, gd.Span);
            EvaluateConst(gd.Init!, gd.Span);
        }

        // ---------------------------------------------------------------- functions

        private void ResolveFunctionBody(FuncSymbol fs)
        {
            _currentFunc = fs;
            FuncDecl? decl = _items.OfType<FuncDecl>().FirstOrDefault(f => f.Name == fs.Name);
            if (decl == null) return;

            Scope scope = new();
            int regParams = Math.Min(4, fs.Params.Count);
            int nextOffset = 4;
            for (int i = 0; i < fs.Params.Count; i++)
            {
                ParamInfo p = fs.Params[i];
                Symbol sym = new() { Name = p.Name, Type = p.Type };
                if (i < regParams)
                {
                    p.InRegister = true;
                    p.Offset = nextOffset;
                    sym.Offset = -nextOffset;
                    nextOffset += 4;
                }
                else
                {
                    p.InRegister = false;
                    p.Offset = 8 + 4 * (i - regParams);
                    sym.Offset = p.Offset;
                    sym.IsStackParam = true;
                }
                scope.Map[p.Name] = sym;
            }

            _nextLocalOffset = nextOffset;
            ResolveBlock(decl.Body, scope, loopDepth: 0);
            fs.RegParams = regParams;
            fs.StackParams = fs.Params.Count - regParams;
            fs.FrameSize = _nextLocalOffset;
        }

        private void ResolveBlock(BlockStmt block, Scope parent, int loopDepth)
        {
            Scope scope = new() { Parent = parent };
            foreach (Stmt s in block.Stmts)
                ResolveStmt(s, scope, loopDepth);
        }

        private void ResolveStmt(Stmt s, Scope scope, int loopDepth)
        {
            switch (s)
            {
                case BlockStmt block:
                    ResolveBlock(block, scope, loopDepth);
                    break;

                case ExprStmt es:
                    ResolveExpr(es.Expr, scope);
                    break;

                case IfStmt ifs:
                    CheckInteger(ResolveExpr(ifs.Cond, scope), ifs.Span, "condition");
                    ResolveStmt(ifs.Then, scope, loopDepth);
                    if (ifs.Else != null) ResolveStmt(ifs.Else, scope, loopDepth);
                    break;

                case WhileStmt ws:
                    CheckInteger(ResolveExpr(ws.Cond, scope), ws.Span, "condition");
                    ResolveStmt(ws.Body, scope, loopDepth + 1);
                    break;

                case DoWhileStmt ds:
                    ResolveStmt(ds.Body, scope, loopDepth + 1);
                    CheckInteger(ResolveExpr(ds.Cond, scope), ds.Span, "condition");
                    break;

                case ForStmt fs:
                    if (fs.Init != null) ResolveStmt(fs.Init, scope, loopDepth);
                    if (fs.Cond != null) CheckInteger(ResolveExpr(fs.Cond, scope), fs.Span, "condition");
                    ResolveStmt(fs.Body, scope, loopDepth + 1);
                    if (fs.Inc != null) ResolveExpr(fs.Inc, scope);
                    break;

                case ReturnStmt rs:
                    if (rs.Value != null)
                    {
                        Type t = ResolveExpr(rs.Value, scope);
                        if (_currentFunc!.Return == PrimType.Void)
                            throw new CompileError(rs.Span, "void function cannot return a value");
                        if (t is StructType)
                            throw new CompileError(rs.Span, "cannot return a struct by value");
                        CheckConvertible(rs.Value, t, _currentFunc.Return, rs.Span);
                    }
                    else if (_currentFunc!.Return != PrimType.Void)
                    {
                        // bare 'return;' in a non-void function is allowed (value is undefined)
                    }
                    break;

                case BreakStmt:
                    if (loopDepth == 0)
                        throw new CompileError(s.Span, "'break' outside of a loop");
                    break;

                case ContinueStmt:
                    if (loopDepth == 0)
                        throw new CompileError(s.Span, "'continue' outside of a loop");
                    break;

                case DeclStmt ds:
                    ResolveLocalDecl(ds, scope);
                    break;
            }
        }

        private void ResolveLocalDecl(DeclStmt ds, Scope scope)
        {
            Type t = ResolveType(ds.Type, ds.Span, allowVoid: false, structByValue: true);
            if (scope.Map.ContainsKey(ds.Name))
                throw new CompileError(ds.Span, $"redefinition of '{ds.Name}' in the same scope");
            if (_globals.ContainsKey(ds.Name) || _funcs.ContainsKey(ds.Name))
                throw new CompileError(ds.Span, $"name '{ds.Name}' shadows a global");

            Symbol sym = new() { Name = ds.Name, Type = t, Offset = -(_nextLocalOffset + SlotSize(t)) };
            _nextLocalOffset += SlotSize(t);
            scope.Map[ds.Name] = sym;
            ds.Symbol = sym;

            if (ds.Init == null) return;

            if (t is ArrayType at)
            {
                if (at.Elem != PrimType.U8 || ds.Init is not StrExpr str)
                    throw new CompileError(ds.Span, "only 'u8 name[N] = \"...\"' local initializers are supported");
                int need = str.Value.Length + 1;
                if (at.Length < need)
                    throw new CompileError(ds.Span, $"string initializer needs {need} bytes but array is only {at.Length}");
                return;
            }
            if (t is StructType)
                throw new CompileError(ds.Span, "struct variables have no initializer; assign fields after declaring");

            Type it = ResolveExpr(ds.Init, scope);
            CheckConvertible(ds.Init, it, t, ds.Span);
        }

        private static int SlotSize(Type t)
        {
            int size = t.Size;
            return size <= 0 ? 4 : ((size + 3) / 4) * 4;
        }

        // ---------------------------------------------------------------- expressions

        private Type ResolveExpr(Expr e, Scope scope)
        {
            switch (e)
            {
                case IntExpr ie:
                    ie.Type = ie.Value < 0 ? PrimType.I32 : PrimType.U32;
                    return ie.Type;

                case StrExpr se:
                    se.Type = new PtrType(PrimType.U8);
                    return se.Type;

                case VarExpr ve:
                {
                    if (scope.TryGet(ve.Name, out Symbol? sym))
                    {
                        ve.Symbol = sym;
                        ve.Type = sym.Type;
                        return ve.Type;
                    }
                    if (_globals.TryGetValue(ve.Name, out Symbol? g))
                    {
                        ve.Symbol = g;
                        ve.Type = g.Type;
                        return ve.Type;
                    }
                    if (_funcs.ContainsKey(ve.Name))
                        throw new CompileError(ve.Span, $"function '{ve.Name}' used as a variable");
                    throw new CompileError(ve.Span, $"undefined variable '{ve.Name}'");
                }

                case UnaryExpr ue:
                    return ResolveUnary(ue, scope);

                case BinaryExpr be:
                    return ResolveBinary(be, scope);

                case AssignExpr ae:
                {
                    Type target = ResolveExpr(ae.Target, scope);
                    if (!IsLValue(ae.Target))
                        throw new CompileError(ae.Span, "assignment target is not assignable");
                    if (target is ArrayType)
                        throw new CompileError(ae.Span, "cannot assign to an array (use elements or a loop)");
                    Type value = ResolveExpr(ae.Value, scope);
                    if (target is StructType st1)
                    {
                        if (value is StructType st2 && st2.Name == st1.Name)
                        {
                            ae.Type = target;
                            return ae.Type;
                        }
                        throw new CompileError(ae.Span, $"cannot assign '{value}' to struct '{st1.Name}'");
                    }
                    if (value is StructType)
                        throw new CompileError(ae.Span, "cannot assign a struct to a non-struct value");
                    CheckConvertible(ae.Value, value, target, ae.Span);
                    ae.Type = target;
                    return ae.Type;
                }

                case CallExpr ce:
                {
                    if (!_funcs.TryGetValue(ce.Name, out FuncSymbol? fs))
                        throw new CompileError(ce.Span, $"call to undefined function '{ce.Name}'");
                    if (ce.Args.Count != fs.Params.Count)
                        throw new CompileError(ce.Span, $"function '{ce.Name}' expects {fs.Params.Count} argument{(fs.Params.Count != 1 ? "s" : "")}, got {ce.Args.Count}");
                    for (int i = 0; i < ce.Args.Count; i++)
                    {
                        Type at = ResolveExpr(ce.Args[i], scope);
                        CheckConvertible(ce.Args[i], at, fs.Params[i].Type, ce.Span);
                    }
                    ce.Symbol = fs;
                    ce.Type = fs.Return;
                    return ce.Type;
                }

                case IndexExpr ie:
                {
                    Type bt = ResolveExpr(ie.Base, scope);
                    Type it = ResolveExpr(ie.Index, scope);
                    if (bt is ArrayType arr)
                    {
                        CheckInteger(it, ie.Span, "array index");
                        ie.Type = arr.Elem;
                    }
                    else if (bt is PtrType ptr)
                    {
                        CheckInteger(it, ie.Span, "index");
                        ie.Type = ptr.Pointee;
                    }
                    else
                    {
                        throw new CompileError(ie.Span, "cannot index a non-array, non-pointer value");
                    }
                    return ie.Type;
                }

                case MemberExpr me:
                    return ResolveMember(me, scope);

                case CastExpr ce:
                {
                    Type target = ResolveType(ce.TargetType, ce.Span, allowVoid: false, structByValue: false);
                    Type operand = ResolveExpr(ce.Operand, scope);
                    if (target is ArrayType || target is StructType)
                        throw new CompileError(ce.Span, $"cannot cast to '{target}'");
                    if (operand is ArrayType || operand is StructType)
                        throw new CompileError(ce.Span, $"cannot cast a value of type '{operand}'");
                    ce.Type = target;
                    return ce.Type;
                }

                default:
                    throw new CompileError(e.Span, "invalid expression");
            }
        }

        private Type ResolveUnary(UnaryExpr ue, Scope scope)
        {
            switch (ue.Op)
            {
                case "&":
                {
                    Type t = ResolveExpr(ue.Operand, scope);
                    if (!IsLValue(ue.Operand))
                        throw new CompileError(ue.Span, "cannot take the address of a non-lvalue");
                    if (t is ArrayType at)
                        ue.Type = new PtrType(at.Elem);
                    else
                        ue.Type = new PtrType(t);
                    return ue.Type;
                }
                case "*":
                {
                    Type t = ResolveExpr(ue.Operand, scope);
                    if (t is PtrType pt)
                    {
                        ue.Type = pt.Pointee;
                        return ue.Type;
                    }
                    throw new CompileError(ue.Span, "cannot dereference a non-pointer");
                }
                case "!":
                {
                    Type t = ResolveExpr(ue.Operand, scope);
                    CheckInteger(t, ue.Span, "operand of '!'");
                    ue.Type = PrimType.U32;
                    return ue.Type;
                }
                case "-":
                {
                    if (ue.Operand is IntExpr nie)
                    {
                        long v = -nie.Value;
                        ue.Type = v < 0 ? PrimType.I32 : PrimType.U32;
                        return ue.Type;
                    }
                    Type t = ResolveExpr(ue.Operand, scope);
                    CheckInteger(t, ue.Span, "operand of unary '-'");
                    ue.Type = PromotedInt(t);
                    return ue.Type;
                }
                case "~":
                {
                    Type t = ResolveExpr(ue.Operand, scope);
                    CheckInteger(t, ue.Span, "operand of '~'");
                    ue.Type = PromotedInt(t);
                    return ue.Type;
                }
                default:
                    throw new CompileError(ue.Span, $"unknown unary operator '{ue.Op}'");
            }
        }

        private Type ResolveBinary(BinaryExpr be, Scope scope)
        {
            switch (be.Op)
            {
                case "&&":
                case "||":
                {
                    CheckInteger(ResolveExpr(be.Left, scope), be.Span, "operand of '&&'/'||'");
                    CheckInteger(ResolveExpr(be.Right, scope), be.Span, "operand of '&&'/'||'");
                    be.Type = PrimType.U32;
                    return be.Type;
                }

                case "+":
                case "-":
                {
                    Type lt = ResolveExpr(be.Left, scope);
                    Type rt = ResolveExpr(be.Right, scope);
                    if (lt is PtrType && rt.IsInteger)
                    {
                        be.Type = lt;
                        return be.Type;
                    }
                    if (rt is PtrType && lt.IsInteger)
                    {
                        be.Type = rt;
                        return be.Type;
                    }
                    return ResolveCommon(be, lt, rt, scope);
                }

                case "*":
                case "/":
                case "%":
                case "&":
                case "|":
                case "^":
                {
                    Type lt = ResolveExpr(be.Left, scope);
                    Type rt = ResolveExpr(be.Right, scope);
                    return ResolveCommon(be, lt, rt, scope);
                }

                case "<<":
                case ">>":
                {
                    Type lt = ResolveExpr(be.Left, scope);
                    Type rt = ResolveExpr(be.Right, scope);
                    CheckInteger(lt, be.Span, "shift operand");
                    CheckInteger(rt, be.Span, "shift count");
                    be.PromotedOperand = PromotedInt(lt);
                    be.Type = be.PromotedOperand;
                    return be.Type;
                }

                case "<":
                case "<=":
                case ">":
                case ">=":
                {
                    Type lt = ResolveExpr(be.Left, scope);
                    Type rt = ResolveExpr(be.Right, scope);
                    be.PromotedOperand = CommonInteger(lt, rt, be.Span);
                    be.Type = PrimType.U32;
                    return be.Type;
                }

                case "==":
                case "!=":
                {
                    Type lt = ResolveExpr(be.Left, scope);
                    Type rt = ResolveExpr(be.Right, scope);
                    if (lt.IsInteger && rt.IsInteger)
                        be.PromotedOperand = CommonInteger(lt, rt, be.Span);
                    else if (lt is PtrType && rt is PtrType)
                        be.PromotedOperand = null;
                    else if (lt is PtrType && rt.IsInteger && rt == PrimType.U32)
                        be.PromotedOperand = null;
                    else if (rt is PtrType && lt.IsInteger && lt == PrimType.U32)
                        be.PromotedOperand = null;
                    else
                        throw new CompileError(be.Span, $"cannot compare '{lt}' and '{rt}'");
                    be.Type = PrimType.U32;
                    return be.Type;
                }

                default:
                    throw new CompileError(be.Span, $"unknown operator '{be.Op}'");
            }
        }

        private Type ResolveCommon(BinaryExpr be, Type lt, Type rt, Scope scope)
        {
            if (lt.IsInteger && rt.IsInteger)
            {
                be.PromotedOperand = CommonInteger(lt, rt, be.Span);
                be.Type = be.PromotedOperand;
                return be.Type;
            }
            throw new CompileError(be.Span, $"operator '{be.Op}' requires integer operands, got '{lt}' and '{rt}'");
        }

        private Type ResolveMember(MemberExpr me, Scope scope)
        {
            Type bt = ResolveExpr(me.Base, scope);
            Type structType = bt;
            if (me.Arrow)
            {
                if (bt is not PtrType pt)
                    throw new CompileError(me.Span, "'->' requires a pointer to a struct");
                structType = pt.Pointee;
            }
            if (structType is not StructType st || st.Layout == null)
                throw new CompileError(me.Span, $"'{me.Name}' accessed on a non-struct value");
            foreach (StructField f in st.Layout.Fields)
            {
                if (f.Name == me.Name)
                {
                    me.Type = f.Type;
                    return me.Type;
                }
            }
            throw new CompileError(me.Span, $"struct '{st.Name}' has no field '{me.Name}'");
        }

        // ---------------------------------------------------------------- helpers

        private Type PromotedInt(Type t)
        {
            if (t is not PrimType p || !p.IsInteger)
                throw new CompileError(new SourceSpan("", 0, 0), $"expected an integer, got '{t}'");
            if (p == PrimType.U32) return PrimType.U32;
            if (p.IsSigned) return PrimType.I32;
            return PrimType.U32;
        }

        private Type CommonInteger(Type a, Type b, SourceSpan span)
        {
            if (!a.IsInteger || !b.IsInteger)
                throw new CompileError(span, $"operator requires integer operands, got '{a}' and '{b}'");
            if (a == PrimType.U32 || b == PrimType.U32) return PrimType.U32;
            if (a.IsSigned || b.IsSigned) return PrimType.I32;
            return PrimType.U32;
        }

        private static void CheckInteger(Type t, SourceSpan span, string what)
        {
            if (!t.IsInteger)
                throw new CompileError(span, $"{what} must be an integer, got '{t}'");
        }

        private static bool IsLValue(Expr e)
            => e is VarExpr or IndexExpr or MemberExpr
               || (e is UnaryExpr ue && ue.Op == "*");

        private void CheckConvertible(Expr from, Type fromType, Type to, SourceSpan span)
        {
            if (fromType == to) return;

            if (to is PtrType pt && fromType is ArrayType at && at.Elem == pt.Pointee)
                return;

            if (to is PtrType && fromType is PtrType)
                return;

            if (from is IntExpr lit && to.IsInteger && Fits(lit.Value, to))
                return;

            if (fromType.IsInteger && to.IsInteger)
            {
                if (fromType.Size < to.Size)
                    return;
            }

            throw new CompileError(span, $"cannot convert '{fromType}' to '{to}'");
        }

        private static bool Fits(long value, Type t)
        {
            if (t == PrimType.U8) return value >= 0 && value <= 255;
            if (t == PrimType.U16) return value >= 0 && value <= 65535;
            if (t == PrimType.U32) return value >= 0 && value <= uint.MaxValue;
            if (t == PrimType.I8) return value >= sbyte.MinValue && value <= sbyte.MaxValue;
            if (t == PrimType.I16) return value >= short.MinValue && value <= short.MaxValue;
            if (t == PrimType.I32) return value >= int.MinValue && value <= int.MaxValue;
            return false;
        }

        // ---------------------------------------------------------------- constant evaluation

        private long EvaluateConst(Expr e, SourceSpan span)
        {
            switch (e)
            {
                case IntExpr ie:
                    return ie.Value;
                case UnaryExpr ue:
                {
                    long v = EvaluateConst(ue.Operand, span);
                    return ue.Op switch
                    {
                        "-" => -v,
                        "~" => ~v,
                        "!" => v == 0 ? 1 : 0,
                        _ => throw new CompileError(span, "not a constant expression")
                    };
                }
                case BinaryExpr be:
                {
                    long l = EvaluateConst(be.Left, span);
                    long r = EvaluateConst(be.Right, span);
                    return be.Op switch
                    {
                        "+" => l + r,
                        "-" => l - r,
                        "*" => l * r,
                        "/" => r == 0 ? throw new CompileError(span, "division by zero in constant expression") : l / r,
                        "%" => r == 0 ? throw new CompileError(span, "division by zero in constant expression") : l % r,
                        "<<" => l << (int) r,
                        ">>" => l >> (int) r,
                        "&" => l & r,
                        "|" => l | r,
                        "^" => l ^ r,
                        _ => throw new CompileError(span, "not a constant expression")
                    };
                }
                default:
                    throw new CompileError(span, "not a constant expression");
            }
        }
    }
}
