namespace cpu
{
    public static class Program
    {
        public static int Main(string[] args)
        {
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
