param(
    [string]$AssemblyPath = "$PSScriptRoot\..\AutoResxTranslator\bin\Debug\AutoResxTranslator.exe"
)

$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path)
$service = $assembly.GetType('AutoResxTranslator.GTranslateService')
$parse = $service.GetMethod('ParseRetryAfter', [Reflection.BindingFlags]'NonPublic,Static')
if ($parse.Invoke($null, @('120')).TotalSeconds -ne 120) { throw 'Retry-After seconds were not parsed.' }
$date = [DateTimeOffset]::UtcNow.AddSeconds(90).ToString('r', [Globalization.CultureInfo]::InvariantCulture)
$seconds = $parse.Invoke($null, @($date)).TotalSeconds
if ($seconds -lt 85 -or $seconds -gt 90) { throw "Retry-After date was not parsed: $seconds" }
if ($parse.Invoke($null, @($null)).TotalSeconds -ne 0) { throw 'Missing Retry-After must use fallback.' }
Write-Output 'Google Retry-After parsing passed.'
