# Contributing

Use Windows x64 and .NET 8. Run `./build.ps1` before submitting changes.

Keep HID device selection strict: never turn this into a global volume-key interceptor. Avoid treating HID release packets as wheel steps. Preserve user session volumes and normal-close restoration. Never hide volume-compensation limitations in the UI or docs.

Use `--diagnose` for physical captures. Keep logs and user configuration out of commits. Report headset connection, Windows version, G HUB state, and reproduction steps for input problems.

UI changes should include a screenshot and checks at common display scales. Audio changes need both unit/contract checks and the local silent-session test. Do not claim hardware or Sonar validation based on mocks.

No network calls belong in the default Windows mixer. Sonar requests must stay on loopback.

## Translations

Languages live in `src/WheelMix/locales/` as plain JSON and need no code changes. See [locales/README.md](src/WheelMix/locales/README.md) for the format and checks. When you add a UI string, add its key to `en.json` and to every other language in the same change, and read it through `L.Text("key")` or bind it with `Theme.LabelKey`/`ButtonKey`/`CheckKey` so it follows language changes.
