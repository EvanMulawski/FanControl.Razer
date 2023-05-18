namespace EMRazer.Synchronization;

internal sealed class RazerDevicesGuardLock : IDisposable
{
    public RazerDevicesGuardLock()
    {
        RazerDevicesGuard.Acquire();
    }

    public void Dispose()
    {
        RazerDevicesGuard.Release();
    }
}

