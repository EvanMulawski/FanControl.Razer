using System.Buffers.Binary;
using System.Diagnostics;

namespace EMRazer.Devices.PwmFanController;

public sealed class PwmFanControllerDevice : IDevice
{
    public enum DeviceStatus : byte
    {
        Default = 0x00,
        Busy = 0x01,
        Success = 0x02,
        Error = 0x03,
    }

    public enum ProtocolType : byte
    {
        Default = 0x00,
    }

    public static class CommandClass
    {
        public static readonly byte Info = 0x00;
        public static readonly byte Pwm = 0x0d;
    }

    public static class PwmCommand
    {
        public static readonly byte SetChannelPercent = 0x0d;
        public static readonly byte SetChannelMode = 0x02;
        public static readonly byte GetChannelSpeed = 0x81;
    }

    private const int DEFAULT_SPEED_CHANNEL_POWER = 50;
    private const byte PERCENT_MIN = 0;
    private const byte PERCENT_MAX = 100;
    private const int DEVICE_READ_DELAY_MS = 5;
    private const int DEVICE_READ_TIMEOUT_MS = 500;
    private const int CHANNEL_COUNT = 8;
    private const int FORCE_WRITE_SPEEDS_INTERVAL_MS = 2500;
    private readonly IHidDeviceProxy _device;
    private readonly IDeviceGuardManager _guardManager;
    private readonly ILogger? _logger;
    private readonly SequenceCounter _sequenceCounter = new();
    private readonly SpeedChannelPowerTrackingStore _requestedChannelPower = new();
    private readonly Dictionary<int, SpeedSensor> _speedSensors = new();
    private readonly Dictionary<int, TemperatureSensor> _temperatureSensors = new(0);

    private long _lastSpeedWrite = 0L;

    public PwmFanControllerDevice(IHidDeviceProxy device, IDeviceGuardManager guardManager, ILogger? logger)
    {
        _device = device;
        _guardManager = guardManager;
        _logger = logger;

        var deviceInfo = device.GetDeviceInfo();
        Name = $"{deviceInfo.ProductName} ({deviceInfo.SerialNumber})";
        UniqueId = deviceInfo.DevicePath;
    }

    public string UniqueId { get; }

    public string Name { get; }

    public IReadOnlyCollection<SpeedSensor> SpeedSensors => _speedSensors.Values;

    public IReadOnlyCollection<TemperatureSensor> TemperatureSensors => _temperatureSensors.Values;

    private void Log(string message)
    {
        _logger?.Log($"{Name}: {message}");
    }

    public bool Connect()
    {
        Disconnect();

        var (opened, exception) = _device.Open();
        if (opened)
        {
            Initialize();
            return true;
        }

        if (exception is not null)
        {
            Log(exception.ToString());
        }

        return false;
    }

    private void Initialize()
    {
        _requestedChannelPower.Clear();

        for (var i = 0; i < CHANNEL_COUNT; i++)
        {
            using (_guardManager.AwaitExclusiveAccess())
            {
                SetChannelModeToManual(i);
            }

            SetChannelPower(i, DEFAULT_SPEED_CHANNEL_POWER);
            _speedSensors[i] = new SpeedSensor($"Fan #{i + 1}", i, default, supportsControl: true);
        }
    }

    public void Disconnect()
    {
        _device.Close();
    }

    public string GetFirmwareVersion()
    {
        try
        {
            using (_guardManager.AwaitExclusiveAccess())
            {
                var packet = new Packet
                {
                    SequenceNumber = _sequenceCounter.Next(),
                    DataLength = 0,
                    CommandClass = CommandClass.Info,
                    Command = 0x81,
                };

                var response = WriteAndRead(packet);
                var versionMajor = response.Data[0];
                var versionMinor = response.Data[1];
                return $"{versionMajor}.{versionMinor}";
            }
        }
        catch (Exception ex)
        {
            Log("Error retrieving firmware version.");
            Log(ex.ToString());
            return "ERROR";
        }
    }

