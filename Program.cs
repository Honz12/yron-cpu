namespace cpu
{
    public static class Program
    {
        private static void Credits()
        {
            Console.WriteLine("Made by GameMage (Honz12)");
            Console.WriteLine("Made in C# (DOTNET 10.0)");
            Console.WriteLine();
            Console.WriteLine("Also check out: \x1b[94mhttps://github.com/Honz12/yronOS\x1b[0m");
            Console.WriteLine();
        }

        public static int Main(string[] args)
        {
            Credits();

            if (args.Length >= 2)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "asm":
                    case "assembler":
                    case "2":
                        return Assembler.AssemblerApp.RunFromArgs(args);
                    case "link":
                    case "linker":
                    case "3":
                        return Assembler.LinkerApp.RunFromArgs(args);
                    case "build":
                    case "builder":
                    case "4":
                        return Assembler.BuilderApp.RunFromArgs(args);
                    case "cc":
                    case "compiler":
                    case "5":
                        return Compiler.CompilerApp.RunFromArgs(args);
                    case "test":
                    case "6":
                        return Simulator.TestApp.RunFromArgs(args);
                    case "new":
                        if (!string.Equals(args[1], "yconf", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("Usage: cpu new yconf");
                            return 1;
                        }
                        try
                        {
                            Simulator.SimConfig.CreateDefault().Save("yconf.json");
                            Console.WriteLine("Created yconf.json");
                            return 0;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERROR: {e.Message}");
                            return 1;
                        }
                    case "sim":
                    case "simulator":
                    case "1":
                        return Simulator.Simulator.RunFromArgs(args[1]);
                }
            }

            while (true)
            {
                Console.WriteLine("What would you like to run?");
                Console.WriteLine("  1. Simulator");
                Console.WriteLine("  2. Assembler");
                Console.WriteLine("  3. Linker");
                Console.WriteLine("  4. Builder");
                Console.WriteLine("  5. Compiler");
                Console.WriteLine("  6. Test (headless)");
                Console.WriteLine("  q. Quit");
                Console.Write("> ");

                string choice = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";

                switch (choice)
                {
                    case "1":
                    case "sim":
                    case "simulator":
                        Simulator.Simulator.Run();
                        break;
                    case "2":
                    case "asm":
                    case "assembler":
                        Assembler.AssemblerApp.Run();
                        break;
                    case "3":
                    case "link":
                    case "linker":
                        Assembler.LinkerApp.Run();
                        break;
                    case "4":
                    case "build":
                    case "builder":
                        Assembler.BuilderApp.Run();
                        break;
                    case "5":
                    case "cc":
                    case "compiler":
                        Compiler.CompilerApp.Run();
                        break;
                    case "6":
                    case "test":
                        Simulator.TestApp.Run();
                        break;
                    case "q":
                    case "quit":
                    case "exit":
                        return 0;
                    default:
                        Console.WriteLine("Unknown choice");
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}
