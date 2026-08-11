using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace cpu.Simulator
{
    public sealed class SimConfig
    {
        private static readonly string[] ValidDevices = { "display", "keyboard" };

        public string Rom { get; set; } = "rom.bin";
        public Dictionary<string, bool> Devices { get; set; } = new();
        public int Ipd { get; set; } = Simulator.InstPerDraw;
        public bool StepMode { get; set; }
        public bool Fullscreen { get; set; }

        public static SimConfig CreateDefault()
        {
            SimConfig config = new();
            foreach (string device in ValidDevices)
                config.Devices[device] = false;
            return config;
        }

        public static SimConfig Load(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                throw new Exception($"cannot read '{path}': {e.Message}");
            }

            SimConfig config;
            try
            {
                config = JsonSerializer.Deserialize<SimConfig>(json, SerializerOptions)
                    ?? throw new Exception($"'{path}' contains no config");
            }
            catch (JsonException e)
            {
                throw new Exception($"'{path}' is not valid JSON: {e.Message}");
            }

            foreach (string device in config.Devices.Keys)
            {
                bool valid = false;
                foreach (string known in ValidDevices)
                    if (device == known) valid = true;
                if (!valid)
                    throw new Exception($"unknown device '{device}' in '{path}' (valid devices: {string.Join(", ", ValidDevices)})");
            }

            if (config.Ipd < 1)
                throw new Exception($"invalid ipd {config.Ipd} in '{path}' (must be at least 1)");

            return config;
        }

        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
