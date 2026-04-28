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

// Хранилище времени последнего обновления для каждого маяка
const beaconLastUpdate = new Map();

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

    initResizeHandler();

    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    initControls();
    i18n.updateStaticUI();
    initMouseHandlers();
    initTouchHandlers();
    connectWebSocket();
    loadInitialData();

    startAgeUpdateTimer();

    drawMap();

    const sysInfo = document.getElementById('system-info');
    if (sysInfo && window.innerWidth <= 768) {
        sysInfo.classList.add('compact');
    }

    if (sysInfo) {
        sysInfo.addEventListener('click', (e) => {
            if (window.innerWidth <= 768) {
                sysInfo.classList.toggle('compact');
            }
        });
    }
}

function initResizeHandler() {
    const resizer = document.getElementById('sidebar-resizer');
    const container = document.getElementById('container');
    const sidebar = document.getElementById('sidebar');
    const mapContainer = document.getElementById('map-container');

    if (!resizer || !container || !sidebar || !mapContainer) return;

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
    sidebar.style.width = '400px';

    resizer.addEventListener('mousedown', function (e) {
        e.preventDefault();
        isResizing = true;
        startX = e.clientX;
        startWidth = sidebar.offsetWidth;
        containerWidth = container.offsetWidth;

        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';

        sidebar.style.transition = 'none';
        mapContainer.style.transition = 'none';
    });

    document.addEventListener('mousemove', function (e) {
        if (!isResizing) return;

        e.preventDefault();

        const deltaX = e.clientX - startX;
        let newWidth = startWidth - deltaX;

        const minWidth = 200;
        const maxWidth = containerWidth * 0.8;

        if (newWidth < minWidth) {
            newWidth = minWidth;
        } else if (newWidth > maxWidth) {
            newWidth = maxWidth;
        }

        sidebar.style.width = newWidth + 'px';

        resizeCanvas();
    });

    document.addEventListener('mouseup', function () {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';

            sidebar.style.transition = '';
            mapContainer.style.transition = '';
        }
    });

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
        if (beacons && beacons.length > 0) {
            let needsUpdate = false;

            beacons.forEach(beacon => {
                if (beacon.dataAge !== undefined && beacon.dataAge !== null) {
                    beacon.dataAge += 1;
                    needsUpdate = true;
                }
            });

            if (needsUpdate) {
                updateBeaconsList(beacons);
            }
        }

        if (localDevice && localDevice.dataAge !== undefined && localDevice.dataAge !== null) {
            localDevice.dataAge += 1;
        }

        updateSystemInfo();

        if (!autoScaleEnabled) {
            drawMap();
        }
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

    if (data.mode) currentMode = data.mode;

    if (data.localDevice) {
        const currentLocalAge = localDevice?.dataAge;
        localDevice = data.localDevice;
        if (currentLocalAge !== undefined && currentLocalAge !== null && currentLocalAge > 0) {
            if (localDevice.dataAge === undefined || localDevice.dataAge === null || localDevice.dataAge < currentLocalAge) {
                localDevice.dataAge = currentLocalAge;
            }
        }
    }

    if (data.systemInfo) {
        systemInfo = data.systemInfo;
        updateControlButtons();
    }

    if (data.beacons && Array.isArray(data.beacons)) {
        const currentBeaconAges = new Map();
        beacons.forEach(b => {
            const key = getBeaconKey(b);
            if (b.dataAge !== undefined && b.dataAge !== null) {
                currentBeaconAges.set(key, b.dataAge);
            }
        });

        beacons = data.beacons.map(beacon => {
            const key = getBeaconKey(beacon);
            const currentAge = currentBeaconAges.get(key);

            if (beacon.isTimeout) {
                if (currentAge !== undefined && currentAge !== null && currentAge > 0) {
                    beacon.dataAge = currentAge;
                } else if (beacon.dataAge === undefined || beacon.dataAge === null) {
                    beacon.dataAge = 0;
                }
            } else {
                if (beacon.dataAge !== undefined && beacon.dataAge !== null) {
                    // оставляем серверное значение
                } else {
                    beacon.dataAge = 0;
                }
            }

            return beacon;
        });

        updateBeaconsList(beacons);
    }

    updateSystemInfo();

    if (data.calibration) {
        updateCalibrationPanel(data.calibration);
    }

    if (data.recentLogs && Array.isArray(data.recentLogs)) {
        logs = [...data.recentLogs, ...logs].slice(0, MAX_LOGS);
        updateLogs(logs);
    }

    if (autoScaleEnabled) autoScale(); else drawMap();
}

