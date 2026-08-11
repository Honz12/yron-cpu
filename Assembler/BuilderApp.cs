using System;
using System.IO;

namespace Assembler
{
    public static class BuilderApp
    {
        public static int Run()
        {
            Console.Write("Link file: ");
            string input = Console.ReadLine()?.Trim() ?? "";
            if (input.Length == 0) return 1;

            Console.Write("Output file (default: rom.bin): ");
            string output = Console.ReadLine()?.Trim() ?? "";
            if (output.Length == 0) output = "rom.bin";

            return BuildFile(input, output);
        }

        public static int RunFromArgs(string[] args)
        {
            string input = args[1];
            string output = args.Length > 2 ? args[2] : "rom.bin";
            return BuildFile(input, output);
        }

        private static int BuildFile(string input, string output)
        {
            if (!File.Exists(input))
            {
                Console.WriteLine($"File '{input}' does not exist");
                return 1;
            }

            try
            {
                LibraryFile lib = LibraryFile.Read(input);

                if (lib.References.Count > 0)
                {
                    Console.WriteLine($"BUILD ERROR: {lib.References.Count} unresolved reference(s) in '{input}' (run cpu link first)");
                    return 1;
                }

                File.WriteAllBytes(output, lib.Binary);
                Console.WriteLine($"Built {input} -> {output} ({lib.Binary.Length} bytes)");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"BUILD ERROR: {e.Message}");
                return 1;
            }
        }
    }
}
