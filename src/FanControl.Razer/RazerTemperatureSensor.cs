using EMRazer;
using FanControl.Plugins;

namespace FanControl.Razer;

public sealed class RazerTemperatureSensor : IPluginSensor
{
    private readonly TemperatureSensor _sensor;

    public RazerTemperatureSensor(IDevice device, TemperatureSensor sensor)
    {
        _sensor = sensor;

        Id = $"Razer/{device.UniqueId}/TemperatureSensor/{sensor.Channel}";
        Name = $"{device.Name} {sensor.Name}";
    }

    public string Id { get; }

    public string Name { get; }

    public float? Value { get; private set; }

    public void Update()
    {
        Value = _sensor.TemperatureCelsius;
    }
}