function getBeaconKey(beacon) {
    if (beacon.address) {
        return `addr_${beacon.address}`;
    }
    return `coord_${beacon.x}_${beacon.y}_${beacon.z}`;
}

function getBeaconAge(beacon) {
    const key = getBeaconKey(beacon);
    const lastUpdate = beaconLastUpdate.get(key);
    if (!lastUpdate) return 0;
    return Date.now() / 1000 - lastUpdate;
}

function getLocalDeviceAge() {
    if (!localDevice || !localDevice.lastUpdate) return 0;
    return Date.now() / 1000 - localDevice.lastUpdate;
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
        const address = b.address || '?';
        const dataAge = (b.dataAge !== undefined && b.dataAge !== null) ? b.dataAge : getBeaconAge(b);

        let details = [];

        let status = 'active';
        let statusClass = 'status-active';
        let statusText = i18n.t('active');

        if (b.isTimeout) {
            status = 'timeout';
            statusClass = 'status-timeout';
            statusText = i18n.t('timeout');
        } else if (dataAge > 10) {
            status = 'timeout';
            statusClass = 'status-timeout';
            statusText = i18n.t('timeout');
        } else if (dataAge > 5) {
            status = 'warning';
            statusClass = 'status-warning';
            statusText = i18n.t('warning');
        }

        function addDetail(label, value, unit = '', precision = null) {
            if (value !== undefined && value !== null && !isNaN(value)) {
                let displayValue = value;
                if (precision !== null) {
                    displayValue = value.toFixed(precision);
                }
                details.push(`<div class="beacon-detail"><span class="detail-label">${label}:</span> <span class="detail-value">${displayValue}${unit}</span></div>`);
            }
        }

        if (b.absoluteDistance !== undefined && b.absoluteDistance !== null && !isNaN(b.absoluteDistance) &&
            b.absoluteAzimuth !== undefined && b.absoluteAzimuth !== null && !isNaN(b.absoluteAzimuth)) {
            addDetail(i18n.t('absdist'), b.absoluteDistance, i18n.t('meter'), 1);
            addDetail(i18n.t('absaz'), b.absoluteAzimuth, i18n.t('degree'), 1);
        }

        addDetail(i18n.t('x'), b.x, i18n.t('meter'), 2);
        addDetail(i18n.t('y'), b.y, i18n.t('meter'), 2);
        addDetail(i18n.t('z'), b.z, i18n.t('meter'), 2);

        addDetail(i18n.t('distance'), b.distance, i18n.t('meter'), 1);
        addDetail(i18n.t('azimuth'), b.azimuth, i18n.t('degree'), 1);
        addDetail(i18n.t('elevation'), b.elevation, i18n.t('degree'), 1);

        addDetail(i18n.t('depth'), b.depth, i18n.t('meter'), 1);
        addDetail(i18n.t('signal'), b.signalLevel, ' dB', 1);
        addDetail(i18n.t('battery'), b.battery, ' V', 2);
        addDetail(i18n.t('temperature'), b.temperature, i18n.t('celsius'), 1);
        addDetail(i18n.t('proptime'), b.propagationTime, ' s', 4);
        addDetail(i18n.t('success'), b.successRate, '%', 1);
        addDetail(i18n.t('requests'), b.totalRequests, '', 0);

        if (b.latitude !== undefined && b.latitude !== null && !isNaN(b.latitude) &&
            b.longitude !== undefined && b.longitude !== null && !isNaN(b.longitude)) {
            addDetail(i18n.t('lat'), b.latitude, i18n.t('degree'), 6);
            addDetail(i18n.t('lon'), b.longitude, i18n.t('degree'), 6);
        }

        if (b.message) {
            details.push(`<div class="beacon-detail"><span class="detail-label">Msg:</span> <span class="detail-value">${b.message}</span></div>`);
        }

        if (b.coordinateType && b.coordinateType !== 'unknown') {
            details.push(`<div class="beacon-detail"><span class="detail-label">Type:</span> <span class="detail-value">${b.coordinateType}</span></div>`);
        }

        let ageColor = '#28a745';
        if (b.isTimeout) {
            ageColor = '#dc3545';
        } else if (dataAge > 5) {
            ageColor = '#ffc107';
        }
        if (dataAge > 10) {
            ageColor = '#dc3545';
        }
        details.push(`<div class="beacon-detail"><span class="detail-label">${i18n.t('dataAge')}:</span> <span class="detail-value" style="color: ${ageColor}">${dataAge.toFixed(1)}${i18n.t('second')}</span></div>`);

        if (details.length <= 1) {
            details.push(`<div class="beacon-detail" style="grid-column: 1/-1; text-align: center; color: #999;">Нет данных</div>`);
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
        const type = log.type || 'info';
        const timestamp = log.timestamp || new Date().toLocaleTimeString();
        const message = log.message || '';
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

    const isExpanded = !panel.classList.contains('compact');

    let html = '';

    const connectionIcon = systemInfo?.connectionActive ? '🟢' : '🔴';
    const interrogIcon = systemInfo?.interrogationActive ? '🔵' : '⚪';

    html += `<div class="sys-header">`;
    html += `<strong>${currentMode.toUpperCase()}</strong> `;

    const localAge = (localDevice?.dataAge !== undefined && localDevice?.dataAge !== null)
        ? localDevice.dataAge
        : getLocalDeviceAge();

    let ageColor = '#28a745';
    if (localAge > 5) ageColor = '#ffc107';
    if (localAge > 10) ageColor = '#dc3545';

    html += `<span class="sys-age-badge" style="color: ${ageColor}">${localAge.toFixed(0)}s</span>`;
    html += `</div>`;

    html += `<div style="display: flex; gap: 8px; margin-top: 4px;">`;
    html += `<span>${connectionIcon}</span>`;
    html += `<span>${interrogIcon}</span>`;
    if (localDevice?.depth !== undefined && localDevice?.depth !== null && !isNaN(localDevice.depth)) {
        html += `<span>🌊 ${localDevice.depth.toFixed(1)}m</span>`;
    }
    html += `</div>`;

    if (systemInfo) {
        html += `<div class="sys-detail" style="margin-top: 8px; padding-top: 6px; border-top: 1px solid rgba(255,255,255,0.2);">`;
        html += `<div><strong>${systemInfo.deviceType || 'Unknown'}</strong></div> `;
        html += `<div style="font-size: 9px;">${systemInfo.serialNumber || ''}</div>`;
        html += `<div style="font-size: 9px;">${systemInfo.version || ''}</div>`;
        html += `</div>`;
    }

    if (localDevice) {
        function addRow(label, value, unit = '', color = '#fff') {
            if (value !== undefined && value !== null && !isNaN(value)) {
                return `<div class="sys-detail" style="display: flex; justify-content: space-between;">
                    <span style="color: #aaa;">${label}:</span>
                    <span style="color: ${color};">${value}${unit}</span>
                </div>`;
            }
            return '';
        }

        if (localDevice.x !== undefined || localDevice.y !== undefined) {
            html += `<div class="sys-section" style="margin-top: 6px; color: #4a90e2; font-weight: bold; font-size: 9px;">${i18n.t('position')}</div>`;
            html += addRow('X', localDevice.x?.toFixed(1), 'm');
            html += addRow('Y', localDevice.y?.toFixed(1), 'm');
            html += addRow('Z', localDevice.z?.toFixed(1), 'm');
        }

        if (localDevice.heading !== undefined || localDevice.course !== undefined) {
            html += `<div class="sys-section" style="margin-top: 6px; color: #4a90e2; font-weight: bold; font-size: 9px;">${i18n.t('orientation')}</div>`;
            html += addRow(i18n.t('heading'), localDevice.heading?.toFixed(1), '°', '#ff4444');
            html += addRow(i18n.t('course'), localDevice.course?.toFixed(1), '°', '#44ff44');
            html += addRow(i18n.t('speed'), localDevice.speed?.toFixed(1), 'm/s');
        }
    }

    html += `<div class="sys-expand-hint">${i18n.t('expandHint')}</div>`;

    panel.innerHTML = html;

    if (window.innerWidth <= 768) {
        if (isExpanded) {
            panel.classList.remove('compact');
        } else {
            panel.classList.add('compact');
        }
    } else {
        panel.classList.remove('compact');
    }

    updateControlButtons();
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

            if (beacon.absoluteDistance && beacon.absoluteAzimuth !== undefined &&
                !isNaN(beacon.absoluteDistance) && !isNaN(beacon.absoluteAzimuth)) {

                let angle = beacon.absoluteAzimuth * Math.PI / 180;
                x = offsetX + beacon.absoluteDistance * Math.sin(angle) * scale;
                y = offsetY - beacon.absoluteDistance * Math.cos(angle) * scale;
                coordType = 'absolute';
            }
            else if (beacon.x !== undefined && beacon.x !== null &&
                beacon.y !== undefined && beacon.y !== null &&
                !isNaN(beacon.x) && !isNaN(beacon.y)) {

                x = beacon.x * scale + offsetX;
                y = -beacon.y * scale + offsetY;
                coordType = 'cartesian';
            }
            else if (beacon.distance && beacon.azimuth !== undefined &&
                beacon.azimuth !== null && !isNaN(beacon.distance) && !isNaN(beacon.azimuth)) {

                let heading = (localDevice?.heading && !isNaN(localDevice.heading) && localDevice.hasHeading)
                    ? localDevice.heading : 0;
                let angle = (beacon.azimuth - heading) * Math.PI / 180;
                x = offsetX + beacon.distance * Math.sin(angle) * scale;
                y = offsetY - beacon.distance * Math.cos(angle) * scale;
                coordType = 'polar';
            }
            else {
                return;
            }

            if (isNaN(x) || isNaN(y) || !isFinite(x) || !isFinite(y)) return;

            const margin = 100;
            if (x < -margin || x > canvas.width + margin ||
                y < -margin || y > canvas.height + margin) return;

            ctx.beginPath();
            ctx.arc(x, y, 18, 0, 2 * Math.PI);

            let fillStyle;
            const age = (beacon.dataAge !== undefined && beacon.dataAge !== null) ? beacon.dataAge : getBeaconAge(beacon);

            if (coordType === 'absolute') {
                const opacity = Math.max(0.3, 1 - age / 20);
                fillStyle = `rgba(255, 215, 0, ${opacity})`;
            } else {
                const hue = ((beacon.address || index + 1) * 30) % 360;
                const opacity = Math.max(0.3, 1 - age / 20);
                fillStyle = `hsla(${hue}, 80%, 60%, ${opacity})`;
            }

            ctx.fillStyle = fillStyle;
            ctx.fill();
            ctx.strokeStyle = 'white';
            ctx.lineWidth = 3;
            ctx.stroke();

            ctx.fillStyle = 'white';
            ctx.font = 'bold 14px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(beacon.address?.toString() || (index + 1).toString(), x, y);

            ctx.font = '10px Arial';
            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';

            let label = '';
            if (coordType === 'absolute') {
                label = `ABS ${beacon.absoluteDistance.toFixed(0)}m`;
            } else if (coordType === 'cartesian') {
                label = `${beacon.x?.toFixed(0) || '?'}, ${beacon.y?.toFixed(0) || '?'}`;
            } else if (coordType === 'polar') {
                label = `${beacon.distance.toFixed(0)}m`;
            }

            if (label) {
                ctx.fillText(label, x, y + 30);
            }

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

    let deviceX = offsetX;
    let deviceY = offsetY;

    if (currentMode === 'lbl' && localDevice.x !== undefined && localDevice.x !== null &&
        localDevice.y !== undefined && localDevice.y !== null) {
        deviceX = localDevice.x * scale + offsetX;
        deviceY = -localDevice.y * scale + offsetY;
    }

    const x = deviceX;
    const y = deviceY;

    ctx.beginPath();
    ctx.moveTo(x, y - 20);
    ctx.lineTo(x + 20, y);
    ctx.lineTo(x, y + 20);
    ctx.lineTo(x - 20, y);
    ctx.closePath();

    let baseColor;
    if (currentMode === 'usbl') {
        baseColor = '0, 255, 255';
    } else {
        baseColor = '74, 144, 226';
    }

    const age = (localDevice.dataAge !== undefined && localDevice.dataAge !== null)
        ? localDevice.dataAge
        : getLocalDeviceAge();

    const opacity = Math.max(0.3, 1 - age / 20);

    ctx.fillStyle = `rgba(${baseColor}, ${opacity})`;
    ctx.fill();
    ctx.strokeStyle = 'white';
    ctx.lineWidth = 3;
    ctx.stroke();

    if (localDevice.heading !== null && !isNaN(localDevice.heading) && localDevice.hasHeading) {
        try {
            const angle = localDevice.heading * Math.PI / 180;
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

            ctx.font = '10px Arial';
            ctx.fillStyle = '#ff4444';
            ctx.fillText('H', arrowX + 10, arrowY - 10);

        } catch (e) {
            console.error('Error drawing heading:', e);
        }
    }

    if (localDevice.course !== undefined && localDevice.course !== null && !isNaN(localDevice.course)) {
        try {
            const angle = localDevice.course * Math.PI / 180;
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

            ctx.font = '10px Arial';
            ctx.fillStyle = '#44ff44';
            ctx.fillText('C', arrowX + 8, arrowY - 8);

        } catch (e) {
            console.error('Error drawing course:', e);
        }
    }

    if (localDevice.speed !== undefined && localDevice.speed !== null && !isNaN(localDevice.speed)) {
        ctx.font = '10px Arial';
        ctx.fillStyle = 'white';
        ctx.fillText(`${localDevice.speed.toFixed(1)} ${i18n.t('meterPerSec')}`, x + 25, y - 25);
    }

    if (localDevice.depth !== undefined && localDevice.depth !== null && !isNaN(localDevice.depth)) {
        ctx.font = '10px Arial';
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.fillText(`${i18n.t('depth')}: ${localDevice.depth.toFixed(1)}${i18n.t('meter')}`, x + 25, y - 35);
    }

    ctx.fillStyle = 'white';
    ctx.font = 'bold 12px Arial';

    if (currentMode === 'usbl') {
        ctx.fillText(i18n.t('antenna'), x - 35, y - 35);
    } else {
        ctx.fillText(i18n.t('station'), x - 30, y - 35);
    }

    if (localDevice.x !== undefined && localDevice.x !== null &&
        localDevice.y !== undefined && localDevice.y !== null) {
        ctx.font = '9px Arial';
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.fillText(`${i18n.t('x')}:${localDevice.x.toFixed(1)} ${i18n.t('y')}:${localDevice.y.toFixed(1)}`, x - 40, y - 50);
    }
}

function drawScaleBar() {
    if (!canvas || !ctx) return;

    const padding = 30;
    const splitterWidth = 5;
    const rightOffset = 20;

    const canvasWidth = canvas.width;

    const rawMeters = 100 / scale;

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

    const maxAllowedWidth = canvasWidth - padding * 2 - splitterWidth - rightOffset;
    if (displayWidth > maxAllowedWidth) {
        displayWidth = maxAllowedWidth;
        displayMeters = Math.round(displayWidth / scale);
    }

    const barX = canvasWidth - padding - displayWidth - splitterWidth - rightOffset;
    const barY = canvas.height - padding;

    ctx.beginPath();
    ctx.moveTo(barX, barY);
    ctx.lineTo(barX + displayWidth, barY);
    ctx.strokeStyle = 'white';
    ctx.lineWidth = 3;
    ctx.stroke();

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

        if (beacon.absoluteDistance && beacon.absoluteAzimuth !== undefined &&
            !isNaN(beacon.absoluteDistance) && !isNaN(beacon.absoluteAzimuth)) {

            let angle = beacon.absoluteAzimuth * Math.PI / 180;
            worldX = beacon.absoluteDistance * Math.sin(angle);
            worldY = beacon.absoluteDistance * Math.cos(angle);
            pointsFound = true;
        }
        else if (beacon.x !== undefined && !isNaN(beacon.x) &&
            beacon.y !== undefined && !isNaN(beacon.y)) {
            worldX = beacon.x;
            worldY = beacon.y;
            pointsFound = true;
        }
        else if (beacon.distance && beacon.azimuth !== undefined && !isNaN(beacon.azimuth)) {
            let heading = (localDevice?.heading && !isNaN(localDevice.heading) && localDevice.hasHeading)
                ? localDevice.heading : 0;
            let angle = (beacon.azimuth - heading) * Math.PI / 180;
            worldX = beacon.distance * Math.sin(angle);
            worldY = beacon.distance * Math.cos(angle);
            pointsFound = true;
        }

        if (worldX !== undefined && worldY !== undefined) {
            minX = Math.min(minX, worldX);
            maxX = Math.max(maxX, worldX);
            minY = Math.min(minY, worldY);
            maxY = Math.max(maxY, worldY);
        }
    });

    if (localDevice) {
        if (localDevice.x !== undefined && !isNaN(localDevice.x)) {
            minX = Math.min(minX, localDevice.x);
            maxX = Math.max(maxX, localDevice.x);
            pointsFound = true;
        }
        if (localDevice.y !== undefined && !isNaN(localDevice.y)) {
            minY = Math.min(minY, localDevice.y);
            maxY = Math.max(maxY, localDevice.y);
            pointsFound = true;
        }
    }

    if (!pointsFound) return;

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

