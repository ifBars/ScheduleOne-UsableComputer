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
    [string]$PackagePath = "",
    [ValidateRange(60, 600)]
    [int]$TimeoutSeconds = 180,
    [switch]$IconLayout,
    [switch]$TextFiles,
    [switch]$Reports,
    [switch]$Deliveries,
    [switch]$DesktopUx,
    [switch]$AppIcons,
    [switch]$Dealers,
    [switch]$Drivers,
    [switch]$Power,
    [switch]$Products,
    [switch]$Studio,
    [switch]$LuaStorage,
    [switch]$TvInventory,
    [switch]$Noodle,
    [switch]$Bridge,
    [switch]$EggRun,
    [switch]$DoomRuntime,
    [string]$DoomIwadPath = '',
    [switch]$NestedRuntime,
    [switch]$Laptop
)

$ErrorActionPreference = "Stop"

function Assert-Path([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Restore-Mutation($Mutation) {
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            if ($Mutation.Existed) {
                Assert-Path $Mutation.Backup 'Restoration backup'
                Copy-Item -LiteralPath $Mutation.Backup -Destination $Mutation.Target -Force
                if ((Get-FileHash -LiteralPath $Mutation.Backup).Hash -ne (Get-FileHash -LiteralPath $Mutation.Target).Hash) {
                    throw "Restored file differs: $($Mutation.Target)"
                }
            } elseif (Test-Path -LiteralPath $Mutation.Target) {
                Remove-Item -LiteralPath $Mutation.Target -Force
            }
            return
        } catch {
            if ($attempt -eq 9) { throw }
            Start-Sleep -Milliseconds 500
        }
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
$companionOutput = $mainOutput
$smokeProject = Join-Path $PSScriptRoot "UsableComputer.VfsSmoke\UsableComputer.VfsSmoke.csproj"
$smokeDllPath = Join-Path $PSScriptRoot "UsableComputer.VfsSmoke\bin\$configuration\$targetFramework\UsableComputer.VfsSmoke.dll"
$timeline = [Collections.Generic.List[string]]::new()
$launchedProcess = $null
$modsMoved = $false
$userLibMutations = @()
$nestedPrepared = $false
$nestedRoot = Get-ContainedPath (Join-Path $GamePath 'UserData/UsableComputer/NestedScheduleOne') $GamePath 'Nested profile'
$nestedBackup = Get-ContainedPath (Join-Path $backupDir 'NestedScheduleOne') $outputDir 'Nested profile backup'

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
if ([string]::IsNullOrWhiteSpace($PackagePath)) { Assert-Path $s1ApiPath "S1API runtime build" }

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
    if ($IconLayout) { $arguments += "--usable-computer-icon-layout-smoke" }
    if ($TextFiles) { $arguments += "--usable-computer-text-files-smoke" }
    if ($Reports) { $arguments += "--usable-computer-reports-smoke" }
    if ($Deliveries) { $arguments += "--usable-computer-deliveries-smoke" }
    if ($DesktopUx) { $arguments += "--usable-computer-desktop-ux-smoke" }
    if ($AppIcons) { $arguments += "--usable-computer-app-icons-smoke" }
    if ($Dealers) { $arguments += "--usable-computer-dealers-smoke" }
    if ($Drivers) { $arguments += "--usable-computer-drivers-smoke" }
    if ($Power) { $arguments += "--usable-computer-power-smoke" }
    if ($Products) { $arguments += "--usable-computer-products-smoke" }
    if ($Studio) { $arguments += "--usable-computer-studio-smoke" }
    if ($LuaStorage) { $arguments += "--usable-computer-lua-storage-smoke" }
    if ($TvInventory) { $arguments += "--usable-computer-tv-inventory-smoke" }
    if ($Noodle) { $arguments += "--usable-computer-noodle-smoke" }
    if ($Bridge) { $arguments += "--usable-computer-bridge-smoke" }
    if ($EggRun) { $arguments += "--usable-computer-egg-run-smoke" }
    if ($DoomRuntime) { $arguments += "--usable-computer-doom-runtime-smoke" }
    if ($NestedRuntime) { $arguments += "--usable-computer-nested-runtime-smoke" }
    if ($Laptop) { $arguments += "--usable-computer-laptop-smoke" }
    $script:launchedProcess = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $GamePath -PassThru -WindowStyle Hidden
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

    if ($NestedRuntime) {
        $childLog = Join-Path $nestedRoot 'Loader/MelonLoader/Latest.log'
        if (Test-Path -LiteralPath $childLog) {
            Copy-Item -LiteralPath $childLog -Destination (Join-Path $phaseDirectory 'nested-child.log') -Force
        }
    }
    if (Test-Path -LiteralPath $logPath) {
        Copy-Item -LiteralPath $logPath -Destination (Join-Path $phaseDirectory "Latest.log") -Force
        if (Select-String -LiteralPath $logPath -Pattern 'Could not open desktop app|Desktop app registry listener failed' -Quiet) {
            throw "Desktop UI failure was logged during $Runtime $Phase; inspect $phaseDirectory/Latest.log"
        }
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
        if (-not $script:launchedProcess.WaitForExit(15000)) { throw 'Smoke game process did not exit.' }
    }
    $script:launchedProcess = $null

    if ($result -notmatch "^PASS\|Runtime=$Runtime\|Phase=$Phase\|") {
        throw "Usable Computer VFS $Runtime $Phase smoke failed: $result"
    }
    if ($result -notmatch "Persisted=True" -or $result -notmatch "Screenshot=") {
        throw "Usable Computer VFS $Runtime $Phase smoke did not prove persistence and screenshot capture: $result"
    }
    if ($Laptop -and $result -notmatch "Laptop=True") {
        throw "Usable Computer VFS $Runtime $Phase smoke did not verify the laptop."
    }
    $screenshotPath = Join-Path $phaseDirectory "vfs-$Phase.png"
    Assert-Path $screenshotPath "$Runtime $Phase screenshot"
    if ((Get-Item -LiteralPath $screenshotPath).Length -eq 0) {
        throw "$Runtime $Phase screenshot is empty: $screenshotPath"
    }
}

try {
    if ($DoomRuntime) {
        Assert-Path $DoomIwadPath 'Freedoom test IWAD'
        if ([IO.Path]::GetFileName($DoomIwadPath) -notmatch '^freedoom[12]\.wad$') { throw 'Use an official Freedoom IWAD for this smoke.' }
        $wadTarget = Get-ContainedPath (Join-Path $GamePath 'UserData/UsableComputer/Doom/freedoom2.wad') $GamePath 'Test IWAD'
        $wadBackup = Join-Path $backupDir 'freedoom2.wad'
        $wadExisted = Test-Path -LiteralPath $wadTarget
        if ($wadExisted) { Copy-Item -LiteralPath $wadTarget -Destination $wadBackup }
        $userLibMutations += [pscustomobject]@{ Target=$wadTarget; Backup=$wadBackup; Existed=$wadExisted }
        New-Item -ItemType Directory -Path (Split-Path -Parent $wadTarget) -Force | Out-Null
        Copy-Item -LiteralPath $DoomIwadPath -Destination $wadTarget -Force
    }
    if ($NestedRuntime) {
        if (Test-Path -LiteralPath $nestedRoot) { Move-Item -LiteralPath $nestedRoot -Destination $nestedBackup }
        $nestedPrepared = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
        . (Join-Path $repoRoot 'scripts/PackageTools.ps1')
        $packageStage = Join-Path $outputDir 'package'
        $packageManifest = Expand-RuntimePackage $PackagePath $Runtime $packageStage
        $mainDllPath = Join-Path $packageStage "Mods/$mainDllName"
        $s1ApiPath = Join-Path $packageStage "Mods/$s1ApiInstalledName"
        $companionOutput = Join-Path $packageStage 'UserLibs'
        $timeline.Add("PACKAGE|Runtime=$Runtime|Label=$($packageManifest.label)|SHA256=$((Get-FileHash -LiteralPath $PackagePath).Hash)")
    }
    if ($IconLayout -or $TextFiles -or $Reports -or $Deliveries -or $DesktopUx -or $AppIcons -or $Dealers -or $Drivers -or $Products -or $Studio -or $Noodle -or $LuaStorage -or $Bridge -or $EggRun) {
        $preferencesPath = Get-ContainedPath (Join-Path $GamePath "UserData/MelonPreferences.cfg") $GamePath "Preferences"
        $preferencesBackup = Join-Path $backupDir "MelonPreferences.cfg"
        $preferencesExisted = Test-Path -LiteralPath $preferencesPath
        if ($preferencesExisted) { Copy-Item -LiteralPath $preferencesPath -Destination $preferencesBackup }
        $userLibMutations += [pscustomobject]@{ Target=$preferencesPath; Backup=$preferencesBackup; Existed=$preferencesExisted }
    }
    $timeline.Add("BUILD|Runtime=$Runtime|Started=$(Get-Date -Format o)")
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        & dotnet build (Join-Path $repoRoot "UsableComputer.csproj") -c $configuration -t:Rebuild -p:AutomateLocalDeployment=false -v:q
        if ($LASTEXITCODE -ne 0) { throw "Usable Computer $Runtime rebuild failed" }
    }
    $smokeBuildArguments = @('build', $smokeProject, '-c', $configuration, '-t:Rebuild', '-p:AutomateLocalDeployment=false', '-v:q')
    if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
        $smokeBuildArguments += "-p:UsableComputerPackageDll=$mainDllPath"
        $apiReferenceProperty = if ($Runtime -eq 'Mono') { 'S1ApiMonoPath' } else { 'S1ApiIl2CppPath' }
        $smokeBuildArguments += "-p:${apiReferenceProperty}=$s1ApiPath"
    }
    & dotnet @smokeBuildArguments
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
        Copy-Item -LiteralPath (Join-Path $companionOutput $name) -Destination $target -Force
    }
    $timeline.Add("INSTALL_PASS|Runtime=$Runtime|Mods=S1API,$mainDllName,UsableComputer.VfsSmoke.dll")

    Invoke-SmokePhase "seed"
    Invoke-SmokePhase "reload"
    Write-Host "Usable Computer VFS $Runtime smoke passed." -ForegroundColor Green
    Write-Host "Evidence: $outputDir" -ForegroundColor Green
}
finally {
    $restoreErrors = [Collections.Generic.List[string]]::new()
    if ($launchedProcess) {
        $launchedProcess.Refresh()
        if (-not $launchedProcess.HasExited) {
            Stop-Process -Id $launchedProcess.Id -Force -ErrorAction SilentlyContinue
            [void]$launchedProcess.WaitForExit(15000)
        }
    }

    if ($NestedRuntime) {
        try {
            $childLog = Join-Path $nestedRoot 'Loader/MelonLoader/Latest.log'
            if (Test-Path -LiteralPath $childLog) {
                Copy-Item -LiteralPath $childLog -Destination (Join-Path $outputDir 'nested-child-final.log') -Force
            }
            $launchedParentIds = @($timeline | ForEach-Object {
                if ($_ -match '^LAUNCH\|Runtime=[^|]+\|Phase=[^|]+\|PID=(\d+)$') { [int]$Matches[1] }
            })
            $expectedLoader = '--melonloader.basedir "' + (Join-Path $nestedRoot 'Loader') + '"'
            $childProcesses = Get-CimInstance Win32_Process -Filter "Name = 'Schedule I.exe'" -ErrorAction Stop
            foreach ($child in $childProcesses) {
                if (-not $child.ExecutablePath -or -not $child.CommandLine -or
                    -not [string]::Equals([IO.Path]::GetFullPath($child.ExecutablePath), $exePath,
                        [StringComparison]::OrdinalIgnoreCase) -or
                    $child.CommandLine.IndexOf($expectedLoader, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                    continue
                }
                $matchesLaunchedParent = $false
                foreach ($parentId in $launchedParentIds) {
                    if ($child.CommandLine -match "(?<!\S)--usable-computer-nested-parent\s+$parentId(?!\d)") {
                        $matchesLaunchedParent = $true
                        break
                    }
                }
                if (-not $matchesLaunchedParent) { continue }
                $ownedChild = Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue
                if (-not $ownedChild) { continue }
                Stop-Process -Id $ownedChild.Id -Force -ErrorAction SilentlyContinue
                if (-not $ownedChild.WaitForExit(15000)) {
                    throw "Nested child process $($ownedChild.Id) did not exit after Stop-Process."
                }
                $timeline.Add("NESTED_CHILD_STOP|PID=$($ownedChild.Id)")
            }
        } catch { $restoreErrors.Add("Nested child cleanup: $_") }
    }

    foreach ($mutation in $userLibMutations) {
        try { Restore-Mutation $mutation }
        catch { $restoreErrors.Add("$($mutation.Target): $_") }
    }

    if ($nestedPrepared) {
        try {
            if (Test-Path -LiteralPath $nestedRoot) {
                $verifiedNested = Get-ContainedPath $nestedRoot $GamePath 'Test nested profile cleanup'
                Remove-Item -LiteralPath $verifiedNested -Recurse -Force
            }
            if (Test-Path -LiteralPath $nestedBackup) { Move-Item -LiteralPath $nestedBackup -Destination $nestedRoot }
        } catch { $restoreErrors.Add("Nested profile: $_") }
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
    if ($restoreErrors.Count -eq 0 -and (Test-Path -LiteralPath $backupDir)) {
        $verifiedBackup = Get-ContainedPath $backupDir $outputDir "Backup cleanup"
        Remove-Item -LiteralPath $verifiedBackup -Recurse -Force
    }
    $timeline.Add("CLEANUP|Completed=$(Get-Date -Format o)")
    $timeline | Set-Content -LiteralPath $timelinePath -Encoding utf8
    if ($restoreErrors.Count -gt 0) { throw "Restoration incomplete; backups preserved at $backupDir. $($restoreErrors -join '; ')" }
}
