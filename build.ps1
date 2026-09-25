param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$releaseDir = Join-Path $PSScriptRoot 'release'
$binaryDir = Join-Path $releaseDir 'WheelMix-win-x64'
New-Item -ItemType Directory -Path $binaryDir -Force | Out-Null
dotnet restore -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet publish -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $binaryDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$exe = Join-Path $binaryDir 'WheelMix.exe'
# Refresh loose translations before testing: they override the bundled ones at runtime.
Remove-Item (Join-Path $binaryDir 'locales') -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item locales -Destination $binaryDir -Recurse -Force
if (!$SkipTests) {
    $result = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $releaseDir 'tests.log') -RedirectStandardError (Join-Path $releaseDir 'tests-error.log')
    Get-Content (Join-Path $releaseDir 'tests.log')
    if ($result.ExitCode -ne 0) { throw 'Self-tests failed.' }
}
Copy-Item LICENSE,THIRD-PARTY-NAudio.txt,QUICKSTART.md -Destination $binaryDir -Force
[xml]$project = Get-Content ControlAudioLogitech.csproj
$runtime = $project.Project.PropertyGroup.RuntimeFrameworkVersion
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
Copy-Item (Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$runtime\LICENSE.TXT") (Join-Path $binaryDir 'LICENSE-DOTNET.txt') -Force
Copy-Item (Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$runtime\THIRD-PARTY-NOTICES.TXT") (Join-Path $binaryDir 'THIRD-PARTY-DOTNET.txt') -Force
Copy-Item (Join-Path $nuget "microsoft.windowsdesktop.app.runtime.win-x64\$runtime\LICENSE") (Join-Path $binaryDir 'LICENSE-WINDOWSDESKTOP.txt') -Force
$version = $project.Project.PropertyGroup.Version
$zip = Join-Path $releaseDir "WheelMix-v$version-win-x64.zip"
$items = 'locales','WheelMix.exe','LICENSE','THIRD-PARTY-NAudio.txt','QUICKSTART.md','LICENSE-DOTNET.txt','THIRD-PARTY-DOTNET.txt','LICENSE-WINDOWSDESKTOP.txt' | ForEach-Object { Join-Path $binaryDir $_ }
Compress-Archive -LiteralPath $items -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($zip + '.sha256', $hash + '  ' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine)
Write-Host "Ready: $zip"
