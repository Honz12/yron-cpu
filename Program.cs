namespace cpu
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("What would you like to run?");
                Console.WriteLine("  1. Simulator");
                Console.WriteLine("  2. Assembler");
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
                    case "q":
                    case "quit":
                    case "exit":
                        return;
                    default:
                        Console.WriteLine("Unknown choice");
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}
