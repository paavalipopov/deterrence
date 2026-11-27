#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
Sets up a limited local account and a private test folder for RimWorld test runs.
- Reads its settings from TestUser.config.psd1 next to this script.
- Installs the packed game, profile and checkout from ..\RimTest when they are present,
  links the checkout into the game, and hands the files to the test account.
- Provisions Claude Desktop for every account and enables the Virtual Machine Platform.
- Safe to run again: an existing account keeps its password, and installed parts are kept.

Run from an administrator PowerShell in this script's folder:
    powershell -ExecutionPolicy Bypass -File .\Setup-TestUser.ps1
Pass -Reinstall to copy the packed game and profile again. The checkout is never replaced.
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'TestUser.config.psd1'),
    [string]$PackRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'RimTest'),
    [switch]$Reinstall
)

$ErrorActionPreference = 'Stop'
$config = Import-PowerShellDataFile -LiteralPath $ConfigPath
$name = $config.UserName
$root = $config.TestRoot

# Use well-known SIDs so group names work in any display language.
$systemSid = 'S-1-5-18'
$adminsSid = 'S-1-5-32-544'
$usersSid  = 'S-1-5-32-545'

# Copy a folder tree with robocopy, without endless retries on locked files.
function Copy-Tree([string]$From, [string]$To) {
    robocopy $From $To /E /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed copying $From (exit code $LASTEXITCODE)." }
}

