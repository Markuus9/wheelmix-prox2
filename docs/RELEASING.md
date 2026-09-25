# Release checklist

1. Set Version in the project and update the displayed UI version and changelog together.
2. Review the pinned .NET runtime security patch and package lock.
3. Run build.ps1 and the checks in TESTING.md.
4. Review screenshots and the README. Do not describe volume compensation as perfect suppression.
5. Publish the repository without work/, bin/, obj/, app/, release/, local logs or settings. A source ZIP is not a runnable download.
6. Run the GitHub build workflow and download its artifact.
7. Create a release for the matching tag; attach the portable ZIP and .sha256.
8. State tested headset/connection and known limitations. Do not imply Sonar was tested if only contracts ran.
9. If signing is available, sign the executable before packaging, regenerate the ZIP and hash, and verify the signature. No certificate is bundled with this project.

The workflow builds artifacts only. No remote repository, release or signing certificate is created by the local build.
