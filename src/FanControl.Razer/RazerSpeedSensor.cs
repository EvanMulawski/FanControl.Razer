using EMRazer;
using FanControl.Plugins;

namespace FanControl.Razer;

public sealed class RazerSpeedSensor : IPluginSensor
{
    private readonly SpeedSensor _sensor;

    public RazerSpeedSensor(IDevice device, SpeedSensor sensor)
    {
        _sensor = sensor;

        Id = $"Razer/{device.UniqueId}/SpeedSensor/{sensor.Channel}";
        Name = $"{device.Name} {sensor.Name}";
    }

    public string Id { get; }

    public string Name { get; }

    public float? Value { get; private set; }

    public void Update()
    {
        Value = _sensor.Rpm;
    }
}
