$file = [System.IO.File]::ReadAllBytes('C:\Users\ericm\Game Dev\hoarders\Shared\Assets\Models\garbagemonster2withanimations.glb')

# GLB layout: 12-byte header, then chunk0 header (8 bytes), then JSON bytes
$chunkLength = [System.BitConverter]::ToUInt32($file, 12)
$jsonBytes = $file[20..(20 + $chunkLength - 1)]
$json = [System.Text.Encoding]::UTF8.GetString($jsonBytes) | ConvertFrom-Json

Write-Host "=== ANIMATIONS ==="
Write-Host "Count: $($json.animations.Count)"
for ($i = 0; $i -lt $json.animations.Count; $i++) {
    $anim = $json.animations[$i]
    Write-Host "  [$i] '$($anim.name)' — $($anim.channels.Count) channels"
}

Write-Host ""
Write-Host "=== NODES ==="
Write-Host "Count: $($json.nodes.Count)"
foreach ($n in $json.nodes) {
    if ($n.name) { Write-Host "  $($n.name)" }
}
