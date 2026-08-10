using System;
using System.IO;

namespace cpu.Simulator
{
    public static class Simulator
    {
        public static bool SafeMode = true;

        public static void Run()
        {
            Console.Write("ROM path (default: rom.bin): ");
            string romPath = Console.ReadLine()?.Trim() ?? "";
            if (romPath.Length == 0) romPath = "rom.bin";

            RunFromArgs(romPath);
        }

        public static void CpuProcess(CPU cpu, bool runInSteps)
        {
            if (runInSteps)
            {
                Console.Clear();
                cpu.RegisterDump();
            }
            
            while (!cpu.Halted)
            {
                if (runInSteps)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        break;
                    }
                    else if (key == ConsoleKey.D)
                    {
                        Console.Write("RAM start address: ");

                        _ = uint.TryParse(Console.ReadLine(), out uint startAddr);

                        Console.Write("Bytes: ");

                        _ = uint.TryParse(Console.ReadLine(), out uint bytesNum);

                        Console.Clear();

                        for (int i = 0; i < bytesNum; i++)
                        {
                            Console.Write($"{cpu.ReadRam(0, (uint) (i + startAddr)):X2} ");
                        }

                        Console.WriteLine("\nAny to continue...");

                        Console.ReadKey();
                    }
                    else
                    {
                        Console.Clear();
                        cpu.RunInst();
                        cpu.RegisterDump();
                    }
                }
                else
                {
                    cpu.RunInst();
                }
            }
        }

        public static bool[] ShowDeviceMenu()
        {
            bool[] selectedDevices = new bool[2];
            int selectedIndex = 0;
            bool confirmed = false;

            Console.WriteLine("Select devices:");

            while (!confirmed)
            {
                Console.Clear();
                Console.WriteLine("Select devices:");
                Console.WriteLine("Arrow keys move, Enter toggles, Tab confirms");

                for (int i = 0; i < selectedDevices.Length; i++)
                {
                    bool isSelected = selectedDevices[i];
                    string deviceName = i == 0 ? "Display device" : "Keyboard device";
                    string arrow = i == selectedIndex ? ">" : " ";

                    Console.WriteLine($"{arrow} [{(isSelected ? 'X' : ' ')}] {deviceName}");
                }

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = selectedIndex == 0 ? selectedDevices.Length - 1 : selectedIndex - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % selectedDevices.Length;
                        break;
                    case ConsoleKey.Enter:
                        selectedDevices[selectedIndex] = !selectedDevices[selectedIndex];
                        break;
                    case ConsoleKey.Tab:
                        confirmed = true;
                        break;
                }
            }

            Console.Clear();

            return selectedDevices;
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

            CPU cpu = new(1024, romBytes);

            bool[] selectedDevices = ShowDeviceMenu();

            if (selectedDevices[0])
            {
                cpu.RegisterDevice(new Device.DisplayDevice());
            }

            if (selectedDevices[1])
            {
                cpu.RegisterDevice(new Device.KeyboardDevice());
            }

            Console.WriteLine("Run in steps?");

            bool runInSteps = Console.ReadKey().Key == ConsoleKey.Y;

            if (SafeMode)
            {
                try
                {
                    CpuProcess(cpu, runInSteps);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"CPU ERROR: {e.Message}");
                }
            }
            else
            {
                CpuProcess(cpu, runInSteps);
            }

            return 0;
        }
    }
}
