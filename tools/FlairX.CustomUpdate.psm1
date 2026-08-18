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

Export-ModuleMember -Function `
    Resolve-FlairXInstallRoot, `
    ConvertFrom-FlairXReleaseJson, `
    Assert-FlairXPushTarget
