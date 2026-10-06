param([string]$KspManagedPath, [switch]$OfflineOnly)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build tests/KerbalSlingshot.Harness -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Offline build failed' }
    dotnet build src/KerbalSlingshot.Core -c Release -f net48
    if ($LASTEXITCODE -ne 0) { throw 'Framework core build failed' }
    if (!$OfflineOnly) {
        $buildArgs = @('build', 'src/KerbalSlingshot.KSP', '-c', 'Release')
        if ($KspManagedPath) { $buildArgs += "-p:KspManagedPath=$KspManagedPath" }
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw 'KSP plugin build failed' }
        $outputFiles = Get-ChildItem src/KerbalSlingshot.KSP/bin/Release/net48 -Filter '*.dll'
        $expectedNames = @('KerbalSlingshot.Core.dll', 'KerbalSlingshot.KSP.dll')
        if (Compare-Object $expectedNames @($outputFiles.Name)) { throw 'Unexpected DLLs in output; package only the two authored mod DLLs' }
        $outputFiles | Get-FileHash -Algorithm SHA256
    }
} finally { Pop-Location }
