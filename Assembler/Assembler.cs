using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assembler
{
    public static class Assembler
    {
        public const int MaxRomSize = 64 * 1024;

        private sealed class Token
        {
            public int Line;
            public string? Label;
            public string? Opcode;
            public bool IsDirective;
            public List<string> Operands = new();
        }

        public static byte[] Assemble(string source)
        {
            List<Token> tokens = Parse(source);
            Dictionary<string, int> labels = BuildLabels(tokens);
            return Emit(tokens, labels);
        }

        private static List<Token> Parse(string source)
        {
            List<Token> tokens = new();
            string[] lines = source.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0) continue;

                Token token = new() { Line = i + 1 };

                Match labelMatch = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:");
                if (labelMatch.Success)
                {
                    token.Label = labelMatch.Groups[1].Value;
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

                if (first.StartsWith("."))
                {
                    token.IsDirective = true;
                    token.Opcode = first.Substring(1).ToLowerInvariant();
                }
                else
                {
                    token.IsDirective = false;
                    token.Opcode = first.ToUpperInvariant();
                }

                token.Operands = rest
                    .Replace(',', ' ')
                    .Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                tokens.Add(token);
            }

            return tokens;
        }

        private static string StripComment(string line)
        {
            int idx = line.IndexOf(';');
            return idx < 0 ? line : line.Substring(0, idx);
        }

        private static Dictionary<string, int> BuildLabels(List<Token> tokens)
        {
            Dictionary<string, int> labels = new(System.StringComparer.OrdinalIgnoreCase);
            int address = 0;

            foreach (Token token in tokens)
            {
                if (token.Label != null)
                {
                    if (labels.ContainsKey(token.Label))
                        throw new System.Exception($"line {token.Line}: duplicate label '{token.Label}'");

                    labels[token.Label] = address;
                }

                if (token.IsDirective)
                {
                    switch (token.Opcode)
                    {
                        case "org":
                            address = checked((int) EvalValue(RequireOne(token), token, labels));
                            break;
                        case "byte":
                            address += token.Operands.Count;
                            break;
                        case "word":
                            address += token.Operands.Count * 2;
                            break;
                        case "dword":
                            address += token.Operands.Count * 4;
                            break;
                        default:
                            throw new System.Exception($"line {token.Line}: unknown directive '.{token.Opcode}'");
                    }
                }
                else if (token.Opcode != null)
                {
                    address += InstructionSize(token.Opcode, token);
                }
            }

            return labels;
        }

        private static byte[] Emit(List<Token> tokens, Dictionary<string, int> labels)
        {
            List<byte> output = new();

            void EnsureAddress(int newAddress)
            {
                if (newAddress < output.Count)
                    throw new System.Exception("address moved backwards (overlapping '.org' or data)");

                while (output.Count < newAddress) output.Add(0);
            }

            foreach (Token token in tokens)
            {
                if (token.IsDirective)
                {
                    switch (token.Opcode)
                    {
                        case "org":
                            EnsureAddress(checked((int) EvalValue(RequireOne(token), token, labels)));
                            break;
                        case "byte":
                            foreach (string operand in token.Operands)
                                output.Add((byte) EvalValue(operand, token, labels));
                            break;
                        case "word":
                            foreach (string operand in token.Operands)
                            {
                                long value = EvalValue(operand, token, labels);
                                output.Add((byte) value);
                                output.Add((byte) (value >> 8));
                            }
                            break;
                        case "dword":
                            foreach (string operand in token.Operands)
                            {
                                long value = EvalValue(operand, token, labels);
                                for (int b = 0; b < 4; b++)
                                    output.Add((byte) (value >> (b * 8)));
                            }
                            break;
                    }
                    continue;
                }

                if (token.Opcode == null) continue;

                EncodeInstruction(token, labels, output);
            }

            if (output.Count > MaxRomSize)
                throw new System.Exception($"program too large: {output.Count} bytes (max {MaxRomSize})");

            return output.ToArray();
        }

        private static void EncodeInstruction(Token token, IReadOnlyDictionary<string, int> labels, List<byte> output)
        {
            List<string> ops = token.Operands;

            void EmitByte(long value) => output.Add((byte) value);

            void EmitReg(string operand) => EmitByte(ParseRegister(operand, token));

            void EmitWord(long value)
            {
                EmitByte(value);
                EmitByte(value >> 8);
            }

            void EmitAddr(string operand)
            {
                long address = EvalValue(operand, token, labels);
                for (int b = 0; b < 4; b++)
                    output.Add((byte) (address >> (b * 8)));
            }

            void Alu(int opcode)
            {
                RequireCount(token, 3);
                output.Add((byte) opcode);
                EmitReg(ops[0]);
                EmitReg(ops[1]);
                EmitReg(ops[2]);
            }

            switch (token.Opcode)
            {
                case "NOP":
                    RequireCount(token, 0);
                    output.Add(0x00);
                    break;
                case "INT":
                    RequireCount(token, 1);
                    output.Add(0x01);
                    EmitByte(EvalValue(ops[0], token, labels));
                    break;
                case "CALL":
                    RequireCount(token, 1);
                    output.Add(0x02);
                    EmitAddr(ops[0]);
                    break;
                case "RET":
                    RequireCount(token, 0);
                    output.Add(0x03);
                    break;
                case "LDIB":
                    RequireCount(token, 2);
                    output.Add(0x04);
                    EmitReg(ops[0]);
                    EmitByte(EvalValue(ops[1], token, labels));
                    break;
                case "LDIW":
                    RequireCount(token, 2);
                    output.Add(0x05);
                    EmitReg(ops[0]);
                    EmitWord(EvalValue(ops[1], token, labels));
                    break;
                case "LDID":
                    RequireCount(token, 2);
                    output.Add(0x06);
                    EmitReg(ops[0]);
                    EmitAddr(ops[1]);
                    break;
                case "LDB":
                case "LDW":
                case "LDD":
                case "STB":
                case "STW":
                case "STD":
                case "MOV":
                    TwoRegs(token, ops, output);
                    break;
                case "ADD": Alu(0x0E); break;
                case "SUB": Alu(0x0F); break;
                case "MUL": Alu(0x10); break;
                case "DIV": Alu(0x11); break;
                case "MOD": Alu(0x12); break;
                case "AND": Alu(0x13); break;
                case "NAND": Alu(0x14); break;
                case "OR": Alu(0x15); break;
                case "NOR": Alu(0x16); break;
                case "XOR": Alu(0x17); break;
                case "EQ": Alu(0x18); break;
                case "GT": Alu(0x19); break;
                case "GTE": Alu(0x1A); break;
                case "LT": Alu(0x1B); break;
                case "LTE": Alu(0x1C); break;
                case "JMP":
                    RequireCount(token, 1);
                    output.Add(0x1D);
                    EmitAddr(ops[0]);
                    break;
                case "JNZ":
                    RequireCount(token, 2);
                    output.Add(0x1E);
                    EmitAddr(ops[0]);
                    EmitReg(ops[1]);
                    break;
                case "JZ":
                    RequireCount(token, 2);
                    output.Add(0x1F);
                    EmitAddr(ops[0]);
                    EmitReg(ops[1]);
                    break;
                case "PUSH":
                    RequireCount(token, 2);
                    output.Add(0x20);
                    EmitReg(ops[0]);
                    EmitByte(ParseSize(ops[1], token));
                    break;
                case "POP":
                    RequireCount(token, 2);
                    output.Add(0x21);
                    EmitReg(ops[0]);
                    EmitByte(ParseSize(ops[1], token));
                    break;
                default:
                    throw new System.Exception($"line {token.Line}: unknown instruction '{token.Opcode}'");
            }
        }

        private static void TwoRegs(Token token, List<string> ops, List<byte> output)
        {
            RequireCount(token, 2);

            int opcode = token.Opcode switch
            {
                "LDB" => 0x07,
                "LDW" => 0x08,
                "LDD" => 0x09,
                "STB" => 0x0A,
                "STW" => 0x0B,
                "STD" => 0x0C,
                "MOV" => 0x0D,
                _ => throw new System.Exception($"line {token.Line}: internal error for '{token.Opcode}'")
            };

            output.Add((byte) opcode);
            output.Add((byte) ParseRegister(ops[0], token));
            output.Add((byte) ParseRegister(ops[1], token));
        }

        private static int InstructionSize(string opcode, Token token)
        {
            int size = opcode switch
            {
                "NOP" or "RET" => 1,
                "INT" => 2,
                "CALL" or "JMP" => 5,
                "LDIB" or "LDB" or "LDW" or "LDD" or "STB" or "STW" or "STD" or "MOV" or "PUSH" or "POP" => 3,
                "LDIW" => 4,
                "LDID" => 6,
                "ADD" or "SUB" or "MUL" or "DIV" or "MOD" or
                "AND" or "NAND" or "OR" or "NOR" or "XOR" or
                "EQ" or "GT" or "GTE" or "LT" or "LTE" => 4,
                "JNZ" or "JZ" => 6,
                _ => -1
            };

            if (size < 0)
                throw new System.Exception($"line {token.Line}: unknown instruction '{opcode}'");

            return size;
        }

        private static string RequireOne(Token token)
        {
            RequireCount(token, 1);
            return token.Operands[0];
        }

        private static void RequireCount(Token token, int count)
        {
            if (token.Operands.Count != count)
                throw new System.Exception($"line {token.Line}: '{token.Opcode}' expected {count} operand{(count != 1 ? "s" : "")}, got {token.Operands.Count}");
        }

        private static long EvalValue(string operand, Token token, IReadOnlyDictionary<string, int>? labels)
        {
            string value = operand.Trim();

            if (labels != null && labels.TryGetValue(value, out int labelAddress))
                return labelAddress;

            bool negative = false;
            if (value.StartsWith("-"))
            {
                negative = true;
                value = value.Substring(1);
            }

            long result;
            if (value.StartsWith("0x") || value.StartsWith("0X"))
            {
                if (!long.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out result))
                    throw new System.Exception($"line {token.Line}: invalid value '{operand}'");
            }
            else if (value.StartsWith("0b") || value.StartsWith("0B"))
            {
                result = 0;
                foreach (char c in value.Substring(2))
                {
                    if (c != '0' && c != '1')
                        throw new System.Exception($"line {token.Line}: invalid value '{operand}'");
                    result = (result << 1) | (c == '1' ? 1L : 0L);
                }
            }
            else
            {
                if (!long.TryParse(value, out result))
                    throw new System.Exception($"line {token.Line}: invalid value '{operand}'");
            }

            return negative ? -result : result;
        }

        private static int ParseRegister(string operand, Token token)
        {
            if (operand.Length < 2 || operand[0] != '$')
                throw new System.Exception($"line {token.Line}: invalid register '{operand}'");

            string body = operand.Substring(1);

            string name = body.StartsWith("{") && body.EndsWith("}") ? body.Substring(1, body.Length - 2) : body;

            if (name.Length == 2 && int.TryParse(name, System.Globalization.NumberStyles.HexNumber, null, out int reg))
                return reg;

            int alias = name.ToLowerInvariant() switch
            {
                "pc" => 0,
                "intr" => 1,
                "sp" => 2,
                _ => -1
            };

            if (alias < 0)
                throw new System.Exception($"line {token.Line}: invalid register '{operand}'");

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
                    throw new System.Exception($"line {token.Line}: invalid size '{operand}', expected BYTE, WORD or DWORD");

                size = (int) value;
            }

            return size;
        }
    }
}
