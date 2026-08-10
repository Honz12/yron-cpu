namespace cpu.Simulator.Device
{
    public interface IDevice
    {
        public string DisplayName { get; }
        public uint DeviceId { get; }

        public void BeforeInterrupt(CPU cpu);
        public void AfterInterrupt(CPU cpu);
        public void Draw(CPU cpu);
        public void Tick(CPU cpu);
    }
}
