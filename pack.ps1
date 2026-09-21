# Builds Auto Stash and creates the release archives in dist\:
#   AutoStash-<version>.zip          for Nexus Mods (extract into the Valheim folder)
#   dbendu-AutoStash-<version>.zip   for Thunderstore (upload as is)
#
# Run: powershell -ExecutionPolicy Bypass -File pack.ps1
# Before a new release, raise the version in AutoStash.csproj, Plugin.cs and package\manifest.json
# and add a CHANGELOG.md entry.

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

[xml]$project = Get-Content (Join-Path $root 'AutoStash.csproj')
$version = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
$manifest = Get-Content (Join-Path $root 'package\manifest.json') -Raw | ConvertFrom-Json
$plugin = Get-Content (Join-Path $root 'Plugin.cs') -Raw
if ($plugin -notmatch 'const string Version = "([^"]+)"') {
    throw 'Version constant not found in Plugin.cs'
}
$pluginVersion = $Matches[1]
if ($manifest.version_number -ne $version -or $pluginVersion -ne $version) {
    throw "Versions differ: AutoStash.csproj=$version, Plugin.cs=$pluginVersion, manifest.json=$($manifest.version_number)"
}
if ($manifest.description.Length -gt 250) {
    throw "manifest.json description is $($manifest.description.Length) characters; Thunderstore allows 250"
}
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw
if ($changelog -notmatch "(?m)^## $([regex]::Escape($version))\s*$") {
    throw "CHANGELOG.md has no entry for $version"
}

dotnet build (Join-Path $root 'AutoStash.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed'
}

$dll = Join-Path $root 'bin\Release\AutoStash.dll'
$readme = Join-Path $root 'README.md'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# Entry names are written explicitly with forward slashes; Compress-Archive in Windows PowerShell
# may store backslashes, which some mod managers and Linux tools do not unpack correctly.
function New-Zip([string]$path, [System.Collections.Specialized.OrderedDictionary]$entries) {
    if (Test-Path $path) {
        Remove-Item $path
    }
    $zip = [System.IO.Compression.ZipFile]::Open($path, 'Create')
    try {
        foreach ($name in $entries.Keys) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entries[$name], $name, 'Optimal')
        }
    }
    finally {
        $zip.Dispose()
    }
    Write-Host "Created $path"
}

New-Zip (Join-Path $dist "AutoStash-$version.zip") ([ordered]@{
    'BepInEx/plugins/AutoStash/AutoStash.dll' = $dll
    'BepInEx/plugins/AutoStash/README.md'     = $readme
})

New-Zip (Join-Path $dist "dbendu-AutoStash-$version.zip") ([ordered]@{
    'manifest.json' = (Join-Path $root 'package\manifest.json')
    'icon.png'      = (Join-Path $root 'package\icon.png')
    'README.md'     = $readme
    'CHANGELOG.md'  = (Join-Path $root 'CHANGELOG.md')
    'AutoStash.dll' = $dll
})