function initTouchHandlers() {
    if (!canvas) return;

    canvas.addEventListener('touchstart', (e) => {
        if (e.touches.length === 1) {
            e.preventDefault();
            const touch = e.touches[0];
            isDragging = true;
            lastMouseX = touch.clientX;
            lastMouseY = touch.clientY;
        }
    });

    canvas.addEventListener('touchmove', (e) => {
        if (isDragging && e.touches.length === 1) {
            e.preventDefault();
            const touch = e.touches[0];
            offsetX += touch.clientX - lastMouseX;
            offsetY += touch.clientY - lastMouseY;
            lastMouseX = touch.clientX;
            lastMouseY = touch.clientY;

            if (autoScaleEnabled) {
                autoScaleEnabled = false;
                document.getElementById('auto-scale-btn')?.classList.add('off');
            }
            drawMap();
        }
    });

    canvas.addEventListener('touchend', (e) => {
        e.preventDefault();
        isDragging = false;
    });

    let initialDistance = 0;
    let initialScale = scale;

    canvas.addEventListener('touchstart', (e) => {
        if (e.touches.length === 2) {
            e.preventDefault();
            const dx = e.touches[0].clientX - e.touches[1].clientX;
            const dy = e.touches[0].clientY - e.touches[1].clientY;
            initialDistance = Math.sqrt(dx * dx + dy * dy);
            initialScale = scale;
            isDragging = false;
        }
    });

    canvas.addEventListener('touchmove', (e) => {
        if (e.touches.length === 2) {
            e.preventDefault();
            const dx = e.touches[0].clientX - e.touches[1].clientX;
            const dy = e.touches[0].clientY - e.touches[1].clientY;
            const distance = Math.sqrt(dx * dx + dy * dy);

            if (initialDistance > 0) {
                scale = initialScale * (distance / initialDistance);
                scale = Math.min(Math.max(scale, 10), 2000);

                if (autoScaleEnabled) {
                    autoScaleEnabled = false;
                    document.getElementById('auto-scale-btn')?.classList.add('off');
                }
                drawMap();
            }
        }
    });
}

