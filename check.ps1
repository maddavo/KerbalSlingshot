param([string]$KspManagedPath, [switch]$OfflineOnly)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./build.ps1 -KspManagedPath $KspManagedPath -OfflineOnly:$OfflineOnly
    $before = Get-ChildItem fixtures -Filter '*.json' | Get-FileHash -Algorithm SHA256
    dotnet run --project tests/KerbalSlingshot.Harness -c Release --no-build -- fixtures
    if ($LASTEXITCODE -ne 0) { throw 'Offline checks failed' }
    $after = Get-ChildItem fixtures -Filter '*.json' | Get-FileHash -Algorithm SHA256
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
        AssemblyVersion = '0.1.0.0'
        SDK = (dotnet --version)
        KspTarget = '1.12.5'
        KspContractBuilt = !$OfflineOnly
        KspChecksRun = $false
        Outputs = @($artifacts | ForEach-Object {
            [ordered]@{ Path = $_; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
        })
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content artifacts/build-manifest.json -Encoding utf8
    Write-Output 'Frozen fixtures unchanged; binary and source manifest saved in artifacts/build-manifest.json.'
} finally { Pop-Location }
