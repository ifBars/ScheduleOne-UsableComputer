[CmdletBinding()]
param(
    [ValidateSet('Mono', 'Il2cpp')]
    [string] $Runtime = 'Mono',
    [string] $GameSourceRoot,
    [string] $GoldbergSourceRoot = 'D:\SteamLibrary\steamapps\common\Schedule I_public_server\Schedule I_Data\Plugins\x86_64',
    [string] $UsableComputerDll,
    [string] $S1ApiDll,
    [string] $SmokeDll,
    [string] $FixtureSavePath = 'C:\Users\ghost\AppData\LocalLow\TVGS\Schedule I\Saves\76561198000000009\SaveGame_2',
    [string] $OutputRoot,
    [string] $HostSteamId = '76561198000000009',
    [string] $ClientSteamId = '76561198000000019',
    [int] $TimeoutSeconds = 720,
    [switch] $StageOnly,
    [switch] $UseExistingStage,
    [switch] $CleanupCopies
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($GameSourceRoot)) {
    $GameSourceRoot = if ($Runtime -eq 'Mono') {
        'D:\ScheduleOneBetaCopies\ScheduleI-beta-mono-25439857'
    } else {
        'D:\ScheduleOneBetaCopies\ScheduleI-beta-il2cpp-25439817'
    }
}
if ([string]::IsNullOrWhiteSpace($UsableComputerDll)) {
    $UsableComputerDll = if ($Runtime -eq 'Mono') {
        Join-Path $repoRoot 'bin\Mono\netstandard2.1\UsableComputer_Mono.dll'
    } else {
        Join-Path $repoRoot 'bin\Il2cpp\net6.0\UsableComputer_Il2cpp.dll'
    }
}
if ([string]::IsNullOrWhiteSpace($S1ApiDll)) {
    $S1ApiDll = if ($Runtime -eq 'Mono') {
        Join-Path $repoRoot '..\S1API-beta-5\S1API\bin\MonoMelon\netstandard2.1\S1API.dll'
    } else {
        Join-Path $repoRoot '..\S1API-beta-5\S1API\bin\Il2CppMelon\net6.0\S1API.dll'
    }
}
if ([string]::IsNullOrWhiteSpace($SmokeDll)) {
    $targetFramework = if ($Runtime -eq 'Mono') { 'netstandard2.1' } else { 'net6.0' }
    $SmokeDll = Join-Path $PSScriptRoot "UsableComputer.MultiplayerSmoke\bin\$Runtime\$targetFramework\UsableComputer.MultiplayerSmoke.dll"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputRoot = "D:\ScheduleOneMultiplayerSmoke\$Runtime-$stamp"
}

