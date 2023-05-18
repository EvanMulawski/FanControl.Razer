using Serilog;

namespace TestingApp;

internal sealed class MockRazerPwmFanController : IRazerPwmFanController
{
    private readonly ILogger _logger;
    private int _rpmToReturn;

    public MockRazerPwmFanController(ILogger logger)
    {
        _logger = logger;
    }

    public bool Connect()
    {
        Log(nameof(Connect), "Connecting to mock device...");
        return true;
    }

    public void Disconnect()
    {
        Log(nameof(Disconnect), "Disconnecting from mock device...");
    }

    public int GetChannelSpeed(int channel)
    {
        Log(nameof(GetChannelSpeed), $"{channel}");
        return _rpmToReturn;
    }

    public void SetChannelMode(int channel, byte mode)
    {
        Log(nameof(SetChannelMode), $"{channel} {mode:X2}");
    }

    public void SetChannelPower(int channel, int power, byte registerToSet)
    {
        Log(nameof(SetChannelPower), $"{channel} {power}% {registerToSet:X2}");

        _rpmToReturn = registerToSet switch
        {
            0x01 => throw new Exception(),
            0x05 => 1000,
            _ => 0,
        };
    }

    private void Log(string category, string message)
    {
        _logger.Debug("<{category}> {message}", category, message);
    }
}
