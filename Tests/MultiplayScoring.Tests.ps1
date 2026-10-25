$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Assets/Scripts/MultiplayScoring.cs')
function Assert-Equal($expected, $actual, $name) {
    if ($expected -ne $actual) { throw "$name expected $expected, got $actual" }
}
Assert-Equal 1000000 ([MultiplayScoring]::Normalize(7000, 7000)) 'Mixed tap/hold all perfect'
Assert-Equal 142857 ([MultiplayScoring]::Normalize(1000, 7000)) 'Fractional score floors only after accumulation'
Assert-Equal 1000000 ([MultiplayScoring]::Normalize(9000, 7000)) 'Maximum clamp'
Assert-Equal 0 ([MultiplayScoring]::Normalize(-1, 7000)) 'Minimum clamp'
Assert-Equal 0 ([MultiplayScoring]::Normalize(0, 0)) 'Empty chart guard'
Assert-Equal 100 ([MultiplayScoring]::Normalize(997900, 1000000) - [MultiplayScoring]::Normalize(997800, 1000000)) 'Plan example lead'
Assert-Equal 1000 ([MultiplayScoring]::JudgmentPoints(0.06)) 'Perfect boundary'
Assert-Equal 800 ([MultiplayScoring]::JudgmentPoints(0.061)) 'Great'
Assert-Equal 800 ([MultiplayScoring]::JudgmentPoints(-0.12)) 'Early hit symmetry'
Assert-Equal 500 ([MultiplayScoring]::JudgmentPoints(0.22)) 'Good boundary'
Assert-Equal 200 ([MultiplayScoring]::JudgmentPoints(0.35)) 'Cool'
Write-Output 'PASS: 11 scoring checks'
