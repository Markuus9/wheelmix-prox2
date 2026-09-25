<p align="center"><img src="assets/wheelmix.png" width="96" alt="WheelMix logo"></p>

<h1 align="center">WheelMix</h1>

<p align="center"><b>Your game and your voice. In balance.</b><br>
Turn the volume wheel of your Logitech PRO X 2 LIGHTSPEED into a Game / Chat mixer on Windows.</p>

<p align="center">
  <a href="https://github.com/Markuus9/wheelmix-prox2/releases/latest"><img src="https://img.shields.io/github/v/release/Markuus9/wheelmix-prox2?label=download&color=9e8bff" alt="Latest release"></a>
  <a href="https://github.com/Markuus9/wheelmix-prox2/actions/workflows/build.yml"><img src="https://github.com/Markuus9/wheelmix-prox2/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078d4" alt="Windows 10 | 11 x64">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Markuus9/wheelmix-prox2" alt="MIT license"></a>
</p>

<p align="center"><a href="README.es.md">Leer en español</a></p>

![WheelMix main window](docs/interface.png)

WheelMix balances Discord (or any voice app) against the rest of your audio using the Windows mixer. No SteelSeries GG, no virtual audio devices, no drivers.

## Download

1. Go to [**Releases**](https://github.com/Markuus9/wheelmix-prox2/releases/latest) and download `WheelMix-vX.Y.Z-win-x64.zip`.
2. Extract the ZIP to a folder you will keep (for example `Documents\WheelMix`).
3. Run **WheelMix.exe**. It is portable: .NET is included and nothing is installed.
4. Plug in the PRO X 2 USB receiver, open Discord and a game, and turn the wheel.

> The executable is not code-signed yet, so Windows SmartScreen may warn the first time. Choose **More info › Run anyway**. You can check the download against the `.sha256` file published with each release.

## Features

- **Wheel mixing**: up favors Chat, down favors Game. Or drag the balance on screen.
- **Center keeps your volumes**: the balance is relative to each app's own level, so a 40 % Spotify stays at 40 %.
- **Runs in the background**: closing the window keeps WheelMix in the notification area (hidden icons). Choose **Exit** from its icon to quit.
- **Starts with Windows** if you enable it in Preferences. It appears as *WheelMix* in Settings › Apps › Startup.
- **Your chat apps**: Discord, Teams, TeamSpeak and Zoom are preset; add any `.exe`.
- **Headset status and battery**, pause, reverse direction and step size.
- **13 languages**: English, Español, Català, Deutsch, Français, Italiano, Nederlands, Polski, Português, Русский, 日本語, 한국어, 简体中文. [Add yours](locales/README.md).

## How it works

WheelMix reads the HID reports of the receiver `046D:0AF7` (Consumer Control `000C:0001`) only, never global keyboard keys. It then scales the volume of each Windows audio session:

| Balance | Game | Chat |
|---|---:|---:|
| Full Game | 100 % | 0 % |
| Center | 100 % | 100 % |
| Full Chat | 0 % | 100 % |

The wheel also sends a volume command to Windows. **Compensate system volume** (on by default) undoes it after each tick. It is best effort: a brief jump or the Windows volume flyout may appear.

## Compatibility and limits

- Windows 10/11 x64 with the Logitech PRO X 2 LIGHTSPEED on its USB receiver. Bluetooth, analog and other headsets are not validated.
- Needs shared-mode audio; browser tabs share one process and cannot be split. Prefer desktop Discord.
- Normal exit restores session volumes. A forced kill can leave apps attenuated; fix them in the Windows volume mixer.
- Optional SteelSeries Sonar backend uses an unofficial local API and has not been validated against a running Sonar.

Not affiliated with Logitech or SteelSeries.

## Privacy and uninstall

No telemetry, no auto-update, no network access in the default Windows mode (Sonar talks to localhost only). Settings and logs live in `%LOCALAPPDATA%\ControlAudioLogitech\`.

To uninstall, untick *Open WheelMix when I sign in* in Preferences, exit from the tray icon and delete the folder (and optionally the settings folder).

## Building from source

Requires Windows x64 and the .NET 8 SDK (see `global.json`).

```powershell
./build.ps1
```

It restores with the lock file, publishes a self-contained single-file `WheelMix.exe`, runs the self-tests and writes `release/WheelMix-vX.Y.Z-win-x64.zip` plus its `.sha256`. See [Testing](docs/TESTING.md) and [Releasing](docs/RELEASING.md).

## Contributing

Issues and pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md). Translations are plain JSON files; see [locales/README.md](locales/README.md).

## License

[MIT](LICENSE). NAudio and .NET components keep their own licenses, included in every download. Headset status protocol reference: [PROX2-AutoSwitch](https://github.com/Ayerdi/PROX2-AutoSwitch).