# Create the local account unless it already exists.
if (Get-LocalUser -Name $name -ErrorAction SilentlyContinue) {
    Write-Host "Account '$name' already exists; keeping its password."
} else {
    $password = Read-Host -AsSecureString "Choose a password for '$name'"
    New-LocalUser -Name $name -Password $password -FullName $config.FullName `
        -Description $config.Description -PasswordNeverExpires -AccountNeverExpires | Out-Null
    Write-Host "Created account '$name'."
}

# Make the account a standard user, never an administrator.
try {
    Add-LocalGroupMember -SID $usersSid -Member $name
} catch [Microsoft.PowerShell.Commands.MemberExistsException] { }

try {
    Remove-LocalGroupMember -SID $adminsSid -Member $name
    Write-Warning "Removed '$name' from Administrators."
} catch [Microsoft.PowerShell.Commands.MemberNotFoundException] { }

# Restrict the test folder to SYSTEM, Administrators and the test account.
# Subfolders are created afterwards so they inherit these permissions.
New-Item -ItemType Directory -Force -Path $root | Out-Null
icacls $root /inheritance:r /grant:r "*${systemSid}:(OI)(CI)F" "*${adminsSid}:(OI)(CI)F" "${name}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls failed with exit code $LASTEXITCODE." }

foreach ($sub in $config.Subfolders) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root $sub) | Out-Null
}
Write-Host "Test folder ready: $root"

# Install the packed game, profile and checkout, keeping parts that are already in place.
$parts = @(
    @{ Name = 'game';    Marker = 'RimWorldWin64.exe';    Replaceable = $true },
    @{ Name = 'profile'; Marker = 'Config';               Replaceable = $true },
    @{ Name = 'repo';    Marker = 'Deterrence\Build.ps1'; Replaceable = $false }
)
$installed = $false
if (Test-Path -LiteralPath $PackRoot) {
    foreach ($part in $parts) {
        $from = Join-Path $PackRoot $part.Name
        $to = Join-Path $root $part.Name
        if (-not (Test-Path -LiteralPath $from)) { continue }
        $present = Test-Path -LiteralPath (Join-Path $to $part.Marker)
        if ($present -and -not ($Reinstall -and $part.Replaceable)) {
            Write-Host "Keeping the existing $($part.Name)."
            continue
        }
        Write-Host "Copying $($part.Name)..."
        Copy-Tree $from $to
        $installed = $true
    }
} else {
    Write-Warning "No pack at $PackRoot. Extract RimTestPack.zip and run this script from its setup folder to install the game."
}

# Clear download marks, which block scripts and make .NET refuse to load DLLs.
if ($installed) {
    Write-Host 'Unblocking installed files...'
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | Unblock-File
}

# Link the checkout into the game's Mods folder, under the name its settings file expects.
$game = Join-Path $root 'game'
$linkScript = Join-Path $root 'repo\Deterrence\Tools\Link-DevelopmentMod.ps1'
if ((Test-Path -LiteralPath (Join-Path $game 'RimWorldWin64.exe')) -and (Test-Path -LiteralPath $linkScript)) {
    & $linkScript -GameRoot $game
} else {
    Write-Warning 'Game or Link-DevelopmentMod.ps1 not found; Deterrence is not linked into the game.'
}

# Give the test account ownership, so Git accepts the checkout and the account can manage its files.
if ($installed) {
    Write-Host "Making '$name' the owner of $root..."
    icacls $root /setowner $name /T /C /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "icacls could not change the owner of some files (exit code $LASTEXITCODE)." }
}

# Write a launcher that starts RimWorld with the packed profile instead of the account's own.
$launcher = Join-Path $root 'Launch-RimWorld.cmd'
Set-Content -LiteralPath $launcher -Encoding ASCII -Value @(
    '@echo off',
    'start "" "%~dp0game\RimWorldWin64.exe" -savedatafolder="%~dp0profile"'
)

# Let every account run local scripts; downloaded scripts still need a signature or Unblock-File.
if ($config.SetRemoteSignedPolicy) {
    try { Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force }
    catch { Write-Warning "Execution policy: $($_.Exception.Message)" }
}

# Provision Claude Desktop machine-wide, since a standard account cannot install it with Cowork.
$restartNeeded = $false
if ($config.InstallClaudeDesktop) {
    $arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
    $msix = Join-Path $root 'installers\Claude.msix'
    if (-not (Test-Path -LiteralPath $msix)) {
        Write-Host 'Downloading Claude Desktop...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        $partial = "$msix.part"
        Invoke-WebRequest -UseBasicParsing -Uri "https://claude.ai/api/desktop/win32/$arch/msix/latest/redirect" -OutFile $partial
        Move-Item -LiteralPath $partial -Destination $msix
    }
    if (Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*Claude*' }) {
        Write-Host 'Claude Desktop is already provisioned.'
    } else {
        Add-AppxProvisionedPackage -Online -PackagePath $msix -SkipLicense -Regions 'all' | Out-Null
        Write-Host 'Provisioned Claude Desktop for all accounts.'
    }

    # Cowork needs the Virtual Machine Platform, which takes effect after a restart.
    $vmp = (Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform).State
    if ($vmp -ne 'Enabled') {
        if ($vmp -ne 'EnablePending') {
            Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart | Out-Null
        }
        $restartNeeded = $true
    }
}

# Keep the machine running while plugged in.
if ($config.NeverSleepOnAC) {
    powercfg /change standby-timeout-ac 0
    powercfg /change hibernate-timeout-ac 0
}
if ($config.IgnoreLidOnAC) {
    powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
    powercfg /setactive SCHEME_CURRENT
}

# Check for tools the test account needs, installed for all users.
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$git = Join-Path $env:ProgramFiles 'Git\cmd\git.exe'

if (-not (Test-Path -LiteralPath $dotnet)) {
    Write-Warning "No .NET SDK at $dotnet. Install the .NET 10 SDK with the regular installer."
} elseif (-not ((& $dotnet --list-sdks) -match '^10\.')) {
    Write-Warning 'The .NET 10 SDK is missing. Install it with the regular installer.'
} else {
    Write-Host 'Found the .NET 10 SDK.'
}

if (Test-Path -LiteralPath $git) { Write-Host 'Found Git.' }
else { Write-Warning "No Git at $git. Install Git for Windows for all users." }

Write-Host ''
Write-Host "Done. Sign in as '$name' to run tests; use your admin account only for installs."
Write-Host "- Start Claude from the Start menu, sign in, and connect $root to sessions."
Write-Host "- Start RimWorld with $launcher."
Write-Host "- Build with: powershell -ExecutionPolicy Bypass -File $root\repo\Deterrence\Build.ps1"
if ($restartNeeded) { Write-Warning 'Restart the computer to finish enabling the Virtual Machine Platform.' }
