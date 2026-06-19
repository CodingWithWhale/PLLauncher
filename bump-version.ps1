param(
    [Parameter(Mandatory=$true)]
    [string]$NewVersion
)

if ($NewVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in format X.Y.Z (e.g. 2.6.27)"
    exit 1
}

$csproj = "PLLauncher\PLLauncher.csproj"
$updateJson = "update.json"
$installerIss = "installer.iss"

(Get-Content $csproj) -replace '<Version>\d+\.\d+\.\d+\.\d+</Version>', "<Version>$NewVersion.0</Version>" `
    -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>" `
    -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>" |
    Set-Content $csproj

$json = Get-Content $updateJson -Raw | ConvertFrom-Json
$json.version = $NewVersion
$json.downloadUrl = "https://github.com/CodingWithWhale/PLLauncher/releases/download/v$NewVersion/PLLauncher_Setup_$NewVersion.exe"
$json.changelog = "v$($NewVersion): "
$json | ConvertTo-Json | Set-Content $updateJson

(Get-Content $installerIss) -replace '#define MyAppVersion "\d+\.\d+\.\d+"', "#define MyAppVersion `"$NewVersion`"" |
    Set-Content $installerIss

Write-Host "All version references bumped to $NewVersion"
Write-Host "Don't forget to edit update.json changelog description before committing!"
