param(
    [string]$Dotnet = 'dotnet',
    [string]$Python = 'python',
    [string]$NativeDependenciesDir,
    [string]$PackageSource,
    [switch]$SkipGraphics
)

$ErrorActionPreference = 'Stop'
Get-Command $Dotnet -ErrorAction Stop | Out-Null
Get-Command $Python -ErrorAction Stop | Out-Null
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactDirectory = Join-Path $repositoryRoot 'artifacts\validation'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments, [string]$LogName)
    $logPath = Join-Path $artifactDirectory $LogName
    Write-Host "Running $LogName"
    # Windows PowerShell wraps a native program's stderr as error records.
    # These probes intentionally log progress there; use the process exit code.
    $savedPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command @Arguments *> $logPath
        $processExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedPreference
    }
    if ($processExitCode -ne 0) {
        Get-Content -LiteralPath $logPath -Tail 35 | Write-Host
        throw "$LogName failed with exit code $processExitCode. See $logPath"
    }
}

Push-Location $repositoryRoot
try {
    Invoke-Checked $Python @('Tools/Localization/generate.py', '--check') 'localization-source.log'
    $buildArguments = @('build', 'LocalValidation.sln', '-c', 'Release', '--nologo')
    $nativePath = Join-Path $repositoryRoot 'Build\Client\Release'
    if ($NativeDependenciesDir) {
        $nativePath = (Resolve-Path -LiteralPath $NativeDependenciesDir).Path
        $buildArguments += "-p:NativeDependenciesDir=$nativePath"
    }
    foreach ($nativeFile in @('msvcr100.dll', 'msvcp100.dll', 'd3dx9_43.dll', 'D3DCompiler_43.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $nativePath $nativeFile))) {
            throw "Missing $nativeFile in $nativePath. Specify -NativeDependenciesDir with the SlimDX x64 dependencies."
        }
    }
    if ($PackageSource) { $buildArguments += @('--source', $PackageSource) }
    Invoke-Checked $Dotnet $buildArguments 'build.log'
    Invoke-Checked $Dotnet @('Checks/ResolutionProbe/bin/Release/net8.0-windows/ResolutionProbe.dll') 'resolution.log'
    Invoke-Checked $Dotnet @(
        'Checks/ItemLocalization/bin/Release/net8.0-windows/ItemLocalization.dll',
        (Join-Path $repositoryRoot 'Checks/Fixtures/item-name-audit.json'),
        (Join-Path $artifactDirectory 'item-localization.json'),
        (Join-Path $repositoryRoot 'Tools/Localization/Data/ItemNames.zh-CN.json')
    ) 'item-localization.log'
    if (-not $SkipGraphics) {
        Invoke-Checked $Dotnet @('Checks/GraphicsProbe/bin/Release/net8.0-windows/GraphicsProbe.dll') 'graphics-narrative.log'
    }
    Write-Host "Validation passed. Graphics skipped: $SkipGraphics. Reports: $artifactDirectory"
}
finally {
    Pop-Location
}
