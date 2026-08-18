Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FlairXInstallRoot {
    param([Parameter(Mandatory)][string]$InstallPath)

    $resolved = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\')
    $driveRoot = [IO.Path]::GetPathRoot($resolved).TrimEnd('\')
    if ($resolved -eq $driveRoot) {
        throw "Drive roots are unsafe: $resolved"
    }

    $exe = Join-Path $resolved 'app\FlairX Mod Manager.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw "Missing FlairX executable: $exe"
    }

    return $resolved
}

function ConvertFrom-FlairXReleaseJson {
    param([Parameter(Mandatory)][string]$Json)

    $release = $Json | ConvertFrom-Json
    if ($release.prerelease -or [string]::IsNullOrWhiteSpace($release.tag_name)) {
        throw 'Not a stable release.'
    }

    $asset = $release.assets |
        Where-Object { $_.name -like '*.zip' } |
        Select-Object -First 1
    if ($null -eq $asset -or [string]::IsNullOrWhiteSpace($asset.browser_download_url)) {
        throw 'Release ZIP missing.'
    }

    return [pscustomobject]@{
        Tag = [string]$release.tag_name
        DownloadUrl = [string]$asset.browser_download_url
    }
}

function Assert-FlairXPushTarget {
    param(
        [Parameter(Mandatory)][string]$Remote,
        [Parameter(Mandatory)][string]$Branch
    )

    if ($Remote -ne 'fork' -or $Branch -ne 'custom/gamebanana-enhancements') {
        throw "Unsafe push target: $Remote/$Branch"
    }
}

function Invoke-FlairXUpdatePipeline {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Steps,
        [switch]$Preview
    )

    if ($Preview) {
        return @($Steps.Keys)
    }

    foreach ($name in $Steps.Keys) {
        Write-Host "==> $name"
        & $Steps[$name]
    }
}

function Get-FlairXBackupArguments {
    param(
        [Parameter(Mandatory)][string]$InstallRoot,
        [Parameter(Mandatory)][string]$BackupRoot
    )

    return @(
        $InstallRoot,
        '__DESTINATION__',
        '/E',
        '/COPY:DAT',
        '/DCOPY:DAT',
        '/R:2',
        '/W:1',
        '/XD',
        $BackupRoot
    )
}

Export-ModuleMember -Function `
    Resolve-FlairXInstallRoot, `
    ConvertFrom-FlairXReleaseJson, `
    Assert-FlairXPushTarget, `
    Invoke-FlairXUpdatePipeline, `
    Get-FlairXBackupArguments
