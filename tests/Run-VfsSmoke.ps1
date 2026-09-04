#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Mono", "Il2cpp")]
    [string]$Runtime,
    [Parameter(Mandatory = $true)]
    [string]$GamePath,
    [Parameter(Mandatory = $true)]
    [string]$SourceSavePath,
    [string]$OutputRoot = "",
    [ValidateRange(60, 600)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Assert-Path([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-ContainedPath([string]$Path, [string]$Root, [string]$Description) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description is outside its expected root: $fullPath"
    }
    return $fullPath
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) "UsableComputerVfsSmoke"
}

$GamePath = [IO.Path]::GetFullPath($GamePath)
$SourceSavePath = [IO.Path]::GetFullPath($SourceSavePath)
$runId = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $Runtime.ToLowerInvariant()
$outputDir = Get-ContainedPath (Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $runId) ([IO.Path]::GetFullPath($OutputRoot)) "Output directory"
$fixturePath = Get-ContainedPath (Join-Path $outputDir "save") $outputDir "Disposable save"
$backupDir = Get-ContainedPath (Join-Path $outputDir "backups") $outputDir "Backup directory"
$timelinePath = Join-Path $outputDir "timeline.txt"
$modsPath = Get-ContainedPath (Join-Path $GamePath "Mods") $GamePath "Mods directory"
$modsBackupPath = Get-ContainedPath (Join-Path $GamePath "Mods.UsableComputerVfsSmoke.$runId") $GamePath "Mods backup"
$userLibsPath = Get-ContainedPath (Join-Path $GamePath "UserLibs") $GamePath "UserLibs directory"
$exePath = Join-Path $GamePath "Schedule I.exe"
$logPath = Join-Path $GamePath "MelonLoader\Latest.log"
$configuration = $Runtime
$targetFramework = if ($Runtime -eq "Mono") { "netstandard2.1" } else { "net6.0" }
$mainDllName = if ($Runtime -eq "Mono") { "UsableComputer_Mono.dll" } else { "UsableComputer_Il2cpp.dll" }
$s1ApiInstalledName = if ($Runtime -eq "Mono") { "S1API.Mono.MelonLoader.dll" } else { "S1API.Il2Cpp.MelonLoader.dll" }
$mainOutput = Join-Path $repoRoot "bin\$configuration\$targetFramework"
$mainDllPath = Join-Path $mainOutput $mainDllName
$smokeProject = Join-Path $PSScriptRoot "UsableComputer.VfsSmoke\UsableComputer.VfsSmoke.csproj"
$smokeDllPath = Join-Path $PSScriptRoot "UsableComputer.VfsSmoke\bin\$configuration\$targetFramework\UsableComputer.VfsSmoke.dll"
$timeline = [Collections.Generic.List[string]]::new()
$launchedProcess = $null
$modsMoved = $false
$userLibMutations = @()

New-Item -ItemType Directory -Path $outputDir, $fixturePath, $backupDir -Force | Out-Null
Assert-Path $exePath "Schedule I executable"
Assert-Path $SourceSavePath "Source completed save"
Assert-Path (Join-Path $SourceSavePath "Game.json") "Source Game.json"
Assert-Path (Join-Path $SourceSavePath "Metadata.json") "Source Metadata.json"
Assert-Path $modsPath "Mods directory"
Assert-Path $userLibsPath "UserLibs directory"

$existingProcesses = Get-CimInstance Win32_Process -Filter "Name = 'Schedule I.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and ([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($GamePath, [StringComparison]::OrdinalIgnoreCase) }
if ($existingProcesses) {
    throw "Schedule I is already running from the target install. Close it before running this smoke test."
}
if (Test-Path -LiteralPath $modsBackupPath) {
    throw "The run-specific Mods backup already exists: $modsBackupPath"
}

Get-ChildItem -LiteralPath $SourceSavePath -Force | Copy-Item -Destination $fixturePath -Recurse -Force
$timeline.Add("FIXTURE|Source=$SourceSavePath|Copy=$fixturePath")

