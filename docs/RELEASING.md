# Release checklist

Releases are published by GitHub Actions when a `v*` tag is pushed. The workflow builds, runs the self-tests, and attaches `WheelMix.exe`, the ZIP and `SHA256SUMS.txt` to a GitHub release. The release notes come from the matching `CHANGELOG.md` section.

1. Set `Version` in `src/WheelMix/WheelMix.csproj` (the window reads it from the assembly). The workflow refuses a tag that does not match the project version.
2. Add a `## X.Y.Z` section to `CHANGELOG.md`.
3. Review the pinned .NET runtime security patch and package lock.
4. Run `./build.ps1` and the checks in [TESTING.md](TESTING.md). Review screenshots and the README. Do not describe volume compensation as perfect suppression.
5. Commit, then tag and push:

   ```powershell
   git tag vX.Y.Z
   git push origin main vX.Y.Z
   ```

6. When the workflow finishes, check the release page: `WheelMix.exe`, ZIP, `SHA256SUMS.txt`, notes. State the tested headset/connection and known limitations. Do not imply Sonar was tested if only contracts ran.

If signing becomes available, sign the executable before packaging, regenerate the ZIP and hash, and verify the signature. No certificate is bundled with this project.
