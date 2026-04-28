# Примеры командной строки

## 1. Старт приложения со стандартными настройками и стандартным выводом по UDP:
AzimuthConsole SETM,AUTO,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000 SETO,,,255.255.255.255:28128

## 2. Старт приложения со стандартными настройками и стандартным выводом по UDP и источником GNSS с последовательного порта на скорости 38400
AzimuthConsole SETM,AUTO,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000 SETA,AUTO,38400,, SETO,,,255.255.255.255:28128

## 3. Задание координат неподвижных маяков-ответчиков в режиме LBL (декартовы координаты)
SRC3,1,0,0,105,0,65.84,41.83 

- режим 1 (декартовы координаты)
- маяк 1 (0.0, 0.0)
- маяк 2 (105.0, 0.0)
- маяк 3 (65.84, 41.83)

## 4. Задание индивидуальных параметров UDP-соединений
SIOC,1,255.255.255.255:28131 SIOC,2,255.255.255.255:28132

- данные в формате NMEA0183 (GGA, RMC, WTM) будут передаваться на указанный Endpoint (адрес:порт)


## Дополнительно

Чтобы не удалять один или несколько параметров командной строки, их можно закомментировать: для этого в начале параметра
достаточно поставить символ # (решетка), т.к. командный процессор игнорирует параметры, начинающиеся с этого символа.


### Таблица некоторых значений маски адреса

| Адреса маяков-ответчиков | Биты              | Маска (Hex) | Маска (Dec) |
| :---                     | :---              | :---        | :---        |
| 0                        | 00000000 00000001 | 0x0001      | 1           |
| 0 .. 2                   | 00000000 00000111 | 0x0007      | 7           |
| 0 .. 3                   | 00000000 00001111 | 0x000F      | 15          |
| 0 .. 7                   | 00000000 11111111 | 0x00FF      | 255         |
| 0 .. 15                  | 11111111 11111111 | 0xFFFF      | 65535       |
| 0, 2                     | 00000000 00000101 | 0x0005      | 5           |
| 8, 9                     | 00000011 00000000 | 0x0300      | 768         |


# Web

Для доступа из локальной сети:

1. Запустить приложение от имени администратора 
2. Определить ip адрес машины:
   Win: cmd > ipconfig, в строке с IPv4 Address
   Linux: ip a или hostname -I
3. Проверить доступность машины ping <ip-адресс>
3. Открыть в браузере http://<ip-адресс>:8080

Если приложение запущено на Win-машине и нет доступа к серверу в локальной сети, выполнить:
1. Запустить терминал от имени администратора и выполнить:
   New-NetFirewallRule -DisplayName "WebServer 8080" -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow




# 🚀 Гайд по настройке Raspberry Pi с автозапуском .NET приложения

## 📦 Что потребуется

- Raspberry Pi (любая модель)
- MicroSD карта (от 8 ГБ, Class 10)
- Блок питания
- Доступ в интернет (Wi-Fi или Ethernet)
- Скомпилированное self-contained .NET приложение под ARM32 (`linux-arm`)


## 1️. Подготовка системы

### 1.1 Запись образа на SD карту