function Get-FileHashHex([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Require-File([string] $Path, [string] $Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label not found: $Path"
    }
}

function Copy-GameRoot([string] $Source, [string] $Destination) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($_.PSIsContainer -and $_.Name -in @('Mods', 'UserLibs', 'UserData', 'Plugins')) { return }
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Copy-GoldbergSettings([string] $DestinationSettingsRoot) {
    New-Item -ItemType Directory -Path $DestinationSettingsRoot -Force | Out-Null
    $sourceSettings = Join-Path $GoldbergSourceRoot 'steam_settings'
    Get-ChildItem -LiteralPath $sourceSettings -Force -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($sourceSettings.TrimEnd('\').Length).TrimStart('\')
        $leafName = $_.Name.ToLowerInvariant()
        if ($leafName -in @('configs.user.ini', 'account_name.txt', 'user_steam_id.txt', 'account_steamid.txt')) { return }
        $destinationPath = Join-Path $DestinationSettingsRoot $relativePath
        if ($_.PSIsContainer) {
            New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
        } else {
            New-Item -ItemType Directory -Path (Split-Path -Parent $destinationPath) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
        }
    }
}

function Set-GoldbergIdentity([string] $GameRoot, [string] $SteamId, [string] $AccountName) {
    $pluginRoot = Join-Path $GameRoot 'Schedule I_Data\Plugins\x86_64'
    $settingsRoot = Join-Path $pluginRoot 'steam_settings'
    Copy-GoldbergSettings $settingsRoot
    Copy-Item -LiteralPath (Join-Path $GoldbergSourceRoot 'steam_api64.dll') -Destination (Join-Path $pluginRoot 'steam_api64.dll') -Force
    Copy-Item -LiteralPath (Join-Path $GoldbergSourceRoot 'steam_api64.dll.real') -Destination (Join-Path $pluginRoot 'steam_api64.dll.real') -Force
    @"
[user::general]
account_name=$AccountName
account_steamid=$SteamId
language=english

[main::connectivity]
disable_networking=0
"@ | Set-Content -LiteralPath (Join-Path $settingsRoot 'configs.user.ini') -Encoding ascii
}

function Copy-GameLogs([string] $GameRoot, [string] $Role) {
    $destinationRoot = Join-Path $OutputRoot "evidence\$Role"
    New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
    $candidates = @(
        (Join-Path $GameRoot 'MelonLoader\Latest.log'),
        (Join-Path $GameRoot 'Player.log')
    )
    foreach ($file in $candidates) {
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            Copy-Item -LiteralPath $file -Destination $destinationRoot -Force
        }
    }
    foreach ($logs in @((Join-Path $GameRoot 'MelonLoader\Logs'), (Join-Path $GameRoot 'UserData\MelonLoader\Logs'))) {
        if (Test-Path -LiteralPath $logs -PathType Container) {
            $destination = Join-Path $destinationRoot ([System.IO.Path]::GetFileName($logs))
            Copy-Item -LiteralPath $logs -Destination $destination -Recurse -Force
        }
    }
}

function Stop-OwnedProcess([System.Diagnostics.Process] $Process) {
    if ($null -eq $Process) { return }
    try {
        $Process.Refresh()
        if (-not $Process.HasExited) {
            Stop-Process -InputObject $Process -Force
            [void]$Process.WaitForExit(10000)
        }
    } catch { }
}

function Assert-ContainedPath([string] $Path, [string] $Root) {
    $absolutePath = [System.IO.Path]::GetFullPath($Path)
    $absoluteRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $absolutePath.StartsWith($absoluteRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the generated run root: $absolutePath"
    }
}

function Join-QuotedArguments([string[]] $Arguments) {
    if ($Arguments | Where-Object { $_.Contains('"') }) { throw 'Game arguments cannot contain a double quote.' }
    return (($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' ')
}

if (-not (Test-Path -LiteralPath $GameSourceRoot -PathType Container)) { throw "Game source root not found: $GameSourceRoot" }
if (-not (Test-Path -LiteralPath $FixtureSavePath -PathType Container)) { throw "Fixture save not found: $FixtureSavePath" }
Require-File (Join-Path $GameSourceRoot 'Schedule I.exe') 'Game executable'
Require-File (Join-Path $GoldbergSourceRoot 'steam_api64.dll') 'Trusted Goldberg steam_api64.dll'
Require-File (Join-Path $GoldbergSourceRoot 'steam_api64.dll.real') 'Trusted Goldberg original steam_api64.dll.real'
Require-File (Join-Path $GoldbergSourceRoot 'steam_settings\configs.user.ini') 'Trusted Goldberg user config'
Require-File $UsableComputerDll 'Runtime-matched UsableComputer assembly'
Require-File $S1ApiDll 'Runtime-matched S1API assembly'
Require-File $SmokeDll 'Runtime-matched multiplayer smoke assembly'
foreach ($dependency in @('MoonSharp.Interpreter.dll', 'ManagedDoom.Core.dll', 'UsableComputer.ChildHost.dll')) {
    Require-File (Join-Path (Split-Path -Parent $UsableComputerDll) $dependency) "UsableComputer runtime dependency $dependency"
}
if ($HostSteamId -eq $ClientSteamId) { throw 'HostSteamId and ClientSteamId must be distinct.' }
foreach ($steamId in @($HostSteamId, $ClientSteamId)) {
    if ($steamId -notmatch '^\d{17}$') { throw "Expected a 17-digit isolated test Steam ID, got: $steamId" }
}
if (-not [System.IO.Path]::IsPathRooted($OutputRoot)) { throw 'OutputRoot must be an absolute path.' }
if ((Test-Path -LiteralPath $OutputRoot) -and -not $UseExistingStage) { throw "OutputRoot already exists; refusing to overwrite: $OutputRoot" }
if (-not (Test-Path -LiteralPath $OutputRoot -PathType Container) -and $UseExistingStage) { throw "Existing stage was not found: $OutputRoot" }
if ($StageOnly -and $UseExistingStage) { throw 'StageOnly cannot be combined with UseExistingStage.' }
$outputAbsolute = [System.IO.Path]::GetFullPath($OutputRoot)
$OutputRoot = $outputAbsolute
$sourceAbsolute = [System.IO.Path]::GetFullPath($GameSourceRoot).TrimEnd('\') + '\'
$fixtureAbsolute = [System.IO.Path]::GetFullPath($FixtureSavePath).TrimEnd('\') + '\'
$goldbergAbsolute = [System.IO.Path]::GetFullPath($GoldbergSourceRoot).TrimEnd('\') + '\'
if ($outputAbsolute.StartsWith($sourceAbsolute, [System.StringComparison]::OrdinalIgnoreCase) -or
    $outputAbsolute.StartsWith($fixtureAbsolute, [System.StringComparison]::OrdinalIgnoreCase) -or
    $outputAbsolute.StartsWith($goldbergAbsolute, [System.StringComparison]::OrdinalIgnoreCase) -or
    $sourceAbsolute.StartsWith($outputAbsolute.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase) -or
    $fixtureAbsolute.StartsWith($outputAbsolute.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase) -or
    $goldbergAbsolute.StartsWith($outputAbsolute.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputRoot must be separate from and must not overlap the source game, Goldberg settings, or fixture save.'
}
if ($TimeoutSeconds -lt 60 -or $TimeoutSeconds -gt 3600) { throw 'TimeoutSeconds must be between 60 and 3600.' }

$goldbergFiles = @(
    (Join-Path $GoldbergSourceRoot 'steam_api64.dll'),
    (Join-Path $GoldbergSourceRoot 'steam_api64.dll.real'),
    (Join-Path $GoldbergSourceRoot 'steam_settings\configs.user.ini')
)
$goldbergHashesBefore = @{}
foreach ($file in $goldbergFiles) { $goldbergHashesBefore[$file] = Get-FileHashHex $file }
$hostProcess = $null
$clientProcess = $null
$stagedRoot = $null

try {
    $hostRoot = Join-Path $OutputRoot 'host'
    $clientRoot = Join-Path $OutputRoot 'client'
    $sharedRoot = Join-Path $OutputRoot 'shared'
    $fixtureCopy = Join-Path $OutputRoot 'fixture\SaveGame_2'
    $stagedRoot = $OutputRoot
    if ($UseExistingStage) {
        foreach ($path in @($hostRoot, $clientRoot, $sharedRoot, $fixtureCopy)) {
            if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw "Existing stage is incomplete; missing $path" }
        }
        if (@(Get-ChildItem -LiteralPath $sharedRoot -Force -Recurse).Count -gt 0) {
            throw 'Existing shared smoke directory is not empty; refusing to accept stale handoff or PASS files. Stage a fresh run root.'
        }
        foreach ($gameRoot in @($hostRoot, $clientRoot)) {
            Require-File (Join-Path $gameRoot 'Schedule I.exe') 'Staged game executable'
            Require-File (Join-Path $gameRoot 'Mods\S1API.dll') 'Staged S1API assembly'
            Require-File (Join-Path $gameRoot ("Mods\" + (Split-Path -Leaf $UsableComputerDll))) 'Staged UsableComputer assembly'
            Require-File (Join-Path $gameRoot ("Mods\" + (Split-Path -Leaf $SmokeDll))) 'Staged smoke assembly'
            foreach ($dependency in @('MoonSharp.Interpreter.dll', 'ManagedDoom.Core.dll', 'UsableComputer.ChildHost.dll')) {
                Require-File (Join-Path $gameRoot "UserLibs\$dependency") "Staged UserLib $dependency"
            }
        }
        $identityPairs = @(
            [pscustomobject]@{ Root = $hostRoot; SteamId = $HostSteamId },
            [pscustomobject]@{ Root = $clientRoot; SteamId = $ClientSteamId }
        )
        foreach ($identity in $identityPairs) {
            $identityConfig = Join-Path $identity.Root 'Schedule I_Data\Plugins\x86_64\steam_settings\configs.user.ini'
            Require-File $identityConfig 'Staged Goldberg identity config'
            if (-not (Select-String -LiteralPath $identityConfig -Pattern ("^account_steamid=" + [regex]::Escape($identity.SteamId) + '$') -Quiet)) {
                throw "Staged Goldberg identity does not match expected ID for $($identity.Root)."
            }
        }
        Write-Host "Reusing staged $Runtime host/client copies at $OutputRoot."
    } else {
        New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $sharedRoot, (Split-Path -Parent $fixtureCopy) | Out-Null

        Write-Host "Copying $Runtime game source to isolated host and client roots."
        Copy-GameRoot $GameSourceRoot $hostRoot
        Copy-GameRoot $GameSourceRoot $clientRoot
        Copy-Item -LiteralPath $FixtureSavePath -Destination $fixtureCopy -Recurse
    }

    foreach ($gameRoot in @($hostRoot, $clientRoot)) {
        $oldLatestLog = Join-Path $gameRoot 'MelonLoader\Latest.log'
        $oldLogs = Join-Path $gameRoot 'MelonLoader\Logs'
        Assert-ContainedPath $oldLatestLog $OutputRoot
        Assert-ContainedPath $oldLogs $OutputRoot
        if (Test-Path -LiteralPath $oldLatestLog) { Remove-Item -LiteralPath $oldLatestLog -Force }
        if (Test-Path -LiteralPath $oldLogs) { Remove-Item -LiteralPath $oldLogs -Recurse -Force }
    }

    if (-not $UseExistingStage) {
        Set-GoldbergIdentity $hostRoot $HostSteamId 'UsableComputerHost'
        Set-GoldbergIdentity $clientRoot $ClientSteamId 'UsableComputerClient'

        foreach ($gameRoot in @($hostRoot, $clientRoot)) {
            $mods = Join-Path $gameRoot 'Mods'
            $userLibs = Join-Path $gameRoot 'UserLibs'
            New-Item -ItemType Directory -Path $mods, $userLibs -Force | Out-Null
            Copy-Item -LiteralPath $UsableComputerDll -Destination (Join-Path $mods (Split-Path -Leaf $UsableComputerDll)) -Force
            Copy-Item -LiteralPath $S1ApiDll -Destination (Join-Path $mods 'S1API.dll') -Force
            Copy-Item -LiteralPath $SmokeDll -Destination $mods -Force
            foreach ($dependency in @('MoonSharp.Interpreter.dll', 'ManagedDoom.Core.dll', 'UsableComputer.ChildHost.dll')) {
                Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $UsableComputerDll) $dependency) -Destination $userLibs -Force
            }
        }
    }

    $hostExe = Join-Path $hostRoot 'Schedule I.exe'
    $clientExe = Join-Path $clientRoot 'Schedule I.exe'
    $lines = @(
        "Runtime=$Runtime",
        "OutputRoot=$OutputRoot",
        "HostRoot=$hostRoot",
        "ClientRoot=$clientRoot",
        "SharedRoot=$sharedRoot",
        "FixtureCopy=$fixtureCopy",
        "HostSteamId=$HostSteamId",
        "ClientSteamId=$ClientSteamId",
        "HostExe=$hostExe",
        "ClientExe=$clientExe"
    )
    $lines | Set-Content -LiteralPath (Join-Path $OutputRoot 'launch-commands.txt') -Encoding utf8

    foreach ($file in $goldbergFiles) {
        if ((Get-FileHashHex $file) -ne $goldbergHashesBefore[$file]) { throw "Trusted Goldberg source changed during staging: $file" }
    }
    $goldbergHashesBefore.GetEnumerator() | ForEach-Object { "$($_.Value)  $($_.Key)" } |
        Set-Content -LiteralPath (Join-Path $OutputRoot 'goldberg-source-sha256.txt') -Encoding ascii
    $lines | ForEach-Object { Write-Host $_ }
    Write-Host "PASS: isolated same-runtime copies staged; trusted Goldberg DLLs/config hashes are unchanged."
    if (-not $StageOnly) {
        $hostArguments = @('--uc-mp-role', 'host', '--uc-mp-root', $sharedRoot, '--uc-mp-save', $fixtureCopy,
            '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
            '--melonloader.hideconsole', '--melonloader.disablestartscreen', '-logFile', (Join-Path $hostRoot 'Player.log'))
        $clientArgumentsPrefix = @('+connect_lobby')
        $argumentsSuffix = @('--uc-mp-role', 'client', '--uc-mp-root', $sharedRoot, '--uc-mp-save', $fixtureCopy,
            '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
            '--melonloader.hideconsole', '--melonloader.disablestartscreen', '-logFile', (Join-Path $clientRoot 'Player.log'))

        Write-Host 'Starting isolated host process (hidden).'
        $hostProcess = Start-Process -FilePath $hostExe -WorkingDirectory $hostRoot -ArgumentList (Join-QuotedArguments $hostArguments) -WindowStyle Hidden -PassThru
        $lobbyPath = Join-Path $sharedRoot 'lobby.txt'
        $joinDeadline = [DateTime]::UtcNow.AddSeconds(180)
        $lobbyId = ''
        while ([DateTime]::UtcNow -lt $joinDeadline) {
            $hostProcess.Refresh()
            if ($hostProcess.HasExited) { throw "Host process exited before lobby creation (exit=$($hostProcess.ExitCode))." }
            if (Test-Path -LiteralPath $lobbyPath -PathType Leaf) {
                $match = [regex]::Match((Get-Content -LiteralPath $lobbyPath -Raw), 'id=(\d+)')
                if ($match.Success -and $match.Groups[1].Value -ne '0') { $lobbyId = $match.Groups[1].Value; break }
            }
            Start-Sleep -Seconds 2
        }
        if ([string]::IsNullOrWhiteSpace($lobbyId)) { throw 'Host did not publish a nonzero lobby ID within 180 seconds.' }

        Write-Host "Host lobby is $lobbyId; starting isolated client process (hidden)."
        $clientArguments = @($clientArgumentsPrefix + @($lobbyId) + $argumentsSuffix)
        $clientProcess = Start-Process -FilePath $clientExe -WorkingDirectory $clientRoot -ArgumentList (Join-QuotedArguments $clientArguments) -WindowStyle Hidden -PassThru

        $hostResult = Join-Path $sharedRoot 'host-result.txt'
        $clientResult = Join-Path $sharedRoot 'client-result.txt'
        $runDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $passed = $false
        while ([DateTime]::UtcNow -lt $runDeadline) {
            foreach ($resultPath in @($hostResult, $clientResult)) {
                if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
                    $resultText = Get-Content -LiteralPath $resultPath -Raw
                    if ($resultText.StartsWith('FAIL|', [System.StringComparison]::Ordinal)) { throw "Smoke failed: $resultText" }
                }
            }
            $hostOk = (Test-Path -LiteralPath $hostResult) -and (Get-Content -LiteralPath $hostResult -Raw).StartsWith('PASS|')
            $clientOk = (Test-Path -LiteralPath $clientResult) -and (Get-Content -LiteralPath $clientResult -Raw).StartsWith('PASS|')
            if ($hostOk -and $clientOk) { $passed = $true; break }
            $hostProcess.Refresh(); $clientProcess.Refresh()
            if ($hostProcess.HasExited -and -not $hostOk) { throw "Host exited before PASS result (exit=$($hostProcess.ExitCode))." }
            if ($clientProcess.HasExited -and -not $clientOk) { throw "Client exited before PASS result (exit=$($clientProcess.ExitCode))." }
            Start-Sleep -Seconds 2
        }
        if (-not $passed) { throw "Host/client smoke did not both pass within $TimeoutSeconds seconds." }
        Write-Host 'PASS: both host-result.txt and client-result.txt report PASS.'
    } else {
        Write-Host "StageOnly requested; isolated files are ready at $OutputRoot."
    }
} catch {
    if (Test-Path -LiteralPath $OutputRoot -PathType Container) {
        $_ | Out-String | Set-Content -LiteralPath (Join-Path $OutputRoot 'runner-failure.txt') -Encoding utf8
    }
    throw
} finally {
    Stop-OwnedProcess $clientProcess
    Stop-OwnedProcess $hostProcess
    if ($stagedRoot -and (Test-Path -LiteralPath $stagedRoot)) {
        try { Copy-GameLogs (Join-Path $stagedRoot 'host') 'host' } catch { Write-Warning "Could not copy host logs: $($_.Exception.Message)" }
        try { Copy-GameLogs (Join-Path $stagedRoot 'client') 'client' } catch { Write-Warning "Could not copy client logs: $($_.Exception.Message)" }
        try {
            $sharedEvidence = Join-Path $stagedRoot 'evidence\shared'
            New-Item -ItemType Directory -Path $sharedEvidence -Force | Out-Null
            foreach ($name in @('lobby.txt', 'manifest.txt', 'host-result.txt', 'client-result.txt')) {
                $path = Join-Path $stagedRoot "shared\$name"
                if (Test-Path -LiteralPath $path -PathType Leaf) { Copy-Item -LiteralPath $path -Destination $sharedEvidence -Force }
            }
        } catch { Write-Warning "Could not copy shared smoke evidence: $($_.Exception.Message)" }
        if ($CleanupCopies) {
            foreach ($name in @('host', 'client', 'fixture')) {
                $candidate = [System.IO.Path]::GetFullPath((Join-Path $stagedRoot $name))
                Assert-ContainedPath $candidate $stagedRoot
                if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate -Recurse -Force }
            }
        }
    }
    foreach ($file in $goldbergFiles) {
        if ((Get-FileHashHex $file) -ne $goldbergHashesBefore[$file]) { throw "Trusted Goldberg source integrity check failed: $file" }
    }
}
