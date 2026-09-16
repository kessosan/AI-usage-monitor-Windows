# Compile le widget avec le compilateur C# fourni par Windows (.NET Framework 4.8).
# Aucun SDK à installer. Usage : powershell -ExecutionPolicy Bypass -File .\build.ps1
$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Compilateur introuvable : $csc" }

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$out = Join-Path $dist 'ClaudeUsageWidget.exe'
$sources = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object { $_.FullName }

& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$out" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll /reference:System.Security.dll `
    $sources

if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation" }
Write-Host "OK -> $out"
