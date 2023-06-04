﻿using HidSharp;

namespace EMRazer;

internal sealed class HidSharpDeviceProxy : IHidDeviceProxy
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

    public HidDeviceInfo GetDeviceInfo()
    {
        return new HidDeviceInfo(
            _device.DevicePath,
            _device.VendorID,
            _device.ProductID,
            _device.GetProductNameOrDefault(),
            _device.GetSerialNumberOrDefault());
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

    public void ReadFeature(byte[] buffer)
    {
        ThrowIfNotReady();

        _stream?.GetFeature(buffer, 0, buffer.Length);
    }

    public void WriteFeature(byte[] buffer)
    {
        ThrowIfNotReady();

        _stream?.SetFeature(buffer, 0, buffer.Length);
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
