namespace cpu.Simulator.Device
{
    public class DisplayDevice : IDevice
    {
        public string DisplayName => "Display Device PROT:1.0";

        public uint DeviceId => 0x01;

        private const uint BUFFER_LENGTH = 4;
        private uint BufferAddress;
        private bool DidInit = false;

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

        public void Draw(CPU cpu)
        {
            throw new NotImplementedException();
        }

        public void Tick(CPU cpu)
        {
            if (DidInit)
            {
                if (cpu.ReadRam(0, BufferAddress) != 0)
                {
                    byte identifier = (byte) cpu.ReadRam(0, BufferAddress + 1);
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
                    cpu.WriteRam(0, 0, BufferAddress);
                }
            }
        }
    }
}
