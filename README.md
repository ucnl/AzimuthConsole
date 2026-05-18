# 🐙 AzimuthConsole 

_version:_ 
- [**2.1.***](https://github.com/ucnl/AzimuthConsole/releases)

_platforms:_
- [win x64](https://github.com/ucnl/AzimuthConsole/releases/download/2.1/AzimuthConsole_win_x64.zip)
- [linux x64](https://github.com/ucnl/AzimuthConsole/releases/download/2.1/AzimuthConsole_linux_x64.zip)
- [linux arm](https://github.com/ucnl/AzimuthConsole/releases/download/2.1/AzimuthConsole_linux_arm.zip)
- [linux arm64](https://github.com/ucnl/AzimuthConsole/releases/download/2.1/AzimuthConsole_linux_arm64.zip)

## RU

### Описание
Это консольное приложение для работы с гидроакустической системой [Zima2](https://docs.unavlab.com/navigation_and_tracking_systems_ru.html#zima2)
- [AzimuthConsole: Руководство пользователя](https://docs.unavlab.com/documentation/RU/Zima/AzimuthConsole_manual_ru.html)
- [Changelog](https://github.com/ucnl/AzimuthConsole/blob/main/src/changelog.md)

### Сборка
#### Что нужно
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git (если будете клонировать, а не скачивать ZIP)

#### Быстрый старт
1. Скачайте репозиторий: `git clone https://github.com/ucnl/AzimuthConsole.git`, или просто скачайте ZIP-архив с GitHub и распакуйте.
2. Запустите скрипт загрузки зависимостей. 
Откройте PowerShell в папке проекта и выполните: `powershell -File download_nugets.ps1`
Скрипт сам скачает все необходимые NuGet-пакеты в папку `NuGetLocal`.
3. Соберите проект.  
В той же папке выполните: `dotnet build` или просто откройте `AzimuthConsole.sln` в Visual Studio и нажмите **Сборка → Собрать решение**.

#### Если что-то пошло не так
- **PowerShell не даёт запустить скрипт** — выполните перед запуском: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`

#### Зависимости

Проект использует следующие библиотеки (все с открытым исходным кодом):

| Библиотека | Репозиторий |
|-----------|-------------|
| AZMLib | [github.com/ucnl/AZMLib](https://github.com/ucnl/AZMLib) |
| UCNLDrivers | [github.com/ucnl/UCNLDrivers](https://github.com/ucnl/UCNLDrivers) |
| UCNLPhysics | [github.com/ucnl/UCNLPhysics](https://github.com/ucnl/UCNLPhysics) |
| UCNLMan | [github.com/ucnl/UCNLMan](https://github.com/ucnl/UCNLMan) |
| UCNLNav | [github.com/ucnl/UCNLNav](https://github.com/ucnl/UCNLNav) |
| UCNLNMEA | [github.com/ucnl/UCNLNMEA](https://github.com/ucnl/UCNLNMEA) |
| UCNLSalinity | [github.com/ucnl/UCNLSalinity](https://github.com/ucnl/UCNLSalinity) |
| UCNLKML | [github.com/ucnl/UCNLKML](https://github.com/ucnl/UCNLKML) |


## EN

Host application for [Zima2](https://docs.unavlab.com/navigation_and_tracking_systems_en.html#zima) underwater acoustic tracking system.

- [User's manual](https://docs.unavlab.com/documentation/RU/Zima/AzimuthConsole_manual_ru.html)

Please report any translation errors in [issues](https://github.com/ucnl/AzimuthSuite/issues)
