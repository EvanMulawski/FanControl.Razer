using HidSharp;
using Serilog;
using System.Diagnostics;
using TestingApp;

var logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File($"log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt")
    .WriteTo.Console()
    .CreateLogger();

await Run(logger);

static async Task Run(ILogger logger)
{
    IRazerPwmFanController? device = null;

    try
    {
        var devices = DeviceList.Local
            .GetHidDevices(vendorID: 0x1532, productID: 0x0f3c)
            .ToList();

        foreach (var d in devices)
        {
            logger.Information("Found device: {devicePath} ({maxInputReportLength}, {maxOutputReportLength}, {maxFeatureReportLength})", d.DevicePath, d.GetMaxInputReportLength(), d.GetMaxOutputReportLength(), d.GetMaxFeatureReportLength());
        }

        if (!Debugger.IsAttached)
        {
            var firstValidDevice = devices.First(x => x.GetMaxFeatureReportLength() > 0);
            logger.Information("Using device: {devicePath}", firstValidDevice.DevicePath);
            device = new RazerPwmFanController(new HidSharpDeviceProxy(firstValidDevice), logger);
        }
        else
        {
            device = new MockRazerPwmFanController(logger);
        }

        if (!device.Connect())
        {
            logger.Error("Device failed to connect - exiting");
            return;
        }

        await RunInvestigation(device, logger);
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Top-level exception occurred - exiting");
    }
    finally
    {
        device?.Disconnect();
    }
}

static async Task RunInvestigation(IRazerPwmFanController device, ILogger logger)
{
    logger.Information("Starting investigation - THIS WILL TAKE A WHILE");
    logger.Information("THIS WILL TEST FAN 1 (ONE) ONLY - ENSURE FAN PORT 1 IS CONNECTED");

    var channelPowerRegistersToTest = Enumerable.Range(0, byte.MaxValue + 1).Select(Convert.ToByte).ToList();
    channelPowerRegistersToTest.Remove(0x02);

    device.SetChannelMode(0, 0x04); // manual
    await SetFan1ToZeroRpm();
    var startRpm = device.GetChannelSpeed(0);
    logger.Information("Fan 1 RPM: {rpm}", startRpm);

    foreach (var channelPowerRegister in channelPowerRegistersToTest)
    {
        logger.Information("Trying to set Fan 1 to 100% (register={register})", channelPowerRegister.ToString("X2"));
        device.SetChannelPower(0, 100, channelPowerRegister);
        await Task.Delay(2000);
        var currentRpm = device.GetChannelSpeed(0);
        logger.Information("Fan 1 RPM: {rpm}", currentRpm);
        if (currentRpm - startRpm > 50)
        {
            logger.Information("POSSIBLE");
            await Task.Delay(2000);
            currentRpm = device.GetChannelSpeed(0);
            logger.Information("Fan 1 RPM: {rpm}", currentRpm);
            if (currentRpm - startRpm > 250)
            {
                logger.Information("PROBABLE");
            }
            await SetFan1ToZeroRpm();
        }
    }

    logger.Information("Investigation completed");

    async Task SetFan1ToZeroRpm()
    {
        logger.Information("Setting Fan 1 to 0 RPM...");
        device.SetChannelPower(0, 0, 0x0d); // 0 rpm
        await Task.Delay(10000);
    }
}