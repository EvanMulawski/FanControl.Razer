using EMRazer.Devices.PwmFanController;
using HidSharp;
using System.Text;

namespace EMRazer;

public static class DeviceManager
{
    public static IReadOnlyCollection<IDevice> GetSupportedDevices(IDeviceGuardManager deviceGuardManager, ILogger? logger)
    {
        var collection = new List<IDevice>();

        var devices = DeviceList.Local
            .GetHidDevices(vendorID: HardwareIds.RazerVendorId)
            .ToList();
        logger?.LogDevices(devices, "Razer device(s)");

        var supportedDevices = devices
            .Where(x => HardwareIds.SupportedProductIds.Contains(x.ProductID) && x.GetMaxFeatureReportLength() > 0)
            .ToList();
        logger?.LogDevices(supportedDevices, "supported Razer device(s)");

        collection.AddRange(supportedDevices.InDeviceDriverGroup(HardwareIds.DeviceDriverGroups.PwmFanController)
            .Select(x => new PwmFanControllerDevice(new HidSharpDeviceProxy(x), deviceGuardManager, logger)));

        return collection;
    }

    private static IEnumerable<HidDevice> InDeviceDriverGroup(this IEnumerable<HidDevice> devices, IEnumerable<int> deviceDriverGroup)
    {
        return devices.Join(deviceDriverGroup, d => d.ProductID, g => g, (d, _) => d);
    }

    private static void LogDevices(this ILogger? logger, IReadOnlyCollection<HidDevice> devices, string description)
    {
        if (logger is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"DeviceManager: Found {devices.Count} {description}");
        foreach (var device in devices)
        {
            sb.AppendLine($"  name={device.GetProductNameOrDefault()}, devicePath={device.DevicePath}");
        }
        logger.Log(sb.ToString());
    }
}
