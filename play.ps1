<#
.SYNOPSIS
    One-step setup and launch: installs the .NET 8 SDK and Godot 4.3 (.NET edition) if they
    are missing, builds the game, and starts it.

.DESCRIPTION
    Windows first (Windows PowerShell 5.1 or PowerShell 7), and also works with PowerShell 7
    on Linux and macOS. Godot goes into .tools/ inside the repo, so nothing is installed
    system-wide except the .NET SDK. When winget is unavailable, the SDK goes per-user with no
    admin rights.

    From the repo folder:
        powershell -ExecutionPolicy Bypass -File .\play.ps1            # play
        powershell -ExecutionPolicy Bypass -File .\play.ps1 -Autopilot # watch the AI fly it
        powershell -ExecutionPolicy Bypass -File .\play.ps1 -Editor    # open in the Godot editor
        powershell -ExecutionPolicy Bypass -File .\play.ps1 -Compat    # no Vulkan? use OpenGL

.PARAMETER Autopilot
    Let the AI fly the player's ship.
.PARAMETER Editor
    Open the project in the Godot editor instead of running the game.
.PARAMETER Mission
    Path to a mission JSON file to fly instead of the built-in vertical slice.
.PARAMETER Compat
    Use the OpenGL renderer. For older GPUs, VMs and remote desktop sessions without Vulkan
    (Godot says "video card drivers seem not to support the required Vulkan version").
.PARAMETER Check
    Set up and build, then run the mission headless with the autopilot and exit. A smoke test.
