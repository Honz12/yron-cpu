using System;
using System.IO;

namespace cpu.Simulator
{
    public static class Simulator
    {
        public static void Run()
        {
            Console.Write("ROM path (default: rom.bin): ");
            string romPath = Console.ReadLine()?.Trim() ?? "";
            if (romPath.Length == 0) romPath = "rom.bin";

            RunFromArgs(romPath);
        }

        public static int RunFromArgs(string romPath)
        {
            if (!File.Exists(romPath))
            {
                Console.WriteLine($"File '{romPath}' does not exist");
                return 1;
            }

            byte[] romBytes = File.ReadAllBytes(romPath);

            Console.WriteLine($"Loaded rom of {romBytes.Length} byte{(romBytes.Length != 1 ? "s" : "")}");

            CPU cpu = new(64, romBytes);

            cpu.RegisterDevice(new Device.DisplayDevice());
            cpu.RegisterDevice(new Device.KeyboardDevice());

            Console.WriteLine("Run in steps?");

            bool runInSteps = Console.ReadKey().Key == ConsoleKey.Y;

            try
            {
                if (runInSteps)
                {
                    Console.Clear();
                    cpu.RegisterDump();
                }
                
                while (!cpu.Halted)
                {
                    if (runInSteps)
                    {
                        ConsoleKey key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Escape)
                        {
                            break;
                        }
                        else if (key == ConsoleKey.D)
                        {
                            Console.Write("RAM start address: ");

                            _ = uint.TryParse(Console.ReadLine(), out uint startAddr);

                            Console.Write("Bytes: ");

                            _ = uint.TryParse(Console.ReadLine(), out uint bytesNum);

                            Console.Clear();

                            for (int i = 0; i < bytesNum; i++)
                            {
                                Console.Write($"{cpu.ReadRam(0, (uint) (i + startAddr)):X2} ");
                            }

                            Console.WriteLine("\nAny to continue...");

                            Console.ReadKey();
                        }
                        else
                        {
                            Console.Clear();
                            cpu.RunInst();
                            cpu.RegisterDump();
                        }
                    }
                    else
                    {
                        cpu.RunInst();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"CPU ERROR: {e.Message}");
            }

            return 0;
        }
    }
}
