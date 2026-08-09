namespace cpu
{
    public class CPU
    {
        public enum InstructionOpcode
        {
            NOP,
            INT,
            CALL,
            RET,

            // Memory management

            LDIb,
            LDIw,
            LDId,

            LDb,
            LDw,
            LDd,

            STb,
            STw,
            STd,

            MOV,

            // Arithmetic, bitwise and logic operations

            ADD,
            SUB,
            MUL,
            DIV,
            MOD,

            AND,
            NAND,
            OR,
            NOR,
            XOR,

            EQ,
            GT,
            GTE,
            LT,
            LTE,

            // Flow control

            JMP,
            JNZ,
            JZ,

            // Stack operations

            PUSH,
            POP,
        }

        public enum StackEntrySize
        {
            BYTE, WORD, DWORD
        }

        public const int REG_PC = 0;
        public const int REG_INT_REASON = 1;
        public const int REG_SP = 2;

        public uint[] Registers = new uint[32];
        private byte[] Ram;

        public CPU(int ramSizeKb, byte[] romBytes)
        {
            Ram = new byte[ramSizeKb * 1024];

            for (int i = 0; i < romBytes.Length; i++)
            {
                Ram[i] = romBytes[i];
            }
        
            Console.WriteLine($"CPU initialized with {ramSizeKb} kilobyte{(ramSizeKb != 1 ? "s" : "")} of RAM");
        }

        public void WriteRam(uint val, byte b, uint address)
        {
            Ram[address + b] = (byte) (val >> (b * 8));
        }

        public uint ReadRam(byte b, uint address)
        {
            return (uint) (Ram[address + b] << (b * 8));
        }

        public void StackPush(uint value, StackEntrySize stackEntrySize)
        {
            switch (stackEntrySize)
            {
                case StackEntrySize.BYTE:
                    {
                        Registers[REG_SP]--;
                        WriteRam(value, 0, Registers[REG_SP]);
                    }
                    break;
                case StackEntrySize.WORD:
                    {
                        Registers[REG_SP] -= 2;
                        WriteRam(value, 0, Registers[REG_SP]);
                        WriteRam(value, 1, Registers[REG_SP]);
                    }
                    break;
                case StackEntrySize.DWORD:
                    {
                        Registers[REG_SP] -= 4;
                        WriteRam(value, 0, Registers[REG_SP]);
                        WriteRam(value, 1, Registers[REG_SP]);
                        WriteRam(value, 2, Registers[REG_SP]);
                        WriteRam(value, 3, Registers[REG_SP]);
                    }
                    break;
            }
        }

        public uint StackPop(StackEntrySize stackEntrySize)
        {
            switch (stackEntrySize)
            {
                case StackEntrySize.BYTE:
                    {
                        Registers[REG_SP]--;
                        return
                            ReadRam(0, Registers[REG_SP]);
                    }
                case StackEntrySize.WORD:
                    {
                        Registers[REG_SP] -= 2;
                        return
                            ReadRam(0, Registers[REG_SP]) |
                            ReadRam(1, Registers[REG_SP]);
                    }
                case StackEntrySize.DWORD:
                    {
                        Registers[REG_SP] -= 4;
                        return
                            ReadRam(0, Registers[REG_SP]) |
                            ReadRam(1, Registers[REG_SP]) |
                            ReadRam(2, Registers[REG_SP]) |
                            ReadRam(3, Registers[REG_SP]);
                    }
            }
            throw new Exception();
        }

        public void SetRegister(byte reg, uint value)
        {
            Registers[reg] = value;
        }

        private void IncrementPC(int inc)
        {
            Registers[REG_PC] += (uint) inc;
        }

        private void RaiseError(string errorMessage)
        {
            Console.WriteLine(errorMessage);
            RegisterDump();
        }

        public void RegisterDump()
        {
            int i = 0;
            Console.WriteLine($"|{" REG",-5}|{" DEC",-12}|{" HEX",-10}|{" ALIAS", -16}|");
            Console.WriteLine("|-----+------------+----------+----------------|");
            foreach (uint value in Registers)
            {
                string alias = i switch
                {
                    REG_PC => "$pc",
                    REG_INT_REASON => "$intr",
                    REG_SP => "$sp",
                    _ => $"${i:X2}".ToLower()
                };
                Console.WriteLine($"| {i:X2}  | {value:D10} | {value:X8} | {alias,-14} |");
                i++;
            }
        }

        public void RunInst()
        {
            uint pc = Registers[REG_PC];
            byte opcode = (byte) ReadRam(0, pc);
            pc++;

            switch ((InstructionOpcode) opcode)
            {
                case InstructionOpcode.NOP:
                    {
                        IncrementPC(1);
                    }
                    break;
                case InstructionOpcode.INT:
                    {
                        byte reason = (byte) ReadRam(0, pc);

                        IncrementPC(2);
                        
                        StackPush(Registers[REG_PC], StackEntrySize.DWORD);

                        CallInterrupt();
                    }
                    break;
                case InstructionOpcode.CALL:
                    {
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);

                        IncrementPC(5);
                        StackPush(Registers[REG_PC], StackEntrySize.DWORD);
                    }
                    break;
                case InstructionOpcode.RET:
                    {
                        
                    }
                    break;
                case InstructionOpcode.LDIb:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint value = ReadRam(0, pc);

                        SetRegister(reg, value);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.LDIw:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint value = ReadRam(0, pc) | ReadRam(1, pc);

                        SetRegister(reg, value);

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.LDId:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);

                        SetRegister(reg, value);

                        IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.LDb:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);

                        uint value = ReadRam(0, address);
                        IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.LDw:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);
                        
                        uint value = ReadRam(0, address) | ReadRam(1, address);
                        IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.LDd:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);
                        
                        uint value = ReadRam(0, address) | ReadRam(1, address) | ReadRam(2, address) | ReadRam(3, address);
                        IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.STb:
                    {
                        
                    }
                    break;
                case InstructionOpcode.STw:
                    {
                        
                    }
                    break;
                case InstructionOpcode.STd:
                    {
                        
                    }
                    break;
                case InstructionOpcode.MOV:
                    {
                        
                    }
                    break;
                case InstructionOpcode.ADD:
                    {
                        
                    }
                    break;
                case InstructionOpcode.SUB:
                    {
                        
                    }
                    break;
                case InstructionOpcode.MUL:
                    {
                        
                    }
                    break;
                case InstructionOpcode.DIV:
                    {
                        
                    }
                    break;
                case InstructionOpcode.MOD:
                    {
                        
                    }
                    break;
                case InstructionOpcode.AND:
                    {
                        
                    }
                    break;
                case InstructionOpcode.NAND:
                    {
                        
                    }
                    break;
                case InstructionOpcode.OR:
                    {
                        
                    }
                    break;
                case InstructionOpcode.NOR:
                    {
                        
                    }
                    break;
                case InstructionOpcode.XOR:
                    {
                        
                    }
                    break;
                case InstructionOpcode.EQ:
                    {
                        
                    }
                    break;
                case InstructionOpcode.GT:
                    {
                        
                    }
                    break;
                case InstructionOpcode.GTE:
                    {
                        
                    }
                    break;
                case InstructionOpcode.LT:
                    {
                        
                    }
                    break;
                case InstructionOpcode.LTE:
                    {
                        
                    }
                    break;
                case InstructionOpcode.JMP:
                    {
                        
                    }
                    break;
                case InstructionOpcode.JNZ:
                    {
                        
                    }
                    break;
                case InstructionOpcode.JZ:
                    {
                        
                    }
                    break;
                default:
                    {
                        RaiseError("UNKNOWN INSTRUCTION");
                    }
                    break;
            }
        }
    }
}
