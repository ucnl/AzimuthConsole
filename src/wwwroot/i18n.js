// i18n.js - поддержка двух языков (русский/английский)
const i18n = {
    // Текущий язык (определяем из браузера)
    currentLang: (navigator.language || navigator.userLanguage || 'en').startsWith('ru') ? 'ru' : 'en',

    // Словарь переводов
    translations: {
        en: {
            // Интерфейс
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

            // Разделы
            position: '📍 POSITION',
            orientation: '🧭 ORIENTATION',
            environment: '🌊 ENVIRONMENT',
            status: '📊 STATUS',

            // Параметры
            x: 'X',
            y: 'Y',
            z: 'Z',
            lat: 'Lat',
            lon: 'Lon',
            heading: 'Heading',
            course: 'Course',
            speed: 'Speed',
            pitch: 'Pitch',
            roll: 'Roll',
            depth: 'Depth',
            temperature: 'Temp',
            pressure: 'Pressure',
            rError: 'R Error',
            dataAge: 'Age',
            distance: 'Dist',
            azimuth: 'Az',
            signal: 'Signal',
            battery: 'Battery',
            elevation: 'Elevation',
            proptime: 'PTime',
            absaz: 'Abs. Azimuth',
            absdist: 'Abs. Dist.',
            success: 'Success',
            requests: 'Requests',

            // Единицы измерения
            meter: 'm',
            meterPerSec: 'm/s',
            degree: '°',
            celsius: '°C',
            mbar: 'mBar',
            second: 's',

            // Статусы маяков
            active: 'ACTIVE',
            warning: 'WARNING',
            timeout: 'TIMEOUT',

            // Кнопки
            zoomIn: 'Zoom In',
            zoomOut: 'Zoom Out',
            reset: 'Reset',
            autoScale: 'Auto Scale',
            open: 'Open',
            close: 'Close',
            interrogate: 'Interrogate',
            pause: 'Pause',

            // Статусы
            mode: 'Mode',
            interrogation: 'Interrogation',

            // Команды
            sending: 'Sending',
            commandSent: 'Command sent',
            commandDone: 'Command done',
            error: 'Error',
            wait: 'Wait, command in progress...',

            // Калибровка
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
        },
        ru: {
            // Интерфейс
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

            // Разделы
            position: '📍 ПОЗИЦИЯ',
            orientation: '🧭 ОРИЕНТАЦИЯ',
            environment: '🌊 СРЕДА',
            status: '📊 СТАТУС',

            // Параметры
            x: 'X',
            y: 'Y',
            z: 'Z',
            lat: 'Широта',
            lon: 'Долгота',
            heading: 'Курс',
            course: 'Направление',
            speed: 'Скорость',
            pitch: 'Дифферент',
            roll: 'Крен',
            depth: 'Глубина',
            temperature: 'Темп',
            pressure: 'Давление',
            rError: 'Ошибка',
            dataAge: 'Возраст',
            distance: 'Дистанция',
            azimuth: 'Азимут',
            signal: 'Сигнал',
            battery: 'Батарея',
            elevation: 'В. угол',
            proptime: 'Время',
            absaz: 'Абс. Азимут',
            absdist: 'Абс. Дист.',
            success: 'Успешно',
            requests: 'Запросов',

            // Единицы измерения
            meter: 'м',
            meterPerSec: 'м/с',
            degree: '°',
            celsius: '°C',
            mbar: 'мБар',
            second: 'с',

            // Статусы маяков
            active: 'АКТИВНО',
            warning: 'ВНИМАНИЕ',
            timeout: 'ТАЙМАУТ',

            // Кнопки
            zoomIn: 'Приблизить',
            zoomOut: 'Отдалить',
            reset: 'Сброс',
            autoScale: 'Автомасштаб',
            open: 'Открыть',
            close: 'Закрыть',
            interrogate: 'Опрос',
            pause: 'Пауза',

            // Статусы
            mode: 'Режим',
            interrogation: 'Опрос',

            // Команды
            sending: 'Отправка',
            commandSent: 'Команда отправлена',
            commandDone: 'Команда выполнена',
            error: 'Ошибка',
            wait: 'Подождите, команда выполняется...',

            // Калибровка
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
        }
    },

    // Получить перевод
    t: function (key) {
        return this.translations[this.currentLang][key] || key;
    },

    // Обновить статические элементы интерфейса
    updateStaticUI: function () {
        // Заголовки панелей
        const beaconsHeader = document.querySelector('#beacons-panel h3');
        if (beaconsHeader) beaconsHeader.innerHTML = `📡 ${this.t('beacons')}`;

        const logsHeader = document.querySelector('#logs-panel h4');
        if (logsHeader) logsHeader.innerHTML = `📋 ${this.t('log')}`;

        const controlHeader = document.querySelector('#control-panel h4');
        if (controlHeader) controlHeader.innerHTML = `🎮 ${this.t('control')}`;

        // Кнопки (text и title атрибуты)
        const zoomIn = document.getElementById('zoom-in');
        if (zoomIn) {
            zoomIn.title = this.t('zoomIn');
        }

        const zoomOut = document.getElementById('zoom-out');
        if (zoomOut) {
            zoomOut.title = this.t('zoomOut');
        }

        const resetView = document.getElementById('reset-view');
        if (resetView) {
            resetView.textContent = this.t('reset');
            resetView.title = this.t('reset');
        }

        const autoScaleBtn = document.getElementById('auto-scale-btn');
        if (autoScaleBtn) {
            autoScaleBtn.title = this.t('autoScale');
        }

        // Кнопки управления (изначально на русском, но обновятся при смене языка)
        const connText = document.getElementById('connection-btn-text');
        const interText = document.getElementById('interrogation-btn-text');
        if (connText) {
            // Не трогаем, они обновляются в updateControlButtons()
        }
    }
};

// Инициализация при загрузке
document.addEventListener('DOMContentLoaded', function () {
    i18n.updateStaticUI();
});