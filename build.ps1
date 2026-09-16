# Compile le widget avec le compilateur C# fourni par Windows (.NET Framework 4.8).
# Aucun SDK à installer.
#   powershell -ExecutionPolicy Bypass -File .\build.ps1                    (version de développement 0.0.0)
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Version v1.2.0    (version publiée, « v » facultatif)
param(
    [string]$Version = '0.0.0'
)
$ErrorActionPreference = 'Stop'

$Version = $Version.TrimStart('v')
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') {
    throw "Version invalide '$Version' : format attendu 1.2.3 ou 1.2.3-beta.1"
}
$numeric = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Compilateur introuvable : $csc" }

$dist = Join-Path $PSScriptRoot 'dist'
$obj = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Force $dist, $obj | Out-Null

# Métadonnées de version générées à chaque compilation (non versionnées).
$versionInfo = Join-Path $obj 'VersionInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$numeric")]
[assembly: AssemblyFileVersion("$numeric")]
[assembly: AssemblyInformationalVersion("$Version")]
"@ | Set-Content -Path $versionInfo -Encoding UTF8

$out = Join-Path $dist 'ClaudeUsageWidget.exe'
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }) + $versionInfo

& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$out" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll /reference:System.Security.dll `
    $sources

if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation" }
Write-Host "OK -> $out (version $Version)"
