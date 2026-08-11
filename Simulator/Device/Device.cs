using Raylib_cs;

namespace cpu.Simulator.Device
{
    public interface IDevice
    {
        public string DisplayName { get; }
        public uint DeviceId { get; }

        public void BeforeInterrupt(CPU cpu);
        public void AfterInterrupt(CPU cpu);
        public void Draw(CPU cpu, Texture2D font);
        public void Tick(CPU cpu);
    }
}
