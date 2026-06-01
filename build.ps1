param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:TEMP = Join-Path $root '.tmp'
$env:TMP = Join-Path $root '.tmp'
New-Item -ItemType Directory -Path $env:TEMP, (Join-Path $root 'bin'), (Join-Path $root 'screenshots') -Force | Out-Null

$candidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath $candidate) { $csc = $candidate; break }
}
if (-not $csc) { throw '未找到 Windows 自带 C# 编译器 csc.exe。请安装 .NET Framework 4.x 或 Visual Studio Build Tools。' }

$out = Join-Path $root 'bin\FivePhaseMotorTwin.exe'
$sources = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName }
$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/utf8output',
    ('/out:' + $out),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
)
$args += $sources
& $csc $args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Build succeeded: $out"
