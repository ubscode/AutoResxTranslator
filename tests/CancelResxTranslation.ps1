param(
    [string]$AssemblyPath = "$PSScriptRoot\..\AutoResxTranslator\bin\Debug\AutoResxTranslator.exe"
)

$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path) | Out-Null
$cancellation = [Threading.CancellationTokenSource]::new()
$cancellation.Cancel()
$result = ''
$watch = [Diagnostics.Stopwatch]::StartNew()
$success = [AutoResxTranslator.GTranslateService]::Translate('Hello', 'en', 'es', '', [ref]$result, $cancellation.Token)
if ($success -or $watch.Elapsed.TotalSeconds -gt 5) {
    throw "Cancelled request continued for $($watch.Elapsed.TotalSeconds) seconds (success=$success)."
}
Write-Output "Cancelled Google request stopped in $($watch.ElapsedMilliseconds) ms."
