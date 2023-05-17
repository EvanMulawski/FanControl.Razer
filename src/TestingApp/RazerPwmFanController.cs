using Serilog;
using System.Buffers.Binary;
using TestingApp;

internal sealed class RazerPwmFanController : IRazerPwmFanController
{
    private readonly HidSharpDeviceProxy _device;
    private readonly ILogger _logger;

    public RazerPwmFanController(HidSharpDeviceProxy device, ILogger logger)
    {
        _device = device;
        _logger = logger;
    }

    public bool Connect()
    {
        Disconnect();

        Log(nameof(Disconnect), "Opening device...");
        var (opened, exception) = _device.Open();
        if (opened)
        {
            Initialize();
            return true;
        }

        if (exception is not null)
        {
            _logger.Error(exception, "Device open failed");
        }

        return false;
    }

    private void Initialize()
    {
        
    }

    public void Disconnect()
    {
        Log(nameof(Disconnect), "Closing device...");
        _device.Close();
    }

    private void Log(string category, string message)
    {
        _logger.Debug("<{category}> {message}", category, message);
    }

    private void Log(string category, string message, ReadOnlySpan<byte> buffer)
    {
        Log(category, string.Concat(message, " ", Utils.ToHexString(buffer)));
    }

    public int GetChannelSpeed(int channel)
    {
        Log(nameof(GetChannelSpeed), $"{channel}");

        var request = CreateRequest();
        request[2] = 0x1f;
        request[6] = 0x06;
        request[7] = 0x0d;
        request[8] = 0x81;
        request[9] = 0x01;
        request[10] = (byte)(0x05 + channel);

        var response = WriteAndRead(request, nameof(GetChannelSpeed));
        var rpm = BinaryPrimitives.ReadInt16BigEndian(response.AsSpan(13, 2));
        return rpm;
    }

    public void SetChannelPower(int channel, int power, byte registerToSet)
    {
        var powerFractionalByte = Utils.ToFractionalByte(power);
        Log(nameof(SetChannelPower), $"ch={channel} p={power}% pfb={powerFractionalByte:X2}");

        var request = CreateRequest();
        request[2] = 0x1f;
        request[6] = 0x03;
        request[7] = 0x0d;
        request[8] = registerToSet; // for invest.
        request[9] = 0x01;
        request[10] = (byte)(0x05 + channel);
        request[11] = powerFractionalByte;

        WriteAndRead(request, nameof(SetChannelPower));
    }

    public void SetChannelMode(int channel, byte mode)
    {
        Log(nameof(SetChannelMode), $"ch={channel} m={mode:X2}");

        var request = CreateRequest();
        request[2] = 0x1f;
        request[6] = 0x03;
        request[7] = 0x0d;
        request[8] = 0x02;
        request[9] = 0x01;
        request[10] = (byte)(0x05 + channel);
        request[11] = mode; // for invest.

        WriteAndRead(request, nameof(SetChannelMode));
    }

    private static void SetChecksumByte(byte[] buffer)
    {
        buffer[89] = GenerateChecksum(buffer);
    }

    internal static byte GenerateChecksum(ReadOnlySpan<byte> buffer)
    {
        byte result = 0;
        for (int i = 3; i < 89; i++)
        {
            result = (byte)(result ^ buffer[i]);
        }
        return result;
    }

    private byte[] WriteAndRead(byte[] buffer, string description)
    {
        var response = CreateResponse();

        SetChecksumByte(buffer);
        Log(nameof(WriteAndRead), $"{description} WRITE: ", buffer);
        _device.Write(buffer);
        _device.Read(response);
        Log(nameof(WriteAndRead), $"{description} READ:  ", response);

        return response;
    }

    private static byte[] CreateRequest()
    {
        var writeBuf = new byte[91];
        return writeBuf;
    }

    private static byte[] CreateResponse()
    {
        return new byte[91];
    }
}