#>
[CmdletBinding()]
param(
    [switch]$Autopilot,
    [switch]$Editor,
    [string]$Mission,
    [switch]$Compat,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is very slow with the progress bar
if ($PSVersionTable.PSVersion.Major -lt 6) {
    # Windows PowerShell 5.1 defaults to old TLS versions that GitHub rejects.
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

$GodotVersion = '4.3-stable'
$Root = $PSScriptRoot
$ToolsDir = Join-Path $Root '.tools'
$ProjectDir = Join-Path $Root 'godot'
$OnWindows = ($PSVersionTable.PSVersion.Major -lt 6) -or $IsWindows

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Update-SessionPath {
    if ($OnWindows) {
        $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
        $user = [Environment]::GetEnvironmentVariable('Path', 'User')
        $env:Path = "$machine;$user;$env:Path"
    }
}

function Test-DotNet8 {
    try {
        $sdks = & dotnet --list-sdks 2>$null
        return [bool]($sdks | Where-Object { $_ -match '^8\.' })
    }
    catch { return $false }
}

function Install-DotNet {
    if (Test-DotNet8) { Write-Step '.NET 8 SDK found'; return }

    if ($OnWindows -and (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-Step 'Installing .NET 8 SDK with winget'
        winget install --id Microsoft.DotNet.SDK.8 --exact --silent --accept-source-agreements --accept-package-agreements
        Update-SessionPath
        if (Test-DotNet8) { return }
        Write-Warning 'winget did not make dotnet available in this session; falling back to a per-user install.'
    }

    Write-Step 'Installing .NET 8 SDK (per-user, no admin needed)'
    if ($OnWindows) {
        $installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
        $script = Join-Path ([IO.Path]::GetTempPath()) 'dotnet-install.ps1'
        Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
        & $script -Channel 8.0 -InstallDir $installDir
        $env:Path = "$installDir;$env:Path"
    }
    else {
        $installDir = Join-Path $HOME '.dotnet'
        $script = Join-Path ([IO.Path]::GetTempPath()) 'dotnet-install.sh'
        Invoke-WebRequest 'https://dot.net/v1/dotnet-install.sh' -OutFile $script
        & bash $script --channel 8.0 --install-dir $installDir
        $env:PATH = "$installDir$([IO.Path]::PathSeparator)$env:PATH"
    }
    $env:DOTNET_ROOT = $installDir
    if (-not (Test-DotNet8)) { throw '.NET 8 SDK installation failed. Install it manually from https://dotnet.microsoft.com/download/dotnet/8.0' }
}

function Get-GodotPaths {
    # Returns @{ Zip; Folder; Gui; Console } for this OS. Console is the binary that blocks
    # until it exits and prints to the terminal (used for importing and the smoke test).
    if ($OnWindows) {
        $name = "Godot_v${GodotVersion}_mono_win64"
        $folder = Join-Path $ToolsDir $name
        return @{
            Zip = "$name.zip"; Folder = $folder
            Gui = Join-Path $folder "$name.exe"
            Console = Join-Path $folder "${name}_console.exe"
        }
    }
    if ($IsMacOS) {
        $folder = Join-Path $ToolsDir 'Godot_mono.app'
        $bin = Join-Path $folder 'Contents/MacOS/Godot'
        return @{ Zip = "Godot_v${GodotVersion}_mono_macos.universal.zip"; Folder = $folder; Gui = $bin; Console = $bin }
    }
    $name = "Godot_v${GodotVersion}_mono_linux_x86_64"
    $folder = Join-Path $ToolsDir $name
    $bin = Join-Path $folder "Godot_v${GodotVersion}_mono_linux.x86_64"
    return @{ Zip = "$name.zip"; Folder = $folder; Gui = $bin; Console = $bin }
}

function Install-Godot {
    $paths = Get-GodotPaths
    if (Test-Path $paths.Gui) { Write-Step "Godot $GodotVersion (.NET) found"; return $paths }

    Write-Step "Downloading Godot $GodotVersion (.NET edition, ~70 MB)"
    New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
    $zip = Join-Path $ToolsDir $paths.Zip
    Invoke-WebRequest "https://github.com/godotengine/godot/releases/download/$GodotVersion/$($paths.Zip)" -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $ToolsDir -Force
    Remove-Item $zip
    if (-not $OnWindows) { & chmod +x $paths.Gui }
    if (-not (Test-Path $paths.Gui)) { throw "Godot executable not found after extracting: $($paths.Gui)" }
    return $paths
}

function Invoke-Native([string]$Exe, [string[]]$Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$([IO.Path]::GetFileName($Exe)) failed with exit code $LASTEXITCODE" }
}

Install-DotNet
$godot = Install-Godot

Write-Step 'Building'
Invoke-Native 'dotnet' @('build', (Join-Path $ProjectDir 'XWingGodot.sln'), '--nologo', '-v', 'quiet')

# dotnet build creates .godot/mono, so check for something only an editor import creates.
if (-not (Test-Path (Join-Path $ProjectDir '.godot/uid_cache.bin'))) {
    Write-Step 'First run: importing the project (one time, ~30 s)'
    & $godot.Console --headless --editor --quit --path $ProjectDir | Out-Null
}

$gameArgs = @()
if ($Autopilot -or $Check) { $gameArgs += '--autopilot' }
if ($Mission) { $gameArgs += @('--mission', (Resolve-Path $Mission).Path) }

if ($Check) {
    Write-Step 'Smoke test: flying the mission headless with the autopilot'
    $out = & $godot.Console --headless --path $ProjectDir --fixed-fps 60 --quit-after 36000 -- @gameArgs 2>&1
    $result = $out | Where-Object { "$_" -match 'Mission (Success|Failure)' }
    if (-not $result) { $out | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }; throw 'Smoke test: the mission did not finish.' }
    Write-Host $result
    return
}

if ($Editor) {
    Write-Step 'Opening the Godot editor (press F5 to play)'
    $editorArgs = @('--editor', '--path', "`"$ProjectDir`"")
    if ($Compat) { $editorArgs += @('--rendering-driver', 'opengl3') }
    Start-Process -FilePath $godot.Gui -ArgumentList $editorArgs
    return
}

Write-Step 'Launching. Esc quits, F5 restarts, P pauses. Controls: docs/CONTROLS.md'
$launch = @('--path', "`"$ProjectDir`"")
if ($Compat) { $launch += @('--rendering-driver', 'opengl3') }
if ($gameArgs.Count -gt 0) { $launch += @('--') + ($gameArgs | ForEach-Object { "`"$_`"" }) }
Start-Process -FilePath $godot.Gui -ArgumentList $launch
