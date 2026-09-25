# Changelog

## 0.3.0
- Interface in 13 languages, switchable from Preferences › Language (Automatic follows Windows). Translations are plain JSON files in `locales/`; see `locales/README.md` to add one.
- Closing the window keeps WheelMix running in the notification area; Exit from the tray icon closes it. A single click on the icon reopens the window.
- Startup entry is now registered as "WheelMix" (legacy name migrated) and Preferences reports when Windows has it switched off in Startup apps; enabling it in WheelMix switches it back on.
- Fixed stale drawing of the balance and cards when resizing or maximizing the window.

## 0.2.1
- Direct PRO X 2 Centurion status query distinguishes the headset from its USB receiver and reports battery.
- Dedicated help window, exercised through the actual Help button in a UI smoke test.
- Executable file picker for Chat applications, with case-insensitive duplicate handling.
- Startup registration status, verification, Windows settings link and save/cancel semantics.
- Live volume-compensation correction count in diagnostics; clarified compensation wording.
- Removed the SteelSeries promotional wording from the main screen.
- 22 logic/contract checks; runtime state lookup validated on the connected headset.

## 0.2.0
- New WheelMix dark interface with interactive Game/Chat balance.
- Live application groups, dedicated preferences and separate diagnostics.
- Pause and reset controls; opening a second copy focuses the first.
- Portable self-contained Windows x64 distribution with .NET 8.0.31.
- Volume compensation enabled for new settings, with visible limitations.
- Source package, MIT license, pinned dependencies, GitHub build workflow, checksums and contribution docs.

## 0.1.0
- Initial PRO X 2 Raw Input prototype.
- Windows audio session mixing, optional Sonar adapter and diagnostic logs.
