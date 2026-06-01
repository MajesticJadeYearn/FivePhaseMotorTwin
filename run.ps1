param()
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'bin\FivePhaseMotorTwin.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1')
}
& $exe
