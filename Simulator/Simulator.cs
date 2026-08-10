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

            return 0;
        }
    }
}
