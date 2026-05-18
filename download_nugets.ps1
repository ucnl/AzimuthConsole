# download_nugets.ps1
$nugetDir = ".\NuGetLocal"
New-Item -ItemType Directory -Force -Path $nugetDir

$repos = @(
    "AZMLib",
    "UCNLDrivers",
    "UCNLKML",
    "UCNLMan",
    "UCNLNav",
    "UCNLNMEA",
    "UCNLPhysics",
    "UCNLSalinity"
)

foreach ($repo in $repos) {
    Write-Host "Downloading latest $repo..." -ForegroundColor Green
    
    # Получаем информацию о последнем релизе
    $releaseUrl = "https://api.github.com/repos/ucnl/$repo/releases/latest"
    $release = Invoke-RestMethod -Uri $releaseUrl
    
    # Ищем .nupkg файл среди прикреплённых файлов
    $nupkg = $release.assets | Where-Object { $_.name -like "*.nupkg" } | Select-Object -First 1
    
    if ($nupkg) {
        $outFile = Join-Path $nugetDir $nupkg.name
        Invoke-WebRequest -Uri $nupkg.browser_download_url -OutFile $outFile
        Write-Host "  OK - $($nupkg.name)" -ForegroundColor Green
    } else {
        Write-Host "  WARNING - No .nupkg found in latest release of $repo" -ForegroundColor Yellow
    }
}

Write-Host "`nAll NuGet packages downloaded to $nugetDir" -ForegroundColor Green