window.addEventListener('beforeunload', function () {
    stopAgeUpdateTimer();
    if (ws) ws.close();
    if (reconnectTimer) clearTimeout(reconnectTimer);
});

// ========== УПРАВЛЕНИЕ КОМАНДАМИ ==========

let commandInProgress = false;

function toggleConnection() {
    if (!systemInfo) return;

    const currentState = systemInfo.connectionActive;
    const command = currentState ? 'CCON' : 'OCON';

    systemInfo.connectionActive = !currentState;
    systemInfo.interrogationActive = systemInfo.connectionActive ? systemInfo.interrogationActive : false;
    updateControlButtons();

    sendCommand(command);
}

function toggleInterrogation() {
    if (!systemInfo) return;

    const currentState = systemInfo.interrogationActive;
    const command = currentState ? 'PITG' : 'RITG';

    systemInfo.interrogationActive = !currentState;
    updateControlButtons();

    sendCommand(command);
}

// ========== ОТПРАВКА КОМАНД ==========

function sendCommand(command) {
    if (commandInProgress) {
        showCommandStatus(i18n.t('wait'), 'info');
        return;
    }

    commandInProgress = true;
    console.log('Sending command:', command);
    showCommandStatus(`${i18n.t('sending')} ${command}...`, 'info');

    const finishCommand = () => {
        commandInProgress = false;
    };

    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ Type: command, Data: null }));
        showCommandStatus(i18n.t('commandSent'), 'success');
        finishCommand();
        return;
    }

    fetch('/api/command', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Type: command, Data: null })
    })
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return response.json();
        })
        .then(data => {
            showCommandStatus(i18n.t('commandDone'), 'success');
            setTimeout(() => loadInitialData(), 500);
            finishCommand();
        })
        .catch(error => {
            showCommandStatus(`${i18n.t('error')}: ${error.message}`, 'error');
            setTimeout(() => loadInitialData(), 1000);
            finishCommand();
        });
}