    public void Refresh()
    {
        using (_guardManager.AwaitExclusiveAccess())
        {
            WriteRequestedSpeeds();
            RefreshSpeeds();
        }
    }

    private void RefreshSpeeds()
    {
        for (var i = 0; i < CHANNEL_COUNT; i++)
        {
            _speedSensors[i].Rpm = GetChannelSpeed(i);
        }
    }

    public int GetChannelSpeed(int channel)
    {
        Log(nameof(GetChannelSpeed));

        var packet = new Packet
        {
            SequenceNumber = _sequenceCounter.Next(),
            DataLength = 6,
            CommandClass = CommandClass.Pwm,
            Command = PwmCommand.GetChannelSpeed,
        };

        packet.Data[0] = 0x01;
        packet.Data[1] = (byte)(0x05 + channel);

        var response = WriteAndRead(packet);
        var rpm = BinaryPrimitives.ReadInt16BigEndian(response.Data.AsSpan(4, 2));
        return rpm;
    }

    public void SetChannelPower(int channel, int percent)
    {
        _requestedChannelPower[channel] = (byte)Utils.Clamp(percent, PERCENT_MIN, PERCENT_MAX);
    }

    private void WriteRequestedSpeeds()
    {
        if (!_requestedChannelPower.Dirty && !IsForceWriteRequestedSpeedsRequired())
        {
            return;
        }

        Log(nameof(WriteRequestedSpeeds));

        for (var i = 0; i < CHANNEL_COUNT; i++)
        {
            var packet = new Packet
            {
                SequenceNumber = _sequenceCounter.Next(),
                DataLength = 3,
                CommandClass = CommandClass.Pwm,
                Command = PwmCommand.SetChannelPercent,
            };

            packet.Data[0] = 0x01;
            packet.Data[1] = (byte)(0x05 + i);
            packet.Data[2] = _requestedChannelPower[i];

            WriteAndRead(packet); 
        }

        _lastSpeedWrite = Stopwatch.GetTimestamp();
        _requestedChannelPower.ResetDirty();
    }

    private bool IsForceWriteRequestedSpeedsRequired()
    {
        return Utils.GetElapsedTime(_lastSpeedWrite, Stopwatch.GetTimestamp()).TotalMilliseconds >= FORCE_WRITE_SPEEDS_INTERVAL_MS;
    }

    private void SetChannelModeToManual(int channel)
    {
        Log(nameof(SetChannelModeToManual));

        var packet = new Packet
        {
            SequenceNumber = _sequenceCounter.Next(),
            DataLength = 3,
            CommandClass = CommandClass.Pwm,
            Command = PwmCommand.SetChannelMode,
        };

        packet.Data[0] = 0x01;
        packet.Data[1] = (byte)(0x05 + channel);
        packet.Data[2] = 0x04;

        WriteAndRead(packet);
    }

    private Packet WriteAndRead(Packet packet)
    {
        var response = Packet.CreateBuffer();
        var buffer = packet.ToBuffer();

        Log($"WRITE: {buffer.ToHexString()}");
        _device.WriteFeature(buffer);
        Thread.Sleep(DEVICE_READ_DELAY_MS);
        _device.ReadFeature(response);
        Log($"READ:  {response.ToHexString()}");
        var readPacket = Packet.FromBuffer(response);

        if (readPacket.Status == DeviceStatus.Busy)
        {
            var cts = new CancellationTokenSource(DEVICE_READ_TIMEOUT_MS);

            while (!cts.IsCancellationRequested && readPacket.Status == DeviceStatus.Busy)
            {
                Thread.Sleep(DEVICE_READ_DELAY_MS);
                _device.ReadFeature(response);
                Log($"READ:  {response.ToHexString()}");
                readPacket = Packet.FromBuffer(response);
            }

            if (cts.IsCancellationRequested)
            {
                throw new RazerDeviceException("Wait expired for successful device status after write.");
            }
        }

        if (readPacket.Status != DeviceStatus.Success)
        {
            throw new RazerDeviceException($"Device status not OK after write ({readPacket.Status}).");
        }

        return readPacket;
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
}
