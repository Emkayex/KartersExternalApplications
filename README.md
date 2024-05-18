# External Applications for The Karters 2
This repository contains applications (well eventually more than 1 hopefully) to improve the gameplay experience of [The Karters 2](https://store.steampowered.com/app/2269950/The_Karters_2_Turbo_Charged/). The programs cannot and will not read or modify the game's memory directly. Instead, all data is obtained by capturing frames from the gameplay window.

The programs are only built to target x86-64 Windows currently.

## Building
To build the projects, several dependencies must be installed.

- .NET 8 SDK
    - https://dotnet.microsoft.com/en-us/download/dotnet/8.0
- Python 3
    - https://www.python.org/downloads/
- Rust and Cargo
    - https://www.rust-lang.org/tools/install
- rnet
    - https://github.com/Diggsey/rnet
    - `cargo install rnet-gen`

Build the Rust DLL with the [BuildRustDll.py script](./BuildRustDll.py).

```bash
python ./BuildRustDll.py
```

Build the C# project with the `dotnet` CLI.

```bash
dotnet restore

# Normal builds
dotnet build
dotnet build -c Release

# Alternatives
dotnet publish
dotnet publish -c Release
```

## WindowCapture
The [WindowCapture](./WindowCapture/) library is a simple wrapper around [NiiightmareXD's "Windows Capture" Rust library](https://github.com/niiightmarexd/windows-capture). This is used to capture frames from the game window. It hasn't been tested thoroughly for use in other C# applications, but feel free to use it at your own risk in your own projects if you want.

## CNK Style Boost Bar
The [CNK Style Boost Bar](./CNKStyleBoostBar/) application draws a set of three boost bars on screen next to your character. The bars change color as the bars fill to indicate how good a boost will be when fired.

Before running the program, the game MUST already be running. The default options should just work, but CLI arguments can be provided to customize the program behavior. If you are customizing the program behavior, it is recommended to use a shortcut with launch arguments set. Available options are as follows.

```
Usage:
  CNKStyleBoostBar [options]

Options:
  -m, --mirror-meter                                               Mirrors the boost meter across the screen. [default: False]
  -d, --draw-debug-box                                             Draws a debug box around the original boost meter. [default: False]
  -b, --boost-bar-style <ArcsSameAngles|ArcsSameLength|Rectangle>  Changes the shape of the boost meters drawn on screen. [default: ArcsSameAngles]
  --boost-meter-color-1                                            The boost meter color when the minimum amount for a boost hasn't been reached yet (RGB
                                                                   hex). [default: 00FF00]
  --boost-meter-color-2                                            The boost meter color when the minimum amount for a boost has been reached (RGB hex).
                                                                   [default: FFD800]
  --boost-meter-color-3                                            The boost meter color when the first threshold within the valid boost window has been
                                                                   reached (RGB hex). [default: FF6A00]
  --boost-meter-color-4                                            The boost meter color when the second threshold within the valid boost window has been
                                                                   reached (RGB hex). [default: FF0000]
  -t, --threshold-percent-for-color-3                              When the boost bar reaches this percent of the maximum, it will change to
                                                                   BoostMeterColor3. [default: 80]
  --threshold-percent-for-color-4                                  When the boost bar reaches this percent of the maximum, it will change to
                                                                   BoostMeterColor4. [default: 95]
  -a, --arc-start-angle                                            The arc style bars are drawn by sweeping from one angle to another. This sets the start
                                                                   angle in degrees. [default: -30]
  --arc-end-angle                                                  The arc style bars are drawn by sweeping from one angle to another. This sets the end
                                                                   angle in degrees. [default: 45]
  -o, --offset-x                                                   The number of pixels by which to offset the boost bars in the X direction. Flips
                                                                   automatically when mirrored. [default: 0]
  --offset-y                                                       The number of pixels by which to offset the boost bars in the Y direction. Negative is
                                                                   up. [default: 0]
  -w, --window-name                                                The name of the application window from which frames should be captured. [default:
                                                                   TheKarters2]
  -?, -h, --help                                                   Show help and usage information
  -v, --version                                                    Show version information
```

[![The Karters 2: CNK Style Boost Bar Application](https://img.youtube.com/vi/HSA9Y10LkD8/0.jpg)](https://www.youtube.com/watch?v=HSA9Y10LkD8)
