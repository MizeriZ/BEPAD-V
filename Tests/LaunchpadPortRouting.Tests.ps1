$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Assets/Scripts/LaunchpadPortRouting.cs')
$script:checks = 0
function Assert-Equal($expected, $actual, $name) {
    if ($expected -ne $actual) { throw "$name expected [$expected], got [$actual]" }
    $script:checks++
}
function Port([uint32]$id, [string]$name, [string]$identity) {
    $p = [LaunchpadPortRouting+Port]::new()
    $p.Id = $id
    $p.Name = $name
    $p.Interface = $identity
    return $p
}
function Ports($items) {
    $list = [Collections.Generic.List[LaunchpadPortRouting+Port]]::new()
    foreach ($item in $items) { $list.Add($item) }
    return ,$list
}
$claimed = [Collections.Generic.HashSet[uint32]]::new()
$a = Port 0 'Launchpad MK2' '\\?\usb#vid_1235&pid_0069#UNIT_A#{input-guid}'
$b = Port 1 'Launchpad MK2' '\\?\usb#vid_1235&pid_0069#UNIT_B#{input-guid}'
$outB = Port 7 'Launchpad MK2' '\\?\usb#vid_1235&pid_0069#UNIT_B#{output-guid}'
$outA = Port 8 'Launchpad MK2' '\\?\usb#vid_1235&pid_0069#UNIT_A#{output-guid}'
$inputs = Ports @($a, $b)
$outputs = Ports @($outB, $outA)
Assert-Equal $false ($a.DeviceKey -eq $b.DeviceKey) 'Two identically named MK2s remain distinct'
Assert-Equal 8 ([LaunchpadPortRouting]::FindOutput($a, $inputs, $outputs, $claimed).Id) 'A matches A even with reversed output order'
Assert-Equal 7 ([LaunchpadPortRouting]::FindOutput($b, $inputs, $outputs, $claimed).Id) 'B matches B'
[void]$claimed.Add(8)
Assert-Equal $null ([LaunchpadPortRouting]::FindOutput($a, $inputs, $outputs, $claimed)) 'Output cannot be shared'
$claimed.Clear()
$aMoved = Port 12 'Launchpad MK2' $a.Interface
Assert-Equal $a.DeviceKey $aMoved.DeviceKey 'Identity survives MIDI index changes'
Assert-Equal 2 ([LaunchpadPortRouting]::SelectPorts($inputs).Count) 'Both MK2s are selected'

$xDaw = Port 3 'LPX DAW' '\\?\usb#vid_1235&pid_0103#X_A#{input-guid}'
$xLegacy = Port 4 'Launchpad X' $xDaw.Interface
$xMidi = Port 5 'MIDIIN2 (Launchpad X)' $xDaw.Interface
$xOut = Port 9 'MIDIOUT2 (Launchpad X)' '\\?\usb#vid_1235&pid_0103#X_A#{output-guid}'
$xSelected = [LaunchpadPortRouting]::SelectPorts((Ports @($xDaw, $xLegacy, $xMidi)))
Assert-Equal 1 $xSelected.Count 'X aliases do not become extra players'
Assert-Equal 5 $xSelected[0].Id 'X uses second Windows MIDI interface'
Assert-Equal 0 ([LaunchpadPortRouting]::SelectPorts((Ports @($xDaw))).Count) 'DAW port is excluded'
$mixed = Ports @($a, $xMidi)
Assert-Equal 9 ([LaunchpadPortRouting]::FindOutput($xMidi, $mixed, (Ports @($outA, $xOut)), $claimed).Id) 'X plus MK2 routes X'
Assert-Equal 8 ([LaunchpadPortRouting]::FindOutput($a, $mixed, (Ports @($outA, $xOut)), $claimed).Id) 'X plus MK2 routes MK2'
$xOther = Port 6 'MIDIIN2 (Launchpad X)' '\\?\usb#vid_1235&pid_0103#X_B#{input-guid}'
Assert-Equal 2 ([LaunchpadPortRouting]::SelectPorts((Ports @($xMidi, $xOther))).Count) 'Two X devices remain distinct'

$fallbackA = Port 0 'Launchpad MK2' ''
$fallbackB = Port 1 'Launchpad MK2' ''
Assert-Equal $false ($fallbackA.DeviceKey -eq $fallbackB.DeviceKey) 'Fallback does not merge same-name inputs'
Assert-Equal $null ([LaunchpadPortRouting]::FindOutput($fallbackA, (Ports @($fallbackA, $fallbackB)), (Ports @((Port 4 'Launchpad MK2' ''), (Port 5 'Launchpad MK2' ''))), $claimed)) 'Ambiguous fallback does not light the wrong device'
Assert-Equal 9 ([LaunchpadPortRouting]::FindOutput((Port 5 'MIDIIN2 (Launchpad X)' ''), (Ports @((Port 5 'MIDIIN2 (Launchpad X)' ''))), (Ports @((Port 9 'MIDIOUT2 (Launchpad X)' ''))), $claimed).Id) 'Direction aliases match when identity unavailable'
Assert-Equal $null ([LaunchpadPortRouting]::FindOutput($a, $inputs, (Ports @($outB)), $claimed)) 'Disconnect cannot reroute A LEDs to B'
Write-Output "PASS: $script:checks MIDI routing checks"