function showCommandStatus(message, type) {
    const statusEl = document.getElementById('cmd-status');
    if (!statusEl) return;

    statusEl.textContent = message;
    statusEl.className = 'cmd-status ' + type;

    if (type === 'success') {
        setTimeout(() => {
            if (statusEl.textContent === message) {
                statusEl.style.opacity = '0';
                setTimeout(() => {
                    statusEl.textContent = '';
                    statusEl.className = 'cmd-status';
                    statusEl.style.opacity = '1';
                }, 300);
            }
        }, 2000);
    }
}

function updateControlButtons() {
    const btnConnection = document.getElementById('btn-connection');
    const btnInterrogation = document.getElementById('btn-interrogation');
    const connText = document.getElementById('connection-btn-text');
    const interText = document.getElementById('interrogation-btn-text');

    if (!systemInfo) {
        if (btnConnection) btnConnection.disabled = true;
        if (btnInterrogation) btnInterrogation.disabled = true;
        return;
    }

    if (btnConnection && connText) {
        btnConnection.disabled = false;
        if (systemInfo.connectionActive) {
            connText.textContent = i18n.t('close');
            btnConnection.className = 'ctrl-btn disconnect';
            btnConnection.querySelector('.btn-icon').textContent = '🔌';
        } else {
            connText.textContent = i18n.t('open');
            btnConnection.className = 'ctrl-btn connect';
            btnConnection.querySelector('.btn-icon').textContent = '🔌';
        }
    }

    if (btnInterrogation && interText) {
        btnInterrogation.disabled = !systemInfo.connectionActive;

        if (systemInfo.interrogationActive) {
            interText.textContent = i18n.t('pause');
            btnInterrogation.className = 'ctrl-btn stop';
            btnInterrogation.querySelector('.btn-icon').textContent = '⏸';
        } else {
            interText.textContent = i18n.t('interrogate');
            btnInterrogation.className = 'ctrl-btn start';
            btnInterrogation.querySelector('.btn-icon').textContent = '▶';
        }
    }
}

