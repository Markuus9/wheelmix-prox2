param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$project = 'src/WheelMix/WheelMix.csproj'
$releaseDir = Join-Path $PSScriptRoot 'release'
$binaryDir = Join-Path $releaseDir 'WheelMix-win-x64'
$packageDir = Join-Path $releaseDir 'package'
Remove-Item $binaryDir, $packageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $binaryDir, (Join-Path $packageDir 'licenses') -Force | Out-Null

dotnet restore $project -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $binaryDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$exe = Join-Path $binaryDir 'WheelMix.exe'
if (!$SkipTests) {
    $result = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $releaseDir 'tests.log') -RedirectStandardError (Join-Path $releaseDir 'tests-error.log')
    Get-Content (Join-Path $releaseDir 'tests.log')
    if ($result.ExitCode -ne 0) { throw 'Self-tests failed.' }
}

# ZIP layout: the executable and a quick start at the top, legal notices in licenses/.
[xml]$xml = Get-Content $project
$version = $xml.Project.PropertyGroup.Version
$runtime = $xml.Project.PropertyGroup.RuntimeFrameworkVersion
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$licenses = Join-Path $packageDir 'licenses'
Copy-Item $exe, packaging/QUICKSTART.md -Destination $packageDir
Copy-Item LICENSE (Join-Path $licenses 'LICENSE-WheelMix.txt')
Copy-Item packaging/THIRD-PARTY-NAudio.txt $licenses
Copy-Item (Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$runtime\LICENSE.TXT") (Join-Path $licenses 'LICENSE-DOTNET.txt')
Copy-Item (Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$runtime\THIRD-PARTY-NOTICES.TXT") (Join-Path $licenses 'THIRD-PARTY-DOTNET.txt')
Copy-Item (Join-Path $nuget "microsoft.windowsdesktop.app.runtime.win-x64\$runtime\LICENSE") (Join-Path $licenses 'LICENSE-WINDOWSDESKTOP.txt')
$zip = Join-Path $releaseDir "WheelMix-v$version-win-x64.zip"
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zip -Force

# Release assets: the bare executable (direct download), the ZIP, and one checksum file for both.
$assets = @($exe, $zip)
$sums = $assets | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllText((Join-Path $releaseDir 'SHA256SUMS.txt'), ($sums -join "`n") + "`n")
Write-Host "Ready: $exe"
Write-Host "Ready: $zip"
