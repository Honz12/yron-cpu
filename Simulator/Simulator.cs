using cpu.Simulator.Device;
using Raylib_cs;


namespace cpu.Simulator
{
    public static class Simulator
    {
        public static bool SafeMode = true;
        public static bool DebugMode = true;

        public static int InstPerDraw = 50000;

        public static Texture2D? font = null;

        public static bool IsFirstTick = false;

        public static void Run()
        {
            Console.Write("ROM path (default: rom.bin): ");
            string romPath = Console.ReadLine()?.Trim() ?? "";
            if (romPath.Length == 0) romPath = "rom.bin";

            RunFromArgs(romPath);
        }

        public static void WriteSep()
        {
            Console.WriteLine("------------------------------");
        }

        private static string FindFontPath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "font.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "font.png")
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return candidate;
            return candidates[0];
        }

        public static void CpuProcess(CPU cpu, bool stepMode, bool startFullscreen = false)
        {
            Console.WriteLine("If you ever feel useless just remember this statement is to make Windows 11 inteligent app blocker shut up :D");

            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(640, 360, "YRON SIMULATOR");
            if (startFullscreen)
            {
                Raylib.ToggleFullscreen();
            }
            Raylib.SetExitKey(KeyboardKey.Null);
            Raylib.SetTargetFPS(30);
        
            font = Raylib.LoadTexture(FindFontPath());

            bool imdInputMode = false;
            
            while (!cpu.Halted && !Raylib.WindowShouldClose())
            {
                if (Raylib.IsKeyPressed(KeyboardKey.F11))
                {
                    Raylib.ToggleFullscreen();
                }
                if (Raylib.IsKeyPressed(KeyboardKey.F2) && stepMode)
                {
                    imdInputMode = !imdInputMode;
                }
                if (Raylib.IsKeyPressed(KeyboardKey.F3) && stepMode)
                {
                    Raylib.CloseWindow();

                    _ = uint.TryParse(Console.ReadLine(), out uint jump);

                    cpu.Registers[CPU.REG_PC] = jump;

                    imdInputMode = !imdInputMode;
                    Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
                    Raylib.InitWindow(640, 360, "YRON SIMULATOR");
                    if (startFullscreen)
                    {
                        Raylib.ToggleFullscreen();
                    }
                    Raylib.SetExitKey(KeyboardKey.Null);
                    Raylib.SetTargetFPS(30);
                }

                IsFirstTick = true;
                if (!stepMode)
                {
                    for (int i = 0; i < InstPerDraw; i++)
                    {
                        cpu.RunInst();
                        IsFirstTick = false;
                    }
                }
                else
                {
                    if (Raylib.IsKeyPressed(KeyboardKey.F1) || Raylib.IsKeyPressedRepeat(KeyboardKey.F1))
                    {
                        cpu.RunInst();
                    }
                    if (imdInputMode)
                    {
                        if (InputHelper.ReadPressedChar() != 0)
                        {
                            cpu.RunInst();
                            imdInputMode = false;
                        }
                    }
                }
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                foreach (IDevice device in cpu.Devices)
                {
                    if (font.HasValue) device.Draw(cpu, font.Value);
                }
                Raylib.DrawText($"FPS: {Raylib.GetFPS()}", 0, 0, 10, Color.Magenta);
                Raylib.DrawText($"IPS: {Raylib.GetFPS() * InstPerDraw}", 0, 12, 10, Color.Magenta);
                cpu.RegisterDumpRaylib(24);
                if (imdInputMode)
                {
                    Raylib.DrawText("IMIDIATE INPUT MODE", 0, Raylib.GetScreenHeight() - 10, 10, Color.Magenta);
                }
                Raylib.EndDrawing();
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

        public static int RunFromArgs(string path)
        {
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return RunFromConfig(path);
            return RunFromRom(path);
        }

        public static int RunFromRom(string romPath)
        {
            InstPerDraw = 50000;

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

            Console.WriteLine("Press [S] to enable step mode.");
            bool stepMode = Console.ReadKey().Key == ConsoleKey.S;

            return RunLoop(cpu, stepMode, false);
        }

        public static int RunFromConfig(string configPath)
        {
            SimConfig config;
            try
            {
                config = SimConfig.Load(configPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"CONFIG ERROR: {e.Message}");
                return 1;
            }

            string romPath = config.Rom;
            if (romPath.Length == 0)
            {
                Console.WriteLine($"CONFIG ERROR: no rom specified in '{configPath}'");
                return 1;
            }

            if (!File.Exists(romPath))
            {
                Console.WriteLine($"File '{romPath}' does not exist (from config '{configPath}')");
                return 1;
            }

            byte[] romBytes = File.ReadAllBytes(romPath);

            Console.WriteLine($"Loaded rom of {romBytes.Length} byte{(romBytes.Length != 1 ? "s" : "")}");

            CPU cpu = new(1024, romBytes);

            if (config.Devices.TryGetValue("display", out bool display) && display)
            {
                cpu.RegisterDevice(new Device.DisplayDevice());
            }

            if (config.Devices.TryGetValue("keyboard", out bool keyboard) && keyboard)
            {
                cpu.RegisterDevice(new Device.KeyboardDevice());
            }

            InstPerDraw = config.Ipd;

            return RunLoop(cpu, config.StepMode, config.Fullscreen);
        }

        private static int RunLoop(CPU cpu, bool stepMode, bool startFullscreen)
        {
            if (SafeMode)
            {
                try
                {
                    CpuProcess(cpu, stepMode, startFullscreen);
                }
                catch (NotImplementedException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"CPU ERROR: {e.Message}");
                }
            }
            else
            {
                CpuProcess(cpu, stepMode, startFullscreen);
            }

            if (font.HasValue)
            {
                Raylib.UnloadTexture(font.Value);
            }
            Raylib.CloseWindow();

            return 0;
        }
    }
}
