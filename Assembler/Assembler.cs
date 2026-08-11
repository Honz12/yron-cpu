using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Assembler
{
    public static class Assembler
    {
        public const int MaxRomSize = 64 * 1024;

        private readonly record struct SourceLine(string File, int Line, string Text);

        private sealed class Token
        {
            public string File = "";
            public int Line;
            public string? Label;
            public string? LocalScope;
            public string? Opcode;
            public bool IsDirective;
            public List<string> Operands = new();
        }

        private static string Where(string file, int line)
            => file.Length > 0 ? $"{file}:{line}" : $"line {line}";

        private static string Where(Token token)
            => Where(token.File, token.Line);

        public static byte[] Assemble(string source, string? sourcePath = null)
        {
            List<Token> tokens = Preprocess(source, sourcePath);
            Dictionary<string, int> labels = BuildLabels(tokens, out HashSet<string> externs);
            if (externs.Count > 0)
                throw new Exception("%extern requires library output (.yrl file)");
            return Emit(tokens, labels);
        }

        public static LibraryFile AssembleLibrary(string source, string? sourcePath = null)
        {
            List<Token> tokens = Preprocess(source, sourcePath);
            Dictionary<string, int> labels = BuildLabels(tokens, out HashSet<string> externs);
            List<(string Name, int Offset)> references = new();
            byte[] binary = Emit(tokens, labels, externs, references);
            return new LibraryFile(labels, references, binary);
        }

        private static List<Token> Preprocess(string source, string? sourcePath)
        {
            string file = string.IsNullOrEmpty(sourcePath) ? "" : Path.GetFullPath(sourcePath);
            string baseDir = file.Length > 0 ? Path.GetDirectoryName(file) ?? "" : Directory.GetCurrentDirectory();

            List<SourceLine> lines = new Preprocessor().Process(source, file, baseDir);
            return Parse(lines);
        }

        // ---------------------------------------------------------------- preprocessor

        private sealed class Preprocessor
        {
            private readonly Dictionary<string, string> _macros = new();
            private readonly HashSet<string> _included = new();

            public List<SourceLine> Process(string source, string file, string baseDir)
            {
                if (file.Length > 0) _included.Add(file);

                List<SourceLine> output = new();
                ProcessLines(source.Split('\n'), file, baseDir, output);
                return output;
            }

            private void ProcessLines(string[] lines, string file, string baseDir, List<SourceLine> output)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    int lineNo = i + 1;
                    string text = StripComment(lines[i]).Trim();
                    if (text.Length == 0) continue;

                    string first = FirstWord(text);
                    if (first.Length > 1 && first[0] == '%')
                    {
                        switch (first.ToLowerInvariant())
                        {
                            case "%include":
                                IncludeFile(ParseIncludePath(text, file, lineNo), baseDir, output, file, lineNo);
                                continue;
                            case "%define":
                                DefineMacro(text, file, lineNo);
                                continue;
                        }
                    }

                    output.Add(new SourceLine(file, lineNo, ExpandMacros(text)));
                }
            }

            private void IncludeFile(string includePath, string baseDir, List<SourceLine> output, string file, int lineNo)
            {
                string full = Path.IsPathRooted(includePath)
                    ? includePath
                    : ResolveInclude(Path.Combine(baseDir, includePath), includePath);

                full = Path.GetFullPath(full);

                if (!File.Exists(full))
                    throw new Exception($"{Where(file, lineNo)}: include file '{includePath}' not found");

                if (!_included.Add(full)) return;

                string incDir = Path.GetDirectoryName(full) ?? baseDir;
                ProcessLines(File.ReadAllLines(full), full, incDir, output);
            }

            private static string ResolveInclude(string relativeToCurrent, string original)
                => File.Exists(relativeToCurrent) ? relativeToCurrent : original;

            private void DefineMacro(string text, string file, int lineNo)
            {
                string rest = text.Substring(FirstWord(text).Length).Trim();
                Match match = Regex.Match(rest, @"^([A-Za-z_][A-Za-z0-9_]*)\s*(.*)$");
                if (!match.Success)
                    throw new Exception($"{Where(file, lineNo)}: '%define' requires a macro name");

                _macros[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            }

            private static string ParseIncludePath(string text, string file, int lineNo)
            {
                string rest = text.Substring(FirstWord(text).Length).Trim();
                if (rest.Length == 0)
                    throw new Exception($"{Where(file, lineNo)}: '%include' requires a file path");

                if (rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"')
                    rest = rest.Substring(1, rest.Length - 2);

                if (rest.Length == 0)
                    throw new Exception($"{Where(file, lineNo)}: '%include' requires a file path");

                return rest;
            }

            private static string FirstWord(string text)
            {
                int i = 0;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                return text.Substring(0, i);
            }

            private string ExpandMacros(string text)
                => ExpandMacros(text, new HashSet<string>());

            private string ExpandMacros(string text, HashSet<string> expanding)
            {
                StringBuilder sb = new();
                int i = 0;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (c == '"' || c == '\'')
                    {
                        int start = i;
                        i++;
                        while (i < text.Length)
                        {
                            if (text[i] == '\\') { i += 2; continue; }
                            if (text[i] == c) { i++; break; }
                            i++;
                        }
                        sb.Append(text, start, i - start);
                        continue;
                    }

                    if (char.IsLetter(c) || c == '_')
                    {
                        int start = i;
                        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                        string word = text.Substring(start, i - start);
                        if (_macros.TryGetValue(word, out string? replacement))
                        {
                            if (expanding.Add(word))
                            {
                                sb.Append(ExpandMacros(replacement, expanding));
                                expanding.Remove(word);
                            }
                            else
                            {
                                sb.Append(word);
                            }
                        }
                        else
                        {
                            sb.Append(word);
                        }
                        continue;
                    }

                    sb.Append(c);
                    i++;
                }

                return sb.ToString();
            }
        }

        private static string StripComment(string line)
        {
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote != '\0')
                {
                    if (c == '\\') { i++; continue; }
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'') quote = c;
                else if (c == ';') return line.Substring(0, i);
            }
            return line;
        }

        // ---------------------------------------------------------------- parsing

        private static List<Token> Parse(List<SourceLine> lines)
        {
            List<Token> tokens = new();

            List<(string File, string? Global)> frames = new();
            string? lastFile = null;

            foreach (SourceLine sourceLine in lines)
            {
                string line = sourceLine.Text;

                if (frames.Count == 0)
                {
                    frames.Add((sourceLine.File, null));
                }
                else if (sourceLine.File != lastFile)
                {
                    if (frames.Count >= 2 && sourceLine.File == frames[^2].File)
                        frames.RemoveAt(frames.Count - 1);
                    else
                        frames.Add((sourceLine.File, null));
                }

                lastFile = sourceLine.File;
                string? currentGlobal = frames[^1].Global;

                Token token = new() { File = sourceLine.File, Line = sourceLine.Line, LocalScope = currentGlobal };

                Match labelMatch = Regex.Match(line, @"^\.?([A-Za-z_][A-Za-z0-9_]*)\s*:");
                if (labelMatch.Success)
                {
                    string name = labelMatch.Groups[1].Value;
                    if (line[0] == '.')
                    {
                        if (currentGlobal == null)
                            throw new Exception($"{Where(token)}: local label '.{name}' used before any global label");
                        token.Label = currentGlobal + "." + name;
                        token.LocalScope = currentGlobal;
                    }
                    else
                    {
                        token.Label = name;
                        token.LocalScope = name;
                        frames[^1] = (sourceLine.File, name);
                    }
                    line = line.Substring(labelMatch.Length).Trim();
                }

                if (line.Length == 0)
                {
                    tokens.Add(token);
                    continue;
                }

                int sep = line.IndexOfAny(new[] { ' ', '\t' });
                string first = sep < 0 ? line : line.Substring(0, sep);
                string rest = sep < 0 ? "" : line.Substring(sep);

                if (first.StartsWith("%"))
                {
                    token.IsDirective = true;
                    token.Opcode = first.Substring(1).ToLowerInvariant();
                }
                else
                {
                    token.IsDirective = false;
                    token.Opcode = first.ToUpperInvariant();
                }

                token.Operands = SplitOperands(rest);
                tokens.Add(token);
            }

            return tokens;
        }

        private static List<string> SplitOperands(string text)
        {
            List<string> operands = new();
            int start = -1;
            char quote = '\0';

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0')
                {
                    if (c == '\\') { i++; continue; }
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }
                if (c == ',')
                {
                    if (start >= 0)
                    {
                        operands.Add(text.Substring(start, i - start).Trim());
                        start = -1;
                    }
                    continue;
                }
                if (start < 0) start = i;
            }

            if (start >= 0) operands.Add(text.Substring(start).Trim());

            return operands;
        }

        // ---------------------------------------------------------------- pass 1: symbols

        private static Dictionary<string, int> BuildLabels(List<Token> tokens, out HashSet<string> externs)
        {
            Dictionary<string, int> labels = new();
            externs = new HashSet<string>();
            List<(string Alias, string Target, Token Token)> aliases = new();
            long address = 0;

            foreach (Token token in tokens)
            {
                if (token.Label != null)
                {
                    if (labels.ContainsKey(token.Label))
                        throw new Exception($"{Where(token)}: duplicate symbol '{token.Label}'");
                    labels[token.Label] = (int) address;
                }

                if (token.IsDirective)
                {
                    switch (token.Opcode)
                    {
                        case "extern":
                            foreach (string op in token.Operands)
                            {
                                if (!IsIdentifier(op))
                                    throw new Exception($"{Where(token)}: invalid symbol name '{op}'");
                                externs.Add(op);
                            }
                            break;
                        case "aliasl":
                            {
                                (string alias, string target) = ParseAlias(token);
                                aliases.Add((alias, target, token));
                            }
                            break;
                        case "org":
                            address = CheckAddress(EvalValue(RequireOne(token), token, labels), token);
                            break;
                        case "byte":
                        case "ascii":
                            foreach (string op in token.Operands)
                                address += DataOperandBytes(token.Opcode, op, token);
                            break;
                        case "word":
                            foreach (string op in token.Operands)
                                address += DataOperandBytes(token.Opcode, op, token);
                            break;
                        case "dword":
                            foreach (string op in token.Operands)
                                address += DataOperandBytes(token.Opcode, op, token);
                            break;
                        case "asciz":
                            foreach (string op in token.Operands)
                                address += DataOperandBytes(token.Opcode, op, token);
                            address += 1;
                            break;
                        case "align":
                            address = AlignAddress(address, EvalValue(RequireAtLeastOne(token), token, labels), token);
                            break;
                        case "fill":
                            RequireAtLeastOne(token);
                            if (token.Operands.Count > 2)
                                throw new Exception($"{Where(token)}: '%fill' expected <numBytes>[, <fill>]");
                            address += EvalValue(token.Operands[0], token, labels);
                            break;
                        default:
                            throw new Exception($"{Where(token)}: unknown directive '%{token.Opcode}'");
                    }
                }
                else if (token.Opcode != null)
                {
                    Instruction instruction = FindInstruction(token.Opcode)
                        ?? throw new Exception($"{Where(token)}: unknown instruction '{token.Opcode}'");
                    address += InstructionSize(instruction);
                }
            }

            foreach ((string alias, string target, Token token) in aliases)
            {
                if (externs.Contains(alias))
                    throw new Exception($"{Where(token)}: cannot alias '{alias}': already declared '%extern'");
                if (labels.ContainsKey(alias))
                    throw new Exception($"{Where(token)}: duplicate symbol '{alias}'");
                if (!labels.TryGetValue(target, out int targetAddress))
                    throw new Exception($"{Where(token)}: alias target '{target}' is not a defined label");
                labels[alias] = targetAddress;
            }

            return labels;
        }

        private static int DataOperandBytes(string directive, string operand, Token token)
        {
            if (operand.StartsWith("\""))
                return DecodeString(operand, token).Length;
            return directive switch
            {
                "word" => 2,
                "dword" => 4,
                _ => 1
            };
        }

        private static long CheckAddress(long value, Token token)
        {
            if (value < 0 || value > MaxRomSize)
                throw new Exception($"{Where(token)}: address {value} out of range (0..{MaxRomSize})");
            return value;
        }

        private static long AlignAddress(long value, long align, Token token)
        {
            if (align <= 0)
                throw new Exception($"{Where(token)}: '%align' requires a positive alignment");

            long remainder = value % align;
            long target = remainder == 0 ? value : value + (align - remainder);

            if (target > MaxRomSize)
                throw new Exception($"{Where(token)}: '%align' target {target} exceeds max ROM size {MaxRomSize}");

            return target;
        }

        // ---------------------------------------------------------------- pass 2: emission

        private static byte[] Emit(List<Token> tokens, IReadOnlyDictionary<string, int> labels, HashSet<string>? externs = null, List<(string Name, int Offset)>? references = null)
        {
            List<byte> output = new();

            foreach (Token token in tokens)
            {
                if (token.IsDirective)
                {
                    switch (token.Opcode)
                    {
                        case "org":
                            EnsureAddress(EvalValue(RequireOne(token), token, labels), token, output);
                            break;
                        case "byte":
                            foreach (string op in token.Operands)
                                EmitDataOperand(token, op, 1, labels, externs, references, output);
                            break;
                        case "word":
                            foreach (string op in token.Operands)
                                EmitDataOperand(token, op, 2, labels, externs, references, output);
                            break;
                        case "dword":
                            foreach (string op in token.Operands)
                                EmitDataOperand(token, op, 4, labels, externs, references, output);
                            break;
                        case "ascii":
                            foreach (string op in token.Operands)
                                EmitDataOperand(token, op, 1, labels, externs, references, output);
                            break;
                        case "asciz":
                            foreach (string op in token.Operands)
                                EmitDataOperand(token, op, 1, labels, externs, references, output);
                            output.Add(0);
                            break;
                        case "align":
                            long align = EvalValue(RequireAtLeastOne(token), token, labels);
                            byte fill = 0;
                            if (token.Operands.Count > 1)
                                EmitByte(EvalValue(token.Operands[1], token, labels), token, output);
                            while (output.Count < AlignAddress(output.Count, align, token))
                                output.Add(fill);
                            break;
                        case "fill":
                            RequireAtLeastOne(token);
                            if (token.Operands.Count > 2)
                                throw new Exception($"{Where(token)}: '%fill' expected <numBytes>[, <fill>]");
                            long fillCount = EvalValue(token.Operands[0], token, labels);
                            if (fillCount < 0)
                                throw new Exception($"{Where(token)}: '%fill' count must be non-negative");
                            long fillValue = token.Operands.Count > 1
                                ? EvalValue(token.Operands[1], token, labels)
                                : 0;
                            if (fillValue < -128 || fillValue > 255)
                                throw new Exception($"{Where(token)}: value {fillValue} out of range for a byte (-128..255)");
                            while (fillCount-- > 0)
                                output.Add((byte) fillValue);
                            break;
                    }
                    continue;
                }

                if (token.Opcode == null) continue;

                EncodeInstruction(token, labels, externs, references, output);
            }

            if (output.Count > MaxRomSize)
                throw new Exception($"program too large: {output.Count} bytes (max {MaxRomSize})");

            return output.ToArray();
        }

        private static void EnsureAddress(long newAddress, Token token, List<byte> output)
        {
            if (newAddress < output.Count)
                throw new Exception($"{Where(token)}: address moved backwards (overlapping '%org' or data)");
            if (newAddress > MaxRomSize)
                throw new Exception($"{Where(token)}: address {newAddress} exceeds max ROM size {MaxRomSize}");

            while (output.Count < newAddress) output.Add(0);
        }

        private static void EmitDataOperand(Token token, string operand, int width, IReadOnlyDictionary<string, int> labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, List<byte> output)
        {
            if (operand.StartsWith("\""))
            {
                if (width != 1)
                    throw new Exception($"{Where(token)}: string literal not allowed in '%{token.Opcode}' (only 8-bit data)");
                foreach (byte b in DecodeString(operand, token))
                    output.Add(b);
                return;
            }

            switch (width)
            {
                case 1:
                    EmitByte(EvalSized(operand, token, labels, externs, references, output.Count, "byte"), token, output);
                    break;
                case 2:
                    EmitWord(EvalSized(operand, token, labels, externs, references, output.Count, "word"), token, output);
                    break;
                default:
                    EmitDword(EvalDword(operand, token, labels, externs, references, output.Count), token, output);
                    break;
            }
        }

        private static void EncodeInstruction(Token token, IReadOnlyDictionary<string, int> labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, List<byte> output)
        {
            Instruction? instruction = FindInstruction(token.Opcode!);
            if (instruction == null)
                throw new Exception($"{Where(token)}: unknown instruction '{token.Opcode}'");

            RequireCount(token, instruction.Operands.Length);
            output.Add(instruction.Opcode);

            for (int i = 0; i < instruction.Operands.Length; i++)
            {
                string operand = token.Operands[i];
                switch (instruction.Operands[i])
                {
                    case OperandKind.Reg:
                        EmitByte(ParseRegister(operand, token), token, output);
                        break;
                    case OperandKind.Byte:
                        EmitByte(EvalSized(operand, token, labels, externs, references, output.Count, "byte"), token, output);
                        break;
                    case OperandKind.Word:
                        EmitWord(EvalSized(operand, token, labels, externs, references, output.Count, "word"), token, output);
                        break;
                    case OperandKind.Dword:
                        EmitDword(EvalDword(operand, token, labels, externs, references, output.Count), token, output);
                        break;
                    case OperandKind.Size:
                        EmitByte(ParseSize(operand, token), token, output);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- instruction table

        private enum OperandKind { Reg, Byte, Word, Dword, Size }

        private sealed class Instruction
        {
            public readonly string Name;
            public readonly byte Opcode;
            public readonly OperandKind[] Operands;

            public Instruction(string name, byte opcode, params OperandKind[] operands)
            {
                Name = name;
                Opcode = opcode;
                Operands = operands;
            }
        }

        private static readonly Dictionary<string, Instruction> InstructionByName = BuildInstructionTable();

        private static Dictionary<string, Instruction> BuildInstructionTable()
        {
            Dictionary<string, Instruction> map = new();

            void Add(string name, byte opcode, params OperandKind[] operands)
                => map[name] = new Instruction(name, opcode, operands);

            Add("NOP", 0x00);
            Add("INT", 0x01, OperandKind.Byte);
            Add("CALL", 0x02, OperandKind.Dword);
            Add("RET", 0x03);

            Add("LDIB", 0x04, OperandKind.Reg, OperandKind.Byte);
            Add("LDIW", 0x05, OperandKind.Reg, OperandKind.Word);
            Add("LDID", 0x06, OperandKind.Reg, OperandKind.Dword);

            string[] regReg = { "LDB", "LDW", "LDD", "STB", "STW", "STD", "MOV" };
            for (int i = 0; i < regReg.Length; i++)
                Add(regReg[i], (byte) (0x07 + i), OperandKind.Reg, OperandKind.Reg);

            string[] alu = { "ADD", "SUB", "MUL", "DIV", "MOD", "AND", "NAND", "OR", "NOR", "XOR", "EQ", "GT", "GTE", "LT", "LTE" };
            for (int i = 0; i < alu.Length; i++)
                Add(alu[i], (byte) (0x0E + i), OperandKind.Reg, OperandKind.Reg, OperandKind.Reg);

            Add("JMP", 0x1D, OperandKind.Dword);
            Add("JNZ", 0x1E, OperandKind.Dword, OperandKind.Reg);
            Add("JZ", 0x1F, OperandKind.Dword, OperandKind.Reg);

            Add("PUSH", 0x20, OperandKind.Reg, OperandKind.Size);
            Add("POP", 0x21, OperandKind.Reg, OperandKind.Size);

            return map;
        }

        private static Instruction? FindInstruction(string name)
            => InstructionByName.TryGetValue(name, out Instruction? instruction) ? instruction : null;

        private static int InstructionSize(Instruction instruction)
        {
            int size = 1;
            foreach (OperandKind kind in instruction.Operands)
                size += kind switch
                {
                    OperandKind.Word => 2,
                    OperandKind.Dword => 4,
                    _ => 1
                };
            return size;
        }

        // ---------------------------------------------------------------- helpers

        private static bool IsIdentifier(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        private static string RequireOne(Token token)
        {
            RequireCount(token, 1);
            return token.Operands[0];
        }

        private static string RequireAtLeastOne(Token token)
        {
            if (token.Operands.Count < 1)
                throw new Exception($"{Where(token)}: '%{token.Opcode}' expected at least 1 operand");
            return token.Operands[0];
        }

        private static void RequireCount(Token token, int count)
        {
            if (token.Operands.Count != count)
                throw new Exception($"{Where(token)}: '{token.Opcode}' expected {count} operand{(count != 1 ? "s" : "")}, got {token.Operands.Count}");
        }

        private static (string Alias, string Target) ParseAlias(Token token)
        {
            string name;
            string target;
            if (token.Operands.Count >= 2)
            {
                name = token.Operands[0];
                target = token.Operands[1];
            }
            else if (token.Operands.Count == 1)
            {
                string rest = token.Operands[0].Trim();
                int sep = rest.IndexOfAny(new[] { ' ', '\t' });
                if (sep < 0)
                    throw new Exception($"{Where(token)}: '%aliasl' expected 'NAME LABEL'");
                name = rest.Substring(0, sep);
                target = rest.Substring(sep).Trim();
            }
            else
            {
                throw new Exception($"{Where(token)}: '%aliasl' expected 'NAME LABEL'");
            }

            if (!IsIdentifier(name))
                throw new Exception($"{Where(token)}: invalid alias name '{name}'");
            if (!IsIdentifier(target))
                throw new Exception($"{Where(token)}: invalid alias target '{target}'");

            return (name, target);
        }

        private static void EmitByte(long value, Token token, List<byte> output)
        {
            if (value < -128 || value > 255)
                throw new Exception($"{Where(token)}: value {value} out of range for a byte (-128..255)");
            output.Add((byte) value);
        }

        private static void EmitWord(long value, Token token, List<byte> output)
        {
            if (value < -32768 || value > 65535)
                throw new Exception($"{Where(token)}: value {value} out of range for a word (-32768..65535)");
            output.Add((byte) value);
            output.Add((byte) (value >> 8));
        }

        private static void EmitDword(long value, Token token, List<byte> output)
        {
            if (value < int.MinValue || value > uint.MaxValue)
                throw new Exception($"{Where(token)}: value {value} out of range for a dword");
            for (int b = 0; b < 4; b++)
                output.Add((byte) (value >> (b * 8)));
        }

        private static byte[] DecodeString(string operand, Token token)
        {
            if (operand.Length < 2 || operand[0] != '"' || operand[^1] != '"')
                throw new Exception($"{Where(token)}: invalid string literal '{operand}'");

            List<byte> bytes = new();
            for (int i = 1; i < operand.Length - 1; i++)
            {
                char c = operand[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= operand.Length - 1)
                        throw new Exception($"{Where(token)}: unterminated escape in string '{operand}'");
                    c = operand[i] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        '0' => '\0',
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        _ => throw new Exception($"{Where(token)}: invalid escape '\\{operand[i]}' in string '{operand}'")
                    };
                }
                bytes.Add((byte) c);
            }
            return bytes.ToArray();
        }

        // ---------------------------------------------------------------- expression evaluator

        private static long EvalValue(string operand, Token token, IReadOnlyDictionary<string, int>? labels)
            => new ExprParser(operand, token, labels, null, null, -1).Parse();

        private static long EvalValue(string operand, Token token, IReadOnlyDictionary<string, int>? labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, int offset)
            => new ExprParser(operand, token, labels, externs, references, offset).Parse();

        private static long EvalDword(string operand, Token token, IReadOnlyDictionary<string, int> labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, int offset)
        {
            int before = references?.Count ?? 0;
            long value = EvalValue(operand, token, labels, externs, references, offset);
            if (references != null && references.Count > before && operand.Trim() != references[^1].Name)
                throw new Exception($"{Where(token)}: external symbol '{references[^1].Name}' must be used by itself as an address");
            return value;
        }

        private static long EvalSized(string operand, Token token, IReadOnlyDictionary<string, int> labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, int offset, string widthName)
        {
            int before = references?.Count ?? 0;
            long value = EvalValue(operand, token, labels, externs, references, offset);
            if (references != null && references.Count > before)
                throw new Exception($"{Where(token)}: external symbol '{references[^1].Name}' cannot be used as a {widthName}-sized operand");
            return value;
        }

        private sealed class ExprParser
        {
            private readonly string _s;
            private readonly Token _token;
            private readonly IReadOnlyDictionary<string, int>? _labels;
            private readonly HashSet<string>? _externs;
            private readonly List<(string Name, int Offset)>? _references;
            private readonly int _refOffset;
            private int _pos;

            public ExprParser(string s, Token token, IReadOnlyDictionary<string, int>? labels, HashSet<string>? externs, List<(string Name, int Offset)>? references, int refOffset)
            {
                _s = s;
                _token = token;
                _labels = labels;
                _externs = externs;
                _references = references;
                _refOffset = refOffset;
            }

            public long Parse()
            {
                long value = ParseOr();
                ExpectEnd();
                return value;
            }

            private long ParseOr()
            {
                long left = ParseXor();
                while (Match("|")) left |= ParseXor();
                return left;
            }

            private long ParseXor()
            {
                long left = ParseAnd();
                while (Match("^")) left ^= ParseAnd();
                return left;
            }

            private long ParseAnd()
            {
                long left = ParseShift();
                while (Match("&")) left &= ParseShift();
                return left;
            }

            private long ParseShift()
            {
                long left = ParseAdd();
                while (true)
                {
                    if (Match("<<")) left = ShiftLeft(left, ParseAdd());
                    else if (Match(">>")) left = ShiftRight(left, ParseAdd());
                    else break;
                }
                return left;
            }

            private long ParseAdd()
            {
                long left = ParseMul();
                while (true)
                {
                    if (Match("+")) left += ParseMul();
                    else if (Match("-")) left -= ParseMul();
                    else break;
                }
                return left;
            }

            private long ParseMul()
            {
                long left = ParseUnary();
                while (true)
                {
                    if (Match("*")) left *= ParseUnary();
                    else if (Match("/")) left = Divide(left, ParseUnary());
                    else if (Match("%")) left = Modulo(left, ParseUnary());
                    else break;
                }
                return left;
            }

            private long ParseUnary()
            {
                if (Match("-")) return -ParseUnary();
                if (Match("~")) return ~ParseUnary();
                if (Match("+")) return ParseUnary();
                return ParsePrimary();
            }

            private long ParsePrimary()
            {
                SkipWhitespace();

                if (_pos < _s.Length && _s[_pos] == '(')
                {
                    _pos++;
                    long value = ParseOr();
                    Expect(')');
                    return value;
                }

                if (_pos < _s.Length && _s[_pos] == '\'')
                    return ParseChar();

                return ParseValue();
            }

            private long ParseValue()
            {
                SkipWhitespace();
                int start = _pos;

                if (_pos + 1 < _s.Length && _s[_pos] == '0' && (_s[_pos + 1] == 'x' || _s[_pos + 1] == 'X'))
                {
                    _pos += 2;
                    int hexStart = _pos;
                    while (_pos < _s.Length && IsHexDigit(_s[_pos])) _pos++;
                    if (_pos == hexStart)
                        throw new Exception($"{Where(_token)}: invalid value '{_s.Trim()}'");
                    return Convert.ToInt64(_s.Substring(hexStart, _pos - hexStart), 16);
                }

                if (_pos + 1 < _s.Length && _s[_pos] == '0' && (_s[_pos + 1] == 'b' || _s[_pos + 1] == 'B'))
                {
                    _pos += 2;
                    int binStart = _pos;
                    while (_pos < _s.Length && (_s[_pos] == '0' || _s[_pos] == '1')) _pos++;
                    if (_pos == binStart)
                        throw new Exception($"{Where(_token)}: invalid value '{_s.Trim()}'");

                    long value = 0;
                    for (int i = binStart; i < _pos; i++)
                        value = (value << 1) | (_s[i] == '1' ? 1L : 0L);
                    return value;
                }

                if (_pos < _s.Length && char.IsDigit(_s[_pos]))
                {
                    while (_pos < _s.Length && char.IsDigit(_s[_pos])) _pos++;
                    return long.Parse(_s.Substring(start, _pos - start), CultureInfo.InvariantCulture);
                }

                if (_pos < _s.Length && _s[_pos] == '.')
                {
                    _pos++;
                    int nameStart = _pos;
                    while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_')) _pos++;
                    if (_pos == nameStart)
                        throw new Exception($"{Where(_token)}: invalid value '{_s.Trim()}'");

                    string name = "." + _s.Substring(nameStart, _pos - nameStart);

                    if (_token.LocalScope == null)
                        throw new Exception($"{Where(_token)}: local label '{name}' used before any global label");

                    string full = _token.LocalScope + name;
                    if (_labels != null && _labels.TryGetValue(full, out int address))
                        return address;

                    throw new Exception($"{Where(_token)}: undefined symbol '{full}'");
                }

                if (_pos < _s.Length && (char.IsLetter(_s[_pos]) || _s[_pos] == '_'))
                {
                    while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_')) _pos++;
                    string name = _s.Substring(start, _pos - start);

                    if (_labels != null && _labels.TryGetValue(name, out int address))
                        return address;

                    if (_externs != null && _externs.Contains(name))
                    {
                        _references?.Add((name, _refOffset));
                        return 0;
                    }

                    throw new Exception($"{Where(_token)}: undefined symbol '{name}'");
                }

                throw new Exception($"{Where(_token)}: invalid value '{_s.Trim()}'");
            }

            private long ParseChar()
            {
                _pos++;
                if (_pos >= _s.Length)
                    throw new Exception($"{Where(_token)}: unterminated character literal '{_s.Trim()}'");

                long value;
                char c = _s[_pos];
                if (c == '\\')
                {
                    _pos++;
                    if (_pos >= _s.Length)
                        throw new Exception($"{Where(_token)}: unterminated character literal '{_s.Trim()}'");
                    c = _s[_pos] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        '0' => '\0',
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        _ => throw new Exception($"{Where(_token)}: invalid escape '\\{_s[_pos]}' in character literal")
                    };
                    value = c;
                    _pos++;
                }
                else
                {
                    value = c;
                    _pos++;
                }

                if (_pos >= _s.Length || _s[_pos] != '\'')
                    throw new Exception($"{Where(_token)}: character literal must contain a single character '{_s.Trim()}'");
                _pos++;
                return value;
            }

            private bool Match(string op)
            {
                SkipWhitespace();
                if (_pos + op.Length > _s.Length) return false;
                for (int i = 0; i < op.Length; i++)
                    if (_s[_pos + i] != op[i]) return false;
                _pos += op.Length;
                return true;
            }

            private void Expect(char c)
            {
                SkipWhitespace();
                if (_pos >= _s.Length || _s[_pos] != c)
                    throw new Exception($"{Where(_token)}: expected '{c}' in value '{_s.Trim()}'");
                _pos++;
            }

            private void ExpectEnd()
            {
                SkipWhitespace();
                if (_pos != _s.Length)
                    throw new Exception($"{Where(_token)}: unexpected characters in value '{_s.Trim()}'");
            }

            private void SkipWhitespace()
            {
                while (_pos < _s.Length && (_s[_pos] == ' ' || _s[_pos] == '\t')) _pos++;
            }

            private long Divide(long a, long b)
            {
                if (b == 0)
                    throw new Exception($"{Where(_token)}: division by zero in '{_s.Trim()}'");
                return a / b;
            }

            private long Modulo(long a, long b)
            {
                if (b == 0)
                    throw new Exception($"{Where(_token)}: division by zero in '{_s.Trim()}'");
                return a % b;
            }

            private long ShiftLeft(long a, long b)
            {
                if (b < 0 || b > 63)
                    throw new Exception($"{Where(_token)}: invalid shift count {b} in '{_s.Trim()}'");
                return a << (int) b;
            }

            private long ShiftRight(long a, long b)
            {
                if (b < 0 || b > 63)
                    throw new Exception($"{Where(_token)}: invalid shift count {b} in '{_s.Trim()}'");
                return a >> (int) b;
            }

            private static bool IsHexDigit(char c)
                => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        // ---------------------------------------------------------------- registers and sizes

        private static int ParseRegister(string operand, Token token)
        {
            if (operand.Length < 2 || operand[0] != '$')
                throw new Exception($"{Where(token)}: invalid register '{operand}'");

            string body = operand.Substring(1);
            string name = body.StartsWith("{") && body.EndsWith("}") ? body.Substring(1, body.Length - 2) : body;

            if (name.Length == 2 && int.TryParse(name, NumberStyles.HexNumber, null, out int reg))
            {
                if (reg > 0x1F)
                    throw new Exception($"{Where(token)}: invalid register '{operand}' (must be $00..$1F)");
                return reg;
            }

            int alias = name.ToLowerInvariant() switch
            {
                "pc" => 0,
                "intr" => 1,
                "sp" => 2,
                _ => -1
            };

            if (alias < 0)
                throw new Exception($"{Where(token)}: invalid register '{operand}'");

            return alias;
        }

        private static int ParseSize(string operand, Token token)
        {
            int size = operand.ToUpperInvariant() switch
            {
                "BYTE" => 0,
                "WORD" => 1,
                "DWORD" => 2,
                _ => -1
            };

            if (size < 0)
            {
                long value = EvalValue(operand, token, null);
                if (value < 0 || value > 2)
                    throw new Exception($"{Where(token)}: invalid size '{operand}', expected BYTE, WORD or DWORD");
                size = (int) value;
            }

            return size;
        }
    }
}
