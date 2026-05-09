param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$GameFilesPath = "",
    [switch]$SkipBuild,
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"

$ProjectFile = "TravellersRestFishingTweaks.csproj"
$ModName = "TravellersRestFishingTweaks"
$DllName = "net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll"
$DefaultVersion = "1.0.0"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $inputVersion = Read-Host "Release version [$DefaultVersion]"
    if ([string]::IsNullOrWhiteSpace($inputVersion)) {
        $Version = $DefaultVersion
    }
    else {
        $Version = $inputVersion.Trim()
    }
}

$Version = $Version.Trim()
if ($Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $Version = $Version.Substring(1)
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version cannot be empty."
}

$TagName = "v$Version"
$RepoRoot = $PSScriptRoot
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$ZipName = "$ModName-$TagName.zip"
$ZipPath = Join-Path $ArtifactsDir $ZipName
$ReleaseNotesPath = Join-Path $ArtifactsDir "release-notes-$TagName.md"
$DllPath = Join-Path $RepoRoot "bin\$Configuration\netstandard2.1\$DllName"

function Assert-CommandExists {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Get-MSBuildPropertyArgument {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    return "-p:$Name=$Value"
}

Set-Location -LiteralPath $RepoRoot

Assert-CommandExists -Name "dotnet"
Assert-CommandExists -Name "gh"

$ghAuthStatus = & gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated. Run: gh auth login`n$ghAuthStatus"
}

if (-not (Test-Path -LiteralPath $ArtifactsDir -PathType Container)) {
    New-Item -ItemType Directory -Path $ArtifactsDir | Out-Null
}

if (-not $SkipBuild) {
    $buildArgs = @(
        "build",
        $ProjectFile,
        "--configuration", $Configuration,
        "--nologo",
        (Get-MSBuildPropertyArgument -Name "Version" -Value $Version),
        (Get-MSBuildPropertyArgument -Name "SkipPluginDeploy" -Value "true")
    )

    if (-not [string]::IsNullOrWhiteSpace($GameFilesPath)) {
        if (-not (Test-Path -LiteralPath $GameFilesPath -PathType Container)) {
            throw "GameFilesPath does not exist: $GameFilesPath"
        }

        foreach ($requiredDll in @("Assembly-CSharp.dll", "Sirenix.Serialization.dll", "UnityEngine.UI.dll")) {
            $requiredPath = Join-Path $GameFilesPath $requiredDll
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Missing required game assembly: $requiredPath"
            }
        }

        $buildArgs += (Get-MSBuildPropertyArgument -Name "GameFilesPath" -Value $GameFilesPath)
    }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

if (-not (Test-Path -LiteralPath $DllPath -PathType Leaf)) {
    throw "DLL not found: $DllPath"
}

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

Compress-Archive -LiteralPath $DllPath -DestinationPath $ZipPath -Force

$md5 = (Get-FileHash -LiteralPath $DllPath -Algorithm MD5).Hash.ToLowerInvariant()
$sha256 = (Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash.ToLowerInvariant()

$releaseNotes = @"
## Checksums

File: ``$DllName``

MD5: ``$md5``

SHA256: ``$sha256``
"@

$releaseNotes | Set-Content -LiteralPath $ReleaseNotesPath -Encoding utf8

$existingRelease = & gh release view $TagName 2>$null
if ($LASTEXITCODE -eq 0) {
    throw "GitHub release already exists for tag $TagName. Delete it first or choose a different version."
}

$releaseArgs = @(
    "release", "create", $TagName,
    $ZipPath,
    "--title", "Travellers Rest Fishing Tweaks $TagName",
    "--notes-file", $ReleaseNotesPath
)

if ($Draft) {
    $releaseArgs += "--draft"
}

if ($Prerelease) {
    $releaseArgs += "--prerelease"
}

& gh @releaseArgs
if ($LASTEXITCODE -ne 0) {
    throw "GitHub release creation failed."
}

Write-Host "Release created: $TagName"
Write-Host "Zip: $ZipPath"
Write-Host "MD5: $md5"
Write-Host "SHA256: $sha256"
