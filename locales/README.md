# Translations

Each language is one JSON file in this folder. No code changes are needed to add one.

```json
{
  "code": "de",
  "name": "Deutsch",
  "strings": { "common.save": "Speichern", "...": "..." }
}
```

- `code`: lowercase language code, optionally with a region (`pt`, `zh-CN`). Name the file after it (`zh-CN.json`).
- `name`: the language's name written in that language. It is what users see in Preferences › Language.
- `strings`: the same keys as `en.json`, which is the reference and fallback.

## Adding or updating a language

1. Copy `en.json` to `<code>.json` and translate the values. Never rename keys.
2. Keep placeholders such as `{0}` and `{1}`, leading symbols (`●  `, `○  `) and trailing spaces.
3. Keep product and channel names as they are: WheelMix, Game, Chat, Sonar, GG, Discord.
4. Run `./build.ps1` from the repository root. The self-test fails if a bundled language misses a key, has an unknown key or breaks a placeholder.
5. Open Preferences, pick the language, click Save and check that the text fits in the window.

When new keys are added to `en.json`, add them to every language in the same change. If you cannot translate them, copy the English text so the coverage test still passes, and mention it in the pull request.

## Trying a language without rebuilding

WheelMix also reads `locales/*.json` next to `WheelMix.exe`. A file there overrides the bundled language with the same code (English cannot be overridden). Missing keys fall back to English; files with unknown keys or broken placeholders are ignored. Start `WheelMix.exe --language <code>` to test it without changing your saved preference.
