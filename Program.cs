namespace cpu
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: cpu-sim <rom-path>");
                Environment.Exit(1);
            }
            
            string romPath = args[0];

            if (!File.Exists(romPath))
            {
                Console.WriteLine($"File '{romPath}' does not exist");
                Environment.Exit(1);
            }

            /*
            byte[] romBytes = File.ReadAllBytes(romPath);

            Console.WriteLine($"Loaded rom of {romBytes.Length} byte{(romBytes.Length != 1 ? "s" : "")}");
            */

            byte[] romBytes = new byte[1024];

            CPU cpu = new((romBytes.Length / 1024) + ((romBytes.Length % 1024 != 0) ? 1 : 0), romBytes);

            cpu.RunInst();
            cpu.RegisterDump();
        }
    }
}
