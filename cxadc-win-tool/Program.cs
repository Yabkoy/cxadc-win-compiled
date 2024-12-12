// SPDX-License-Identifier: GPL-2.0-or-later
/*
 * cxadc-win-tool - Example tool for using the cxadc-win driver
 *
 * Copyright (C) 2024 Jitterbug
 */

using cxadc_win_tool;
using System.Buffers.Binary;
using System.CommandLine;

const uint READ_SIZE = 2 * 1024 * 1024; // 2MB
const uint BUFFER_SIZE = 64 * 1024 * 1024; // 64MB
const uint MAX_CARDS = byte.MaxValue;

Cxadc? cx = null;
Clockgen? clockgen = null;

Console.CancelKeyPress += (sender, e) =>
{
    cx?.Dispose();
    clockgen?.Dispose();
};

// commandline handling
// global args
var inputDeviceArg = new Argument<string>(name: "device", description: "device path");

// capture command
var captureOutputArg = new Argument<string>(name: "output", description: "output path (- for STDOUT)");
var captureCommand = new Command("capture", description: "capture data")
{
    inputDeviceArg,
    captureOutputArg
};

captureCommand.SetHandler((device, output) =>
{
    using (cx = new Cxadc(device))
    {
        using var stream = output == "-" ? Console.OpenStandardOutput() : File.Open(output, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        while (true)
        {
            var buffer = new byte[READ_SIZE];
            var bufSpan = new Span<byte>(buffer);

            if (cx.Read(bufSpan) is var bytesRead)
            {
                writer.Write(buffer, 0, bytesRead);
            }
        }
    }
}, inputDeviceArg, captureOutputArg);


// get command
var getCommand = new Command("get", description: "get device options")
{
    inputDeviceArg,
};

getCommand.SetHandler((device) =>
{
    PrintCxConfig(device);
}, inputDeviceArg);

// set command
var setNameArg = new Argument<string>("name").FromAmong("vmux", "level", "tenbit", "sixdb", "center_offset");
var setValueArg = new Argument<uint>("value");
var setCommand = new Command("set", description: "set device options")
{
    inputDeviceArg,
    setNameArg,
    setValueArg
};

setCommand.SetHandler((device, name, value) =>
{
    using var cx = new Cxadc(device);
    uint code = name switch
    {
        "vmux" => Cxadc.CX_IOCTL_SET_VMUX,
        "level" => Cxadc.CX_IOCTL_SET_LEVEL,
        "tenbit" => Cxadc.CX_IOCTL_SET_TENBIT,
        "sixdb" => Cxadc.CX_IOCTL_SET_SIXDB,
        "center_offset" => Cxadc.CX_IOCTL_SET_CENTER_OFFSET,
        _ => 0
    };

    if (code != 0)
    {
        cx.Set(code, value);
    }

}, inputDeviceArg, setNameArg, setValueArg);


// reset command
var resetNameArg = new Argument<string>("name").FromAmong("ouflow_count");
var resetCommand = new Command("reset", description: "reset device state")
{
    inputDeviceArg,
    resetNameArg
};

resetCommand.SetHandler((device, name) =>
{
    using var cx = new Cxadc(device);
    uint code = name switch
    {
        "ouflow_count" => Cxadc.CX_IOCTL_RESET_OUFLOW_COUNT,
        _ => 0
    };

    if (code != 0)
    {
        cx.Set(code, 0);
    }

}, inputDeviceArg, resetNameArg);


// leveladj command
var levelAdjStartingLevelOption = new Option<uint>(name: "--level", description: "starting level", getDefaultValue: () => 16);
var levelAdjSampelCountOption = new Option<uint>(name: "--samples", getDefaultValue: () => BUFFER_SIZE + READ_SIZE);
var levelAdjCommand = new Command("leveladj", "automatic level adjustment")
{
    inputDeviceArg,
    levelAdjStartingLevelOption,
    levelAdjSampelCountOption
};

levelAdjCommand.SetHandler((device, startingLevel, sampleCount) =>
{
    using (cx = new Cxadc(device))
    {
        // based on code from cxadc-linux3 leveladj.c
        var tenbit = Convert.ToBoolean(cx.Get(Cxadc.CX_IOCTL_GET_TENBIT));
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
            cx.Set(Cxadc.CX_IOCTL_SET_LEVEL, level);
            cx.Read(buffer);

            for (var i = 0; i < sampleCount / inc && over < overLimit; i += inc)
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
}, inputDeviceArg, levelAdjStartingLevelOption, levelAdjSampelCountOption);

// clockgen command
var clockgenCxClockIndexArg = new Argument<uint>("clock").FromAmong("0", "1");
var clockgenCxClockValueArg = new Argument<uint>(
    "value",
    description: "1 = 20.00 MHz, 2 = 28.636 MHz, 3 = 40.00 MHz, 4 = 50.000 MHz"
    ).FromAmong("1", "2", "3", "4");

var clockgenCxGetCommand = new Command("get", "get frequency")
{
    clockgenCxClockIndexArg
};

clockgenCxGetCommand.SetHandler((idx) =>
{
    using (clockgen = new Clockgen())
    {
        Console.WriteLine($"{clockgen.GetClock(idx):0.000}");
    }
}, clockgenCxClockIndexArg);

var clockgenCxSetCommand = new Command("set", "set frequency")
{
    clockgenCxClockIndexArg,
    clockgenCxClockValueArg
};

clockgenCxSetCommand.SetHandler((idx, valueIdx) =>
{
    using (clockgen = new Clockgen())
    {
        var currentFreqIdx = clockgen.GetFreqIdx(idx);

        // stepped change of clock to prevent crash
        while (valueIdx != currentFreqIdx)
        {
            currentFreqIdx += (byte)(valueIdx < currentFreqIdx ? -1 : 1);
            clockgen.SetClock(idx, currentFreqIdx);
            Thread.Sleep(100);
        }
    }
}, clockgenCxClockIndexArg, clockgenCxClockValueArg);

var clockgenCxCommand = new Command("cx", "configure cx rates")
{
    clockgenCxGetCommand,
    clockgenCxSetCommand
};

var clockgenAudioRateArg = new Argument<int>("set").FromAmong("46875", "48000");

var clockgenAudioRateSetCommand = new Command("set")
{
    clockgenAudioRateArg
};

clockgenAudioRateSetCommand.SetHandler((rate) =>
{
    using (clockgen = new Clockgen())
    {
        clockgen.SetAudioRate(rate);
    }
}, clockgenAudioRateArg);

var clockgenAudioRateGetCommand = new Command("get");

clockgenAudioRateGetCommand.SetHandler(() =>
{
    using (clockgen = new Clockgen())
    {
        Console.WriteLine(clockgen.GetAudioRate());
    }
});

var clockgenAudioCommand = new Command("audio", "configure audio rate")
{
    clockgenAudioRateSetCommand,
    clockgenAudioRateGetCommand
};

var clockgenCommand = new Command("clockgen", "configure clockgen")
{
    clockgenCxCommand,
    clockgenAudioCommand
};

// scan command
var scanCommand = new Command("scan", "list devices");

scanCommand.SetHandler(() =>
{
    foreach (var device in GetDevices())
    {
        Console.WriteLine(device);
    }
});

// status command
var statusCommand = new Command("status", "show all device config");

statusCommand.SetHandler(() =>
{
    foreach (var device in GetDevices())
    {
        PrintCxConfig(device);
        Console.WriteLine();
    }

    // attempt clockgen
    try
    {
        using (clockgen = new Clockgen())
        {
            Console.WriteLine("{0,-15} {1,-8}", "audio", clockgen.GetAudioRate());

            for (uint i = 0; i < 2; i++)            
            {
                Console.WriteLine("{0,-15} {1,-8}", $"clock {i}", $"{clockgen.GetClock(i):0.000}");
            }
        }
    }
    catch
    {
    }
});

// root
var rootCommand = new RootCommand("cxadc-win-tool - https://github.com/JuniorIsAJitterbug/cxadc-win")
{
    statusCommand,
    scanCommand,
    captureCommand,
    getCommand,
    setCommand,
    resetCommand,
    clockgenCommand,
    levelAdjCommand
};

List<string> GetDevices()
{
    var devices = new List<string>();

    for (var i = 0; i < MAX_CARDS; i++)
    {
        var path = $"\\\\.\\cxadc{i}";

        try
        {
            using (cx = new Cxadc(path))
            {
                devices.Add(path);
            }
        }
        catch
        {
            // assume not a valid path or some other error
        }
    }

    return devices;
}

void PrintCxConfig(string device)
{
    using (cx = new Cxadc(device))
    {
        Console.WriteLine("{0,-15} {1,-8}", "device", device);
        Console.WriteLine("{0,-15} {1,-8}", "vmux", cx.Get(Cxadc.CX_IOCTL_GET_VMUX));
        Console.WriteLine("{0,-15} {1,-8}", "level", cx.Get(Cxadc.CX_IOCTL_GET_LEVEL));
        Console.WriteLine("{0,-15} {1,-8}", "tenbit", cx.Get(Cxadc.CX_IOCTL_GET_TENBIT));
        Console.WriteLine("{0,-15} {1,-8}", "sixdb", cx.Get(Cxadc.CX_IOCTL_GET_SIXDB));
        Console.WriteLine("{0,-15} {1,-8}", "center_offset", cx.Get(Cxadc.CX_IOCTL_GET_CENTER_OFFSET));
        Console.WriteLine("{0,-15} {1,-8}", "ouflow_count", cx.Get(Cxadc.CX_IOCTL_GET_OUFLOW_COUNT));
    }
}

await rootCommand.InvokeAsync(args);
