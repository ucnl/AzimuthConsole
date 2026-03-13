// Состояние
let canvas, ctx;
let scale = 100;
let offsetX = 0, offsetY = 0;
let isDragging = false;
let lastMouseX, lastMouseY;
let localDevice = null;
let beacons = [];
let logs = [];
let systemInfo = null;
const MAX_LOGS = 8;
let ws = null;
let reconnectTimer = null;
let autoScaleEnabled = true;
let currentMode = 'unknown';

// Таймер для обновления возраста данных
let ageUpdateTimer = null;
const AGE_UPDATE_INTERVAL = 1000; // 1 секунда

// Для изменения размера панелей
let isResizing = false;
let startX = 0;
let startWidth = 0;
let containerWidth = 0;

// Глобальная ссылка на i18n
window.i18n = i18n;

document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded');
    init();
});

function init() {
    canvas = document.getElementById('map-canvas');
    if (!canvas) {
        console.error('Canvas not found!');
        return;
    }

    ctx = canvas.getContext('2d');

    // Инициализация изменения размера панелей
    initResizeHandler();

    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    initControls();
    i18n.updateStaticUI();
    initMouseHandlers();
    connectWebSocket();
    loadInitialData();

    // Запускаем таймер обновления возраста
    startAgeUpdateTimer();

    drawMap();
}

function initResizeHandler() {
    const resizer = document.getElementById('sidebar-resizer');
    const container = document.getElementById('container');
    const sidebar = document.getElementById('sidebar');
    const mapContainer = document.getElementById('map-container');

    if (!resizer || !container || !sidebar || !mapContainer) return;

    // Устанавливаем начальные стили
    container.style.display = 'flex';
    container.style.flexDirection = 'row';
    container.style.width = '100%';
    container.style.height = '100vh';

    mapContainer.style.flex = '1 1 auto';
    mapContainer.style.width = 'auto';

    resizer.style.flex = '0 0 auto';
    resizer.style.width = '5px';
    resizer.style.cursor = 'col-resize';
    resizer.style.backgroundColor = '#4a90e2';

    sidebar.style.flex = '0 0 auto';
    sidebar.style.width = '400px'; // начальная ширина

    resizer.addEventListener('mousedown', function (e) {
        e.preventDefault();
        isResizing = true;
        startX = e.clientX;
        startWidth = sidebar.offsetWidth;
        containerWidth = container.offsetWidth;

        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';

        // Временно отключаем transition
        sidebar.style.transition = 'none';
        mapContainer.style.transition = 'none';
    });

    document.addEventListener('mousemove', function (e) {
        if (!isResizing) return;

        e.preventDefault();

        const deltaX = e.clientX - startX;
        let newWidth = startWidth - deltaX;

        // Ограничения
        const minWidth = 200;
        const maxWidth = containerWidth * 0.8;

        if (newWidth < minWidth) {
            newWidth = minWidth;
        } else if (newWidth > maxWidth) {
            newWidth = maxWidth;
        }

        // Обновляем ширину боковой панели
        sidebar.style.width = newWidth + 'px';

        // Перерисовываем canvas
        resizeCanvas();
    });

    document.addEventListener('mouseup', function () {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            // Возвращаем transition
            sidebar.style.transition = '';
            mapContainer.style.transition = '';
        }
    });

    // Защита от потери разделителя при изменении размера окна
    window.addEventListener('resize', function () {
        if (!container || !sidebar) return;

        containerWidth = container.offsetWidth;
        const currentWidth = sidebar.offsetWidth;
        const maxWidth = containerWidth * 0.8;

        if (currentWidth > maxWidth) {
            sidebar.style.width = maxWidth + 'px';
            resizeCanvas();
        }
    });
}

function startAgeUpdateTimer() {
    if (ageUpdateTimer) clearInterval(ageUpdateTimer);

    ageUpdateTimer = setInterval(() => {
        // Увеличиваем возраст данных для всех маяков
        if (beacons && beacons.length > 0) {
            let needsRedraw = false;

            beacons.forEach(beacon => {
                if (beacon.DataAge !== undefined && beacon.DataAge !== null) {
                    beacon.DataAge += 1; // Увеличиваем на 1 секунду
                    needsRedraw = true;
                } else {
                    beacon.DataAge = 1; // Инициализируем возраст
                    needsRedraw = true;
                }
            });

            if (needsRedraw) {
                updateBeaconsList(beacons);

                // Перерисовываем карту для обновления цвета возраста
                if (!autoScaleEnabled) {
                    drawMap();
                }
            }
        }

        // Обновляем возраст локального устройства
        if (localDevice) {
            if (localDevice.DataAge !== undefined) {
                localDevice.DataAge += 1;
            } else {
                localDevice.DataAge = 1;
            }
        }

        // Всегда обновляем системную информацию (там отображается возраст)
        updateSystemInfo();

    }, AGE_UPDATE_INTERVAL);
}

