param([string]$KspManagedPath, [switch]$OfflineOnly)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./build.ps1 -KspManagedPath $KspManagedPath -OfflineOnly:$OfflineOnly
    $before = Get-ChildItem fixtures -Filter '*.json' -Recurse | Get-FileHash -Algorithm SHA256
    dotnet run --project tests/KerbalSlingshot.Harness -c Release --no-build -- fixtures
    if ($LASTEXITCODE -ne 0) { throw 'Offline checks failed' }
    if (!$OfflineOnly) {
        if (!$KspManagedPath) {
            if (!(Test-Path -LiteralPath local.props)) { throw 'Reference inspection needs KspManagedPath or local.props' }
            $KspManagedPath = [string]([xml](Get-Content -LiteralPath local.props -Raw)).Project.PropertyGroup.KspManagedPath
        }
        dotnet run --project tests/KerbalSlingshot.Harness -c Release --no-build -- --inspect-plugin src/KerbalSlingshot.KSP/bin/Release/net48/KerbalSlingshot.KSP.dll $KspManagedPath | Tee-Object -FilePath artifacts/plugin-inspection.txt
        if ($LASTEXITCODE -ne 0) { throw 'Plugin startup/reference metadata inspection failed' }
    }
    $after = Get-ChildItem fixtures -Filter '*.json' -Recurse | Get-FileHash -Algorithm SHA256
    if (Compare-Object ($before | ForEach-Object { $_.Path + $_.Hash }) ($after | ForEach-Object { $_.Path + $_.Hash })) {
        throw 'Checks modified frozen fixtures'
    }
    $trackedAssemblies = git ls-files -- '*.dll' '*.pdb'
    if ($trackedAssemblies) { throw 'Binary assemblies must not be tracked' }
    $artifacts = @(
        'src/KerbalSlingshot.Core/bin/Release/net48/KerbalSlingshot.Core.dll',
        'src/KerbalSlingshot.Core/bin/Release/net8.0/KerbalSlingshot.Core.dll',
        'tests/KerbalSlingshot.Harness/bin/Release/net8.0/KerbalSlingshot.Harness.dll'
    )
    if (!$OfflineOnly) { $artifacts += 'src/KerbalSlingshot.KSP/bin/Release/net48/KerbalSlingshot.KSP.dll' }
    $manifest = [ordered]@{
        SourceCommit = (git rev-parse HEAD)
        WorkingTreeDirty = [bool](git status --porcelain)
        Configuration = 'Release'
        AssemblyVersion = [string]([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.AssemblyVersion
        SDK = (dotnet --version)
        KspTarget = '1.12.5'
        KspPluginBuilt = !$OfflineOnly
        PluginMetadataInspected = !$OfflineOnly
        KspChecksRun = $false
        Outputs = @($artifacts | ForEach-Object {
            [ordered]@{ Path = $_; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
        })
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content artifacts/build-manifest.json -Encoding utf8
    Write-Output 'Frozen fixtures unchanged; binary and source manifest saved in artifacts/build-manifest.json.'
} finally { Pop-Location }
