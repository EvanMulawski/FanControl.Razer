namespace EMRazer;

public interface IDeviceGuardManager
{
    IDisposable AwaitExclusiveAccess();
}
