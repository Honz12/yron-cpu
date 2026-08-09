using System;
using System.IO;

namespace Assembler
{
    public static class AssemblerApp
    {
        public static void Run()
        {
            Console.Write("Source file: ");
            string sourcePath = Console.ReadLine()?.Trim() ?? "";

            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"File '{sourcePath}' does not exist");
                return;
            }

            Console.Write($"Output file (default: rom.bin): ");
            string outputPath = Console.ReadLine()?.Trim() ?? "";
            if (outputPath.Length == 0) outputPath = "rom.bin";

            string source = File.ReadAllText(sourcePath);

            try
            {
                byte[] rom = Assembler.Assemble(source);

                File.WriteAllBytes(outputPath, rom);

                Console.WriteLine($"Assembled {sourcePath} -> {outputPath} ({rom.Length} byte{(rom.Length != 1 ? "s" : "")})");
            }
            catch (Exception e)
            {
                Console.WriteLine($"ASSEMBLY ERROR: {e.Message}");
            }
        }
    }
}