$localPropsPath = Join-Path $repoRoot "local.build.props"
[xml]$localProps = Get-Content -Raw -LiteralPath $localPropsPath
$propertyGroup = $localProps.Project.PropertyGroup
$s1ApiPath = if ($Runtime -eq "Mono") { [string]$propertyGroup.S1ApiMonoPath } else { [string]$propertyGroup.S1ApiIl2CppPath }
$msbuildDirectory = (Split-Path -Parent $localPropsPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$s1ApiPath = [IO.Path]::GetFullPath($s1ApiPath.Replace('$(MSBuildThisFileDirectory)', $msbuildDirectory))
Assert-Path $s1ApiPath "S1API runtime build"

function Invoke-SmokePhase([string]$Phase) {
    $phaseDirectory = Get-ContainedPath (Join-Path $outputDir $Phase) $outputDir "Phase directory"
    New-Item -ItemType Directory -Path $phaseDirectory -Force | Out-Null
    $resultPath = Join-Path $phaseDirectory "result.txt"
    $arguments = @(
        "--usable-computer-vfs-smoke",
        "--usable-computer-vfs-smoke-phase", $Phase,
        "--usable-computer-vfs-smoke-dir", "`"$phaseDirectory`"",
        "--usable-computer-vfs-smoke-save", "`"$fixturePath`"",
        "-screen-fullscreen", "0",
        "-screen-width", "1280",
        "-screen-height", "720"
    )
    $script:launchedProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $GamePath -PassThru -WindowStyle Normal
    $timeline.Add("LAUNCH|Runtime=$Runtime|Phase=$Phase|PID=$($script:launchedProcess.Id)")

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $script:launchedProcess.Refresh()
        if (Test-Path -LiteralPath $resultPath) {
            $candidate = Get-Content -Raw -LiteralPath $resultPath
            if ($candidate -match "^(PASS|FAIL)\|") { break }
        }
        if ($script:launchedProcess.HasExited) { break }
    }

    if (Test-Path -LiteralPath $logPath) {
        Copy-Item -LiteralPath $logPath -Destination (Join-Path $phaseDirectory "Latest.log") -Force
    }
    $result = if (Test-Path -LiteralPath $resultPath) {
        Get-Content -Raw -LiteralPath $resultPath
    } else {
        "FAIL|Runtime=$Runtime|Phase=$Phase|Reason=No result file was written"
    }
    $timeline.Add("RESULT|$result")

    $script:launchedProcess.Refresh()
    if (-not $script:launchedProcess.HasExited) {
        Stop-Process -Id $script:launchedProcess.Id -Force -ErrorAction SilentlyContinue
        [void]$script:launchedProcess.WaitForExit(15000)
    }
    $script:launchedProcess = $null

    if ($result -notmatch "^PASS\|Runtime=$Runtime\|Phase=$Phase\|") {
        throw "Usable Computer VFS $Runtime $Phase smoke failed: $result"
    }
    if ($result -notmatch "Persisted=True" -or $result -notmatch "Screenshot=") {
        throw "Usable Computer VFS $Runtime $Phase smoke did not prove persistence and screenshot capture: $result"
    }
    $screenshotPath = Join-Path $phaseDirectory "vfs-$Phase.png"
    Assert-Path $screenshotPath "$Runtime $Phase screenshot"
    if ((Get-Item -LiteralPath $screenshotPath).Length -eq 0) {
        throw "$Runtime $Phase screenshot is empty: $screenshotPath"
    }
}

