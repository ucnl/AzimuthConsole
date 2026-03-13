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

            // Статусы
            mode: 'Mode',
            interrogation: 'Interrogation',
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
            distance: 'Дист',
            azimuth: 'Азимут',
            signal: 'Сигнал',
            battery: 'Батарея',

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

            // Статусы
            mode: 'Режим',
            interrogation: 'Опрос',
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

        // Кнопки (text и title атрибуты)
        const zoomIn = document.getElementById('zoom-in');
        if (zoomIn) {
            zoomIn.title = this.t('zoomIn');
            // Текст кнопки оставляем "+"
        }

        const zoomOut = document.getElementById('zoom-out');
        if (zoomOut) {
            zoomOut.title = this.t('zoomOut');
            // Текст кнопки оставляем "−"
        }

        const resetView = document.getElementById('reset-view');
        if (resetView) {
            resetView.textContent = this.t('reset');
            resetView.title = this.t('reset');
        }

        const autoScaleBtn = document.getElementById('auto-scale-btn');
        if (autoScaleBtn) {
            autoScaleBtn.title = this.t('autoScale');
            // Текст кнопки оставляем "A"
        }
    }
};

// Инициализация при загрузке
document.addEventListener('DOMContentLoaded', function () {
    i18n.updateStaticUI();
});