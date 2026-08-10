namespace cpu.Simulator.Device
{
    public class KeyboardDevice : IDevice
    {
        public string DisplayName => "Keyboard Device PROT:0.1";

        public uint DeviceId => 0x01;

        private bool DidInit = false;

        public void AfterInterrupt(CPU cpu)
        {
            Console.WriteLine("KEYBOARD INIT SUCCESS");
            DidInit = true;
        }

        public void BeforeInterrupt(CPU cpu)
        {
            cpu.SetRegister(0x05, 0); // we dont need memory
        }

        public void Draw(CPU cpu)
        {
            throw new NotImplementedException();
        }

        public void Tick(CPU cpu)
        {
        }
    }
}