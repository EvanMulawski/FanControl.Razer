using Serilog;
using System.Buffers.Binary;
using TestingApp;

internal sealed class RazerPwmFanController : IRazerPwmFanController
{
    public enum DeviceStatus : byte
    {
        Default = 0x00,
        Busy = 0x01,
        Success = 0x02,
    }

    public enum ProtocolType : byte
    {
        Default = 0x00,
    }

    public static class CommandClass
    {
        public static readonly byte Pwm = 0x0d;
    }

    public sealed class Packet
    {
        public byte ReportId { get; set; }
        public DeviceStatus Status { get; set; }
        public byte SequenceNumber { get; set; }
        public short RemainingCount { get; set; }
        public ProtocolType ProtocolType { get; set; }
        public byte DataLength { get; set; }
        public byte CommandClass { get; set; }
        public byte Command { get; set; }
        public byte[] Data { get; } = new byte[80];
        public byte CRC { get; set; }
        public byte Reserved { get; set; }

        public byte[] ToBuffer()
        {
            var buffer = CreateBuffer();
            buffer[0] = ReportId;
            buffer[1] = (byte)Status;
            buffer[2] = SequenceNumber;
            BinaryPrimitives.WriteInt16BigEndian(buffer.AsSpan(3), RemainingCount);
            buffer[5] = (byte)ProtocolType;
            buffer[6] = DataLength;
            buffer[7] = CommandClass;
            buffer[8] = Command;
            Data.CopyTo(buffer.AsSpan(9, Data.Length));
            buffer[89] = GenerateChecksum(buffer);
            buffer[90] = Reserved;
            return buffer;
        }

        public static Packet FromBuffer(ReadOnlySpan<byte> buffer)
        {
            var packet = new Packet
            {
                ReportId = buffer[0],
                Status = (DeviceStatus)buffer[1],
                SequenceNumber = buffer[2],
                RemainingCount = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(3)),
                ProtocolType = (ProtocolType)buffer[5],
                DataLength = buffer[6],
                CommandClass = buffer[7],
                Command = buffer[8],
                CRC = buffer[89],
                Reserved = buffer[90]
            };
            buffer.Slice(9, packet.Data.Length).CopyTo(packet.Data);
            return packet;
        }

        public static byte[] CreateBuffer() => new byte[91];

        internal static byte GenerateChecksum(ReadOnlySpan<byte> buffer)
        {
            byte result = 0;
            for (int i = 3; i < 89; i++)
            {
                result = (byte)(result ^ buffer[i]);
            }
            return result;
        }
    }

    private sealed class SequenceCounter
    {
        private byte _sequenceId = 0x00;

        public byte Next()
        {
            do
            {
                _sequenceId += 0x08;
            }
            while (_sequenceId == 0x00);
            return _sequenceId;
        }
    }

    private readonly HidSharpDeviceProxy _device;
    private readonly ILogger _logger;
    private readonly SequenceCounter _sequenceCounter = new();

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

        var packet = new Packet
        {
            SequenceNumber = _sequenceCounter.Next(),
            DataLength = 6,
            CommandClass = CommandClass.Pwm,
            Command = 0x81,
        };

        packet.Data[0] = 0x01;
        packet.Data[1] = (byte)(0x05 + channel);

        var response = WriteAndRead(packet, nameof(GetChannelSpeed));
        var rpm = BinaryPrimitives.ReadInt16BigEndian(response.Data.AsSpan(5, 2));
        return rpm;
    }

    public void SetChannelPower(int channel, int power, byte command)
    {
        var powerFractionalByte = Utils.ToFractionalByte(power);
        Log(nameof(SetChannelPower), $"ch={channel} p={power}% pfb={powerFractionalByte:X2} cmd={command:X2}");

        var packet = new Packet
        {
            SequenceNumber = _sequenceCounter.Next(),
            DataLength = 3,
            CommandClass = CommandClass.Pwm,
            Command = command,
        };

        packet.Data[0] = 0x01;
        packet.Data[1] = (byte)(0x05 + channel);
        packet.Data[2] = powerFractionalByte;

        WriteAndRead(packet, nameof(SetChannelPower));
    }

    public void SetChannelMode(int channel, byte mode)
    {
        Log(nameof(SetChannelMode), $"ch={channel} m={mode:X2}");

        var packet = new Packet
        {
            SequenceNumber = _sequenceCounter.Next(),
            DataLength = 3,
            CommandClass = CommandClass.Pwm,
            Command = 0x02,
        };

        packet.Data[0] = 0x01;
        packet.Data[1] = (byte)(0x05 + channel);
        packet.Data[2] = mode;

        WriteAndRead(packet, nameof(SetChannelMode));
    }

    private Packet WriteAndRead(Packet packet, string description)
    {
        var response = Packet.CreateBuffer();
        var buffer = packet.ToBuffer();

        Log(nameof(WriteAndRead), $"{description} WRITE: ", buffer);
        _device.Write(buffer);
        Thread.Sleep(50);
        Read(response, description);
        var readPacket = Packet.FromBuffer(response);

        if (readPacket.Status == DeviceStatus.Busy)
        {
            var cts = new CancellationTokenSource(500);

            while (!cts.IsCancellationRequested && readPacket.Status == DeviceStatus.Busy)
            {
                Thread.Sleep(50);
                Read(response, description);
                readPacket = Packet.FromBuffer(response);
            }

            if (cts.IsCancellationRequested)
            {
                throw new Exception($"{description}: Wait expired for successful device status after write.");
            }
        }

        if (readPacket.Status != DeviceStatus.Success)
        {
            throw new Exception($"{description}: Device status not OK after write ({readPacket.Status}).");
        }

        return readPacket;
    }

    private void Read(byte[] buffer, string description)
    {
        _device.Read(buffer);
        Log(nameof(WriteAndRead), $"{description} READ:  ", buffer);
    }
}
