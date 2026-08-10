namespace cpu.Simulator.Device
{
    public class DisplayDevice : IDevice
    {
        public string DisplayName => "Display Device PROT:0.1";

        public uint DeviceId => 0x01;

        private const uint BUFFER_LENGTH = 4;
        private uint? BufferAddress = null;

        public void AfterInterrupt(CPU cpu)
        {
            BufferAddress = cpu.GetRegister(0x06);
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
            if (BufferAddress != null)
            {
                
            }
        }
    }
}