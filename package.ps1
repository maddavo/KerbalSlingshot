param([Parameter(Mandatory=$true)][string]$KspManagedPath)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (git status --porcelain) { throw 'Commit the source first; packaging requires a clean working tree for exact build identity' }
    & ./check.ps1 -KspManagedPath $KspManagedPath
    $manifest = Get-Content artifacts/build-manifest.json -Raw | ConvertFrom-Json
    $commit = [string]$manifest.SourceCommit
    if ($manifest.WorkingTreeDirty -or $commit -ne (git rev-parse HEAD)) { throw 'Build identity is not the clean current commit' }
    $version = [string]([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    $name = "KerbalSlingshot-$version-$($commit.Substring(0,12))"
    $stage = Join-Path $PSScriptRoot "artifacts/packages/$name"
    if (Test-Path -LiteralPath $stage) { throw "Package already exists; preserved: $stage" }
    $plugins = Join-Path $stage 'GameData/KerbalSlingshot/Plugins'
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
    foreach ($dll in @('KerbalSlingshot.Core.dll','KerbalSlingshot.KSP.dll')) {
        $source = Join-Path $PSScriptRoot "src/KerbalSlingshot.KSP/bin/Release/net48/$dll"
        Copy-Item -LiteralPath $source -Destination (Join-Path $plugins $dll)
        if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath (Join-Path $plugins $dll)).Hash) { throw "Copy hash mismatch: $dll" }
    }
    Copy-Item -LiteralPath docs/INSTALL.md -Destination (Join-Path $stage 'INSTALL.md')
    Copy-Item -LiteralPath docs/KSP-TEST.md -Destination (Join-Path $stage 'KSP-TEST.md')
    Copy-Item -LiteralPath docs/MILESTONE-2.md -Destination (Join-Path $stage 'MILESTONE-2.md')
    Copy-Item -LiteralPath docs/DEPENDENCIES.md -Destination (Join-Path $stage 'DEPENDENCIES.md')
    Copy-Item -LiteralPath artifacts/build-manifest.json -Destination (Join-Path $stage 'build-manifest.json')
    Copy-Item -LiteralPath artifacts/offline-results.json -Destination (Join-Path $stage 'offline-results.json')
    Copy-Item -LiteralPath artifacts/plugin-inspection.txt -Destination (Join-Path $stage 'plugin-inspection.txt')
    $files = @(Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
        [ordered]@{ Path = $_.FullName.Substring($stage.Length+1).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
    })
    if (@($files | Where-Object { $_.Path.EndsWith('.dll') }).Count -ne 2) { throw 'Package contains unexpected DLLs' }
    $files | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $stage 'package-files.json') -Encoding utf8
    $zip = "$stage.zip"
    Compress-Archive -LiteralPath (Join-Path $stage 'GameData'),(Join-Path $stage 'INSTALL.md'),(Join-Path $stage 'KSP-TEST.md'),(Join-Path $stage 'MILESTONE-2.md'),(Join-Path $stage 'DEPENDENCIES.md'),(Join-Path $stage 'build-manifest.json'),(Join-Path $stage 'offline-results.json'),(Join-Path $stage 'plugin-inspection.txt'),(Join-Path $stage 'package-files.json') -DestinationPath $zip
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $dllEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll') })
        if ($dllEntries.Count -ne 2 -or ($dllEntries.Name | Where-Object { $_ -notin @('KerbalSlingshot.Core.dll','KerbalSlingshot.KSP.dll') })) { throw 'ZIP contains unexpected assemblies' }
        foreach ($entry in $dllEntries) {
            $entryStream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $zipHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-','')
                if ($zipHash -ne (Get-FileHash -LiteralPath (Join-Path $plugins $entry.Name)).Hash) { throw 'ZIP DLL hash mismatch' }
            } finally { $sha.Dispose(); $entryStream.Dispose() }
        }
    } finally { $archive.Dispose() }
    Get-FileHash -LiteralPath $zip -Algorithm SHA256
    Write-Output "Test package ready (not installed/published): $zip"
} finally { Pop-Location }
