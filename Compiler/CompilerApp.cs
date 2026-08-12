using System;
using System.IO;

namespace Compiler
{
    public static class CompilerApp
    {
        public static int Run()
        {
            Console.Write("Source file: ");
            string sourcePath = Console.ReadLine()?.Trim() ?? "";
            if (sourcePath.Length == 0) return 1;

            Console.Write("Output file (default: <source>.yrn): ");
            string outputPath = Console.ReadLine()?.Trim() ?? "";
            if (outputPath.Length == 0) outputPath = Path.ChangeExtension(sourcePath, ".yrn");

            return CompileFile(sourcePath, outputPath);
        }

        public static int RunFromArgs(string[] args)
        {
            string sourcePath = args[1];
            string outputPath = "";
            bool build = false;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--build")
                    build = true;
                else
                    outputPath = args[i];
            }

            if (outputPath.Length == 0)
                outputPath = Path.ChangeExtension(sourcePath, ".yrn");

            int result = CompileFile(sourcePath, outputPath);
            if (result == 0 && build)
                return Assembler.AssemblerApp.RunFromArgs(new[] { "asm", outputPath });
            return result;
        }

        private static int CompileFile(string sourcePath, string outputPath)
        {
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"File '{sourcePath}' does not exist");
                return 1;
            }

            try
            {
                string text = File.ReadAllText(sourcePath);

                List<Token> tokens = Lexer.Tokenize(text, sourcePath);
                Parser parser = new(tokens);
                List<TopLevel> items = parser.ParseProgram();

                Resolver resolver = new(items);
                resolver.Resolve();

                Codegen codegen = new(resolver);
                string assembly = codegen.Emit();

                File.WriteAllText(outputPath, assembly);
                Console.WriteLine($"Compiled {sourcePath} -> {outputPath}");
                return 0;
            }
            catch (CompileError e)
            {
                Console.WriteLine($"COMPILE ERROR: {e.Message}");
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"COMPILE ERROR: {e.Message}");
                return 1;
            }
        }
    }
}
