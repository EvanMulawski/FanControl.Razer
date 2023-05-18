namespace EMRazer;

public static class HardwareIds
{
    public static readonly int RazerVendorId = 0x1532;

    public static readonly int RazerPwmFanControllerProductId = 0x0f3c;

    public static readonly IReadOnlyCollection<int> SupportedProductIds = new List<int>
    {
        // PWM Fan Controller
        RazerPwmFanControllerProductId,
    };

    public static class DeviceDriverGroups
    {
        public static readonly IReadOnlyCollection<int> PwmFanController = new List<int>
        {
            RazerPwmFanControllerProductId,
        };
    }
}
