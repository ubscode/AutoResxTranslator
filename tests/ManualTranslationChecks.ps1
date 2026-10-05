param(
    [string]$AssemblyPath = "$PSScriptRoot\..\AutoResxTranslator\bin\Debug\AutoResxTranslator.exe"
)

$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path $AssemblyPath).Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$compiler = & $vswhere -all -products * -prerelease -property installationPath |
    ForEach-Object { Join-Path $_ 'MSBuild\Current\Bin\Roslyn\csc.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Visual Studio C# compiler was not found.' }
$output = Join-Path (Split-Path $assembly) 'ManualTranslationChecks.exe'
$json = Join-Path (Split-Path $assembly) 'Newtonsoft.Json.dll'
& $compiler /nologo /target:exe "/out:$output" "/reference:$assembly" "/reference:$json" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\ManualTranslationChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Checks did not compile.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Manual translation checks failed.' }
