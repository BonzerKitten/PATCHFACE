# Builds the mod and makes the two release zips in dist\:
#   PatchFace-<version>.zip           BepInEx + the mod, extract into the game folder
#   PatchFace-<version>-mod-only.zip  just the mod, for players who already have BepInEx 5
# Usage: powershell -ExecutionPolicy Bypass -File package.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = $PSScriptRoot
$bepinexUrl = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x64_5.4.23.6.zip'
$bepinexSha256 = '6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755'

# The version comes from Plugin.cs so the zip name always matches the mod.
$match = Select-String -Path "$root\src\Plugin.cs" -Pattern 'Version = "([^"]+)"'
if (-not $match) { throw 'Version not found in src\Plugin.cs' }
$version = $match.Matches[0].Groups[1].Value

# Build.
& dotnet build "$root\PatchFace.csproj" -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$dll = "$root\bin\Release\netstandard2.1\PatchFace.dll"

# Official BepInEx zip, downloaded once and checked against its published hash.
$cache = "$root\obj\bepinex"
$bepinexZip = "$cache\BepInEx_win_x64_5.4.23.6.zip"
New-Item -ItemType Directory -Force $cache | Out-Null
if (-not (Test-Path $bepinexZip)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $bepinexUrl -OutFile $bepinexZip -UseBasicParsing
}
$hash = (Get-FileHash $bepinexZip -Algorithm SHA256).Hash.ToLower()
if ($hash -ne $bepinexSha256) {
    Remove-Item $bepinexZip
    throw "BepInEx download has the wrong hash ($hash), deleted it. Run again."
}

function Add-File([IO.Compression.ZipArchive]$zip, [string]$source, [string]$entryName) {
    # Forward slashes so every unzip tool recreates the folders.
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $source, $entryName.Replace('\', '/'))
}

function New-Zip([string]$path) {
    if (Test-Path $path) { Remove-Item $path }
    return [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
}

$dist = "$root\dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$full = "$dist\PatchFace-$version.zip"
$modOnly = "$dist\PatchFace-$version-mod-only.zip"

# Full zip: BepInEx's loader and core (not its changelog), then the mod.
$zip = New-Zip $full
$source = [IO.Compression.ZipFile]::OpenRead($bepinexZip)
try {
    foreach ($entry in $source.Entries) {
        if ($entry.Name -eq '' -or $entry.FullName -eq 'changelog.txt') { continue }
        $target = $zip.CreateEntry($entry.FullName.Replace('\', '/'))
        $in = $entry.Open(); $out = $target.Open()
        try { $in.CopyTo($out) } finally { $in.Dispose(); $out.Dispose() }
    }
    Add-File $zip "$root\third-party\BepInEx-LICENSE.txt" 'BepInEx\licenses\BepInEx-LICENSE.txt'
    Add-File $zip $dll 'BepInEx\plugins\PatchFace\PatchFace.dll'
    Add-File $zip "$root\LICENSE" 'BepInEx\plugins\PatchFace\LICENSE.txt'
    Add-File $zip "$root\README.md" 'PatchFace-README.txt'
}
finally {
    $source.Dispose()
    $zip.Dispose()
}

# Mod-only zip: same paths, so it also extracts straight into the game folder.
$zip = New-Zip $modOnly
try {
    Add-File $zip $dll 'BepInEx\plugins\PatchFace\PatchFace.dll'
    Add-File $zip "$root\LICENSE" 'BepInEx\plugins\PatchFace\LICENSE.txt'
    Add-File $zip "$root\README.md" 'PatchFace-README.txt'
}
finally {
    $zip.Dispose()
}

Write-Output "Made:"
Get-Item $full, $modOnly | ForEach-Object { "  {0}  ({1:N0} KB)" -f $_.Name, ($_.Length / 1KB) }
