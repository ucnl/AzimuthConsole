# Устанавливаем кодировку UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
chcp 65001 > $null

# Настройки
$platforms = @(
    @{rid="win-x64"; name="win_x64"; ext=".exe"},
    @{rid="linux-x64"; name="linux_x64"; ext=""},
    @{rid="linux-arm"; name="linux_arm"; ext=""},
    @{rid="linux-arm64"; name="linux_arm64"; ext=""},
    @{rid="osx-x64"; name="osx_x64"; ext=""},
    @{rid="osx-arm64"; name="osx_arm64"; ext=""}
)

$basePath = ".\bin\Release\net8.0\publish"
$archivesPath = ".\bin\Release\net8.0\archives"
$projectName = "AzimuthConsole"

# Создаем папку для архивов
if (!(Test-Path $archivesPath)) {
    New-Item -ItemType Directory -Path $archivesPath -Force | Out-Null
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Publishing and Archiving AzimuthConsole" -ForegroundColor Cyan
Write-Host "  Started at: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$total = $platforms.Count
$current = 0

foreach ($platform in $platforms) {
    $current = $current + 1
    $rid = $platform.rid
    $name = $platform.name
    $ext = $platform.ext
    
    $outputPath = "$basePath\$rid"
    $archiveName = "${projectName}_${name}.zip"
    $archivePath = "$archivesPath\$archiveName"
    
    Write-Host "[$current/$total] Processing $rid..." -ForegroundColor Yellow
    
    # 1. Публикация
    Write-Host "  Publishing..."
    dotnet publish -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $outputPath
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAILED] Publishing FAILED for $rid!" -ForegroundColor Red
        continue
    }
    
    # 2. Удаляем .pdb файлы (если есть)
    Get-ChildItem -Path $outputPath -Filter "*.pdb" | Remove-Item -Force
    
    # 3. Переименовываем исполняемый файл (добавляем версию)
    $exeFile = Get-ChildItem -Path $outputPath -Filter "${projectName}*${ext}" | Select-Object -First 1
    if ($exeFile) {
        # Получаем версию из файла
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exeFile.FullName).FileVersion
        if ($version) {
            $newName = "${projectName}_v${version}_${name}${ext}"
            Rename-Item -Path $exeFile.FullName -NewName $newName -Force
            Write-Host "  Renamed to: $newName" -ForegroundColor Gray
        }
    }
    
    # 4. Создаем ZIP архив
    Write-Host "  Creating archive: $archiveName"
    Compress-Archive -Path "$outputPath\*" -DestinationPath $archivePath -Force -CompressionLevel Optimal
    
    if (Test-Path $archivePath) {
        $size = [math]::Round((Get-Item $archivePath).Length / 1MB, 2)
        Write-Host "  [OK] Archive created: $archiveName ($size MB)" -ForegroundColor Green
    } else {
        Write-Host "  [FAILED] Failed to create archive!" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ALL DONE!" -ForegroundColor Green
Write-Host "  Archives location: $archivesPath" -ForegroundColor Gray
Write-Host "  Finished at: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

# Показываем список созданных архивов
Write-Host ""
Write-Host "Created archives:" -ForegroundColor Yellow
Get-ChildItem -Path $archivesPath -Filter "*.zip" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  [OK] $($_.Name) ($size MB)" -ForegroundColor Green
}