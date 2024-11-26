# cxadc-win - CX2388x ADC DMA driver for Windows
> [!WARNING]  
> ⚠️ **THIS IS EXPERIMENTAL AND SHOULD NOT BE USED IN A PRODUCTION ENVIRONMENT** ⚠️  

This was made for use with the [decode](https://github.com/oyvindln/vhs-decode) projects, see [here](https://github.com/oyvindln/vhs-decode/wiki/CX-Cards) for more information on these cards.  

## Usage
### Configure device
`cxadc-win-tool scan`  
`cxadc-win-tool get \\.\cxadc0`  
`cxadc-win-tool set \\.\cxadc0 vmux 1`  
`cxadc-win-tool set \\.\cxadc1 level 20`  

See [cxadc-linux3](https://github.com/happycube/cxadc-linux3) for parameter descriptions.  
Parameter       | Range | Default 
----------------|-------|--------
`vmux`          | `0-2`  | `2`
`level`         | `0-31` | `16`
`tenbit`        | `0-1`  | `0`
`sixdb`         | `0-1`  | `0`
`center_offset` | `0-63` | `0`

### Capture data to a file or pipe to `STDOUT` for compression
> [!TIP]  
> Use Command Prompt instead of PowerShell if piping to `STDOUT`  

`cxadc-win-tool capture \\.\cxadc0 test.u8`  
`cxadc-win-tool capture \\.\cxadc1 - | flac -0 --blocksize=65535 --lax --sample-rate=28636 --channels=1 --bps=8 --sign=unsigned --endian=little -f - -o test.flac`  

## Limitations
Due to various security features in Windows 10/11, Secure Boot and Signature Enforcement must be disabled. I recommend re-enabling when not capturing.

## Pre-installation  
1. Disable Secure Boot in your BIOS  
2. Disable Signature Enforcement, this can be done by typing `bcdedit -set testsigning on` in an Administrator Command Prompt and rebooting  

## Installation
1. Open **Device Manager**  
2. Right click **Multimedia Video Controller**, click **Properties**  
3. Click **Update Driver...**  
4. Click **Browse my computer for drivers**  
5. Browse to the path containing **cxadc-win.inf** and **cxadc-win.sys**, click **Next**  
6. Click **Install this driver software anyway** when prompted  

## Building
This has only been tested with VS 2022, WSDK/WDK 10.0.26100 and .NET 8.0.  

## Notes
There are issues using the [clockgen mod](https://github.com/oyvindln/vhs-decode/wiki/Clockgen-Mod) in Windows. (2024/11/27)  
- It is currently not possible to configure, you will need to change the defaults and re-build the firmware
- The audio input device format limited to `3 channel, 24 bit, 48000 Hz`  
- The 3rd audio channel (HSW) does not work correctly  

## Disclaimer
I take absolutely no responsibility for (including but not limited to) any crashes, instability, security vulnerabilities or interactions with anti-virus/anti-cheat software, nor do I guarantee the accuracy of captures.  

## Credits
This is based on the [Linux cxadc driver](https://github.com/happycube/cxadc-linux3), without which this would not exist.  