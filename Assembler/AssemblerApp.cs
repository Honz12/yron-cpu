using System;
using System.IO;

namespace Assembler
{
    public static class AssemblerApp
    {
        public static int Run()
        {
            Console.Write("Source file: ");
            string sourcePath = Console.ReadLine()?.Trim() ?? "";
            if (sourcePath.Length == 0) return 1;

            Console.Write($"Output file (default: rom.bin): ");
            string outputPath = Console.ReadLine()?.Trim() ?? "";
            if (outputPath.Length == 0) outputPath = "rom.bin";

            return AssembleFile(sourcePath, outputPath);
        }

        public static int RunFromArgs(string[] args)
        {
            string sourcePath = args[1];
            string outputPath = args.Length > 2 ? args[2] : "rom.bin";
            return AssembleFile(sourcePath, outputPath);
        }

        private static int AssembleFile(string sourcePath, string outputPath)
        {
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"File '{sourcePath}' does not exist");
                return 1;
            }

            try
            {
                string source = File.ReadAllText(sourcePath);
                byte[] rom = Assembler.Assemble(source, sourcePath);

                File.WriteAllBytes(outputPath, rom);

                Console.WriteLine($"Assembled {sourcePath} -> {outputPath} ({rom.Length} byte{(rom.Length != 1 ? "s" : "")})");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"ASSEMBLY ERROR: {e.Message}");
                return 1;
            }
        }
    }
}