// ========== КАЛИБРОВКА ==========

function startCalibration() {
    if (!confirm(i18n.t('confirmCalibration') || 'Start calibration? This will rotate the antenna.')) return;
    sendCommand('SCAL,,,');
}

function stopCalibration() {
    sendCommand('FCAL');
}

function updateCalibrationPanel(calData) {
    const panel = document.getElementById('calibration-panel');
    if (!panel) return;

    if (!calData || !calData.isAvailable) {
        panel.style.display = 'none';
        return;
    }

    panel.style.display = 'block';

    const stateEl = document.getElementById('cal-state');
    if (stateEl) {
        stateEl.textContent = calData.state || 'Idle';
        switch (calData.state) {
            case 'Measuring': stateEl.style.color = '#4caf50'; break;
            case 'Moving': stateEl.style.color = '#ff9800'; break;
            case 'Completed': stateEl.style.color = '#2196f3'; break;
            case 'Failed': stateEl.style.color = '#f44336'; break;
            default: stateEl.style.color = '#aaa'; break;
        }
    }

    const pointsEl = document.getElementById('cal-points');
    if (pointsEl) {
        pointsEl.textContent = `${calData.collectedPoints || 0} / ${calData.totalPoints || '?'}`;
    }

    const angleEl = document.getElementById('cal-current-angle');
    if (angleEl) {
        const angle = calData.currentAngle;
        if (angle !== null && angle !== undefined && !isNaN(angle)) {
            angleEl.textContent = angle.toFixed(1);
        }
    }

    const errorRow = document.getElementById('cal-error-row');
    const errorEl = document.getElementById('cal-error');
    if (calData.lastError) {
        if (errorRow) errorRow.style.display = '';
        if (errorEl) errorEl.textContent = calData.lastError;
    } else {
        if (errorRow) errorRow.style.display = 'none';
    }
}

// Отладка
window.debug = {
    state: () => ({ localDevice, beacons, logs, currentMode, systemInfo, beaconLastUpdate: Array.from(beaconLastUpdate.entries()) }),
    redraw: drawMap,
    logState: () => {
        console.log('=== DEBUG STATE ===');
        console.log('Mode:', currentMode);
        console.log('localDevice:', localDevice);
        console.log('systemInfo:', systemInfo);
        console.log('beacons:', beacons);
        console.log('beaconLastUpdate:', Array.from(beaconLastUpdate.entries()));
        console.log('logs:', logs);
        console.log('scale:', scale);
        console.log('offset:', offsetX, offsetY);
        console.log('canvas:', canvas ? `${canvas.width}x${canvas.height}` : 'null');
        console.log('==================');
    }
};