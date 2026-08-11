using System;
using System.Collections.Generic;
using System.IO;

namespace Assembler
{
    public static class LinkerApp
    {
        public static int Run()
        {
            Console.Write("Input files (space separated): ");
            string[] parts = Console.ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();

            Console.Write("Output file (default: linked.yrl): ");
            string output = Console.ReadLine()?.Trim() ?? "";
            if (output.Length == 0) output = "linked.yrl";

            return LinkFiles(new List<string>(parts), output);
        }

        public static int RunFromArgs(string[] args)
        {
            List<string> inputs = new();
            string output = "linked.yrl";

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-o" && i + 1 < args.Length)
                    output = args[++i];
                else
                    inputs.Add(args[i]);
            }

            return LinkFiles(inputs, output);
        }

        private static int LinkFiles(List<string> inputs, string output)
        {
            if (inputs.Count < 2)
            {
                Console.WriteLine("Usage: cpu link <a.yrl> <b.yrl> [more...] [-o output.yrl]");
                return 1;
            }

            try
            {
                LibraryFile linked = Linker.Link(inputs);
                linked.Write(output);
                Console.WriteLine($"Linked {inputs.Count} files -> {output} ({linked.Binary.Length} bytes, {linked.Symbols.Count} symbols)");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"LINK ERROR: {e.Message}");
                return 1;
            }
        }
    }
}
