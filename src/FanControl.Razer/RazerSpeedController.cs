using EMRazer;
using FanControl.Plugins;

namespace FanControl.Razer;

public sealed class RazerSpeedController : IPluginControlSensor
{
    private readonly IDevice _device;
    private readonly SpeedSensor _sensor;
    private float? _value;

    public RazerSpeedController(IDevice device, SpeedSensor sensor)
    {
        _device = device;
        _sensor = sensor;

        Id = $"Razer/{device.UniqueId}/SpeedController/{sensor.Channel}";
        Name = $"{device.Name} {sensor.Name}";
        _sensor = sensor;
    }

    public string Id { get; }

    public string Name { get; }

    public float? Value { get; private set; }

    public void Reset()
    {
        _value = null;
        _device.SetChannelPower(_sensor.Channel, 50);
    }

    public void Set(float val)
    {
        _value = val;
        _device.SetChannelPower(_sensor.Channel, (int)val);
    }

    public void Update()
    {
        Value = _value;
    }
}
