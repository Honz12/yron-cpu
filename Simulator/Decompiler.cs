using System.Text;

namespace cpu.Simulator
{
    public static class Decompiler
    {
        private enum OperandKind { Reg, Byte, Word, Dword, Size }

        private sealed record InstructionInfo(string Name, OperandKind[] Operands);

        private static readonly Dictionary<byte, InstructionInfo> Table = BuildTable();

        private static Dictionary<byte, InstructionInfo> BuildTable()
        {
            Dictionary<byte, InstructionInfo> map = new();

            void Add(byte opcode, string name, params OperandKind[] operands)
                => map[opcode] = new InstructionInfo(name, operands);

            Add(0x00, "NOP");
            Add(0x01, "INT", OperandKind.Byte);
            Add(0x02, "CALL", OperandKind.Dword);
            Add(0x03, "RET");

            Add(0x04, "LDIb", OperandKind.Reg, OperandKind.Byte);
            Add(0x05, "LDIw", OperandKind.Reg, OperandKind.Word);
            Add(0x06, "LDId", OperandKind.Reg, OperandKind.Dword);

            string[] regReg = { "LDB", "LDW", "LDD", "STB", "STW", "STD", "MOV" };
            for (int i = 0; i < regReg.Length; i++)
                Add((byte) (0x07 + i), regReg[i], OperandKind.Reg, OperandKind.Reg);

            string[] alu = { "ADD", "SUB", "MUL", "DIV", "MOD", "AND", "NAND", "OR", "NOR", "XOR", "EQ", "GT", "GTE", "LT", "LTE" };
            for (int i = 0; i < alu.Length; i++)
                Add((byte) (0x0E + i), alu[i], OperandKind.Reg, OperandKind.Reg, OperandKind.Reg);

            Add(0x1D, "JMP", OperandKind.Dword);
            Add(0x1E, "JNZ", OperandKind.Dword, OperandKind.Reg);
            Add(0x1F, "JZ", OperandKind.Dword, OperandKind.Reg);

            Add(0x20, "PUSH", OperandKind.Reg, OperandKind.Size);
            Add(0x21, "POP", OperandKind.Reg, OperandKind.Size);

            return map;
        }

        public static string Decompile(CPU cpu, uint address, bool include_bytes = true, bool include_inst = true)
        {
            if (address >= cpu.RamLength)
                return "(out of range)";

            byte opcode = (byte) cpu.ReadRam(0, address);

            if (!Table.TryGetValue(opcode, out InstructionInfo? info))
                return $"{opcode:X2}  ???";

            List<byte> raw = new() { opcode };
            StringBuilder sb = new(info.Name);
            uint pos = address + 1;

            for (int i = 0; i < info.Operands.Length; i++)
            {
                OperandKind kind = info.Operands[i];
                int width = kind switch
                {
                    OperandKind.Word => 2,
                    OperandKind.Dword => 4,
                    _ => 1
                };

                if (!Fits(cpu, pos, width))
                    break;

                uint value = ReadValue(cpu, pos, width);

                for (int b = 0; b < width; b++)
                    raw.Add((byte) cpu.ReadRam(0, pos + (uint) b));

                sb.Append(i == 0 ? " " : ", ");

                switch (kind)
                {
                    case OperandKind.Reg:
                        sb.Append($"${value:X2}");
                        break;
                    case OperandKind.Byte:
                        sb.Append($"0x{value:X2}");
                        break;
                    case OperandKind.Word:
                        sb.Append($"0x{value:X4}");
                        break;
                    case OperandKind.Dword:
                        sb.Append($"0x{value:X8}");
                        break;
                    case OperandKind.Size:
                        sb.Append(value switch
                        {
                            0 => "BYTE",
                            1 => "WORD",
                            2 => "DWORD",
                            _ => $"0x{value:X2}"
                        });
                        break;
                }

                pos += (uint) width;
            }

            string bytes = string.Join(" ", raw.Select(b => b.ToString("X2")));
            if (include_bytes == include_inst)
                return $"{bytes,-23} {sb}";
            if (include_inst)
            {
                return $"{sb}";
            }
            if (include_bytes)
            {
                return $"{bytes}";
            }
            return "";
        }

        private static bool Fits(CPU cpu, uint address, int width)
            => (ulong) address + (ulong) width <= (ulong) cpu.RamLength;

        private static uint ReadValue(CPU cpu, uint address, int width)
        {
            uint value = 0;
            for (int b = 0; b < width; b++)
                value |= cpu.ReadRam((byte) b, address);
            return value;
        }
    }
}
