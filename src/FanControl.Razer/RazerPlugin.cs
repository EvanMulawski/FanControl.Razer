﻿namespace FanControl.Razer;

using FanControl.Plugins;
using global::EMRazer;
using global::EMRazer.Synchronization;
using System.Timers;

public class RazerPlugin : IPlugin
{
    private readonly IDeviceGuardManager _deviceGuardManager;
    private readonly ILogger _logger;
    private readonly Timer _timer;
    private readonly object _timerLock = new();
    private IReadOnlyCollection<IDevice> _devices = new List<IDevice>(0);

    string IPlugin.Name => "Razer";

    public RazerPlugin(IPluginLogger logger)
    {
        _logger = new RazerPluginLogger(logger);
        _deviceGuardManager = new RazerDevicesGuardManager();
        _timer = new Timer(1000)
        {
            Enabled = false,
        };
        _timer.Elapsed += new ElapsedEventHandler(OnTimerTick);
    }

    public bool IsInitialized { get; private set; }

    private void OnTimerTick(object sender, ElapsedEventArgs e)
    {
        bool lockTaken = false;

        try
        {
            Monitor.TryEnter(_timerLock, 100, ref lockTaken);
            if (lockTaken)
            {
                foreach (var device in _devices)
                {
                    try
                    {
                        device.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Log($"An exception occurred refreshing device '{device.Name}' ({device.UniqueId}):");
                        Log(ex.ToString());
                    }
                }
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(_timerLock);
            }
        }
    }

    private void Log(string message)
    {
        _logger?.Log(message);
    }

    void IPlugin.Close()
    {
        CloseImpl();
    }

    private void CloseImpl()
    {
        if (!IsInitialized)
        {
            return;
        }

        _timer.Enabled = false;

        foreach (var device in _devices)
        {
            device.Disconnect();
        }

        IsInitialized = false;
    }

    void IPlugin.Initialize()
    {
        if (IsInitialized)
        {
            CloseImpl();
        }

        var initializedDevices = new List<IDevice>();
        var devices = DeviceManager.GetSupportedDevices(_deviceGuardManager, _logger);

        foreach (var device in devices)
        {
            try
            {
                if (!device.Connect())
                {
                    Log($"Device '{device.Name}' ({device.UniqueId}) failed to connect! This device will not be available.");
                    continue;
                }

                initializedDevices.Add(device);
            }
            catch (Exception ex)
            {
                Log($"An exception occurred attempting to initialize device '{device.Name}' ({device.UniqueId}):");
                Log(ex.ToString());
            }
        }

        _devices = initializedDevices;
        _timer.Enabled = true;
        IsInitialized = true;
    }

    void IPlugin.Load(IPluginSensorsContainer container)
    {
        if (!IsInitialized)
        {
            return;
        }

        foreach (var device in _devices)
        {
            Log($"Device '{device.Name}' ({device.UniqueId}, FW: {device.GetFirmwareVersion()}):");

            AddDeviceSpeedSensors(container, device);
            AddDeviceSpeedControllers(container, device);
            AddDeviceTemperatureSensors(container, device);
        }
    }

    private void AddDeviceSpeedSensors(IPluginSensorsContainer container, IDevice device)
    {
        foreach (var sensor in device.SpeedSensors)
        {
            var pluginSensor = new RazerSpeedSensor(device, sensor);
            container.FanSensors.Add(pluginSensor);
            Log($"  added {pluginSensor.Id}");
        }
    }

    private void AddDeviceSpeedControllers(IPluginSensorsContainer container, IDevice device)
    {
        foreach (var sensor in device.SpeedSensors.Where(ss => ss.SupportsControl))
        {
            var pluginController = new RazerSpeedController(device, sensor);
            container.ControlSensors.Add(pluginController);
            Log($"  added {pluginController.Id}");
        }
    }

    private void AddDeviceTemperatureSensors(IPluginSensorsContainer container, IDevice device)
    {
        foreach (var sensor in device.TemperatureSensors)
        {
            var pluginSensor = new RazerTemperatureSensor(device, sensor);
            container.TempSensors.Add(pluginSensor);
            Log($"  added {pluginSensor.Id}");
        }
    }
}
