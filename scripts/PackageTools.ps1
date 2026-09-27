function Get-PackageFiles([string]$Runtime) {
    $mod = if ($Runtime -eq 'Mono') { 'UsableComputer_Mono.dll' } else { 'UsableComputer_Il2cpp.dll' }
    $api = if ($Runtime -eq 'Mono') { 'S1API.Mono.MelonLoader.dll' } else { 'S1API.Il2Cpp.MelonLoader.dll' }
    return @("Mods/$mod", "Mods/$api", 'UserLibs/ManagedDoom.Core.dll', 'UserLibs/MoonSharp.Interpreter.dll',
        'UserLibs/UsableComputer.ChildHost.dll', 'LICENSE.txt', 'S1API-LICENSE.txt', 'THIRD_PARTY_NOTICES.md', 'INSTALL.md')
}

function New-PackageZip([string]$Directory, [string]$Destination) {
    if (Test-Path -LiteralPath $Destination) { throw "Archive already exists: $Destination" }
    $zip = [IO.Compression.ZipFile]::Open($Destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName) {
            $name = [IO.Path]::GetRelativePath($Directory, $file.FullName).Replace('\', '/')
            $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $inputStream = [IO.File]::OpenRead($file.FullName)
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        }
    } finally { $zip.Dispose() }
}

function Test-RuntimePackage([string]$Path, [string]$Runtime) {
    if ($Runtime -notin @('Mono', 'Il2cpp')) { throw 'Unsupported package runtime.' }
    if ((Get-Item -LiteralPath $Path).Length -gt 64MB) { throw 'Package archive exceeds 64 MiB.' }
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
    try {
        $expected = @(Get-PackageFiles $Runtime)
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        [long]$expandedBytes = 0
        foreach ($entry in $zip.Entries) {
            $expandedBytes += $entry.Length
            if ($entry.FullName -cnotin ($expected + 'manifest.json') -or -not $names.Add($entry.FullName)) {
                throw "Unexpected or duplicate package entry: $($entry.FullName)"
            }
            if ($entry.Length -gt 64MB -or $expandedBytes -gt 128MB -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
                throw "Oversized or symbolic-link package entry: $($entry.FullName)"
            }
        }
        if ($names.Count -ne $expected.Count + 1) { throw 'Package is missing required files.' }
        $manifestEntry = $zip.GetEntry('manifest.json')
        if ($manifestEntry.Length -gt 64KB) { throw 'Package manifest is too large.' }
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try {
            $chars = [char[]]::new(65537)
            $count = $reader.ReadBlock($chars, 0, $chars.Length)
            if ($count -gt 65536) { throw 'Expanded manifest exceeds its limit.' }
            $manifest = [string]::new($chars, 0, $count) | ConvertFrom-Json
        } finally { $reader.Dispose() }
        if ($manifest.schema -ne 1 -or $manifest.runtime -cne $Runtime -or @($manifest.files).Count -ne $expected.Count) {
            throw 'Package manifest has an unsupported schema, runtime, or file count.'
        }
        $listed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($file in $manifest.files) {
            if ($file.path -cnotin $expected -or -not $listed.Add($file.path) -or $file.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
                throw 'Package manifest contains an invalid file record.'
            }
            $entry = $zip.GetEntry($file.path)
            if ($entry.Length -ne $file.bytes) { throw "Package size mismatch: $($file.path)" }
            $stream = $entry.Open()
            $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
            try {
                $buffer = [byte[]]::new(65536)
                [long]$total = 0
                while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $total += $read
                    if ($total -gt $entry.Length) { throw 'Package entry exceeds its declared length.' }
                    $hash.AppendData($buffer, 0, $read)
                }
                if ($total -ne $entry.Length -or [Convert]::ToHexString($hash.GetHashAndReset()) -ine $file.sha256) {
                    throw "Package hash mismatch: $($file.path)"
                }
            } finally { $hash.Dispose(); $stream.Dispose() }
        }
        return $manifest
    } finally { $zip.Dispose() }
}

function Expand-RuntimePackage([string]$Path, [string]$Runtime, [string]$Destination) {
    $manifest = Test-RuntimePackage $Path $Runtime
    if (Test-Path -LiteralPath $Destination) { throw "Package staging directory already exists: $Destination" }
    # The exact entry allowlist above excludes traversal, links, extra DLLs, and directory aliases.
    [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($Path), [IO.Path]::GetFullPath($Destination))
    foreach ($file in $manifest.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $Destination $file.path)).Hash -ine $file.sha256) {
            throw "Extracted package hash mismatch: $($file.path)"
        }
    }
    return $manifest
}
