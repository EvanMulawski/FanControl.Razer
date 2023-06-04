using System.Diagnostics;

namespace EMRazer;

public static class Utils
{
    public static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
    }

    public static byte ToFractionalByte(int value) => (byte)((value * byte.MaxValue + 50) / 100);

    public static int FromFractionalByte(byte value) => (100 * value + byte.MaxValue / 2) / byte.MaxValue;

    private static readonly char[] HEX_CHARS = "0123456789ABCDEF".ToCharArray();

    public static string ToHexString(this ReadOnlySpan<byte> bytes)
    {
        char[] hexChars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            int v = bytes[i] & 0xff;
            hexChars[i * 2] = HEX_CHARS[v >> 4];
            hexChars[i * 2 + 1] = HEX_CHARS[v & 0x0f];
        }
        return new string(hexChars);
    }

    public static string ToHexString(this byte[] bytes) => ToHexString(bytes.AsSpan());

    private const long TICKS_PER_MILLISECOND = 10000;
    private const long TICKS_PER_SECOND = TICKS_PER_MILLISECOND * 1000;
    private static readonly double _tickFrequency = (double)TICKS_PER_SECOND / Stopwatch.Frequency;

    public static TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp) =>
        new((long)((endTimestamp - startTimestamp) * _tickFrequency));

    public static bool GetEnvironmentFlag(string flagName)
    {
        var variableValue = Environment.GetEnvironmentVariable(flagName);
        if (string.IsNullOrEmpty(variableValue))
        {
            variableValue = Environment.GetEnvironmentVariable(flagName, EnvironmentVariableTarget.Machine);
        }
        return !string.IsNullOrEmpty(variableValue) && (variableValue.ToLower() == "true" || variableValue == "1");
    }
}
