using cpu.Simulator.Device;
using Raylib_cs;

namespace cpu.Simulator
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

        public const int INTERRUPT_TABLE_START_ADDRESS = 0x200;

        public const int INTERRUPT_DEVICE_INIT = 0x01;

        private const int MAX_PC_HISTORY = 32;

        public uint[] Registers = new uint[32];

        public List<IDevice> Devices = [];
        private bool InitializingDevice = false;
        private int InitDeviceIndex = 0;
        private byte[] Ram;

        public bool Halted = false;

        public uint RamLength => (uint) Ram.Length;

        public ulong intsCalled = 0;

        public List<uint> ProgramCounterHistory = [];

        public CPU(int ramSizeKb, byte[] romBytes)
        {
            Ram = new byte[ramSizeKb * 1024];

            for (int i = 0; i < romBytes.Length; i++)
            {
                Ram[i] = romBytes[i];
            }

            Registers[REG_SP] = (uint) Ram.Length;
        
            Console.WriteLine($"CPU initialized with {ramSizeKb} kilobyte{(ramSizeKb != 1 ? "s" : "")} of RAM");
        }

        public void RegisterDevice(IDevice device)
        {
            Devices.Add(device);
        }

        public void WriteRam(uint val, byte b, uint address)
        {
            if (address + b < Ram.Length)
            {
                Ram[address + b] = (byte) (val >> (b * 8));
                return;
            }

            RegisterDump();
            
            if (Simulator.DebugMode) Console.WriteLine($"DEBUG: INVALID RAM WRITE {address + b} / 0x{address + b:X8}");

            Console.ReadKey();
        }

        public uint ReadRam(byte b, uint address)
        {
            if (address + b < Ram.Length)
                return (uint) (Ram[address + b] << (b * 8));

            RegisterDump();

            if (Simulator.DebugMode) Console.WriteLine($"DEBUG: INVALID RAM READ {address + b} / 0x{address + b:X8}");
            Console.ReadKey();
            return 0;
        }

        public void StackPush(uint value, StackEntrySize stackEntrySize)
        {
            byte bytes = stackEntrySize switch
            {
                StackEntrySize.BYTE => 1,
                StackEntrySize.WORD => 2,
                _ => 4
            };

            if (Registers[REG_SP] < bytes || (ulong) Registers[REG_SP] > (ulong) Ram.Length)
                RaiseError("STACK UNDERFLOW");

            Registers[REG_SP] -= bytes;

            for (int b = 0; b < bytes; b++)
            {
                WriteRam(value, (byte) b, Registers[REG_SP]);
            }
        }

        public uint StackPop(StackEntrySize stackEntrySize)
        {
            byte bytes = stackEntrySize switch
            {
                StackEntrySize.BYTE => 1,
                StackEntrySize.WORD => 2,
                _ => 4
            };

            if ((ulong) Registers[REG_SP] + bytes > (ulong) Ram.Length)
                RaiseError("STACK UNDERFLOW");

            uint value = 0;

            for (int b = 0; b < bytes; b++)
            {
                value |= ReadRam((byte) b, Registers[REG_SP]);
            }

            Registers[REG_SP] += bytes;

            return value;
        }

        public void SetRegister(byte reg, uint value)
        {
            Registers[reg] = value;
        }

        public uint GetRegister(byte reg)
        {
            return Registers[reg];
        }

        private void IncrementPC(int inc)
        {
            Registers[REG_PC] += (uint) inc;
        }

        private void RaiseError(string errorMessage)
        {
            foreach (uint pc in ProgramCounterHistory)
            {
                Console.WriteLine($"0x{pc:X8}: {Decompiler.Decompile(this, pc)}");
            }
            RegisterDump();
            throw new Exception(errorMessage);
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

            uint pc = Registers[REG_PC];
            Console.WriteLine();
            Console.WriteLine($"NEXT INSTRUCTION @0x{pc:X8}");
            Console.WriteLine($"  {Decompiler.Decompile(this, pc)}");
        }

        public void RegisterDumpRaylib(int debugBeginY)
        {
            int i = 0;
            foreach (uint value in Registers)
            {
                string alias = i switch
                {
                    REG_PC => "$pc",
                    REG_INT_REASON => "$intr",
                    REG_SP => "$sp",
                    _ => $"${i:X2}".ToLower()
                };
                Raylib.DrawText($"REG {alias,-14}: {value:D10} 0x{value:X8}", 0, debugBeginY + 12 * i, 10, Color.Magenta);
                i++;
            }

            uint pc = Registers[REG_PC];
            Raylib.DrawText($"NEXT INSTRUCTION @0x{pc:X8}", 0, debugBeginY + 12 * (i + 1), 10, Color.Magenta);
            Raylib.DrawText($"{Decompiler.Decompile(this, pc)}", 0, debugBeginY + 12 * (i + 2), 10, Color.Magenta);
        }

        public void CallInterrupt(byte reason)
        {
            StackPush(Registers[REG_PC], StackEntrySize.DWORD);

            Registers[REG_INT_REASON] = reason;

            uint jumpValueAddress = (uint) (reason * 4 + INTERRUPT_TABLE_START_ADDRESS);

            uint jumpValue = 
                ReadRam(0, jumpValueAddress) |
                ReadRam(1, jumpValueAddress) |
                ReadRam(2, jumpValueAddress) |
                ReadRam(3, jumpValueAddress);

            Registers[REG_PC] = jumpValue;

            if (Simulator.DebugMode)
            {
                Simulator.WriteSep();
                Console.WriteLine($"CALLED INTERRUPT {intsCalled} 0x{reason:X2}, JUMPING TO {jumpValue}");
                switch (reason)
                {
                    case 0x00:
                        Console.WriteLine("This seems to be a error interrupt.");
                        RegisterDump();
                        break;
                    case 0x01:
                        Console.WriteLine("This seems to be a device initialization interrupt.");
                        Console.WriteLine($"Device ID: {GetRegister(0x03):D10} (0x{GetRegister(0x03):X8})");
                        Console.WriteLine($"Memory needed: {GetRegister(0x05):D10} (0x{GetRegister(0x05):X8})");
                        break;
                    case 0x02:
                        Console.WriteLine("This seems to be a device input interrupt.");
                        Console.WriteLine($"Device ID: {GetRegister(0x03):D10} (0x{GetRegister(0x03):X8})");
                        Console.WriteLine($"Input: {GetRegister(0x04):D10} (0x{GetRegister(0x04):X8})");
                        break;
                }
            }
            intsCalled++;
        }

        public void RunInst()
        {

            foreach (IDevice device in Devices)
            {
                device.Tick(this);
            }

            if (Halted)
                return;
            
            uint pc = Registers[REG_PC];

            ProgramCounterHistory.Add(pc);

            if (ProgramCounterHistory.Count > MAX_PC_HISTORY)
            {
                ProgramCounterHistory.RemoveAt(0);
            }

            InstructionOpcode opcode = (InstructionOpcode) ReadRam(0, pc);
            pc++;
            if (InitializingDevice)
            {
                if (GetRegister(0x04) != 0)
                {
                    Devices[InitDeviceIndex].AfterInterrupt(this);
                    InitDeviceIndex++;

                    InitializingDevice = false;
                }
            }

            if (InitDeviceIndex < Devices.Count)
            {
                if (!InitializingDevice)
                {
                    SetRegister(0x03, Devices[InitDeviceIndex].DeviceId);
                    SetRegister(0x04, 0);

                    Devices[InitDeviceIndex].BeforeInterrupt(this);

                    if (Simulator.DebugMode) Console.WriteLine($"INIT DEVICE {Devices[InitDeviceIndex].DisplayName} [ID:{Devices[InitDeviceIndex].DeviceId:X8}]");

                    CallInterrupt(INTERRUPT_DEVICE_INIT);

                    InitializingDevice = true;
                    return;
                }
            }

            switch (opcode)
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

                        CallInterrupt(reason);
                    }
                    break;
                case InstructionOpcode.CALL:
                    {
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);

                        IncrementPC(5);
                        StackPush(Registers[REG_PC], StackEntrySize.DWORD);

                        Registers[REG_PC] = value;
                    }
                    break;
                case InstructionOpcode.RET:
                    {
                        uint value = StackPop(StackEntrySize.DWORD);

                        Registers[REG_PC] = value;
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
                        uint address = GetRegister((byte) ReadRam(0, pc));

                        uint value = ReadRam(0, address);
                        
                        SetRegister(reg, value);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.LDw:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = GetRegister((byte) ReadRam(0, pc));
                        
                        uint value = ReadRam(0, address) | ReadRam(1, address);
                        
                        SetRegister(reg, value);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.LDd:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = GetRegister((byte) ReadRam(0, pc));
                        
                        uint value = ReadRam(0, address) | ReadRam(1, address) | ReadRam(2, address) | ReadRam(3, address);
                        
                        SetRegister(reg, value);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.STb:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = GetRegister((byte) ReadRam(0, pc));

                        uint value = GetRegister(reg);

                        WriteRam(value, 0, address);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.STw:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = GetRegister((byte) ReadRam(0, pc));

                        uint value = GetRegister(reg);

                        WriteRam(value, 0, address);
                        WriteRam(value, 1, address);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.STd:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        uint address = GetRegister((byte) ReadRam(0, pc));

                        uint value = GetRegister(reg);

                        WriteRam(value, 0, address);
                        WriteRam(value, 1, address);
                        WriteRam(value, 2, address);
                        WriteRam(value, 3, address);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.MOV:
                    {
                        byte dest = (byte) ReadRam(0, pc);
                        pc++;
                        byte src = (byte) ReadRam(0, pc);
                        SetRegister(dest, GetRegister(src));

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.ADD:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            +
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.SUB:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            -
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.MUL:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            *
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.DIV:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        if (b == 0)
                        {
                            SetRegister(
                                str,
                                0
                            );
                            RaiseError("DIV - DIV BY ZERO");
                        }
                        else
                        {
                            SetRegister(
                                str,
                                a
                                /
                                b
                            );
                        }
                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.MOD:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        if (b == 0)
                        {
                            SetRegister(
                                str,
                                0
                            );
                            RaiseError("MOD - DIV BY ZERO");
                        }
                        else
                        {
                            SetRegister(
                                str,
                                a
                                %
                                b
                            );
                        }
                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.AND:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            &
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.NAND:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            ~(a
                            &
                            b)
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.OR:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            |
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.NOR:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            ~(a
                            |
                            b)
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.XOR:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            a
                            ^
                            b
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.EQ:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            (a
                            ==
                            b) ? 1u : 0u
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.GT:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            (a
                            >
                            b) ? 1u : 0u
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.GTE:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            (a
                            >=
                            b) ? 1u : 0u
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.LT:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            (a
                            <
                            b) ? 1u : 0u
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.LTE:
                    {
                        uint a = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        uint b = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte str = (byte) ReadRam(0, pc);

                        SetRegister(
                            str,
                            (a
                            <=
                            b) ? 1u : 0u
                        );

                        IncrementPC(4);
                    }
                    break;
                case InstructionOpcode.JMP:
                    {
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);
                        
                        Registers[REG_PC] = value;
                    }
                    break;
                case InstructionOpcode.JNZ:
                    {
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);
                        uint cond = GetRegister((byte) ReadRam(0, pc + 4));

                        if (cond != 0) Registers[REG_PC] = value;
                        else IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.JZ:
                    {
                        uint value = ReadRam(0, pc) | ReadRam(1, pc) | ReadRam(2, pc) | ReadRam(3, pc);
                        uint cond = GetRegister((byte) ReadRam(0, pc + 4));

                        if (cond == 0) Registers[REG_PC] = value;
                        else IncrementPC(6);
                    }
                    break;
                case InstructionOpcode.PUSH:
                    {
                        uint value = GetRegister((byte) ReadRam(0, pc));
                        pc++;
                        byte size = (byte) ReadRam(0, pc);

                        if (size >= 3) RaiseError($"INVALID PUSH SIZE {size}");

                        StackPush(value, (StackEntrySize) size);

                        IncrementPC(3);
                    }
                    break;
                case InstructionOpcode.POP:
                    {
                        byte reg = (byte) ReadRam(0, pc);
                        pc++;
                        byte size = (byte) ReadRam(0, pc);

                        if (size >= 3) RaiseError($"INVALID POP SIZE {size}");

                        uint value = StackPop((StackEntrySize) size);

                        SetRegister(reg, value);

                        IncrementPC(3);
                    }
                    break;
                default:
                    {
                        RaiseError($"UNKNOWN INSTRUCTION {opcode}");
                    }
                    break;
            }
        }
    }
}
