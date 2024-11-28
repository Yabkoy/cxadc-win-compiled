// SPDX-License-Identifier: GPL-2.0-or-later
/*
 * cxadc-win-tool - Example tool for using the cxadc-win driver
 *
 * Copyright (C) 2024 Jitterbug
 */

using cxadc_win_tool;
using System.Buffers.Binary;
using System.CommandLine;
using System.Runtime.InteropServices;

const uint READ_SIZE = 2 * 1024 * 1024; // 2MB
const uint BUFFER_SIZE = 64 * 1024 * 1024; // 64MB
const uint MAX_CARDS = byte.MaxValue;

static void Exit()
{
    if (State.deviceHandle != null)
    {
        Ioctl.CloseDevice(State.deviceHandle);
    }
}

Console.CancelKeyPress += (sender, e) =>
{
    Exit();
};

var rootCommand = new RootCommand("cxadc-win-tool");

var inputDeviceArg = new Argument<string>(name: "device", description: "device path");
var outputFileArg = new Argument<string>(name: "output", description: "output path");
var setNameArg = new Argument<string>("name").FromAmong("vmux", "level", "tenbit", "sixdb", "center_offset");
var setValueArg = new Argument<uint>("value");

var startingLevelOption = new Option<uint>(name: "--level", description: "starting level", getDefaultValue: () => 16);
var sampleCountOption = new Option<uint>(name: "--samples", getDefaultValue: () => BUFFER_SIZE + READ_SIZE);

var captureCommand = new Command("capture", description: "capture data")
{
    inputDeviceArg,
    outputFileArg
};

var getCommand = new Command("get", description: "get device options")
{
    inputDeviceArg,
};

var setCommand = new Command("set", description: "set device options")
{
    inputDeviceArg,
    setNameArg,
    setValueArg
};

var levelAdjCommand = new Command("leveladj", "automatic level adjustment")
{
    inputDeviceArg,
    startingLevelOption,
    sampleCountOption
};

var scanCommand = new Command("scan", "list devices");

rootCommand.AddCommand(captureCommand);
rootCommand.AddCommand(getCommand);
rootCommand.AddCommand(setCommand);
rootCommand.AddCommand(levelAdjCommand);
rootCommand.AddCommand(scanCommand);

setCommand.SetHandler((device, name, value) =>
{
    try
    {
        State.deviceHandle = Ioctl.OpenDevice(device);
        uint code = 0;

        switch (name)
        {
            case "vmux":
                code = Ioctl.CX_IOCTL_SET_VMUX;
                break;

            case "level":
                code = Ioctl.CX_IOCTL_SET_LEVEL;
                break;

            case "tenbit":
                code = Ioctl.CX_IOCTL_SET_TENBIT;
                break;

            case "sixdb":
                code = Ioctl.CX_IOCTL_SET_SIXDB;
                break;

            case "center_offset":
                code = Ioctl.CX_IOCTL_SET_CENTER_OFFSET;
                break;
        }

        if (code != 0)
        {
            Ioctl.DeviceSet(State.deviceHandle, code, value);
        }
    }
    finally
    {
        Exit();
    }
}, inputDeviceArg, setNameArg, setValueArg);

captureCommand.SetHandler((device, output) =>
{
    try
    {
        State.deviceHandle = Ioctl.OpenDevice(device);

        using var stream = output == "-" ? Console.OpenStandardOutput() : File.Open(output, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        while (true)
        {
            var buffer = new byte[READ_SIZE];
            var bufSpan = new Span<byte>(buffer);

            if (Ioctl.ReadDevice(State.deviceHandle, bufSpan) is var bytesRead)
            {
                writer.Write(buffer, 0, bytesRead);
            }
        }
    }
    finally
    {
        Exit();
    }
}, inputDeviceArg, outputFileArg);

getCommand.SetHandler((device) =>
{
    try
    {
        State.deviceHandle = Ioctl.OpenDevice(device);

        Console.WriteLine("{0,-15} {1,-8}", "device", device);
        Console.WriteLine("{0,-15} {1,-8}", "vmux", Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_VMUX));
        Console.WriteLine("{0,-15} {1,-8}", "level", Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_LEVEL));
        Console.WriteLine("{0,-15} {1,-8}", "tenbit", Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_TENBIT));
        Console.WriteLine("{0,-15} {1,-8}", "sixdb", Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_SIXDB));
        Console.WriteLine("{0,-15} {1,-8}", "center_offset", Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_CENTER_OFFSET));

    }
    finally
    {
        Exit();
    }
}, inputDeviceArg);

levelAdjCommand.SetHandler((device, startingLevel, sampleCount) =>
{
    try
    {
        // based on code from cxadc-linux3 leveladj.c

        State.deviceHandle = Ioctl.OpenDevice(device);
        var tenbit = Convert.ToBoolean(Ioctl.DeviceQuery(State.deviceHandle, Ioctl.CX_IOCTL_GET_TENBIT));
        uint running = 1;
        var overLimit = 20;

        uint level = startingLevel;
        var lower = tenbit ? 0x0800 : 0x08;
        var upper = tenbit ? 0xF800 : 0xF8;
        var inc = tenbit ? sizeof(ushort) : sizeof(byte);
        var max = tenbit ? ushort.MaxValue : byte.MaxValue;

        while (running > 0)
        {
            var buffer = new Span<byte>(new byte[sampleCount]);
            uint over = 0;
            bool clipping = false;

            var low = tenbit ? ushort.MaxValue : byte.MaxValue;
            var high = 0;

            Console.WriteLine($"Testing level {level}");
            Ioctl.DeviceSet(State.deviceHandle, Ioctl.CX_IOCTL_SET_LEVEL, level);
            Ioctl.ReadDevice(State.deviceHandle, buffer);

            for (var i = 0; i < sampleCount / inc && over < overLimit; i+= inc)
            {
                var value = tenbit ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[i..]) : buffer[i];

                if (value < low)
                {
                    low = value;
                }

                if (value > high)
                {
                    high = value;
                }

                if (value < lower || value > upper)
                {
                    over += 1;
                }

                if (value == 0 || value == max)
                {
                    clipping = true;
                    break;
                }
            }

            Console.WriteLine("Low: {0,-5} High: {1,-5} Over: {2,-5} {3}", low, high, over, clipping ? "CLIPPING" : "");

            if (over > overLimit || clipping)
            {
                running = 2;
            }
            else if (running == 2)
            {
                running = 0;
            }

            if (running == 1)
            {
                level += 1;
            }
            else if (running == 2)
            {
                level -= 1;
            }

            if (level < 0 || level > 31)
            {
                running = 0;
            }
        }

        Console.WriteLine($"Stopped on level {level}");
    }
    finally
    {
        Exit();
    }
}, inputDeviceArg, startingLevelOption, sampleCountOption);

scanCommand.SetHandler(() =>
{
    var validPaths = new List<string>();

    for (var i = 0; i < MAX_CARDS; i++)
    {
        var path = $"\\\\.\\cxadc{i}";
        SafeHandle? handle = null;

        try
        {
            handle = Ioctl.OpenDevice(path);
            validPaths.Add(path);
        }
        catch
        {
            // assume not a valid path or some other error
        }
        finally
        {
            if (handle != null)
            {
                Ioctl.CloseDevice(handle);
            }
        }
    }


    foreach (var path in validPaths)
    {
        Console.WriteLine(path);
    }
});

await rootCommand.InvokeAsync(args);

struct State
{
    public static SafeHandle? deviceHandle;
}
