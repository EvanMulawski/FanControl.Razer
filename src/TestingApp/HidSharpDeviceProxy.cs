﻿using HidSharp;

namespace TestingApp;

internal class HidSharpDeviceProxy
{
    private readonly HidDevice _device;
    private HidStream? _stream;

    public HidSharpDeviceProxy(HidDevice device)
    {
        _device = device;
    }

    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public (bool Opened, Exception? Exception) Open()
    {
        Close();

        try
        {
            var opened = _device.TryOpen(out _stream);
            return (opened, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    public void Read(byte[] buffer)
    {
        ThrowIfNotReady();

        _stream?.GetFeature(buffer, 0, buffer.Length);
    }

    public void Write(byte[] buffer)
    {
        ThrowIfNotReady();

        //ClearEnqueuedReports();
        _stream?.SetFeature(buffer, 0, buffer.Length);
    }

    public void ClearEnqueuedReports()
    {
        ThrowIfNotReady();

        var originalReadTimeout = _stream!.ReadTimeout;
        _stream.ReadTimeout = 1;

        try
        {
            while (true)
            {
                _ = _stream.Read();
            }
        }
        catch (TimeoutException)
        {
            // cleared!
        }
        finally
        {
            _stream.ReadTimeout = originalReadTimeout;
        }
    }

    private void ThrowIfNotReady()
    {
        bool @throw;
        try
        {
            @throw = _stream is null;
        }
        catch (ObjectDisposedException)
        {
            @throw = true;
        }

        if (@throw)
        {
            throw new InvalidOperationException("The device is not ready.");
        }
    }
}
