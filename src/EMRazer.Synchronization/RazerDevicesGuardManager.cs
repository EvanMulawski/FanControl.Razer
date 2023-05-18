namespace EMRazer.Synchronization;

public class RazerDevicesGuardManager : IDeviceGuardManager
{
    public IDisposable AwaitExclusiveAccess()
    {
        return new RazerDevicesGuardLock();
    }
}