try {
    $timeline.Add("BUILD|Runtime=$Runtime|Started=$(Get-Date -Format o)")
    & dotnet build (Join-Path $repoRoot "UsableComputer.csproj") -c $configuration -t:Rebuild -p:AutomateLocalDeployment=false -v:q
    if ($LASTEXITCODE -ne 0) { throw "Usable Computer $Runtime rebuild failed" }
    & dotnet build $smokeProject -c $configuration -t:Rebuild -v:q
    if ($LASTEXITCODE -ne 0) { throw "VFS smoke $Runtime rebuild failed" }
    Assert-Path $mainDllPath "Usable Computer build artifact"
    Assert-Path $smokeDllPath "VFS smoke build artifact"
    $timeline.Add("BUILD_PASS|Runtime=$Runtime|SHA256=$((Get-FileHash -LiteralPath $mainDllPath -Algorithm SHA256).Hash)")

    Move-Item -LiteralPath $modsPath -Destination $modsBackupPath
    $modsMoved = $true
    New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
    Copy-Item -LiteralPath $s1ApiPath -Destination (Join-Path $modsPath $s1ApiInstalledName) -Force
    Copy-Item -LiteralPath $mainDllPath -Destination (Join-Path $modsPath $mainDllName) -Force
    Copy-Item -LiteralPath $smokeDllPath -Destination (Join-Path $modsPath "UsableComputer.VfsSmoke.dll") -Force

    foreach ($name in @("ManagedDoom.Core.dll", "MoonSharp.Interpreter.dll", "UsableComputer.ChildHost.dll")) {
        $target = Get-ContainedPath (Join-Path $userLibsPath $name) $userLibsPath "UserLib target"
        $backup = Join-Path $backupDir $name
        $existed = Test-Path -LiteralPath $target
        if ($existed) { Copy-Item -LiteralPath $target -Destination $backup -Force }
        $userLibMutations += [pscustomobject]@{ Target=$target; Backup=$backup; Existed=$existed }
        Copy-Item -LiteralPath (Join-Path $mainOutput $name) -Destination $target -Force
    }
    $timeline.Add("INSTALL_PASS|Runtime=$Runtime|Mods=S1API,$mainDllName,UsableComputer.VfsSmoke.dll")

    Invoke-SmokePhase "seed"
    Invoke-SmokePhase "reload"
    Write-Host "Usable Computer VFS $Runtime smoke passed." -ForegroundColor Green
    Write-Host "Evidence: $outputDir" -ForegroundColor Green
}
finally {
    if ($launchedProcess) {
        $launchedProcess.Refresh()
        if (-not $launchedProcess.HasExited) {
            Stop-Process -Id $launchedProcess.Id -Force -ErrorAction SilentlyContinue
            [void]$launchedProcess.WaitForExit(15000)
        }
    }

    foreach ($mutation in $userLibMutations) {
        if ($mutation.Existed -and (Test-Path -LiteralPath $mutation.Backup)) {
            Copy-Item -LiteralPath $mutation.Backup -Destination $mutation.Target -Force
        } elseif (-not $mutation.Existed -and (Test-Path -LiteralPath $mutation.Target)) {
            Remove-Item -LiteralPath $mutation.Target -Force
        }
    }

    if ($modsMoved) {
        if (Test-Path -LiteralPath $modsPath) {
            $verifiedModsPath = Get-ContainedPath $modsPath $GamePath "Temporary Mods cleanup"
            Remove-Item -LiteralPath $verifiedModsPath -Recurse -Force
        }
        if (Test-Path -LiteralPath $modsBackupPath) {
            Move-Item -LiteralPath $modsBackupPath -Destination $modsPath
        }
    }

    if (Test-Path -LiteralPath $fixturePath) {
        $verifiedFixture = Get-ContainedPath $fixturePath $outputDir "Disposable save cleanup"
        Remove-Item -LiteralPath $verifiedFixture -Recurse -Force
    }
    if (Test-Path -LiteralPath $backupDir) {
        $verifiedBackup = Get-ContainedPath $backupDir $outputDir "Backup cleanup"
        Remove-Item -LiteralPath $verifiedBackup -Recurse -Force
    }
    $timeline.Add("CLEANUP|Completed=$(Get-Date -Format o)")
    $timeline | Set-Content -LiteralPath $timelinePath -Encoding utf8
}
