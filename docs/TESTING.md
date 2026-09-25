# Validation

## Automated
- 15 checks: clamp at both ends, inversion, center, native attenuation factors, Sonar parsing, route fallback and locale-invariant writes.
- Native Core Audio test: its own silent session only; mute, half-volume, restoration.
- UI render smoke: main screen and settings; no audio modifications.
- Build and publish: zero warnings/errors expected. CI runs the non-hardware checks.

## Hardware
Confirmed on the available PRO X 2 receiver: VID 046D, PID 0AF7, consumer page 000C, usage 0001. Reports observed: 02 01 (up), 02 02 (down), 02 00 (release). Release packets do not move the mix.

The Windows prototype classified Discord as Chat and Spotify/Chrome as Game and processed real wheel events. This does not prove perfect global-volume suppression.

Before calling a release fully validated:
1. Discord and game audio: both mix directions and exact center.
2. Keyboard volume changes master volume without moving the balance.
3. Compensation on/off, including fast wheel bursts and simultaneous keyboard/slider changes.
4. Receiver hot-plug, sleep/wake, output changes and G HUB running/stopped.
5. Normal close restores volumes; pause resets mix and allows normal wheel volume.
6. Startup off/on; second launch opens the first instance.
7. UI at 100%, 125%, 150% and 200% display scale.
8. Downloaded ZIP on a clean Windows x64 machine without .NET.
9. Sonar tests only if that optional backend is claimed supported.

The included runtime was verified by publishing self-contained. A clean-machine installation matrix, digital signing and physical testing on other computers remain release-owner tasks.

## Developer commands
Use the published executable with `--self-test`, `--test-windows`, `--enumerate --diagnose`, `--diagnose`, or `--probe-sonar`.

For a screenshot without audio changes:
`WheelMix.exe --ui-smoke preview.png`
or
`WheelMix.exe --ui-smoke --ui-settings settings.png`.
