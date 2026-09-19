param(
    [Parameter(Mandatory=$true)][string]$Package,
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('pet-runtime-' + [Guid]::NewGuid().ToString('N'))),
    [ValidateSet('actions','edge','edge-size','edge-size-big','official-actions','official-wardrobe','official-edge','official-motion','official-cheongsam','official-size','official-free','official-wave-size','official-audit')][string]$Mode = 'actions'
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$petPackage = (Resolve-Path -LiteralPath $Package).Path
$petCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$petProbeExe = Join-Path $OutputDirectory 'RuntimeProbe.exe'
& $petCompiler /nologo /codepage:65001 /target:winexe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$petProbeExe" (Join-Path $PSScriptRoot 'RuntimeProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runtime probe compilation failed' }
# Shows the actual pet for eight seconds; no mouse input is injected. Keep unrelated windows off the pet.
$petProbeProcess = Start-Process -FilePath $petProbeExe -ArgumentList @(('"'+$petPackage+'"'),('"'+$OutputDirectory+'"'),$Mode) -WindowStyle Hidden -PassThru -Wait
if ($petProbeProcess.ExitCode -ne 0) { throw 'Runtime probe failed' }
Add-Type -AssemblyName System.Drawing
$petResults = @()
foreach ($sample in @(2,4,6,8)) {
    $frame = [Drawing.Bitmap]::FromFile((Join-Path $OutputDirectory "frame-$sample.png"))
    $screen = [Drawing.Bitmap]::FromFile((Join-Path $OutputDirectory "screen-$sample.png"))
    try {
        $errorSum = 0.0; $points = 0
        $sampleStep = if ($frame.Width -lt 80) { 1 } else { 2 }
        for ($y=3; $y -lt $frame.Height-3; $y+=$sampleStep) {
            for ($x=3; $x -lt $frame.Width-3; $x+=$sampleStep) {
                $expected = $frame.GetPixel($x,$y)
                # Some authored RGBA sprites use alpha 253-254 on their solid interior.
                # At alpha >=253, unknown background contributes at most two RGB levels.
                if ($expected.A -lt 253 -or $frame.GetPixel($x-2,$y).A -lt 253 -or $frame.GetPixel($x+2,$y).A -lt 253 -or $frame.GetPixel($x,$y-2).A -lt 253 -or $frame.GetPixel($x,$y+2).A -lt 253) { continue }
                $actual = $screen.GetPixel([int][Math]::Floor(($x+0.5)*$screen.Width/$frame.Width),[int][Math]::Floor(($y+0.5)*$screen.Height/$frame.Height))
                $errorSum += [Math]::Abs([int]$expected.R-$actual.R)+[Math]::Abs([int]$expected.G-$actual.G)+[Math]::Abs([int]$expected.B-$actual.B)
                $points++
            }
        }
        if ($points -lt 500) { throw 'Too few opaque character pixels' }
        $meanError = $errorSum / (3*$points)
        $petResults += [pscustomobject]@{sample=$sample; opaquePixels=$points; minimumAlpha=253; meanRgbError=[Math]::Round($meanError,3); passed=($meanError -lt 8)}
    } finally { $frame.Dispose(); $screen.Dispose() }
}
$petResults | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'composition.json') -Encoding UTF8
$petResults | Format-Table
if (@($petResults | Where-Object { !$_.passed }).Count) { throw 'Desktop composition differs from submitted frames; inspect local screen images for stale rendering or occlusion' }
Write-Output "PASS: native desktop composition matches four sampled frames. Evidence: $OutputDirectory"
