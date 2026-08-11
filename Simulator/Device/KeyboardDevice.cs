using Raylib_cs;

namespace cpu.Simulator.Device
{
    public class KeyboardDevice : IDevice
    {
        public string DisplayName => "Keyboard Device PROT:0.1";

        public uint DeviceId => 0x02;

        private bool DidInit = false;

        public bool RaylibMode = true;

        public void AfterInterrupt(CPU cpu)
        {
            Console.WriteLine("KEYBOARD INIT SUCCESS");
            DidInit = true;
        }

        public void BeforeInterrupt(CPU cpu)
        {
            cpu.SetRegister(0x05, 0); // we dont need memory
        }

        public void Draw(CPU cpu, Texture2D font)
        {
        }

        public void Tick(CPU cpu)
        {
            if (DidInit)
            {
                if (RaylibMode)
                {
                    char read = InputHelper.ReadPressedChar();
                    
                    if (read != 0)
                    {
                        cpu.SetRegister(0x03, DeviceId);
                        cpu.SetRegister(0x04, read);

                        cpu.CallInterrupt(0x02);
                    }
                }
                else
                {
                    if (Console.KeyAvailable)
                    {
                        uint read = Console.ReadKey(true).KeyChar;
                        if (read == 17) // CTRL+Q
                        {
                            cpu.Halted = true;
                        }
                        cpu.SetRegister(0x03, DeviceId);
                        cpu.SetRegister(0x04, read);

                        cpu.CallInterrupt(0x02);
                    }
                }
            }
        }
    }
}
