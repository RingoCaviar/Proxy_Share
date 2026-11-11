[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'Version')]
    [string]$Task = 'Build'
)

$ErrorActionPreference = 'Stop'

$versionPath = Join-Path $PSScriptRoot 'VERSION'
$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '\A(0|[1-9][0-9]{0,4})\.[0-9]\.[0-9]\z' -or [int]$Matches[1] -gt 65534) {
    throw 'VERSION must be X.Y.Z with X between 0 and 65534 and Y and Z each a single digit.'
}

if ($Task -eq 'Version') {
    Write-Output $version
    return
}

$frameworkPath = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319'
$compiler = Join-Path $frameworkPath 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw ".NET Framework C# compiler not found: $compiler"
}

$references = 'System.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'Accessibility.dll'
$sources = 'HelloMessageBox.cs', 'ProxyTakeoverLifecycle.cs', 'ProxyHosts.cs', 'ProxyHostEditor.cs'

$tempPath = Join-Path ([System.IO.Path]::GetTempPath()) ('ProxyShareBuild-' + [guid]::NewGuid().ToString('N'))
$versionSource = Join-Path $tempPath 'VersionInfo.cs'
$outputName = if ($Task -eq 'Test') { 'ProxyTakeoverLifecycleTests.exe' } else { 'ProxyShare.exe' }
$tempOutput = Join-Path $tempPath $outputName

[void][System.IO.Directory]::CreateDirectory($tempPath)
try {
    @"
using System.Reflection;
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $versionSource -Encoding ASCII

    $compilerArgs = @('/nologo', '/warn:4', '/codepage:65001', "/out:$tempOutput")
    $compilerArgs += $references | ForEach-Object { "/reference:$(Join-Path $frameworkPath $_)" }
    if ($Task -eq 'Test') {
        $compilerArgs += '/t:exe', '/main:ProxyTakeoverLifecycleTests'
        $sources += 'ProxyTakeoverLifecycleTests.cs'
    } else {
        $icon = Join-Path $PSScriptRoot 'logo.ico'
        $compilerArgs += '/t:winexe', '/platform:anycpu', '/optimize+', "/win32icon:$icon"
    }
    $compilerArgs += $sources | ForEach-Object { Join-Path $PSScriptRoot $_ }
    $compilerArgs += $versionSource

    & $compiler @compilerArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if ($Task -eq 'Test') {
        & $tempOutput
        exit $LASTEXITCODE
    }

    Copy-Item -LiteralPath $tempOutput -Destination (Join-Path $PSScriptRoot $outputName) -Force
    Write-Host "Built $outputName (v$version)"
} finally {
    [System.IO.File]::Delete($versionSource)
    [System.IO.File]::Delete($tempOutput)
    [System.IO.Directory]::Delete($tempPath)
}