1. Скачать **Raspberry Pi Imager** с [официального сайта](https://www.raspberrypi.com/software/)
2. Выбрать: **Raspberry Pi OS Lite** — без рабочего стола, легче и быстрее
3. В настройках (шестерёнка) можно сразу задать:
   - Имя хоста: `myapp-server`
   - Включить SSH
   - Логин/пароль
   - Wi-Fi сеть
4. Записать образ на карту

### 1.2 Первый запуск и базовая настройка

```bash
# Подключение по SSH или напрямую и выполнить
sudo raspi-config
```

**Что настроить:**
- `System Options` → `Password` — сменить пароль
- `System Options` → `Hostname` — задать удобное имя в сети
- `System Options` → `Boot / Auto Login` → `Console Autologin`
- `Localisation Options` → `Timezone` — установить часовой пояс
- `Interface Options` → `SSH` — включить если не включено

После выхода — перезагрузка.

### 1.3 Обновление пакетов

```bash
sudo apt update && sudo apt upgrade -y
```


## 2️. Установка приложения

### 2.1 Создание директории

```bash
sudo mkdir -p /opt/ac
sudo chown $USER:$USER /opt/ac
```

### 2.2 Копирование файлов с флешки

```bash
# Посмотреть список устройств
lsblk

# Примонтировать флешку (обычно /dev/sda1)
sudo mkdir -p /mnt/usb
sudo mount /dev/sda1 /mnt/usb

# Скопировать содержимое
cp -r /mnt/usb/* /opt/ac/

# Дать права на выполнение исполняемому файлу
chmod +x /opt/ac/MyApp

# Отмонтировать флешку
sudo umount /mnt/usb
```

### 2.3 Альтернатива: копирование по SCP с компьютера

```bash
# Выполняется НА КОМПЬЮТЕРЕ, не на Raspberry Pi
scp -r ./publish/* pi@192.168.1.XXX:/opt/ac/
```

## 3️. Создание скриптов запуска

### 3.1 Скрипт для ручного запуска `start.sh` (один маяк, без AUX)

```bash
nano /opt/ac/start.sh
```

```bash
#!/bin/bash
cd /opt/ac
./AzimuthConsole "setm,auto,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000" "seto,,,255.255.255.255:28128" 
```

### 3.2. Скрипт для демона `dstart.sh`

```bash
nano /opt/ac/dstart.sh
```

```bash
#!/bin/bash
cd /opt/ac
./AzimuthConsole "daemon" "setm,AUTO,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000" "seto,,,255.255.255.255:28128"
```

### 3.3. Права на выполнение

```bash
chmod +x /opt/ac/start.sh
chmod +x /opt/ac/dstart.sh
```


## 4️. Настройка автозапуска (systemd)

### 4.1. Создание службы

```bash
sudo nano /etc/systemd/system/ac.service
```

```ini
[Unit]
Description=AzimuthConsole
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/ac
ExecStart=/opt/ac/dstart.sh
Restart=on-failure
RestartSec=10
TimeoutStopSec=30
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

### 4.2. Активация службы

```bash
sudo systemctl daemon-reload
sudo systemctl enable ac.service
sudo systemctl start ac.service
```

## 5️. Проверка работы

### 5.1. Статус службы

```bash
sudo systemctl status ac.service
```

Должно быть: `Active: active (running)`

### 5.2. Просмотр логов

```bash
# Последние 50 строк
sudo journalctl -u ac.service -n 50 --no-pager

# В реальном времени
sudo journalctl -u ac.service -f
```

### 5.3. Узнать IP-адрес

```bash
hostname -I
```

### 5.4. Доступ из браузера

С любого устройства в сети: `http://IP_АДРЕС:5000`


## 6️. Управление приложением

| Действие | Команда |
|----------|---------|
| Запустить | `sudo systemctl start ac.service` |
| Остановить | `sudo systemctl stop ac.service` |
| Перезапустить | `sudo systemctl restart ac.service` |
| Статус | `sudo systemctl status ac.service` |
| Включить автозапуск | `sudo systemctl enable ac.service` |
| Отключить автозапуск | `sudo systemctl disable ac.service` |
| Логи | `sudo journalctl -u ac.service -f` |


## 7️. Обновление приложения

```bash
# 1. Остановить службу
sudo systemctl stop ac.service

# 2. Скопировать новые файлы (с флешки или SCP) в /opt/ac/

# 3. Обновить права
chmod +x /opt/ac/MyApp

# 4. Запустить службу
sudo systemctl start ac.service

# 5. Проверить статус
sudo systemctl status ac.service
```


## 8️. Полезные команды

```bash
# Перезагрузка Raspberry Pi
sudo reboot

# Выключение
sudo shutdown -h now

# Информация о системе
uname -a
cat /etc/os-release

# Свободное место
df -h

# Процессы
htop  # если установлен, иначе top
```


## 9️. Возможные проблемы и решения

| Проблема | Решение |
|----------|---------|
| Служба падает с `exited` | Проверить логи: `journalctl -u ac.service -n 50` |
| `failed to determine user credentials` | Исправить `User=root` в `/etc/systemd/system/ac.service` |
| Приложение не запускается вручную | Проверить права: `chmod +x /opt/ac/AzimuthConsole` |
| Не та архитектура | Собрать под `linux-arm`: `dotnet publish -r linux-arm` |


## 📌 Контакты и ссылки

- **IP сервера:** `hostname -I`
- **Директория приложения:** `/opt/ac/`
- **Файл службы:** `/etc/systemd/system/ac.service`


*Гайд актуален на 2026 год для Raspberry Pi OS Lite*



# 🛠️ Пошаговая инструкция для запуска в качестве слубжы Windows

1.  **Открой командную строку от имени администратора**:
    Нажми `Win`, введи `cmd`, кликни правой кнопкой мыши и выбери "Запуск от имени администратора".

2.  **Создай службу (замени пути на свои)**:
    Выполни следующую команду. Здесь `binPath=` — это путь к `.exe` файлу, а `start= auto` означает автоматический запуск при старте системы .
    ```cmd
    sc create "AzimuthConsoleService" binPath= "\"C:\opt\ac\AzimuthConsole.exe\" daemon setm,AUTO,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000 seto,,,255.255.255.255:28128" start= auto
    ```
    *Важно:* Пробел после `=` в параметрах `binPath=` и `start=` обязателен.

3.  **Настрой службу**:
    Чтобы она перезапускалась при сбоях (как `Restart=on-failure` в твоём systemd юните):
    ```cmd
    sc failure "AzimuthConsoleService" reset= 86400 actions= restart/60000/restart/60000/restart/60000
    ```
    *   `reset= 86400` — сброс счётчика ошибок через сутки.
    *   `actions= restart/60000` — перезапуск через 60 секунд после падения (и так до трёх раз).

4.  **Запусти службу**:
    ```cmd
    sc start "AzimuthConsoleService"

## Альтернативный вариант через bat-файл

1. Создай файл `C:\opt\ac\dstart.bat`

```
C:\opt\ac\AzimuthConsole.exe daemon setm,AUTO,,255.255.255.255:28127,255.255.255.255:28129,1,0,1000 seto,,,255.255.255.255:28128
```

2. Команда инициализации сервиса
```
sc create "AzimuthConsoleService" binPath= "C:\opt\ac\dstart.bat" start= auto
```


## 🩺 Управление и проверка

*   **Посмотреть статус службы**:
    ```cmd
    sc query "AzimuthConsoleService"
    ```
*   **Графический интерфейс**: Нажми `Win + R`, введи `services.msc` и нажми Enter. В открывшемся окне найди свою службу "MyAppService", где сможешь запустить, остановить или изменить её свойства вручную .

**Ключевое отличие от Linux**:
Если твоё приложение имеет графический интерфейс (GUI), создавать службу не нужно — Windows Service не предназначены для взаимодействия с рабочим столом. В этом случае достаточно добавить ярлык твоего приложения в папку автозагрузки (`shell:startup`), и оно будет запускаться при входе пользователя в систему.

