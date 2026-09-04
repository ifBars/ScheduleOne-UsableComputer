#!/usr/bin/env pwsh
param(
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
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Description not found: $Path" }
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
    $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) "UsableComputerDisplaySmoke"
}

$GamePath = [IO.Path]::GetFullPath($GamePath)
$SourceSavePath = [IO.Path]::GetFullPath($SourceSavePath)
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Get-ContainedPath (Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $runId) ([IO.Path]::GetFullPath($OutputRoot)) "Output directory"
$fixturePath = Get-ContainedPath (Join-Path $outputDir "save") $outputDir "Disposable save"
$backupDir = Get-ContainedPath (Join-Path $outputDir "backups") $outputDir "Backup directory"
$modsPath = Get-ContainedPath (Join-Path $GamePath "Mods") $GamePath "Mods directory"
$modsBackupPath = Get-ContainedPath (Join-Path $GamePath "Mods.UsableComputerDisplaySmoke.$runId") $GamePath "Mods backup"
$userLibsPath = Get-ContainedPath (Join-Path $GamePath "UserLibs") $GamePath "UserLibs directory"
$exePath = Join-Path $GamePath "Schedule I.exe"
$logPath = Join-Path $GamePath "MelonLoader\Latest.log"
$mainOutput = Join-Path $repoRoot "bin\Mono\netstandard2.1"
$mainDllPath = Join-Path $mainOutput "UsableComputer_Mono.dll"
$smokeProject = Join-Path $PSScriptRoot "UsableComputer.DisplaySmoke\UsableComputer.DisplaySmoke.csproj"
$smokeDllPath = Join-Path $PSScriptRoot "UsableComputer.DisplaySmoke\bin\Mono\netstandard2.1\UsableComputer.DisplaySmoke.dll"
$launchedProcess = $null
$modsMoved = $false
$userLibMutations = @()

New-Item -ItemType Directory -Path $outputDir, $fixturePath, $backupDir -Force | Out-Null
Assert-Path $exePath "Schedule I executable"
Assert-Path $SourceSavePath "Source completed save"
Assert-Path (Join-Path $SourceSavePath "Game.json") "Source Game.json"
Assert-Path $modsPath "Mods directory"
Assert-Path $userLibsPath "UserLibs directory"

$existingProcesses = Get-CimInstance Win32_Process -Filter "Name = 'Schedule I.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and ([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($GamePath, [StringComparison]::OrdinalIgnoreCase) }
if ($existingProcesses) { throw "Schedule I is already running from the target install. Close it before running this smoke test." }
if (Test-Path -LiteralPath $modsBackupPath) { throw "The run-specific Mods backup already exists: $modsBackupPath" }

Get-ChildItem -LiteralPath $SourceSavePath -Force | Copy-Item -Destination $fixturePath -Recurse -Force

[xml]$localProps = Get-Content -Raw -LiteralPath (Join-Path $repoRoot "local.build.props")
$s1ApiPath = [string]$localProps.Project.PropertyGroup.S1ApiMonoPath
$msbuildDirectory = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$s1ApiPath = [IO.Path]::GetFullPath($s1ApiPath.Replace('$(MSBuildThisFileDirectory)', $msbuildDirectory))
Assert-Path $s1ApiPath "S1API Mono build"

try {
    & dotnet build (Join-Path $repoRoot "UsableComputer.csproj") -c Mono -t:Rebuild -p:AutomateLocalDeployment=false -v:q
    if ($LASTEXITCODE -ne 0) { throw "Usable Computer Mono rebuild failed" }
    & dotnet build $smokeProject -c Mono -t:Rebuild -v:q
    if ($LASTEXITCODE -ne 0) { throw "Display smoke rebuild failed" }
    Assert-Path $mainDllPath "Usable Computer build artifact"
    Assert-Path $smokeDllPath "Display smoke build artifact"

    Move-Item -LiteralPath $modsPath -Destination $modsBackupPath
    $modsMoved = $true
    New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
    Copy-Item -LiteralPath $s1ApiPath -Destination (Join-Path $modsPath "S1API.Mono.MelonLoader.dll") -Force
    Copy-Item -LiteralPath $mainDllPath -Destination (Join-Path $modsPath "UsableComputer_Mono.dll") -Force
    Copy-Item -LiteralPath $smokeDllPath -Destination (Join-Path $modsPath "UsableComputer.DisplaySmoke.dll") -Force

    foreach ($name in @("ManagedDoom.Core.dll", "MoonSharp.Interpreter.dll", "UsableComputer.ChildHost.dll")) {
        $target = Get-ContainedPath (Join-Path $userLibsPath $name) $userLibsPath "UserLib target"
        $backup = Join-Path $backupDir $name
        $existed = Test-Path -LiteralPath $target
        if ($existed) { Copy-Item -LiteralPath $target -Destination $backup -Force }
        $userLibMutations += [pscustomobject]@{ Target=$target; Backup=$backup; Existed=$existed }
        Copy-Item -LiteralPath (Join-Path $mainOutput $name) -Destination $target -Force
    }

    $arguments = @(
        "--usable-computer-display-smoke",
        "--usable-computer-display-smoke-dir", "`"$outputDir`"",
        "--usable-computer-display-smoke-save", "`"$fixturePath`"",
        "-screen-fullscreen", "0",
        "-screen-width", "1280",
        "-screen-height", "720"
    )
    $launchedProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $GamePath -PassThru -WindowStyle Normal
    $resultPath = Join-Path $outputDir "result.txt"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $launchedProcess.Refresh()
        if (Test-Path -LiteralPath $resultPath) {
            $candidate = Get-Content -Raw -LiteralPath $resultPath
            if ($candidate -match "^(PASS|FAIL)\|") { break }
        }
        if ($launchedProcess.HasExited) { break }
    }

    if (Test-Path -LiteralPath $logPath) {
        Copy-Item -LiteralPath $logPath -Destination (Join-Path $outputDir "Latest.log") -Force
    }
    $result = if (Test-Path -LiteralPath $resultPath) {
        Get-Content -Raw -LiteralPath $resultPath
    } else {
        "FAIL|Runtime=Mono|Reason=No result file was written"
    }
    if ($result -notmatch "^PASS\|Runtime=Mono\|") { throw "Usable Computer display smoke failed: $result" }
    if ($result -notmatch "DesktopFits=True" -or $result -notmatch "Colliders=0") {
        throw "Display smoke did not prove viewport fit and collider isolation: $result"
    }
    $screenshotPath = Join-Path $outputDir "display.png"
    Assert-Path $screenshotPath "Display screenshot"
    Write-Host "Usable Computer display smoke passed." -ForegroundColor Green
    Write-Host $result -ForegroundColor Green
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
}
