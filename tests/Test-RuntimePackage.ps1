param([Parameter(Mandatory)][string]$PackagePath, [ValidateSet('Mono', 'Il2cpp')][string]$Runtime = 'Mono')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../scripts/PackageTools.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('UsableComputerPackageVerifier-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$null = Test-RuntimePackage $PackagePath $Runtime
$null = Expand-RuntimePackage $PackagePath $Runtime (Join-Path $root 'valid')
$checks = 2
function Require-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Invalid package was accepted.' }
}
$otherRuntime = if ($Runtime -eq 'Mono') { 'Il2cpp' } else { 'Mono' }
Require-Rejected { Test-RuntimePackage $PackagePath $otherRuntime }; $checks++
foreach ($kind in @('missing', 'dependency', 'corrupt', 'hash', 'extra', 'traversal', 'duplicate', 'manifest')) {
    $path = Join-Path $root "$kind.zip"
    Copy-Item -LiteralPath $PackagePath -Destination $path
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Update)
    try {
        switch ($kind) {
            'missing' { $zip.GetEntry('LICENSE.txt').Delete() }
            'dependency' {
                $apiName = if ($Runtime -eq 'Mono') { 'S1API.Mono.MelonLoader.dll' } else { 'S1API.Il2Cpp.MelonLoader.dll' }
                $zip.GetEntry("Mods/$apiName").Delete()
            }
            'corrupt' { $zip.GetEntry('INSTALL.md').Delete(); $entry = $zip.CreateEntry('INSTALL.md') }
            'hash' {
                $original = $zip.GetEntry('INSTALL.md')
                $inputStream = $original.Open()
                $memory = [IO.MemoryStream]::new()
                try { $inputStream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $inputStream.Dispose(); $memory.Dispose() }
                $bytes[0] = $bytes[0] -bxor 1
                $original.Delete()
                $outputStream = $zip.CreateEntry('INSTALL.md').Open()
                try { $outputStream.Write($bytes, 0, $bytes.Length) } finally { $outputStream.Dispose() }
            }
            'extra' { $entry = $zip.CreateEntry('UserLibs/Assembly-CSharp.dll') }
            'traversal' { $entry = $zip.CreateEntry('../outside.txt') }
            'duplicate' { $entry = $zip.CreateEntry('INSTALL.md') }
            'manifest' { $zip.GetEntry('manifest.json').Delete(); $entry = $zip.CreateEntry('manifest.json') }
        }
        if ($kind -notin @('missing', 'dependency', 'hash')) {
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write('invalid') } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    Require-Rejected { Expand-RuntimePackage $path $Runtime (Join-Path $root $kind) }
    if (Test-Path -LiteralPath (Join-Path $root $kind)) { throw 'Rejected package created an extraction directory.' }
    $checks++
}
Write-Host "PASS | Runtime package | $checks checks | Evidence: $root"
