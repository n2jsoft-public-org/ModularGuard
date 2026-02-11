#Requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Configuration
$Repo = "n2jsoft-public-org/ModularGuard"
$InstallDir = Join-Path $env:USERPROFILE ".local\bin"
$BinaryName = "modularguard.exe"

Write-Host "Installing ModularGuard..." -ForegroundColor Cyan

# Detect architecture
$Arch = [System.Environment]::Is64BitOperatingSystem
$ArchType = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") {
    "arm64"
} elseif ($Arch) {
    "x64"
} else {
    Write-Host "Error: 32-bit Windows is not supported" -ForegroundColor Red
    exit 1
}

# Construct download URL
$ArtifactName = "modularguard-win-$ArchType"
$DownloadUrl = "https://github.com/$Repo/releases/latest/download/$ArtifactName.zip"

Write-Host "Detected platform: win-$ArchType" -ForegroundColor Gray
Write-Host "Download URL: $DownloadUrl" -ForegroundColor Gray

# Create installation directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Create temporary directory for download
$TmpDir = Join-Path $env:TEMP "modularguard-install-$(Get-Random)"
New-Item -ItemType Directory -Path $TmpDir -Force | Out-Null

try {
    Write-Host "Downloading ModularGuard..." -ForegroundColor Yellow
    $ZipPath = Join-Path $TmpDir "$ArtifactName.zip"

    # Use faster method if available
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipPath -UseBasicParsing
    } else {
        # For Windows PowerShell 5.1
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $WebClient = New-Object System.Net.WebClient
        $WebClient.DownloadFile($DownloadUrl, $ZipPath)
    }

    Write-Host "Extracting archive..." -ForegroundColor Yellow
    Expand-Archive -Path $ZipPath -DestinationPath $TmpDir -Force

    # Find the binary (it might be named modularguard.exe or CLI.exe depending on the build)
    $BinaryPath = $null
    if (Test-Path (Join-Path $TmpDir "modularguard.exe")) {
        $BinaryPath = Join-Path $TmpDir "modularguard.exe"
    } elseif (Test-Path (Join-Path $TmpDir "CLI.exe")) {
        $BinaryPath = Join-Path $TmpDir "CLI.exe"
    } else {
        Write-Host "Error: Binary not found in archive" -ForegroundColor Red
        exit 1
    }

    Write-Host "Installing to $InstallDir\$BinaryName..." -ForegroundColor Yellow
    Copy-Item $BinaryPath -Destination (Join-Path $InstallDir $BinaryName) -Force

    Write-Host "✓ ModularGuard installed successfully!" -ForegroundColor Green

    # Check if directory is in PATH
    $UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($UserPath -notlike "*$InstallDir*") {
        Write-Host ""
        Write-Host "Warning: $InstallDir is not in your PATH" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Would you like to add it to your PATH now? (y/n)" -ForegroundColor Yellow
        $Response = Read-Host

        if ($Response -eq 'y' -or $Response -eq 'Y') {
            $NewPath = "$UserPath;$InstallDir"
            [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")
            Write-Host "✓ Added $InstallDir to your PATH" -ForegroundColor Green
            Write-Host ""
            Write-Host "Please restart your terminal for the changes to take effect." -ForegroundColor Cyan
        } else {
            Write-Host ""
            Write-Host "To add it to your PATH manually, run:" -ForegroundColor Yellow
            Write-Host "  [Environment]::SetEnvironmentVariable('Path', `$env:Path + ';$InstallDir', 'User')" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Or run ModularGuard using the full path: $InstallDir\$BinaryName" -ForegroundColor Gray
        }
    } else {
        Write-Host ""
        Write-Host "Run 'modularguard --help' to get started!" -ForegroundColor Cyan
        Write-Host "(You may need to restart your terminal first)" -ForegroundColor Gray
    }
} finally {
    # Cleanup
    if (Test-Path $TmpDir) {
        Remove-Item $TmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
