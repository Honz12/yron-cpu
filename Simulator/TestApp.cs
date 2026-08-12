using System;
using System.IO;

namespace cpu.Simulator
{
    /// <summary>
    /// Headless ROM runner used to validate compiled programs. Loads the ROM,
    /// runs instructions until the PC stops moving (the compiler's "halt"
    /// loop is a jump-to-self) or the step budget is exhausted, then prints
    /// all registers. No devices, no window, no input prompts.
    /// </summary>
    public static class TestApp
    {
        private const ulong DefaultMaxSteps = 20_000_000;

        public static int Run()
        {
            Console.Write("ROM path: ");
            string romPath = Console.ReadLine()?.Trim() ?? "";
            if (romPath.Length == 0) return 1;
            return RunFromArgs(new[] { "test", romPath });
        }

        public static int RunFromArgs(string[] args)
        {
            string romPath = args[1];
            ulong maxSteps = DefaultMaxSteps;
            bool disasm = false;
            ulong trace = 0;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--disasm")
                    disasm = true;
                else if (args[i] == "--trace")
                    trace = ulong.MaxValue;
                else if (ulong.TryParse(args[i], out ulong custom))
                {
                    if (trace == ulong.MaxValue)
                        trace = custom;
                    else
                        maxSteps = custom;
                }
            }

            if (!File.Exists(romPath))
            {
                Console.WriteLine($"File '{romPath}' does not exist");
                return 1;
            }

            byte[] rom = File.ReadAllBytes(romPath);
            CPU cpu = new(1024, rom);

            if (disasm)
            {
                Console.WriteLine($"Disassembly of '{romPath}' ({rom.Length} bytes):");
                for (uint addr = 0; addr < rom.Length; addr += (uint) Decompiler.InstructionLength(cpu, addr))
                    Console.WriteLine($"  0x{addr:X4}: {Decompiler.Decompile(cpu, addr)}");
                return 0;
            }

            ulong steps = 0;
            bool halted = false;
            bool traceStop = false;
            try
            {
                for (ulong i = 0; i < maxSteps; i++)
                {
                    uint prevPc = cpu.Registers[CPU.REG_PC];
                    if (trace > 0)
                        Console.WriteLine($"  {steps,5}: 0x{prevPc:X4}: {Decompiler.Decompile(cpu, prevPc)}  [pc=0x{cpu.Registers[0]:X4} sp=0x{cpu.Registers[2]:X6} r03=0x{cpu.Registers[3]:X8} r0E=0x{cpu.Registers[0x0E]:X8} r0F=0x{cpu.Registers[0x0F]:X8} r10=0x{cpu.Registers[0x10]:X8} fp=0x{cpu.Registers[0x1F]:X6}]");
                    cpu.RunInst();
                    steps++;
                    if (trace > 0 && steps == trace)
                    {
                        traceStop = true;
                        break;
                    }
                    if (cpu.Registers[CPU.REG_PC] == prevPc)
                    {
                        halted = true;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"CPU ERROR: {e.Message}");
                return 1;
            }

            if (traceStop)
                Console.WriteLine($"Trace stopped after {steps} steps");
            else if (steps >= maxSteps && !halted)
                Console.WriteLine($"Timed out after {steps} steps (program did not halt)");
            else
                Console.WriteLine($"Finished after {steps} steps{(halted ? " (halted)" : "")}");

            Console.WriteLine("Registers:");
            for (int r = 0; r < 32; r++)
                Console.WriteLine($"  ${r:X2} = {cpu.Registers[r]:D10} (0x{cpu.Registers[r]:X8})");

            Console.WriteLine($"Return value ($0F): {cpu.Registers[0x0F]:D10} (0x{cpu.Registers[0x0F]:X8})");
            return 0;
        }
    }
}
