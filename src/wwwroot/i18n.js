// i18n.js — финальная версия
const i18n = {
    currentLang: (navigator.language || navigator.userLanguage || 'en').startsWith('ru') ? 'ru' : 'en',

    translations: {
        en: {
            // Interface
            beacons: 'Beacons',
            log: 'Log',
            noBeacons: 'No beacons detected',
            noLogs: 'No logs',
            connected: 'CONNECTED',
            disconnected: 'DISCONNECTED',
            station: 'STATION',
            antenna: 'ANTENNA',
            control: 'Control',
            expandHint: '▼ tap ▼',

            // Sections
            position: '📍 POSITION',
            orientation: '🧭 ORIENTATION',
            environment: '🌊 ENVIRONMENT',
            status: '📊 STATUS',
            pause: 'PAUSED',

            // Parameters
            x: 'X', y: 'Y', z: 'Z',
            lat: 'Lat', lon: 'Lon',
            heading: 'Heading', course: 'Course', speed: 'Speed',
            pitch: 'Pitch', roll: 'Roll',
            depth: 'Depth', temperature: 'Temp', pressure: 'Pressure',
            rError: 'R Error', dataAge: 'Age',
            distance: 'Dist', azimuth: 'Az', signal: 'Signal',
            battery: 'Battery', elevation: 'Elevation',
            proptime: 'PTime', absaz: 'Abs. Azimuth', absdist: 'Abs. Dist.',
            success: 'Success', requests: 'Requests',

            // Units
            meter: 'm', meterPerSec: 'm/s', degree: '°', celsius: '°C',
            mbar: 'mBar', second: 's',

            // Beacon statuses
            active: 'ACTIVE', warning: 'WARNING', timeout: 'TIMEOUT',

            // Map buttons
            zoomIn: 'Zoom In', zoomOut: 'Zoom Out',
            reset: 'Reset', autoScale: 'Auto Scale',

            // Control buttons
            open: 'Open', close: 'Close',
            interrogate: 'Interrogate', pause: 'Pause',

            // Commands
            sending: 'Sending',
            send: 'Send',
            commandDone: 'Command done',
            error: 'Error',
            wait: 'Wait, command in progress...',

            // Settings modal
            settings: '⚙️ Settings',
            portsTitle: '🔌 Ports',
            outputTitle: '📤 Output',
            positionTitle: '📍 Position',
            transceiverTitle: '📡 Transceiver',
            applyAll: 'Apply All',
            applyRestart: 'Apply & Restart',
            cancel: 'Cancel',
            disableOverride: 'Disable Override',
            serialOutput: 'Serial Output',
            udpOutput: 'UDP Output',
            psimssbOutput: 'PSIMSSB output',
            transceiverHint: 'Changes sent on Apply & Restart.',
            mask: 'Mask',
            salinity: 'Salinity',
            maxDist: 'Max Dist',
            soundSpeed: 'Sound Spd',
            offsets: 'Antenna Offsets',
            locationOverride: 'Location Override',
            overrideHint: 'Empty fields = disable override.',

            // Validation
            invalidNumber: 'invalid number',
            mustBeRange: 'must be',

            // Calibration
            calibration: 'Calibration',
            calStart: 'Start',
            calStop: 'Stop',
            calState: 'State',
            calProgress: 'Progress',
            calAngle: 'Angle',
            calError: 'Error',
            calPts: 'pts',
            confirmCalibration: 'Start calibration?\n\nRequirements:\n• Antenna rotator angle 0° must point to North\n• Responder must be in line of sight\n• Position and heading will be set (LHOV)\n',
            calStateIdle: 'Idle',
            calStateMoving: 'Moving',
            calStateMeasuring: 'Measuring',
            calStateCompleted: 'Completed',
            calStateFailed: 'Failed',

            // Angular Calibration
            angularCalibration: 'Angular Calibration',
            angCalState: 'State',
            angCalPoints: 'Points',
            angCalResult: 'Result',
            angCalCollecting: 'Collecting',
            angCalCompleted: 'Completed',
            angCalIdle: 'Idle',

            saveAsDefault: 'Save as default settings',
            resetDefault: 'Reset default settings',
            saveInitConfirm: 'Save current settings as default?\n\nThey will be automatically loaded on server start.',
            resetInitConfirm: 'Delete init.cmd?\n\nServer will start with defaults on next launch.',
            savingInit: 'Saving init.cmd...',
            initSaved: '✓ Settings saved as init.cmd. They will load on next start.',
            saveFailed: '✗ Save failed',
            deletingInit: 'Deleting init.cmd...',
            initDeleted: '✓ init.cmd deleted. Next start will use factory defaults.',
            deleteFailed: '✗ Delete failed',
            noAddresses: '(no addresses)',
            addresses: 'addresses: ',
            addressesCount: 'addresses: ',
        },
        ru: {
            // Interface
            beacons: 'Маяки',
            log: 'Журнал',
            noBeacons: 'Нет маяков',
            noLogs: 'Нет записей',
            connected: 'ПОДКЛЮЧЕНО',
            disconnected: 'ОТКЛЮЧЕНО',
            station: 'СТАНЦИЯ',
            antenna: 'АНТЕННА',
            control: 'Управление',
            expandHint: '▼ нажмите ▼',

            // Sections
            position: '📍 ПОЗИЦИЯ',
            orientation: '🧭 ОРИЕНТАЦИЯ',
            environment: '🌊 СРЕДА',
            status: '📊 СТАТУС',
            pause: 'ПАУЗА',

            // Parameters
            x: 'X', y: 'Y', z: 'Z',
            lat: 'Широта', lon: 'Долгота',
            heading: 'Курс', course: 'Направление', speed: 'Скорость',
            pitch: 'Дифферент', roll: 'Крен',
            depth: 'Глубина', temperature: 'Темп', pressure: 'Давление',
            rError: 'Ошибка', dataAge: 'Возраст',
            distance: 'Дистанция', azimuth: 'Азимут', signal: 'Сигнал',
            battery: 'Батарея', elevation: 'В. угол',
            proptime: 'Время', absaz: 'Абс. Азимут', absdist: 'Абс. Дист.',
            success: 'Успешно', requests: 'Запросов',

            // Units
            meter: 'м', meterPerSec: 'м/с', degree: '°', celsius: '°C',
            mbar: 'мБар', second: 'с',

            // Beacon statuses
            active: 'АКТИВНО', warning: 'ВНИМАНИЕ', timeout: 'ТАЙМАУТ',

            // Map buttons
            zoomIn: 'Приблизить', zoomOut: 'Отдалить',
            reset: 'Сброс', autoScale: 'Автомасштаб',

            // Control buttons
            open: 'Открыть', close: 'Закрыть',
            interrogate: 'Опрос', pause: 'Пауза',

            // Commands
            sending: 'Отправка',
            send: 'Отправить',
            commandDone: 'Команда выполнена',
            error: 'Ошибка',
            wait: 'Подождите, команда выполняется...',

            // Settings modal
            settings: '⚙️ Настройки',
            portsTitle: '🔌 Порты',
            outputTitle: '📤 Вывод',
            positionTitle: '📍 Позиция',
            transceiverTitle: '📡 Трансивер',
            applyAll: 'Применить всё',
            applyRestart: 'Применить и перезапустить',
            cancel: 'Отмена',
            disableOverride: 'Отключить переопределение',
            serialOutput: 'Посл.',
            udpOutput: 'UDP',
            psimssbOutput: 'Вывод PSIMSSB',
            transceiverHint: 'Изменения отправляются при Apply & Restart.',
            mask: 'Маска',
            salinity: 'Солёность',
            maxDist: 'Макс. дист.',
            soundSpeed: 'Скор. звука',
            offsets: 'Смещения антенны',
            locationOverride: 'Переопределение позиции',
            overrideHint: 'Пустые поля = отключить.',

            // Validation
            invalidNumber: 'неверное число',
            mustBeRange: 'должно быть',

            // Calibration
            calibration: 'Калибровка',
            calStart: 'Старт',
            calStop: 'Стоп',
            calState: 'Состояние',
            calProgress: 'Прогресс',
            calAngle: 'Угол',
            calError: 'Ошибка',
            calPts: 'тчк',
            confirmCalibration: 'Начать калибровку?\n\nТребования:\n• Угол 0° поворотного устройства должен соответствовать направлению на Север\n• Ответчик должен быть в зоне прямой видимости\n• Координаты и курс должны будут заданы (LHOV)\n',
            calStateIdle: 'Ожидание',
            calStateMoving: 'Поворот',
            calStateMeasuring: 'Измерение',
            calStateCompleted: 'Завершено',
            calStateFailed: 'Ошибка',

            // Angular Calibration
            angularCalibration: 'Угловая калибровка',
            angCalState: 'Состояние',
            angCalPoints: 'Точек',
            angCalResult: 'Результат',
            angCalCollecting: 'Сбор данных',
            angCalCompleted: 'Завершено',
            angCalIdle: 'Ожидание',

            saveAsDefault: 'Сохранить как настройки по умолчанию',
            resetDefault: 'Сбросить настройки по умолчанию',
            saveInitConfirm: 'Сохранить текущие настройки как настройки по умолчанию?\n\nОни будут автоматически загружаться при старте сервера.',
            resetInitConfirm: 'Удалить init.cmd?\n\nПри следующем старте сервер запустится с настройками по умолчанию.',
            savingInit: 'Сохраняю init.cmd...',
            initSaved: '✓ Настройки сохранены как init.cmd. Они загрузятся при следующем старте.',
            saveFailed: '✗ Ошибка сохранения',
            deletingInit: 'Удаляю init.cmd...',
            initDeleted: '✓ init.cmd удалён. Следующий старт — с настройками по умолчанию.',
            deleteFailed: '✗ Ошибка удаления',
            noAddresses: '(нет адресов)',
            addresses: 'адреса: ',
            addressesCount: 'адресов: ',
        }
    },

    t: function (key) {
        return this.translations[this.currentLang][key] || key;
    },

    updateStaticUI: function () {
        const t = (key) => this.t(key);

        const beaconsHeader = document.querySelector('#beacons-panel h3');
        if (beaconsHeader) beaconsHeader.innerHTML = `📡 ${t('beacons')}`;

        const logsHeader = document.querySelector('#logs-panel h4');
        if (logsHeader) logsHeader.innerHTML = `
    <span>📋 ${t('log')}</span>
    <span style="font-size:11px; font-weight:normal;">
        <a href="/api/logs/current" download title="Download current log" style="color:#4a90e2; text-decoration:none;">📄</a>
        <a href="/api/logs" download title="Download all logs (ZIP)" style="color:#4a90e2; text-decoration:none; margin-left:6px;">📦</a>
    </span>`;

        const controlHeader = document.querySelector('#control-panel h4');
        if (controlHeader) controlHeader.innerHTML = `🎮 ${t('control')}`;

        const beaconsList = document.getElementById('beacons-list');
        if (beaconsList && beaconsList.textContent.trim() === 'No beacons detected') {
            beaconsList.textContent = t('noBeacons');
        }

        const logsList = document.getElementById('logs-list');
        if (logsList && logsList.children.length === 0) {
            if (!logsList.querySelector('.empty-placeholder')) {
                const placeholder = document.createElement('div');
                placeholder.className = 'empty-placeholder';
                placeholder.style.cssText = 'color:#888; padding:10px; text-align:center;';
                placeholder.textContent = t('noLogs');
                logsList.appendChild(placeholder);
            }
        }

        const zoomIn = document.getElementById('zoom-in');
        if (zoomIn) zoomIn.title = t('zoomIn');
        const zoomOut = document.getElementById('zoom-out');
        if (zoomOut) zoomOut.title = t('zoomOut');
        const resetView = document.getElementById('reset-view');
        if (resetView) resetView.textContent = t('reset');
        const autoScale = document.getElementById('auto-scale-btn');
        if (autoScale) autoScale.title = t('autoScale');

        const sendBtn = document.getElementById('cmd-send-btn');
        if (sendBtn) sendBtn.textContent = t('send');
        const settingsBtn = document.getElementById('settings-btn');
        if (settingsBtn) settingsBtn.title = t('settings');

        const calState = document.getElementById('cal-state');
        if (calState && (!calState.textContent || calState.textContent === '-')) {
            calState.textContent = t('calStateIdle');
        }

        const angCalHeader = document.querySelector('#angular-calibration-panel h4');
        if (angCalHeader) angCalHeader.innerHTML = `🔄 ${t('angularCalibration')}`;
    }
};