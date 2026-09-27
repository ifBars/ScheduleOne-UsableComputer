#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)]
    [string]$S1ApiSourceRoot,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,70}$')]
    [string]$Label = ('local-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string]$OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) 'UsableComputerPackages'),
    [ValidatePattern('^0\.[0-9]+\.[0-9]+(?:f[0-9]+)?$')]
    [string]$GameVersion = '0.4.7f6'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PackageTools.ps1')
$repo = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$apiRepo = [IO.Path]::GetFullPath($S1ApiSourceRoot)
$apiProject = Join-Path $apiRepo 'S1API/S1API.csproj'
if (-not (Test-Path -LiteralPath $apiProject)) { throw 'S1ApiSourceRoot must contain S1API/S1API.csproj.' }
$output = [IO.Path]::GetFullPath((Join-Path $OutputRoot $Label))
if ($output.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep package output outside the repository so it cannot enter the source snapshot.'
}
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null

function Get-ApiSourceFiles {
    $files = & git -C $apiRepo -c core.quotepath=false ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate S1API source files.' }
    return @($files | Where-Object {
        ($_ -match '^(S1API|S1API.Tests|tools/S1APICoverageAnalyzer)/.*\.(cs|csproj)$' -or $_ -in @('LICENSE', 'README.md', 'AGENTS.md',
            'CODING_STANDARDS.md', 'VERSIONING.md', 'example.build.props', 'github.build.props')) -and
        $_ -notmatch '(^|/)(bin|obj|api|docs|templates|_site)/'
    } | Sort-Object -Unique)
}

$apiSource = Join-Path $output 's1api-source'
$apiHashes = @{}
foreach ($relative in Get-ApiSourceFiles) {
    $origin = Join-Path $apiRepo $relative
    $item = Get-Item -LiteralPath $origin
    for ($ancestor = $item; $ancestor -and $ancestor.FullName -ne $apiRepo; $ancestor = $ancestor.Parent ?? $ancestor.Directory) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source links are not packaged: $relative" }
    }
    $destination = Join-Path $apiSource $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $origin -Destination $destination
    $apiHashes[$relative] = (Get-FileHash -LiteralPath $destination).Hash
}
$apiSourceZip = Join-Path $output "S1API-hosting-preview-$Label-source.zip"
New-PackageZip $apiSource $apiSourceZip
$apiSourceSha = (Get-FileHash -LiteralPath $apiSourceZip).Hash
$apiCommit = & git -C $apiRepo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not read S1API revision.' }
$apiDirty = [bool](& git -C $apiRepo status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not read S1API source status.' }

function Get-SourceFiles {
    $files = & git -C $repo -c core.quotepath=false ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate source files.' }
    # Source-only snapshot: never ship installed game assemblies, build outputs, or local path configuration.
    return @($files | Where-Object {
        ($_ -match '^(src|child|vendor|examples|tests|scripts|docs)/.*\.(cs|csproj|md|ps1|lua|txt)$' -or
            $_ -in @('.gitignore', 'LICENSE', 'README.md', 'THIRD_PARTY_NOTICES.md', 'AGENTS.md', 'CODING_STANDARDS.md',
                'UsableComputer.csproj', 'local.build.props.example')) -and $_ -notmatch '(^|/)(bin|obj)/'
    } | Sort-Object -Unique)
}

$source = Join-Path $output 'source'
New-Item -ItemType Directory -Path $source | Out-Null
$sourceHashes = @{}
foreach ($relative in Get-SourceFiles) {
    $origin = Join-Path $repo $relative
    $item = Get-Item -LiteralPath $origin
    for ($ancestor = $item; $ancestor -and $ancestor.FullName -ne $repo; $ancestor = $ancestor.Parent ?? $ancestor.Directory) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source links are not packaged: $relative" }
    }
    $destination = Join-Path $source $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $origin -Destination $destination
    $sourceHashes[$relative] = (Get-FileHash -LiteralPath $destination).Hash
}
$sourceZip = Join-Path $output "UsableComputer-$Label-source.zip"
New-PackageZip $source $sourceZip
$sourceSha = (Get-FileHash -LiteralPath $sourceZip).Hash
$commit = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not read source revision.' }
$dirty = [bool](& git -C $repo status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not read source status.' }

foreach ($runtime in @('Mono', 'Il2cpp')) {
    $apiConfiguration = if ($runtime -eq 'Mono') { 'MonoMelon' } else { 'Il2CppMelon' }
    $framework = if ($runtime -eq 'Mono') { 'netstandard2.1' } else { 'net6.0' }
    & dotnet build $apiProject -c $apiConfiguration -t:Rebuild -p:AutomateLocalDeployment=false -p:GeneratePackageOnBuild=false -v:q
    if ($LASTEXITCODE -ne 0) { throw "S1API $runtime rebuild failed." }
    $apiDll = Join-Path $apiRepo "S1API/bin/$apiConfiguration/$framework/S1API.dll"
    $referenceProperty = if ($runtime -eq 'Mono') { 'S1ApiMonoPath' } else { 'S1ApiIl2CppPath' }
    $configuredApi = & dotnet msbuild (Join-Path $repo 'UsableComputer.csproj') "-p:Configuration=$runtime" "-getProperty:$referenceProperty"
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath([string]$configuredApi) -ine [IO.Path]::GetFullPath($apiDll)) {
        throw "Configure $referenceProperty to the S1API build in S1ApiSourceRoot before packaging."
    }
    & dotnet build (Join-Path $repo 'UsableComputer.csproj') -c $runtime -t:Rebuild -p:AutomateLocalDeployment=false -v:q
    if ($LASTEXITCODE -ne 0) { throw "$runtime rebuild failed." }
    $framework = if ($runtime -eq 'Mono') { 'netstandard2.1' } else { 'net6.0' }
    $build = Join-Path $repo "bin/$runtime/$framework"
    $stage = Join-Path $output $runtime
    New-Item -ItemType Directory -Path (Join-Path $stage 'Mods'), (Join-Path $stage 'UserLibs') -Force | Out-Null
    foreach ($relative in Get-PackageFiles $runtime) {
        $origin = switch ($relative) {
            'LICENSE.txt' { Join-Path $repo 'LICENSE' }
            'S1API-LICENSE.txt' { Join-Path $apiRepo 'LICENSE' }
            'Mods/S1API.Mono.MelonLoader.dll' { $apiDll }
            'Mods/S1API.Il2Cpp.MelonLoader.dll' { $apiDll }
            'THIRD_PARTY_NOTICES.md' { Join-Path $repo $relative }
            'INSTALL.md' { Join-Path $repo 'docs/package-install.md' }
            default { Join-Path $build ([IO.Path]::GetFileName($relative)) }
        }
        Copy-Item -LiteralPath $origin -Destination (Join-Path $stage $relative)
    }
    $mod = Get-ChildItem -LiteralPath (Join-Path $stage 'Mods') -Filter 'UsableComputer_*.dll' -File
    $version = [Reflection.AssemblyName]::GetAssemblyName($mod.FullName).Version.ToString()
    $files = @(foreach ($relative in Get-PackageFiles $runtime) {
        $file = Get-Item -LiteralPath (Join-Path $stage $relative)
        [ordered]@{ path = $relative; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    })
    [ordered]@{ schema = 1; runtime = $runtime; assemblyVersion = $version; label = $Label; commit = $commit;
        dirtySource = $dirty; targetGameVersion = $GameVersion;
        s1api = [ordered]@{ channel = 'local-hosting-preview'; commit = $apiCommit; dirtySource = $apiDirty;
            sourceArchive = [IO.Path]::GetFileName($apiSourceZip); sourceSha256 = $apiSourceSha };
        sourceArchive = [IO.Path]::GetFileName($sourceZip); sourceSha256 = $sourceSha; files = $files
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
    $archive = Join-Path $output "UsableComputer-$Label-$runtime.zip"
    New-PackageZip $stage $archive
    $null = Test-RuntimePackage $archive $runtime
}

if (@(Get-ApiSourceFiles).Count -ne $apiHashes.Count) { throw 'S1API source file list changed during packaging.' }
foreach ($relative in Get-ApiSourceFiles) {
    if (-not $apiHashes.ContainsKey($relative) -or (Get-FileHash -LiteralPath (Join-Path $apiRepo $relative)).Hash -ne $apiHashes[$relative]) {
        throw "S1API source changed during packaging: $relative. Do not distribute these artifacts."
    }
}

$currentFiles = @(Get-SourceFiles)
if ($currentFiles.Count -ne $sourceHashes.Count) { throw 'Source file list changed during packaging. Do not publish these artifacts.' }
foreach ($relative in $currentFiles) {
    if (-not $sourceHashes.ContainsKey($relative) -or (Get-FileHash -LiteralPath (Join-Path $repo $relative)).Hash -ne $sourceHashes[$relative]) {
        throw "Source changed during packaging: $relative. Do not publish these artifacts."
    }
}
Get-ChildItem -LiteralPath $output -Filter '*.zip' | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.Name
} | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8
Write-Host "Packages and matching source: $output"
