using System.Numerics;
using Raylib_cs;

namespace cpu.Simulator.Device
{
    public class DisplayDevice : IDevice
    {
        public string DisplayName => "Display Device PROT:1.0";

        public uint DeviceId => 0x01;

        private const uint BUFFER_LENGTH = 4;
        private uint BufferAddress;
        private bool DidInit = false;

        private const int WIDTH = 640 / FONT_WIDTH;
        private const int HEIGHT = 360 / FONT_SIZE;
        private const int FONT_SIZE = 16;
        private const int FONT_WIDTH = 8;
        private const int TAB_WIDTH = 4;
        private char[] display = new char[WIDTH * HEIGHT];
        private int caret = 0;

        byte caretBlink = 0; 

        public bool RaylibMode = true;

        public void AfterInterrupt(CPU cpu)
        {
            Console.WriteLine("DISPLAY INIT SUCCESS");
            BufferAddress = cpu.GetRegister(0x06);
            DidInit = true;
        }

        public void BeforeInterrupt(CPU cpu)
        {
            cpu.SetRegister(0x05, BUFFER_LENGTH);
        }

        public void Draw(CPU cpu, Texture2D font)
        {
            caretBlink += (byte) Raylib.GetFPS();

            int scale = Math.Min(Raylib.GetScreenWidth() / 640, Raylib.GetScreenHeight() / 360);

            Raylib.ClearBackground(Color.Black);

            for (int i = 0; i < WIDTH * HEIGHT; i++)
            {
                int x = i % WIDTH * FONT_WIDTH * scale;
                int y = i / WIDTH * FONT_SIZE * scale;

                int codepoint = display[i];

                Raylib.DrawTexturePro(
                    font,
                    new(codepoint * FONT_WIDTH, 0, FONT_WIDTH, FONT_SIZE),
                    new(x, y, FONT_WIDTH * scale, FONT_SIZE * scale),
                    Vector2.Zero,
                    0,
                    (codepoint < 32 || codepoint >= 127) ? new(16, 16, 16) : new(200, 200, 200)
                );

                if (i == caret && caretBlink < 128)
                {
                    Raylib.DrawTexturePro(
                        font,
                        new('_' * FONT_WIDTH, 0, FONT_WIDTH, FONT_SIZE),
                        new(x, y, FONT_WIDTH * scale, FONT_SIZE * scale),
                        Vector2.Zero,
                        0,
                        new(255, 255, 255)
                    );
                }
            }
        }

        private void ProcessInstConsoleMode(uint identifier, CPU cpu)
        {
            switch (identifier)
            {
                case 0x00:
                    break;
                case 0x01:
                    {
                        char c = (char) cpu.ReadRam(0, BufferAddress + 2);
                        Console.Write(c);
                    }
                    break;
                case 0x02:
                    Console.Clear();
                    break;
            }
        }

        private void ProcessInstRaylibMode(uint identifier, CPU cpu)
        {
            switch (identifier)
            {
                case 0x00:
                    break;
                case 0x01:
                    {
                        char c = (char) cpu.ReadRam(0, BufferAddress + 2);
                        if (c == '\n')
                        {
                            caret = (caret / WIDTH + 1) * WIDTH;
                        }
                        else if (c == '\t')
                        {
                            caret = (caret / TAB_WIDTH + 1) * TAB_WIDTH;
                        }
                        else
                        {
                            display[caret] = c;
                            caret++;
                        }
                        caret %= WIDTH * HEIGHT;
                    }
                    break;
                case 0x02:
                    {
                        for (int i = 0; i < WIDTH * HEIGHT; i++)
                        {
                            display[i] = '\0';
                        }
                        caret = 0;
                    }
                    break;
            }
        }

        public void Tick(CPU cpu)
        {
            if (DidInit)
            {
                if (cpu.ReadRam(0, BufferAddress) != 0)
                {
                    byte identifier = (byte) cpu.ReadRam(0, BufferAddress + 1);
                    if (RaylibMode)
                    {
                        ProcessInstRaylibMode(identifier, cpu);
                    }
                    else
                    {
                        ProcessInstConsoleMode(identifier, cpu);
                    }
                    cpu.WriteRam(0, 0, BufferAddress);
                }
            }
        }
    }
}
