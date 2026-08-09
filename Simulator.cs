using System;
using System.IO;

namespace cpu
{
    public static class Simulator
    {
        public static void Run()
        {
            Console.Write("ROM path (default: rom.bin): ");
            string romPath = Console.ReadLine()?.Trim() ?? "";
            if (romPath.Length == 0) romPath = "rom.bin";

            if (!File.Exists(romPath))
            {
                Console.WriteLine($"File '{romPath}' does not exist");
                return;
            }

            byte[] romBytes = File.ReadAllBytes(romPath);

            Console.WriteLine($"Loaded rom of {romBytes.Length} byte{(romBytes.Length != 1 ? "s" : "")}");

            CPU cpu = new(64, romBytes);

            try
            {
                while (true)
                {
                    cpu.RunInst();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"CPU ERROR: {e.Message}");
            }
        }
    }
}