function stopAgeUpdateTimer() {
    if (ageUpdateTimer) {
        clearInterval(ageUpdateTimer);
        ageUpdateTimer = null;
    }
}

function resizeCanvas() {
    if (!canvas) return;
    const container = document.getElementById('map-container');
    if (!container) return;

    // Убеждаемся, что container имеет правильные размеры
    const computedStyle = window.getComputedStyle(container);
    if (computedStyle.display === 'none') return;

    canvas.width = container.clientWidth;
    canvas.height = container.clientHeight;

    if (canvas.width > 0 && canvas.height > 0) {
        if (autoScaleEnabled) autoScale(); else drawMap();
    }
}

function initControls() {
    document.getElementById('zoom-in').onclick = (e) => {
        e.preventDefault();
        autoScaleEnabled = false;
        document.getElementById('auto-scale-btn').classList.add('off');
        zoomIn();
    };

    document.getElementById('zoom-out').onclick = (e) => {
        e.preventDefault();
        autoScaleEnabled = false;
        document.getElementById('auto-scale-btn').classList.add('off');
        zoomOut();
    };

    document.getElementById('reset-view').onclick = (e) => {
        e.preventDefault();
        autoScaleEnabled = false;
        document.getElementById('auto-scale-btn').classList.add('off');
        resetView();
    };

    document.getElementById('auto-scale-btn').onclick = (e) => {
        e.preventDefault();
        autoScaleEnabled = !autoScaleEnabled;
        const btn = document.getElementById('auto-scale-btn');
        if (autoScaleEnabled) {
            btn.classList.remove('off');
            autoScale();
        } else {
            btn.classList.add('off');
        }
    };
}

function connectWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws`;

    try {
        ws = new WebSocket(wsUrl);

        ws.onopen = function () {
            updateConnectionStatus(true);
            if (reconnectTimer) clearTimeout(reconnectTimer);

            // Перезапускаем таймер возраста при подключении
            stopAgeUpdateTimer();
            startAgeUpdateTimer();
        };

        ws.onmessage = function (event) {
            try {
                updateData(JSON.parse(event.data));
            } catch (e) {
                console.error('Error parsing message:', e);
            }
        };

        ws.onclose = function () {
            updateConnectionStatus(false);
            reconnectTimer = setTimeout(connectWebSocket, 3000);

            // Таймер продолжает работать, показывая устаревание данных
        };

        ws.onerror = function (error) {
            console.error('WebSocket error:', error);
        };

    } catch (e) {
        console.error('Error creating WebSocket:', e);
        reconnectTimer = setTimeout(connectWebSocket, 3000);
    }
}

function updateConnectionStatus(connected) {
    const statusEl = document.getElementById('connection-status');
    if (!statusEl) return;

    if (connected) {
        statusEl.textContent = `● ${i18n.t('connected')}`;
        statusEl.classList.add('connected');
    } else {
        statusEl.textContent = `● ${i18n.t('disconnected')}`;
        statusEl.classList.remove('connected');
    }
}

async function loadInitialData() {
    try {
        const response = await fetch('/api/data');
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        updateData(await response.json());
        setTimeout(() => drawMap(), 100);
    } catch (e) {
        console.error('Error loading initial data:', e);
    }
}

function updateData(data) {
    if (!data) return;

    // Сбрасываем возраст при получении новых данных
    if (data.Mode) currentMode = data.Mode;

    if (data.LocalDevice) {
        localDevice = data.LocalDevice;
        localDevice.DataAge = 0; // Сбрасываем возраст
    }

    if (data.SystemInfo) {
        systemInfo = data.SystemInfo;
    }

    if (data.Beacons && Array.isArray(data.Beacons)) {
        // Обновляем маяки, сбрасывая возраст для полученных
        beacons = data.Beacons.map(beacon => {
            beacon.DataAge = 0; // Сбрасываем возраст
            return beacon;
        });

        updateBeaconsList(beacons);
    } else {
        beacons = [];
        updateBeaconsList([]);
    }

    updateSystemInfo();

    if (data.RecentLogs && Array.isArray(data.RecentLogs)) {
        logs = [...data.RecentLogs, ...logs].slice(0, MAX_LOGS);
        updateLogs(logs);
    }

    if (autoScaleEnabled) autoScale(); else drawMap();
}

function updateBeaconsList(beacons) {
    const list = document.getElementById('beacons-list');
    if (!list) return;

    if (!beacons || beacons.length === 0) {
        list.innerHTML = `<div style="color: #999; text-align: center; padding: 20px;">${i18n.t('noBeacons')}</div>`;
        return;
    }

    let html = '';

    beacons.forEach(b => {
        const address = b.Address || '?';
        let details = [];

        // Определяем статус на основе возраста данных
        let status = 'active';
        let statusClass = 'status-active';
        let statusText = i18n.t('active');

        if (b.DataAge !== undefined && b.DataAge !== null) {
            if (b.DataAge > 10) {
                status = 'timeout';
                statusClass = 'status-timeout';
                statusText = i18n.t('timeout');
            } else if (b.DataAge > 5) {
                status = 'warning';
                statusClass = 'status-warning';
                statusText = i18n.t('warning');
            }
        }

        function addDetail(label, value, unit = '') {
            if (value !== null && value !== undefined && !isNaN(value)) {
                details.push(`<div class="beacon-detail"><span class="detail-label">${label}:</span> <span class="detail-value">${value}${unit}</span></div>`);
            }
        }

        // Основные координаты
        if (b.AbsoluteDistance && b.AbsoluteAzimuth) {
            addDetail('ABS Dist', b.AbsoluteDistance.toFixed(1), i18n.t('meter'));
            addDetail('ABS Az', b.AbsoluteAzimuth.toFixed(1), i18n.t('degree'));
        }

        addDetail(i18n.t('x'), b.X?.toFixed(2), i18n.t('meter'));
        addDetail(i18n.t('y'), b.Y?.toFixed(2), i18n.t('meter'));
        addDetail(i18n.t('z'), b.Z?.toFixed(2), i18n.t('meter'));
        addDetail(i18n.t('depth'), b.Depth?.toFixed(1), i18n.t('meter'));
        addDetail(i18n.t('distance'), b.Distance?.toFixed(1), i18n.t('meter'));
        addDetail(i18n.t('azimuth'), b.Azimuth?.toFixed(1), i18n.t('degree'));
        addDetail(i18n.t('signal'), b.SignalLevel?.toFixed(1), ' dB');
        addDetail(i18n.t('battery'), b.Battery?.toFixed(2), ' V');
        addDetail(i18n.t('temperature'), b.Temperature?.toFixed(1), i18n.t('celsius'));

        // Возраст данных
        if (b.DataAge !== undefined && b.DataAge !== null) {
            let ageColor = '#28a745';
            if (b.DataAge > 5) ageColor = '#ffc107';
            if (b.DataAge > 10) ageColor = '#dc3545';

            details.push(`<div class="beacon-detail"><span class="detail-label">${i18n.t('dataAge')}:</span> <span class="detail-value" style="color: ${ageColor}">${b.DataAge.toFixed(1)}${i18n.t('second')}</span></div>`);
        }

        if (b.Message) {
            details.push(`<div class="beacon-detail"><span class="detail-label">Msg:</span> <span class="detail-value">${b.Message}</span></div>`);
        }

        html += `
            <div class="beacon-item ${status}">
                <div class="beacon-header">
                    <span class="beacon-address">#${address}</span>
                    <span class="beacon-status ${statusClass}">${statusText}</span>
                </div>
                <div class="beacon-details">
                    ${details.join('')}
                </div>
            </div>
        `;
    });

    list.innerHTML = html;
}

function updateLogs(logs) {
    const logsDiv = document.getElementById('logs-list');
    if (!logsDiv) return;

    if (!logs || logs.length === 0) {
        logsDiv.innerHTML = `<div style="color: #666; text-align: center; padding: 10px;">${i18n.t('noLogs')}</div>`;
        return;
    }

    let html = '';
    logs.forEach(log => {
        const type = log.Type || 'info';
        const timestamp = log.Timestamp || new Date().toLocaleTimeString();
        const message = log.Message || '';
        html += `<div class="log-entry log-${type}">[${timestamp}] ${message}</div>`;
    });

    logsDiv.innerHTML = html;

    setTimeout(() => {
        logsDiv.scrollTop = 0;
    }, 10);
}

function updateSystemInfo() {
    const panel = document.getElementById('system-info');
    if (!panel) return;

    let html = '';

    // Заголовок с устройством
    if (systemInfo) {
        html += `<div style="margin-bottom: 8px;"><strong>${systemInfo.DeviceType || 'Unknown'}</strong> ${systemInfo.SerialNumber || ''}</div>`;
        html += `<div style="margin-bottom: 8px; font-size: 11px;">${systemInfo.Version || '- - -'} | ${i18n.t('mode')}: ${currentMode.toUpperCase()}</div>`;
        html += `<div style="margin-bottom: 8px; display: flex; gap: 10px;">`;
        
        // Статус соединения
        if (systemInfo.ConnectionActive) {
            html += `<span style="color: #28a745;">● ${i18n.t('connected')}</span>`;
        } else {
            html += `<span style="color: #dc3545;">○ ${i18n.t('disconnected')}</span>`;
        }

        // Статус опроса
        if (systemInfo.InterrogationActive) {
            html += `<span style="color: #28a745;">● ${i18n.t('interrogation')}</span>`;
        } else {
            html += `<span style="color: #ffc107;">○ ${i18n.t('interrogation')}</span>`;
        }


        html += `</div>`;
    } else {
        html += `<div style="margin-bottom: 8px;"><strong>Unknown</strong></div>`;
        html += `<div style="margin-bottom: 8px; font-size: 11px;">${i18n.t('mode')}: ${currentMode.toUpperCase()}</div>`;
    }

    if (!localDevice) {
        panel.innerHTML = html;
        return;
    }

    // Функция для создания строки с выравниванием
    function addRow(label, value, unit = '', color = '#ffffff') {
        if (value !== undefined && value !== null && !isNaN(value)) {
            return `<div style="display: flex; justify-content: space-between; margin: 2px 0;">
                <span style="color: #aaa;">${label}:</span>
                <span style="color: ${color};">${value}${unit}</span>
            </div>`;
        }
        return '';
    }

    // ПОЗИЦИЯ
    if (localDevice.X !== undefined || localDevice.Y !== undefined ||
        localDevice.Latitude !== undefined || localDevice.Longitude !== undefined) {
        html += `<div style="margin-top: 8px; margin-bottom: 4px; color: #4a90e2; font-weight: bold; border-bottom: 1px solid #4a90e2;">${i18n.t('position')}</div>`;

        if (localDevice.X !== undefined && localDevice.X !== null && !isNaN(localDevice.X) &&
            localDevice.Y !== undefined && localDevice.Y !== null && !isNaN(localDevice.Y)) {
            html += addRow(i18n.t('x'), localDevice.X.toFixed(2), i18n.t('meter'));
            html += addRow(i18n.t('y'), localDevice.Y.toFixed(2), i18n.t('meter'));
        }

        if (localDevice.Latitude !== undefined && localDevice.Latitude !== null && !isNaN(localDevice.Latitude) &&
            localDevice.Longitude !== undefined && localDevice.Longitude !== null && !isNaN(localDevice.Longitude)) {
            html += addRow(i18n.t('lat'), localDevice.Latitude.toFixed(6), i18n.t('degree'));
            html += addRow(i18n.t('lon'), localDevice.Longitude.toFixed(6), i18n.t('degree'));
        }

        if (localDevice.Z !== undefined && localDevice.Z !== null && !isNaN(localDevice.Z)) {
            html += addRow(i18n.t('z'), localDevice.Z.toFixed(2), i18n.t('meter'));
        }
    }

    // ОРИЕНТАЦИЯ И ДВИЖЕНИЕ
    if (localDevice.Heading !== undefined || localDevice.Course !== undefined ||
        localDevice.Speed !== undefined || localDevice.Pitch !== undefined || localDevice.Roll !== undefined) {
        html += `<div style="margin-top: 8px; margin-bottom: 4px; color: #4a90e2; font-weight: bold; border-bottom: 1px solid #4a90e2;">${i18n.t('orientation')}</div>`;

        if (localDevice.Heading !== undefined && localDevice.Heading !== null && !isNaN(localDevice.Heading)) {
            html += addRow(i18n.t('heading'), localDevice.Heading.toFixed(1), i18n.t('degree'), '#ff4444');
        }

        if (localDevice.Course !== undefined && localDevice.Course !== null && !isNaN(localDevice.Course)) {
            html += addRow(i18n.t('course'), localDevice.Course.toFixed(1), i18n.t('degree'), '#44ff44');
        }

        if (localDevice.Speed !== undefined && localDevice.Speed !== null && !isNaN(localDevice.Speed)) {
            html += addRow(i18n.t('speed'), localDevice.Speed.toFixed(2), i18n.t('meterPerSec'));
        }

        if (localDevice.Pitch !== undefined && localDevice.Pitch !== null && !isNaN(localDevice.Pitch)) {
            html += addRow(i18n.t('pitch'), localDevice.Pitch.toFixed(1), i18n.t('degree'));
        }

        if (localDevice.Roll !== undefined && localDevice.Roll !== null && !isNaN(localDevice.Roll)) {
            html += addRow(i18n.t('roll'), localDevice.Roll.toFixed(1), i18n.t('degree'));
        }
    }

    // ОКРУЖАЮЩАЯ СРЕДА
    if (localDevice.Depth !== undefined || localDevice.Temperature !== undefined ||
        localDevice.Pressure !== undefined) {
        html += `<div style="margin-top: 8px; margin-bottom: 4px; color: #4a90e2; font-weight: bold; border-bottom: 1px solid #4a90e2;">${i18n.t('environment')}</div>`;

        if (localDevice.Depth !== undefined && localDevice.Depth !== null && !isNaN(localDevice.Depth)) {
            html += addRow(i18n.t('depth'), localDevice.Depth.toFixed(1), i18n.t('meter'));
        }

        if (localDevice.Temperature !== undefined && localDevice.Temperature !== null && !isNaN(localDevice.Temperature)) {
            html += addRow(i18n.t('temperature'), localDevice.Temperature.toFixed(1), i18n.t('celsius'));
        }

        if (localDevice.Pressure !== undefined && localDevice.Pressure !== null && !isNaN(localDevice.Pressure)) {
            html += addRow(i18n.t('pressure'), localDevice.Pressure.toFixed(1), i18n.t('mbar'));
        }
    }

    // СТАТУС И КАЧЕСТВО
    if (localDevice.RError !== undefined || localDevice.DataAge !== undefined) {
        html += `<div style="margin-top: 8px; margin-bottom: 4px; color: #4a90e2; font-weight: bold; border-bottom: 1px solid #4a90e2;">${i18n.t('status')}</div>`;

        if (localDevice.RError !== undefined && localDevice.RError !== null && !isNaN(localDevice.RError)) {
            let color = localDevice.RError < 1 ? '#28a745' : (localDevice.RError < 3 ? '#ffc107' : '#dc3545');
            html += addRow(i18n.t('rError'), localDevice.RError.toFixed(2), i18n.t('meter'), color);
        }

        if (localDevice.DataAge !== undefined) {
            let ageColor = '#28a745';
            if (localDevice.DataAge > 5) ageColor = '#ffc107';
            if (localDevice.DataAge > 10) ageColor = '#dc3545';
            html += addRow(i18n.t('dataAge'), localDevice.DataAge.toFixed(1), i18n.t('second'), ageColor);
        }
    }

    panel.innerHTML = html;
}

function drawMap() {
    if (!canvas || !ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    drawGrid();
    drawBeacons();
    drawLocalDevice();
    drawScaleBar();
}

function drawGrid() {
    const gridSize = 50;
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
    ctx.lineWidth = 1;

    const startX = ((offsetX % gridSize) + gridSize) % gridSize;
    for (let x = startX; x < canvas.width; x += gridSize) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();
    }

    const startY = ((offsetY % gridSize) + gridSize) % gridSize;
    for (let y = startY; y < canvas.height; y += gridSize) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();
    }

    ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(offsetX, 0);
    ctx.lineTo(offsetX, canvas.height);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, offsetY);
    ctx.lineTo(canvas.width, offsetY);
    ctx.stroke();
}

function drawBeacons() {
    if (!beacons || beacons.length === 0) return;

    beacons.forEach((beacon, index) => {
        try {
            let x, y;
            let coordType = 'none';

            // ПРИОРИТЕТ 1: Абсолютные координаты (уже пересчитаны из географических)
            if (beacon.AbsoluteDistance && beacon.AbsoluteAzimuth !== undefined &&
                !isNaN(beacon.AbsoluteDistance) && !isNaN(beacon.AbsoluteAzimuth)) {

                let angle = beacon.AbsoluteAzimuth * Math.PI / 180;
                x = offsetX + beacon.AbsoluteDistance * Math.sin(angle) * scale;
                y = offsetY - beacon.AbsoluteDistance * Math.cos(angle) * scale;
                coordType = 'absolute';
            }
            // ПРИОРИТЕТ 2: Декартовы координаты
            else if (beacon.X !== undefined && beacon.X !== null &&
                beacon.Y !== undefined && beacon.Y !== null &&
                !isNaN(beacon.X) && !isNaN(beacon.Y)) {

                x = beacon.X * scale + offsetX;
                y = -beacon.Y * scale + offsetY;
                coordType = 'cartesian';
            }
            // ПРИОРИТЕТ 3: Полярные координаты
            else if (beacon.Distance && beacon.Azimuth !== undefined &&
                beacon.Azimuth !== null && !isNaN(beacon.Distance) && !isNaN(beacon.Azimuth)) {

                let heading = (localDevice?.Heading && !isNaN(localDevice.Heading) && localDevice.HasHeading)
                    ? localDevice.Heading : 0;
                let angle = (beacon.Azimuth - heading) * Math.PI / 180;
                x = offsetX + beacon.Distance * Math.sin(angle) * scale;
                y = offsetY - beacon.Distance * Math.cos(angle) * scale;
                coordType = 'polar';
            }
            else {
                return;
            }

            if (isNaN(x) || isNaN(y) || !isFinite(x) || !isFinite(y)) return;

            // Проверка видимости
            const margin = 100;
            if (x < -margin || x > canvas.width + margin ||
                y < -margin || y > canvas.height + margin) return;

            // Рисуем маяк
            ctx.beginPath();
            ctx.arc(x, y, 18, 0, 2 * Math.PI);

            // Цвет зависит от возраста и типа координат
            let fillStyle;
            const age = beacon.DataAge || 0;

            if (coordType === 'absolute') {
                // Абсолютные координаты - золотой оттенок с учетом возраста
                const opacity = Math.max(0.3, 1 - age / 20);
                fillStyle = `rgba(255, 215, 0, ${opacity})`;
            } else {
                const hue = ((beacon.Address || index + 1) * 30) % 360;
                const opacity = Math.max(0.3, 1 - age / 20);
                fillStyle = `hsla(${hue}, 80%, 60%, ${opacity})`;
            }

            ctx.fillStyle = fillStyle;
            ctx.fill();
            ctx.strokeStyle = 'white';
            ctx.lineWidth = 3;
            ctx.stroke();

            // Адрес маяка
            ctx.fillStyle = 'white';
            ctx.font = 'bold 14px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(beacon.Address?.toString() || (index + 1).toString(), x, y);

            // Подпись с типом координат
            ctx.font = '10px Arial';
            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';

            let label = '';
            if (coordType === 'absolute') {
                label = `ABS ${beacon.AbsoluteDistance.toFixed(0)}m`;
            } else if (coordType === 'cartesian') {
                label = `${beacon.X?.toFixed(0) || '?'}, ${beacon.Y?.toFixed(0) || '?'}`;
            } else if (coordType === 'polar') {
                label = `${beacon.Distance.toFixed(0)}m`;
            }

            if (label) {
                ctx.fillText(label, x, y + 30);
            }

            // Если возраст большой, показываем предупреждение
            if (age > 8) {
                ctx.font = '12px Arial';
                ctx.fillStyle = '#ff4444';
                ctx.fillText('!', x + 15, y - 20);
            }

        } catch (e) {
            console.error('Error drawing beacon:', e);
        }
    });
}

function drawLocalDevice() {
    if (!localDevice) return;

    // Определяем позицию устройства
    let deviceX = offsetX;
    let deviceY = offsetY;

    // В LBL режиме устройство может иметь свои координаты
    if (currentMode === 'lbl' && localDevice.X !== undefined && localDevice.X !== null &&
        localDevice.Y !== undefined && localDevice.Y !== null) {
        deviceX = localDevice.X * scale + offsetX;
        deviceY = -localDevice.Y * scale + offsetY;
    }

    const x = deviceX;
    const y = deviceY;

    // Рисуем устройство (ромб)
    ctx.beginPath();
    ctx.moveTo(x, y - 20);
    ctx.lineTo(x + 20, y);
    ctx.lineTo(x, y + 20);
    ctx.lineTo(x - 20, y);
    ctx.closePath();

    // Цвет в зависимости от режима и возраста данных
    let baseColor;
    if (currentMode === 'usbl') {
        baseColor = '0, 255, 255'; // Голубой для USBL
    } else {
        baseColor = '74, 144, 226'; // Синий для LBL
    }

    // Учитываем возраст данных для прозрачности
    const age = localDevice.DataAge || 0;
    const opacity = Math.max(0.3, 1 - age / 20);

    ctx.fillStyle = `rgba(${baseColor}, ${opacity})`;
    ctx.fill();
    ctx.strokeStyle = 'white';
    ctx.lineWidth = 3;
    ctx.stroke();

    // Рисуем направление (Heading) - красная стрелка
    if (localDevice.Heading !== null && !isNaN(localDevice.Heading) && localDevice.HasHeading) {
        try {
            const angle = localDevice.Heading * Math.PI / 180;
            const arrowX = x + 40 * Math.sin(angle);
            const arrowY = y - 40 * Math.cos(angle);

            ctx.beginPath();
            ctx.moveTo(x, y);
            ctx.lineTo(arrowX, arrowY);
            ctx.strokeStyle = '#ff4444';
            ctx.lineWidth = 3;
            ctx.stroke();

            ctx.beginPath();
            ctx.arc(arrowX, arrowY, 5, 0, 2 * Math.PI);
            ctx.fillStyle = '#ff4444';
            ctx.fill();

            // Подпись Heading
            ctx.font = '10px Arial';
            ctx.fillStyle = '#ff4444';
            ctx.fillText('H', arrowX + 10, arrowY - 10);

        } catch (e) {
            console.error('Error drawing heading:', e);
        }
    }

    // Рисуем курс (Course) - зеленая стрелка
    if (localDevice.Course !== undefined && localDevice.Course !== null && !isNaN(localDevice.Course)) {
        try {
            const angle = localDevice.Course * Math.PI / 180;
            const arrowX = x + 35 * Math.sin(angle);
            const arrowY = y - 35 * Math.cos(angle);

            ctx.beginPath();
            ctx.moveTo(x, y);
            ctx.lineTo(arrowX, arrowY);
            ctx.strokeStyle = '#44ff44';
            ctx.lineWidth = 2;
            ctx.stroke();

            ctx.beginPath();
            ctx.arc(arrowX, arrowY, 4, 0, 2 * Math.PI);
            ctx.fillStyle = '#44ff44';
            ctx.fill();

            // Подпись Course
            ctx.font = '10px Arial';
            ctx.fillStyle = '#44ff44';
            ctx.fillText('C', arrowX + 8, arrowY - 8);

        } catch (e) {
            console.error('Error drawing course:', e);
        }
    }

    // Скорость если есть
    if (localDevice.Speed !== undefined && localDevice.Speed !== null && !isNaN(localDevice.Speed)) {
        ctx.font = '10px Arial';
        ctx.fillStyle = 'white';
        ctx.fillText(`${localDevice.Speed.toFixed(1)} ${i18n.t('meterPerSec')}`, x + 25, y - 25);
    }

    // Глубина если есть
    if (localDevice.Depth !== undefined && localDevice.Depth !== null && !isNaN(localDevice.Depth)) {
        ctx.font = '10px Arial';
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.fillText(`${i18n.t('depth')}: ${localDevice.Depth.toFixed(1)}${i18n.t('meter')}`, x + 25, y - 35);
    }

    // Подпись устройства
    ctx.fillStyle = 'white';
    ctx.font = 'bold 12px Arial';

    if (currentMode === 'usbl') {
        ctx.fillText(i18n.t('antenna'), x - 35, y - 35);
    } else {
        ctx.fillText(i18n.t('station'), x - 30, y - 35);
    }

    // Координаты если есть
    if (localDevice.X !== undefined && localDevice.X !== null &&
        localDevice.Y !== undefined && localDevice.Y !== null) {
        ctx.font = '9px Arial';
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.fillText(`${i18n.t('x')}:${localDevice.X.toFixed(1)} ${i18n.t('y')}:${localDevice.Y.toFixed(1)}`, x - 40, y - 50);
    }
}

function drawScaleBar() {
    if (!canvas || !ctx) return;

    const padding = 30; // отступ от правого края canvas
    const splitterWidth = 5; // ширина сплиттера
    const rightOffset = 20; // дополнительный отступ от сплиттера

    // Получаем ширину canvas
    const canvasWidth = canvas.width;

    // Сначала определяем длину линейки в метрах и пикселях
    const rawMeters = 100 / scale; // сколько метров в 100 пикселях

    // Выбираем красивую длину для отображения
    let displayMeters, displayWidth;

    if (rawMeters < 5) {
        displayMeters = 5;
        displayWidth = 5 * scale;
    } else if (rawMeters < 10) {
        displayMeters = 10;
        displayWidth = 10 * scale;
    } else if (rawMeters < 25) {
        displayMeters = 25;
        displayWidth = 25 * scale;
    } else if (rawMeters < 50) {
        displayMeters = 50;
        displayWidth = 50 * scale;
    } else if (rawMeters < 100) {
        displayMeters = 100;
        displayWidth = 100 * scale;
    } else if (rawMeters < 250) {
        displayMeters = 250;
        displayWidth = 250 * scale;
    } else if (rawMeters < 500) {
        displayMeters = 500;
        displayWidth = 500 * scale;
    } else if (rawMeters < 1000) {
        displayMeters = 1000;
        displayWidth = 1000 * scale;
    } else {
        displayMeters = Math.round(rawMeters / 1000) * 1000;
        displayWidth = displayMeters * scale;
    }

    // Ограничиваем максимальную ширину, чтобы не вылезать за левый край
    const maxAllowedWidth = canvasWidth - padding * 2 - splitterWidth - rightOffset;
    if (displayWidth > maxAllowedWidth) {
        displayWidth = maxAllowedWidth;
        displayMeters = Math.round(displayWidth / scale);
    }

    // Теперь вычисляем позицию - от правого края с отступами
    const barX = canvasWidth - padding - displayWidth - splitterWidth - rightOffset;
    const barY = canvas.height - padding;

    // Рисуем линейку
    ctx.beginPath();
    ctx.moveTo(barX, barY);
    ctx.lineTo(barX + displayWidth, barY);
    ctx.strokeStyle = 'white';
    ctx.lineWidth = 3;
    ctx.stroke();

    // Рисуем вертикальные ограничители
    ctx.beginPath();
    ctx.moveTo(barX, barY - 8);
    ctx.lineTo(barX, barY + 8);
    ctx.strokeStyle = 'white';
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(barX + displayWidth, barY - 8);
    ctx.lineTo(barX + displayWidth, barY + 8);
    ctx.stroke();

    // Подпись
    ctx.font = 'bold 12px Arial';
    ctx.fillStyle = 'white';
    ctx.textAlign = 'center';
    ctx.shadowColor = 'black';
    ctx.shadowBlur = 4;

    let label;
    if (displayMeters >= 1000) {
        label = `${(displayMeters / 1000).toFixed(1)} km`;
    } else {
        label = `${Math.round(displayMeters)} m`;
    }

    ctx.fillText(label, barX + displayWidth / 2, barY - 15);
    ctx.shadowBlur = 0;
}

function autoScale() {
    if (!autoScaleEnabled || !beacons || beacons.length === 0) return;

    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    let pointsFound = false;

    beacons.forEach(beacon => {
        let worldX, worldY;

        // ПРИОРИТЕТ 1: Абсолютные координаты
        if (beacon.AbsoluteDistance && beacon.AbsoluteAzimuth !== undefined &&
            !isNaN(beacon.AbsoluteDistance) && !isNaN(beacon.AbsoluteAzimuth)) {

            let angle = beacon.AbsoluteAzimuth * Math.PI / 180;
            worldX = beacon.AbsoluteDistance * Math.sin(angle);
            worldY = beacon.AbsoluteDistance * Math.cos(angle);
            pointsFound = true;
        }
        // ПРИОРИТЕТ 2: Декартовы координаты
        else if (beacon.X !== undefined && !isNaN(beacon.X) &&
            beacon.Y !== undefined && !isNaN(beacon.Y)) {
            worldX = beacon.X;
            worldY = beacon.Y;
            pointsFound = true;
        }
        // ПРИОРИТЕТ 3: Полярные координаты
        else if (beacon.Distance && beacon.Azimuth !== undefined && !isNaN(beacon.Azimuth)) {
            let heading = (localDevice?.Heading && !isNaN(localDevice.Heading) && localDevice.HasHeading)
                ? localDevice.Heading : 0;
            let angle = (beacon.Azimuth - heading) * Math.PI / 180;
            worldX = beacon.Distance * Math.sin(angle);
            worldY = beacon.Distance * Math.cos(angle);
            pointsFound = true;
        }

        if (worldX !== undefined && worldY !== undefined) {
            minX = Math.min(minX, worldX);
            maxX = Math.max(maxX, worldX);
            minY = Math.min(minY, worldY);
            maxY = Math.max(maxY, worldY);
        }
    });

    // Добавляем локальное устройство
    if (localDevice) {
        if (localDevice.X !== undefined && !isNaN(localDevice.X)) {
            minX = Math.min(minX, localDevice.X);
            maxX = Math.max(maxX, localDevice.X);
            pointsFound = true;
        }
        if (localDevice.Y !== undefined && !isNaN(localDevice.Y)) {
            minY = Math.min(minY, localDevice.Y);
            maxY = Math.max(maxY, localDevice.Y);
            pointsFound = true;
        }
    }

    if (!pointsFound) return;

    // Добавляем отступы
    const rangeX = maxX - minX;
    const rangeY = maxY - minY;
    const padding = 0.2;

    if (rangeX > 0) {
        minX -= rangeX * padding;
        maxX += rangeX * padding;
    } else {
        minX -= 50;
        maxX += 50;
    }

    if (rangeY > 0) {
        minY -= rangeY * padding;
        maxY += rangeY * padding;
    } else {
        minY -= 50;
        maxY += 50;
    }

    const scaleX = canvas.width / (maxX - minX);
    const scaleY = canvas.height / (maxY - minY);
    scale = Math.min(scaleX, scaleY);

    offsetX = canvas.width / 2 - ((minX + maxX) / 2) * scale;
    offsetY = canvas.height / 2 + ((minY + maxY) / 2) * scale;

    drawMap();
}

function zoomIn() {
    scale *= 1.2;
    drawMap();
}

function zoomOut() {
    scale /= 1.2;
    drawMap();
}

function resetView() {
    scale = 100;
    offsetX = canvas.width / 2;
    offsetY = canvas.height / 2;
    drawMap();
}

function initMouseHandlers() {
    if (!canvas) return;

    canvas.addEventListener('wheel', (e) => {
        e.preventDefault();
        scale *= e.deltaY > 0 ? 0.8 : 1.2;
        if (autoScaleEnabled) {
            autoScaleEnabled = false;
            document.getElementById('auto-scale-btn').classList.add('off');
        }
        drawMap();
    });

    canvas.addEventListener('mousedown', (e) => {
        e.preventDefault();
        isDragging = true;
        lastMouseX = e.clientX;
        lastMouseY = e.clientY;
        canvas.style.cursor = 'grabbing';
    });

    canvas.addEventListener('mousemove', (e) => {
        if (isDragging) {
            e.preventDefault();
            offsetX += e.clientX - lastMouseX;
            offsetY += e.clientY - lastMouseY;
            lastMouseX = e.clientX;
            lastMouseY = e.clientY;
            if (autoScaleEnabled) {
                autoScaleEnabled = false;
                document.getElementById('auto-scale-btn').classList.add('off');
            }
            drawMap();
        }
    });

    canvas.addEventListener('mouseup', (e) => {
        e.preventDefault();
        isDragging = false;
        canvas.style.cursor = 'grab';
    });

    canvas.addEventListener('mouseleave', (e) => {
        e.preventDefault();
        isDragging = false;
        canvas.style.cursor = 'grab';
    });
}

// Очистка при выгрузке страницы
window.addEventListener('beforeunload', function () {
    stopAgeUpdateTimer();
    if (ws) ws.close();
    if (reconnectTimer) clearTimeout(reconnectTimer);
});

// Отладка
window.debug = {
    state: () => ({ localDevice, beacons, logs, currentMode, systemInfo }),
    redraw: drawMap,
    logState: () => {
        console.log('=== DEBUG STATE ===');
        console.log('Mode:', currentMode);
        console.log('localDevice:', localDevice);
        console.log('systemInfo:', systemInfo);
        console.log('beacons:', beacons);
        console.log('logs:', logs);
        console.log('scale:', scale);
        console.log('offset:', offsetX, offsetY);
        console.log('canvas:', canvas ? `${canvas.width}x${canvas.height}` : 'null');
        console.log('==================');
    }
};