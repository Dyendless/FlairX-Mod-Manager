$module = Join-Path $PSScriptRoot '..\FlairX.CustomUpdate.psm1'
Import-Module $module -Force

Describe 'Resolve-FlairXInstallRoot' {
    It 'accepts a root containing the main executable' {
        $root = Join-Path $TestDrive 'flairx'
        New-Item -ItemType Directory -Path (Join-Path $root 'app') | Out-Null
        New-Item -ItemType File -Path (Join-Path $root 'app\FlairX Mod Manager.exe') | Out-Null

        Resolve-FlairXInstallRoot $root | Should Be ([IO.Path]::GetFullPath($root))
    }

    It 'rejects a root without the executable' {
        { Resolve-FlairXInstallRoot $TestDrive } | Should Throw
    }

    It 'rejects a drive root' {
        { Resolve-FlairXInstallRoot ([IO.Path]::GetPathRoot($TestDrive)) } | Should Throw
    }
}

Describe 'ConvertFrom-FlairXReleaseJson' {
    It 'returns the stable tag and zip URL' {
        $json = '{"tag_name":"4.1.0","prerelease":false,"assets":[{"name":"FlairX.Mod.Manager.zip","browser_download_url":"https://example/app.zip"}]}'

        $result = ConvertFrom-FlairXReleaseJson $json

        $result.Tag | Should Be '4.1.0'
        $result.DownloadUrl | Should Be 'https://example/app.zip'
    }

    It 'rejects prereleases' {
        { ConvertFrom-FlairXReleaseJson '{"tag_name":"beta","prerelease":true,"assets":[]}' } | Should Throw
    }
}

Describe 'Assert-FlairXPushTarget' {
    It 'only allows the fork maintenance branch' {
        { Assert-FlairXPushTarget fork 'custom/gamebanana-enhancements' } | Should Not Throw
        { Assert-FlairXPushTarget origin 'custom/gamebanana-enhancements' } | Should Throw
        { Assert-FlairXPushTarget fork main } | Should Throw
    }
}

Describe 'Invoke-FlairXUpdatePipeline' {
    It 'stops before deployment when a validation stage fails' {
        $calls = New-Object System.Collections.ArrayList
        $steps = [ordered]@{
            Sync = { [void]$calls.Add('Sync') }
            Test = { [void]$calls.Add('Test'); throw 'failed' }
            Deploy = { [void]$calls.Add('Deploy') }
        }

        { Invoke-FlairXUpdatePipeline $steps } | Should Throw
        ($calls -join ',') | Should Be 'Sync,Test'
    }

    It 'runs no stage in preview mode' {
        $calls = New-Object System.Collections.ArrayList
        $steps = [ordered]@{
            Sync = { [void]$calls.Add('Sync') }
            Deploy = { [void]$calls.Add('Deploy') }
        }

        $names = Invoke-FlairXUpdatePipeline $steps -Preview

        $calls.Count | Should Be 0
        ($names -join ',') | Should Be 'Sync,Deploy'
    }
}

Describe 'Get-FlairXBackupArguments' {
    It 'excludes the backup directory' {
        $arguments = Get-FlairXBackupArguments 'E:\flairx' 'E:\flairx\backups'

        ($arguments -join ' ') | Should Match '/XD E:\\flairx\\backups'
    }
}
