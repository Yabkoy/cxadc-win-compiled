// SPDX-License-Identifier: GPL-2.0-or-later
/*
 * cxadc-win-tool - Example tool for using the cxadc-win driver
 *
 * Copyright (C) 2024 Jitterbug
 */

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace cxadc_win_tool;

public class Cxadc : IDisposable
{
    public const uint CX_IOCTL_GET_VMUX = 0x821;
    public const uint CX_IOCTL_GET_LEVEL = 0x822;
    public const uint CX_IOCTL_GET_TENBIT = 0x823;
    public const uint CX_IOCTL_GET_SIXDB = 0x824;
    public const uint CX_IOCTL_GET_CENTER_OFFSET = 0x825;
    public const uint CX_IOCTL_SET_VMUX = 0x921;
    public const uint CX_IOCTL_SET_LEVEL = 0x922;
    public const uint CX_IOCTL_SET_TENBIT = 0x923;
    public const uint CX_IOCTL_SET_SIXDB = 0x924;
    public const uint CX_IOCTL_SET_CENTER_OFFSET = 0x925;

    const uint FILE_DEVICE_UNKNOWN = 0x00000022;
    const uint METHOD_BUFFERED = 0;
    const uint FILE_READ_DATA = 0x0001;
    const uint FILE_WRITE_DATA = 0x0002;

    private readonly SafeHandle _handle;
    private bool _disposed = false;

    public Cxadc(string devicePath)
    {
        this._handle = PInvoke.CreateFile(
            devicePath,
            (uint)0x80000000L | (uint)0x40000000L, // R/W
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);

        var err = Marshal.GetLastWin32Error();
        var errStr = new Win32Exception(err).Message;

        if (err != 0)
        {
            throw new Exception($"Unable to open {devicePath}: {errStr}");
        }
    }

    public int Read(Span<byte> buffer)
    {
        uint bytesRead = 0;
        bool ret;

        unsafe
        {
            ret = PInvoke.ReadFile(this._handle, buffer, &bytesRead, null);
        }

        if (!ret)
        {
            var err = Marshal.GetLastWin32Error();
            var errStr = new Win32Exception(err).Message;

            if (err != 0)
            {
                throw new Exception($"Read failed: {errStr}");
            }
        }

        return (int)bytesRead;
    }

    public uint Get(uint code)
    {
        nint buffer = nint.Zero;
        uint bytesRead = 0;
        uint ret = 0;

        unsafe
        {
            buffer = Marshal.AllocHGlobal(sizeof(uint));

            PInvoke.DeviceIoControl(
                this._handle,
                GetCtlCode(code, FILE_READ_DATA),
                null,
                0,
                buffer.ToPointer(),
                sizeof(uint),
                &bytesRead,
                null);

            var err = Marshal.GetLastWin32Error();
            var errStr = new Win32Exception(err).Message;

            if (err != 0)
            {
                throw new Exception($"Get failed: {errStr}");
            }

            ret = *(uint*)buffer;
        }

        return ret;
    }

    public uint Set(uint code, uint data)
    {
        nint buffer = nint.Zero;
        uint bytesRead = 0;
        uint ret = 0;

        unsafe
        {
            buffer = Marshal.AllocHGlobal(sizeof(uint));

            PInvoke.DeviceIoControl(
                this._handle,
                GetCtlCode(code, FILE_WRITE_DATA),
                &data,
                sizeof(uint),
                buffer.ToPointer(),
                sizeof(uint),
                &bytesRead,
                null);

            var err = Marshal.GetLastWin32Error();
            var errStr = new Win32Exception(err).Message;

            if (err != 0)
            {
                throw new Exception($"Set failed: {errStr}");
            }

            ret = *(uint*)buffer;
        }

        return ret;
    }

    ~Cxadc() => Dispose();

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            if (!this._handle.IsClosed)
            {
                this._handle.Close();
            }
        }

        this._disposed = true;
    }

    public static uint GetCtlCode(uint function, uint method)
    {
        return FILE_DEVICE_UNKNOWN << 16 | method << 14 | function << 2 | METHOD_BUFFERED;
    }
}
