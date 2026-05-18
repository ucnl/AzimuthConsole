// AzimuthConsole.wwwroot.script.js

let commandSchema = null;

// ========== ПАРСЕР ПРОТОКОЛА AZIMUTH ==========

const AZMREM_FIELDS = [
    'sRange', 'azimuth', 'pTime', 'msr', 'depth', 'sRangeProjection',
    'aDistance', 'aAzimuth', 'elevation', 'vcc', 'waterTemp',
    'lat', 'lon', 'rAzimuth', 'message', 'x', 'y', 'z'
];

function parseAZMREM(line) {
    const parts = line.split(',');
    if (parts.length < 2 || parts[0] !== '@AZMREM') return null;

    const addr = parseInt(parts[1], 10);
    if (isNaN(addr)) return null;

    let idx = 2;

    const readIgnoreAge = () => {
        const v = parts[idx++];
        return (v !== undefined && v !== '') ? parseFloat(v) : null;
    };

    const readWithAge = () => {
        const v = parts[idx++];
        const a = parts[idx++];
        return {
            value: (v !== undefined && v !== '') ? parseFloat(v) : null,
            age: (a !== undefined && a !== '') ? parseFloat(a) : null
        };
    };

    const b = { address: addr };

    b.sRange = readIgnoreAge();
    b.azimuth = readIgnoreAge();
    b.pTime = readIgnoreAge();

    const msr = readWithAge();
    b.msr = msr.value; b.msrAge = msr.age;

    const dpt = readWithAge();
    b.depth = dpt.value; b.depthAge = dpt.age;

    const srp = readWithAge();
    b.sRangeProjection = srp.value; b.srpAge = srp.age;

    const ad = readWithAge();
    b.aDistance = ad.value; b.adAge = ad.age;

    const aa = readWithAge();
    b.aAzimuth = aa.value; b.aaAge = aa.age;

    const el = readWithAge();
    b.elevation = el.value; b.elAge = el.age;

    const vcc = readWithAge();
    b.vcc = vcc.value; b.vccAge = vcc.age;

    const wt = readWithAge();
    b.waterTemp = wt.value; b.wtAge = wt.age;

    b.lat = readIgnoreAge();
    b.lon = readIgnoreAge();

    const ra = readWithAge();
    b.rAzimuth = ra.value; b.raAge = ra.age;

    const msg = readWithAge();
    b.message = msg.value !== null ? String(msg.value) : null;
    b.msgAge = msg.age;

    b.x = readIgnoreAge();
    b.y = readIgnoreAge();
    b.z = readIgnoreAge();

    b.isTimeout = (parts[idx] || '').toLowerCase() === 'true';

    const ages = [b.msrAge, b.depthAge, b.srpAge, b.adAge, b.aaAge,
    b.elAge, b.vccAge, b.wtAge, b.raAge, b.msgAge]
        .filter(a => a !== null && !isNaN(a));
    b.dataAge = ages.length > 0 ? Math.min(...ages) : 0;

    b.distance = b.sRange;
    b.signalLevel = b.msr;
    b.battery = b.vcc;
    b.temperature = b.waterTemp;
    b.latitude = b.lat;
    b.longitude = b.lon;
    b.absoluteDistance = b.aDistance;
    b.absoluteAzimuth = b.aAzimuth;

    return b;
}

function parseAZMLOC(line) {
    const parts = line.split(',');
    if (parts.length < 2 || parts[0] !== '@AZMLOC') return null;

    let idx = 1;

    const readIgnoreAge = () => {
        const v = parts[idx++];
        return (v !== undefined && v !== '') ? parseFloat(v) : null;
    };

    const readWithAge = () => {
        const v = parts[idx++];
        const a = parts[idx++];
        return {
            value: (v !== undefined && v !== '') ? parseFloat(v) : null,
            age: (a !== undefined && a !== '') ? parseFloat(a) : null
        };
    };

    const loc = {};

    loc.pressure = readIgnoreAge();
    loc.depth = readIgnoreAge();
    loc.waterTemp = readIgnoreAge();
    loc.pitch = readIgnoreAge();

    const roll = readWithAge();
    loc.roll = roll.value; loc.rollAge = roll.age;

    loc.lat = readIgnoreAge();
    loc.lon = readIgnoreAge();
    loc.course = readIgnoreAge();

    const spd = readWithAge();
    loc.speed = spd.value; loc.speedAge = spd.age;

    const hdg = readWithAge();
    loc.heading = hdg.value; loc.headingAge = hdg.age;
    loc.hasHeading = hdg.value !== null;

    loc.x = readIgnoreAge();
    loc.y = readIgnoreAge();
    loc.z = readIgnoreAge();

    const rerr = readWithAge();
    loc.rError = rerr.value; loc.rerrAge = rerr.age;

    const ages = [loc.rollAge, loc.speedAge, loc.headingAge, loc.rerrAge]
        .filter(a => a !== null && !isNaN(a));
    loc.dataAge = ages.length > 0 ? Math.max(...ages) : 0;

    loc.temperature = loc.waterTemp;

    return { type: 'localDevice', data: loc };
}

// ========== СОСТОЯНИЕ ==========

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

let ageUpdateTimer = null;
const AGE_UPDATE_INTERVAL = 1000;

let isResizing = false;
let startX = 0;
let startWidth = 0;
let containerWidth = 0;

let commandInProgress = false;
let cmdHistory = [];

let portsTimer = null;

let currentSettings = {
    mask: '1', salinity: '0.0', maxDist: '1000', soundSpeed: '',
    offsX: '0.0', offsY: '0.0', offsPhi: '0.0'
};

let portStatuses = {
    azm: { port: 'AUTO', baud: '9600', status: 'Inactive' },
    aux1: { proto: 'NMEA', port: 'OFF', baud: '9600', status: 'Inactive' },
    aux2: { port: 'OFF', baud: '9600', status: 'Inactive' },
    rdt: { port: 'OFF', baud: '9600', status: 'Inactive' },
    outs: { port: 'OFF', baud: '9600' },
    outu: { addr: '' },
};

// Глобальная ссылка на i18n
window.i18n = i18n;

document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded');
    init();
});

function addLogEntry(message) {
    logs.unshift({
        timestamp: new Date().toLocaleTimeString(),
        message: message.length > 100 ? message.substring(0, 100) + '...' : message,
        type: message.includes('ERR') ? 'error' :
            message.startsWith('>>') ? 'info' : 'info'
    });
    if (logs.length > MAX_LOGS) logs.pop();
    updateLogs(logs);
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
    logsDiv.scrollTop = logsDiv.scrollHeight;
}

function init() {
    canvas = document.getElementById('map-canvas');
    if (!canvas) {
        console.error('Canvas not found!');
        return;
    }

    ctx = canvas.getContext('2d');

    initResizeHandler();
    resizeCanvas();

    scale = 100;
    offsetX = canvas.width / 2;
    offsetY = canvas.height / 2;
    drawMap();

    window.addEventListener('resize', resizeCanvas);
    initControls();
    i18n.updateStaticUI();
    initMouseHandlers();
    initTouchHandlers();
    connectWebSocket();
    requestSystemInfo();

    startAgeUpdateTimer();

    // Запрос статуса портов и калибровки каждые 5 секунд
    setInterval(() => {
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send('PORTS');
            ws.send('CAL?');
            ws.send('ITG?');
        }
    }, 5000);

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

    // Инициализация IntelliSense для командной строки
    initCommandAutocomplete();
}

function requestSystemInfo() {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send('STAT');
        ws.send('CNA?');
        ws.send('ITG?');
        ws.send('CAL?');
        ws.send('PORTS');
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

        if (newWidth < minWidth) newWidth = minWidth;
        else if (newWidth > maxWidth) newWidth = maxWidth;

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
            beacons.forEach(beacon => {
                if (beacon.dataAge !== undefined && beacon.dataAge !== null) {
                    beacon.dataAge += 1;
                }
            });
            updateBeaconsList(beacons);
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

    const newWidth = container.clientWidth;
    const newHeight = container.clientHeight;

    if (newWidth !== canvas.width || newHeight !== canvas.height) {
        canvas.width = newWidth;
        canvas.height = newHeight;

        if (canvas.width > 0 && canvas.height > 0) {
            drawMap();
        }
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
    const wsUrl = `${protocol}//${window.location.host}`;

    try {
        ws = new WebSocket(wsUrl);

        ws.onopen = function () {
            updateConnectionStatus(true);
            if (reconnectTimer) clearTimeout(reconnectTimer);
            requestSystemInfo();
        };

        ws.onmessage = function (event) {            
            const msg = event.data;

            // --- СХЕМА КОМАНД от сервера ---
            if (typeof msg === 'string' && msg.startsWith('!SCHEMA,')) {
                try {
                    const schemaJson = msg.substring(8);
                    commandSchema = JSON.parse(schemaJson);
                    console.log('[WS] Command schema loaded:', commandSchema.commands.length, 'commands');
                } catch (e) {
                    console.error('[WS] Failed to parse schema:', e);
                }
                return;
            }

            handleAzimuthLine(msg);
        };

        ws.onclose = function () {
            updateConnectionStatus(false);
            
            systemInfo.connectionActive = false;
            systemInfo.interrogationActive = false;
            for (const key in portStatuses) {
                portStatuses[key].status = 'Inactive';
            }
            updateControlButtons();
            updatePortsInfo(["PORTS", "OK"]);

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

// ========== ОБРАБОТКА ВХОДЯЩИХ СТРОК ==========

function handleAzimuthLine(line) {
    if (typeof line !== 'string') return;
    line = line.trim();

    if (line === '!DINFO_UPDATED') {
        requestSystemInfo();
        return;
    }

    addLogEntry(line);

    if (line.startsWith('@AZMREM,')) {
        handleBeaconLine(line);
    } else if (line.startsWith('@AZMLOC,')) {
        handleLocalDeviceLine(line);
    } else if (line.includes(',OK') || line.includes(',ERR')) {        
        handleCommandResponse(line);
    }
}

function handleBeaconLine(line) {
    const parsed = parseAZMREM(line);
    if (!parsed) return;

    parsed.dataAge = 0;

    const existingIdx = beacons.findIndex(b => b.address === parsed.address);
    if (existingIdx >= 0) {
        beacons[existingIdx] = { ...beacons[existingIdx], ...parsed, dataAge: parsed.dataAge };
    } else {
        beacons.push({ ...parsed });
    }

    updateBeaconsList(beacons);
    if (autoScaleEnabled) autoScale(); else drawMap();
}

function handleLocalDeviceLine(line) {
    const parsed = parseAZMLOC(line);
    if (!parsed || parsed.type !== 'localDevice') return;

    localDevice = { ...localDevice, ...parsed.data, dataAge: parsed.data.dataAge || 0 };
    updateSystemInfo();
    if (!autoScaleEnabled) drawMap();
}

function handleCommandResponse(line) {
    const parts = line.split(',');
    const cmdId = parts[0];
    const status = parts[1];

    commandInProgress = false;

    if (status === 'OK') {
        showCommandStatus(`${cmdId}: OK`, 'success');

        if (cmdId === 'CNA?') {
            const active = parts.find(p => p.startsWith('active='))?.split('=')[1];
            if (!systemInfo) systemInfo = {};
            systemInfo.connectionActive = active === 'TRUE';
        }
        else if (cmdId === 'ITG?') {
            const active = parts.find(p => p.startsWith('active='))?.split('=')[1];
            if (!systemInfo) systemInfo = {};
            systemInfo.interrogationActive = active === 'TRUE';
        }
        else if (cmdId === 'STAT') {
            if (!systemInfo) systemInfo = {};
            for (const p of parts) {
                if (p.startsWith('azm_status=')) systemInfo.azmStatus = p.split('=')[1];
                else if (p.startsWith('interrogation=')) systemInfo.interrogationActive = p.split('=')[1] === 'True';
                else if (p.startsWith('position_valid=')) systemInfo.positionValid = p.split('=')[1] === 'True';
                else if (p.startsWith('address_mask=')) currentSettings.mask = p.split('=')[1];
                else if (p.startsWith('salinity=')) currentSettings.salinity = p.split('=')[1];
                else if (p.startsWith('max_distance=')) currentSettings.maxDist = p.split('=')[1];
                else if (p.startsWith('sound_speed=')) currentSettings.soundSpeed = p.split('=')[1];
                else if (p.startsWith('antenna_x=')) currentSettings.offsX = p.split('=')[1];
                else if (p.startsWith('antenna_y=')) currentSettings.offsY = p.split('=')[1];
                else if (p.startsWith('antenna_phi=')) currentSettings.offsPhi = p.split('=')[1];
                else if (p.startsWith('device_type=')) currentMode = p.split('=')[1].toLowerCase();
                else if (p.startsWith('serial_number=')) systemInfo.serialNumber = p.split('=')[1];
            }
            updateSystemInfo();
        }
        else if (cmdId === 'DET?') {
            const id = parts.find(p => p.startsWith('id='))?.split('=')[1];
            const detected = parts.find(p => p.startsWith('detected='))?.split('=')[1];
            if (!systemInfo) systemInfo = {};
            if (id === 'AZM') systemInfo.azimuthDetected = detected === 'TRUE';
            if (id === 'AUX1') systemInfo.aux1Detected = detected === 'TRUE';
            if (id === 'AUX2') systemInfo.aux2Detected = detected === 'TRUE';
        }
        else if (cmdId === 'CAL?') {
            // Поворотная калибровка
            const calData = {};
            for (const p of parts) {
                if (p.startsWith('state=')) calData.state = p.split('=')[1];
                if (p.startsWith('points=')) calData.points = parseInt(p.split('=')[1]) || 0;
                if (p.startsWith('total=')) calData.total = parseInt(p.split('=')[1]) || 0;
                if (p.startsWith('angle=')) calData.angle = parseFloat(p.split('=')[1]) || 0;
            }
            updateCalibrationPanel(calData);

            // Угловая калибровка
            const acalData = {};
            for (const p of parts) {
                if (p.startsWith('acal_state=')) acalData.state = p.split('=')[1];
                if (p.startsWith('acal_collected=')) acalData.collected = parseInt(p.split('=')[1]) || 0;
                if (p.startsWith('acal_total=')) acalData.total = parseInt(p.split('=')[1]) || 0;
                if (p.startsWith('acal_phi=')) acalData.phi = parseFloat(p.split('=')[1]) || 0;
            }
            updateAngularCalibrationPanel(acalData);
        }
        else if (cmdId === 'PORTS') {
            updatePortsInfo(parts);
            const portsData = parts.filter(p => p.startsWith('port'));
            for (const p of portsData) {
                const val = p.substring(p.indexOf('=') + 1);
                const info = val.split('|');
                if (info.length >= 3) {
                    const id = info[0];
                    const portName = info[1] || 'AUTO';
                    const status = info[2] || 'Inactive';
                    if (portStatuses[id]) {
                        portStatuses[id].port = portName === 'N/A' ? 'AUTO' : (portName || 'AUTO');
                        portStatuses[id].status = status;
                    }
                }
            }
        }
        else if (cmdId === 'OCON') {
            systemInfo.connectionActive = true;
            requestSystemInfo();
        }
        else if (cmdId === 'CCON') {
            systemInfo.connectionActive = false;
            systemInfo.interrogationActive = false;
            requestSystemInfo();
        }
        else if (cmdId === 'PITG') {
            systemInfo.interrogationActive = false;
            updateControlButtons();
        }
        else if (cmdId === 'RITG') {
            systemInfo.interrogationActive = true;
            updateControlButtons();
        }

        updateControlButtons();
    } else {
        const msgPart = parts.find(p => p.startsWith('msg='));
        const msg = msgPart ? msgPart.substring(4) : 'unknown error';
        showCommandStatus(`${cmdId}: ${msg}`, 'error');
    }
}

function updateSystemInfoFromStats(parts) {
    if (!systemInfo) systemInfo = {};
    for (const p of parts) {
        if (p.startsWith('azm_status=')) systemInfo.azmStatus = p.split('=')[1];
        if (p.startsWith('interrogation=')) systemInfo.interrogationActive = p.split('=')[1] === 'True';
        if (p.startsWith('position_valid=')) systemInfo.positionValid = p.split('=')[1] === 'True';
    }
    updateControlButtons();
}

// ========== ОТПРАВКА КОМАНД ==========

function sendCommand(command, params = {}) {
    if (commandInProgress) {
        showCommandStatus(i18n.t('wait'), 'info');
        return;
    }

    commandInProgress = true;

    let line = command;
    const paramParts = Object.entries(params)
        .filter(([_, v]) => v !== null && v !== undefined && v !== '')
        .map(([k, v]) => `${k}=${v}`);
    if (paramParts.length > 0) {
        line += ',' + paramParts.join(',');
    }

    showCommandStatus(`${i18n.t('sending')} ${line}...`, 'info');

    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(line);
        addLogEntry(`>> ${line}`);
    } else {
        showCommandStatus('WebSocket not connected', 'error');
        commandInProgress = false;
    }
}

function sendRawCommand() {
    const input = document.getElementById('cmd-input');
    const line = input.value.trim();
    if (!line) return;

    // История через автодополнение
    if (typeof CommandAutocomplete !== 'undefined') {
        CommandAutocomplete.addToHistory(line);
    } else {
        addToHistory(line);
    }

    commandInProgress = true;
    showCommandStatus(`${i18n.t('sending')} ${line}...`, 'info');

    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(line);
        addLogEntry(`>> ${line}`);
    }
    input.value = '';

    // Закрыть подсказки
    if (typeof CommandAutocomplete !== 'undefined') {
        CommandAutocomplete.removeDropdown();
    }
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

function addToHistory(line) {
    cmdHistory.unshift(line);
    if (cmdHistory.length > 20) cmdHistory.pop();

    const histDiv = document.getElementById('cmd-history');
    if (histDiv) {
        histDiv.innerHTML = cmdHistory.slice(0, 5).map(c =>
            `<span style="cursor:pointer; margin-right:8px;" onclick="document.getElementById('cmd-input').value='${c.replace(/'/g, "\\'")}'">${c}</span>`
        ).join('');
    }
}

// ========== КНОПКИ УПРАВЛЕНИЯ ==========

function toggleConnection() {
    if (!systemInfo) return;
    const command = systemInfo.connectionActive ? 'CCON' : 'OCON';
    sendCommand(command);
}

function toggleInterrogation() {
    if (!systemInfo) return;
    const command = systemInfo.interrogationActive ? 'PITG' : 'RITG';
    sendCommand(command);
}

function updateControlButtons() {
    const btnConnection = document.getElementById('btn-connection');
    const btnInterrogation = document.getElementById('btn-interrogation');
    const connText = document.getElementById('connection-btn-text');
    const interText = document.getElementById('interrogation-btn-text');

    // Кнопка соединения — всегда enabled
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

    // Кнопка опроса — enabled только когда соединение активно
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
    if (!confirm(i18n.t('confirmCalibration') || 'Start calibration?')) return;
    sendCommand('SCAL', { start: '0', step: '15', n: '20' });
}

function stopCalibration() {
    sendCommand('FCAL');
}

function updateCalibrationPanel(calData) {
    const panel = document.getElementById('calibration-panel');
    if (!panel) return;

    // Показываем только если RDT подключен
    if (!portStatuses.rdt || portStatuses.rdt.status !== 'Detected') {
        panel.style.display = 'none';
        return;
    }

    // RDT подключен — панель видна всегда
    panel.style.display = 'block';

    // Обновляем содержимое если есть данные
    const state = calData?.state || 'Idle';

    const stateEl = document.getElementById('cal-state');
    if (stateEl) {
        stateEl.textContent = state;
        stateEl.className = 'cal-state ' + state;
        switch (state) {
            case 'Measuring': stateEl.style.color = '#4caf50'; break;
            case 'Moving': stateEl.style.color = '#ff9800'; break;
            case 'Completed': stateEl.style.color = '#2196f3'; break;
            case 'Failed': stateEl.style.color = '#f44336'; break;
            default: stateEl.style.color = '#aaa'; break;
        }
    }

    const pointsEl = document.getElementById('cal-points');
    if (pointsEl) {
        pointsEl.textContent = `${calData?.points || 0} / ${calData?.total || '?'}`;
    }

    const angleEl = document.getElementById('cal-current-angle');
    if (angleEl) {
        const angle = calData?.angle;
        if (angle !== null && angle !== undefined && !isNaN(angle)) {
            angleEl.textContent = parseFloat(angle).toFixed(1) + '°';
        } else {
            angleEl.textContent = '--';
        }
    }
}

function updateAngularCalibrationPanel(acalData) {
    const panel = document.getElementById('angular-calibration-panel');
    if (!panel) return;

    const state = acalData?.state || 'idle';
    if (state === 'idle' && !acalData?.phi) {
        panel.style.display = 'none';
        return;
    }
    panel.style.display = 'block';

    const stateEl = document.getElementById('acal-state');
    if (stateEl) {
        const stateLabels = {
            'collecting': i18n.t('angCalCollecting'),
            'completed': i18n.t('angCalCompleted'),
            'idle': i18n.t('angCalIdle')
        };
        stateEl.textContent = stateLabels[state] || state;
        switch (state) {
            case 'collecting': stateEl.style.color = '#4caf50'; break;
            case 'completed': stateEl.style.color = '#2196f3'; break;
            default: stateEl.style.color = '#aaa'; break;
        }
    }

    const pointsEl = document.getElementById('acal-points');
    if (pointsEl) {
        pointsEl.textContent = `${acalData?.collected || 0} / ${acalData?.total || '?'}`;
    }

    const phiEl = document.getElementById('acal-phi');
    if (phiEl) {
        if (acalData?.state === 'completed' && acalData?.phi) {
            phiEl.textContent = 'φ = ' + parseFloat(acalData.phi).toFixed(1) + '°';
            phiEl.style.color = '#4ec9b0';
        } else {
            phiEl.textContent = '--';
            phiEl.style.color = '#aaa';
        }
    }
}

function updatePortsInfo(parts) {
    const panel = document.getElementById('ports-info');
    if (!panel) return;

    let html = '<div style="font-weight: bold; margin-bottom: 4px; color: #4a90e2;">🔌 PORTS</div>';
    const ports = parts.filter(p => p.startsWith('port'));

    if (ports.length === 0) {
        panel.style.display = 'none';
        return;
    }

    for (const p of ports) {
        const val = p.substring(p.indexOf('=') + 1);
        const info = val.split('|');
        if (info.length >= 2) {
            const id = info[0];
            const portName = info[1] || '?';
            const status = info[2] || '?';

            // Обновляем portStatuses для формы настроек
            if (portStatuses[id]) {
                portStatuses[id].port = (portName === 'N/A' || portName === '?') ? 'AUTO' : portName;
                portStatuses[id].status = status;
            }

            const icon = status === 'Detected' ? '✓' : status === 'Active' ? '↻' : '○';
            const color = status === 'Detected' ? '#4caf50' : status === 'Active' ? '#ff9800' : '#999';
            html += `<div style="color: ${color};">${icon} ${id}: ${portName}</div>`;
        }
    }

    panel.innerHTML = html;
    panel.style.display = 'block';
}

// ========== ОТРИСОВКА ==========

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
            const age = (beacon.dataAge !== undefined && beacon.dataAge !== null) ? beacon.dataAge : 0;

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

            let label = '';
            if (coordType === 'absolute') {
                label = `ABS ${beacon.absoluteDistance.toFixed(0)}m`;
            } else if (coordType === 'cartesian') {
                label = `${beacon.x?.toFixed(0) || '?'}, ${beacon.y?.toFixed(0) || '?'}`;
            } else if (coordType === 'polar') {
                label = `${beacon.distance.toFixed(0)}m`;
            }

            if (label) {
                ctx.font = '10px Arial';
                ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
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
        ? localDevice.dataAge : 0;

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
            ctx.fillStyle = '#ff4444';
            ctx.font = '10px Arial';
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
            ctx.fillStyle = '#44ff44';
            ctx.font = '10px Arial';
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

    if (rawMeters < 5) { displayMeters = 5; displayWidth = 5 * scale; }
    else if (rawMeters < 10) { displayMeters = 10; displayWidth = 10 * scale; }
    else if (rawMeters < 25) { displayMeters = 25; displayWidth = 25 * scale; }
    else if (rawMeters < 50) { displayMeters = 50; displayWidth = 50 * scale; }
    else if (rawMeters < 100) { displayMeters = 100; displayWidth = 100 * scale; }
    else if (rawMeters < 250) { displayMeters = 250; displayWidth = 250 * scale; }
    else if (rawMeters < 500) { displayMeters = 500; displayWidth = 500 * scale; }
    else if (rawMeters < 1000) { displayMeters = 1000; displayWidth = 1000 * scale; }
    else { displayMeters = Math.round(rawMeters / 1000) * 1000; displayWidth = displayMeters * scale; }

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

    let label = displayMeters >= 1000
        ? `${(displayMeters / 1000).toFixed(1)} km`
        : `${Math.round(displayMeters)} m`;

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

    if (rangeX > 0) { minX -= rangeX * padding; maxX += rangeX * padding; }
    else { minX -= 50; maxX += 50; }

    if (rangeY > 0) { minY -= rangeY * padding; maxY += rangeY * padding; }
    else { minY -= 50; maxY += 50; }

    const scaleX = canvas.width / (maxX - minX);
    const scaleY = canvas.height / (maxY - minY);
    scale = Math.min(scaleX, scaleY);

    offsetX = canvas.width / 2 - ((minX + maxX) / 2) * scale;
    offsetY = canvas.height / 2 + ((minY + maxY) / 2) * scale;

    drawMap();
}

function zoomIn() { scale *= 1.2; drawMap(); }
function zoomOut() { scale /= 1.2; drawMap(); }

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
    if (portsTimer) clearInterval(portsTimer);
    if (ws) ws.close();
    if (reconnectTimer) clearTimeout(reconnectTimer);
});

function updateSystemInfo() {
    const panel = document.getElementById('system-info');
    if (!panel) return;

    let html = '';

    const connectionIcon = systemInfo?.connectionActive ? '🟢' : '🔴';
    const interrogIcon = systemInfo?.interrogationActive ? '🔵' : '⚪';

    html += `<div class="sys-header">`;
    html += `<strong>${currentMode.toUpperCase()}</strong> `;
    if (systemInfo?.serialNumber) {
        html += `<div style="font-size:9px; color:#888;">S/N: ${systemInfo.serialNumber}</div>`;
    }

    const localAge = localDevice?.dataAge ?? 0;
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

    if (localDevice) {
        function addRow(label, value, unit = '', color = '#fff') {
            if (value !== undefined && value !== null && !isNaN(value)) {
                return `<div class="sys-detail" style="display: flex; justify-content: space-between;">
                    <span style="color: #aaa;">${label}:</span>
                    <span style="color: ${color};">${value.toFixed ? value.toFixed(1) : value}${unit}</span>
                </div>`;
            }
            return '';
        }

        // Позиция (LBL)
        if (localDevice.x !== undefined || localDevice.y !== undefined) {
            html += `<div class="sys-section" style="margin-top: 6px; color: #4a90e2; font-weight: bold; font-size: 9px;">${i18n.t('position')}</div>`;
            html += addRow('X', localDevice.x, 'm');
            html += addRow('Y', localDevice.y, 'm');
            html += addRow('Z', localDevice.z, 'm');
        }

        // Ориентация
        if (localDevice.heading !== undefined || localDevice.course !== undefined) {
            html += `<div class="sys-section" style="margin-top: 6px; color: #4a90e2; font-weight: bold; font-size: 9px;">${i18n.t('orientation')}</div>`;
            html += addRow(i18n.t('heading'), localDevice.heading, '°', '#ff4444');
            html += addRow(i18n.t('course'), localDevice.course, '°', '#44ff44');
            html += addRow(i18n.t('speed'), localDevice.speed, 'm/s');
            html += addRow(i18n.t('pitch'), localDevice.pitch, '°', '#aaa');
            html += addRow(i18n.t('roll'), localDevice.roll, '°', '#aaa');
        }

        // Среда (всегда, если есть данные)
        if (localDevice.temperature !== undefined || localDevice.pressure !== undefined || localDevice.depth !== undefined) {
            html += `<div class="sys-section" style="margin-top: 6px; color: #4a90e2; font-weight: bold; font-size: 9px;">${i18n.t('environment')}</div>`;
            html += addRow(i18n.t('temperature'), localDevice.temperature, '°C', '#ffa500');
            html += addRow(i18n.t('pressure'), localDevice.pressure, 'mBar', '#aaa');
            html += addRow(i18n.t('depth'), localDevice.depth, 'm', '#4a90e2');
        }
    }

    html += `<div class="sys-expand-hint">${i18n.t('expandHint')}</div>`;
    panel.innerHTML = html;

    updateControlButtons();
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
        const address = (b.address !== undefined && b.address !== null) ? (b.address + 1) : '?';
        const dataAge = (b.dataAge !== undefined && b.dataAge !== null) ? b.dataAge : 0;

        let details = [];
        let status = 'active', statusClass = 'status-active', statusText = i18n.t('active');

        // Если опрос остановлен — всё на паузе
        if (systemInfo && !systemInfo.interrogationActive) {
            status = 'warning';
            statusClass = 'status-warning';
            statusText = i18n.t('pause');
        } else if (b.isTimeout) {
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
                let displayValue = precision !== null ? value.toFixed(precision) : value;
                details.push(`<div class="beacon-detail"><span class="detail-label">${label}:</span> <span class="detail-value">${displayValue}${unit}</span></div>`);
            }
        }

        if (b.absoluteDistance !== undefined && b.absoluteAzimuth !== undefined &&
            !isNaN(b.absoluteDistance) && !isNaN(b.absoluteAzimuth)) {
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
        addDetail(i18n.t('proptime'), b.pTime, ' s', 4);

        if (b.lat !== undefined && b.lon !== undefined && !isNaN(b.lat) && !isNaN(b.lon)) {
            addDetail(i18n.t('lat'), b.lat, i18n.t('degree'), 6);
            addDetail(i18n.t('lon'), b.lon, i18n.t('degree'), 6);
        }

        if (b.message) {
            details.push(`<div class="beacon-detail"><span class="detail-label">Msg:</span> <span class="detail-value">${b.message}</span></div>`);
        }

        let ageColor = b.isTimeout ? '#dc3545' : dataAge > 5 ? '#ffc107' : '#28a745';
        if (dataAge > 10) ageColor = '#dc3545';
        details.push(`<div class="beacon-detail"><span class="detail-label">${i18n.t('dataAge')}:</span> <span class="detail-value" style="color: ${ageColor}">${dataAge.toFixed(1)}${i18n.t('second')}</span></div>`);

        html += `
            <div class="beacon-item ${status}">
                <div class="beacon-header">
                    <span class="beacon-address">#${address}</span>
                    <span class="beacon-status ${statusClass}">${statusText}</span>
                </div>
                <div class="beacon-details">${details.join('')}</div>
            </div>`;
    });

    list.innerHTML = html;
}



function validateField(id, min, max, name) {
    const el = document.getElementById(id);
    if (!el || el.value === '') return true;
    const val = parseFloat(el.value);
    if (isNaN(val)) {
        showCommandStatus(`${name}: invalid number`, 'error');
        el.focus();
        return false;
    }
    if (val < min || val > max) {
        showCommandStatus(`${name}: must be ${min}..${max}`, 'error');
        el.focus();
        return false;
    }
    return true;
}

function validateAll() {
    const checks = [
        ['cfg-msk', 0, 65535, 'Mask'],
        ['cfg-sln', 0, 40, 'Salinity'],
        ['cfg-mdst', 500, 5500, 'Max Distance'],
        ['cfg-sos', 1300, 1800, 'Sound Speed'],
        ['cfg-ofsx', -100, 100, 'Offset X'],
        ['cfg-ofsy', -100, 100, 'Offset Y'],
        ['cfg-ofsphi', 0, 360, 'Offset φ'],
        ['cfg-lat', -90, 90, 'Latitude'],
        ['cfg-lon', -180, 180, 'Longitude'],
        ['cfg-hdg', 0, 360, 'Heading'],
    ];
    for (const [id, min, max, name] of checks) {
        if (!validateField(id, min, max, name)) return false;
    }
    return true;
}


// ========== МОДАЛЬНОЕ ОКНО НАСТРОЕК ==========

function openSettings() {
    document.getElementById('settings-modal').style.display = 'flex';

    // Сначала показать форму с ТЕКУЩИМИ значениями (которые уже есть в currentSettings)
    document.getElementById('tab-content').innerHTML = renderAllSettings();
    fillPortSelectors();

    document.getElementById('settings-title').textContent = i18n.t('settings');
    document.getElementById('btn-apply-all').textContent = i18n.t('applyAll');
    document.getElementById('btn-cancel').textContent = i18n.t('cancel');

    // Запросить свежие данные и ОБНОВИТЬ форму когда придут
    if (ws && ws.readyState === WebSocket.OPEN) {
        requestSettingsAndUpdateForm();
    }
}

function requestSettingsAndUpdateForm() {
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    const originalHandler = ws.onmessage;
    let responseCount = 0;
    const expectedResponses = 2; // STAT + PORTS

    ws.onmessage = function (event) {
        const msg = event.data;

        // Пропускаем широковещательные сообщения (beacons, local device)
        if (typeof msg === 'string' && msg.startsWith('@')) {
            handleAzimuthLine(msg);
            return;
        }

        // Обрабатываем STAT и PORTS
        if (typeof msg === 'string') {
            const line = msg.trim();
            addLogEntry(line);

            if (line.startsWith('STAT,OK') || line.startsWith('PORTS,OK')) {
                handleCommandResponse(line);
                responseCount++;

                if (responseCount >= expectedResponses) {
                    // Все ответы получены — обновляем форму
                    ws.onmessage = originalHandler;
                    document.getElementById('tab-content').innerHTML = renderAllSettings();
                    fillPortSelectors();
                }
            } else if (line.includes(',OK') || line.includes(',ERR')) {
                handleCommandResponse(line);
            }
        }
    };

    ws.send('STAT');
    ws.send('PORTS');

    // Таймаут — восстановить обработчик если ответ не пришёл
    setTimeout(() => {
        if (ws.onmessage !== originalHandler) {
            ws.onmessage = originalHandler;
        }
    }, 3000);
}

function closeSettings() {
    document.getElementById('settings-modal').style.display = 'none';
}

async function applySettings() {
    const commands = [];

    // AZM
    const azmPort = document.getElementById('cfg-azm-port')?.value;
    const azmBaud = document.getElementById('cfg-azm-baud')?.value;
    if (azmPort !== undefined) {
        commands.push({ cmd: 'AZM', params: { port: azmPort || 'AUTO', baud: azmBaud } });
    }

    // AUX1
    const aux1Proto = document.getElementById('cfg-aux1-proto')?.value;
    const aux1Port = document.getElementById('cfg-aux1-port')?.value;
    const aux1Baud = document.getElementById('cfg-aux1-baud')?.value;
    if (aux1Port === 'OFF') {
        commands.push({ cmd: 'AUX1', params: { port: 'OFF' } });
    } else if (aux1Proto) {
        commands.push({ cmd: 'AUX1', params: { proto: aux1Proto, port: aux1Port || 'AUTO', baud: aux1Baud } });
    }

    // AUX2
    const aux2Port = document.getElementById('cfg-aux2-port')?.value;
    const aux2Baud = document.getElementById('cfg-aux2-baud')?.value;
    if (aux2Port === 'OFF') {
        commands.push({ cmd: 'AUX2', params: { port: 'OFF' } });
    } else if (aux2Port !== undefined) {
        commands.push({ cmd: 'AUX2', params: { port: aux2Port || 'AUTO', baud: aux2Baud } });
    }

    // RDT
    const rdtPort = document.getElementById('cfg-rdt-port')?.value;
    const rdtBaud = document.getElementById('cfg-rdt-baud')?.value;
    if (rdtPort === 'OFF') {
        commands.push({ cmd: 'RDT', params: { port: 'OFF' } });
    } else if (rdtPort !== undefined) {
        commands.push({ cmd: 'RDT', params: { port: rdtPort || 'AUTO', baud: rdtBaud } });
    }

    // Output
    const outsPort = document.getElementById('cfg-outs-port')?.value;
    const outsBaud = document.getElementById('cfg-outs-baud')?.value;
    if (outsPort) commands.push({ cmd: 'OUTS', params: { port: outsPort, baud: outsBaud } });

    const outuAddr = document.getElementById('cfg-outu-addr')?.value;
    if (outuAddr) commands.push({ cmd: 'OUTU', params: { addr: outuAddr || 'OFF' } });

    const psimssb = document.getElementById('cfg-psimssb')?.checked;
    commands.push({ cmd: 'PSIMSSB', params: { on: psimssb ? 'TRUE' : 'FALSE' } });

    for (const c of commands) {
        await sendCommandAsync(c.cmd, c.params);
        await sleep(200);
    }

    closeSettings();
}

function renderAllSettings() {
    const t = i18n.t.bind(i18n);

    return `
        <div style="display:flex; flex-direction:column; gap:15px;">
            ${renderPortsSection()}
            ${renderOutputSection()}
            ${renderPositionSection()}
            ${renderTransceiverSection()}
            
            <!-- === СЕРВИСНЫЕ КОМАНДЫ === -->
            <div style="border-top: 1px solid #ddd; padding-top: 12px; display:flex; gap:8px; flex-wrap:wrap;">
                <button onclick="saveAsDefaultSettings()" style="padding:8px 16px; background:#4a90e2; color:#fff; border:none; border-radius:4px; cursor:pointer; font-weight:bold;">
                    💾 ${i18n.t('saveAsDefault')}
                </button>
                <button onclick="resetDefaultSettings()" style="padding:8px 16px; background:#dc3545; color:#fff; border:none; border-radius:4px; cursor:pointer;">
                    🗑 ${i18n.t('resetDefault')}
                </button>
            </div>
            <div id="settings-service-status" style="font-size:11px; color:#666; min-height:20px;"></div>
        </div>`;
}

function renderPortsSection() {
    const t = i18n.t.bind(i18n);
    const azm = portStatuses.azm;
    const aux1 = portStatuses.aux1;
    const aux2 = portStatuses.aux2;
    const rdt = portStatuses.rdt;
    const sel = (val, opt) => val === opt ? 'selected' : '';

    return `
        <details open>
            <summary style="font-weight:bold; color:#4a90e2; cursor:pointer; margin-bottom:8px;">${t('portsTitle')}</summary>
            <div style="display:flex; flex-direction:column; gap:10px; margin-left:10px;">
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:50px; font-weight:bold;">AZM</span>
                    <select id="cfg-azm-port" style="flex:1; padding:4px;"><option value="">AUTO</option></select>
                    <select id="cfg-azm-baud" style="width:100px; padding:4px;">
                        <option value="9600" ${sel(azm.baud, '9600')}>9600</option>
                        <option value="115200" ${sel(azm.baud, '115200')}>115200</option></select>
                </div>
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:50px; font-weight:bold;">AUX1</span>
                    <select id="cfg-aux1-proto" style="width:80px; padding:4px;">
                        <option value="NMEA" ${sel(aux1.proto, 'NMEA')}>NMEA</option>
                        <option value="BP" ${sel(aux1.proto, 'BP')}>BP</option></select>
                    <select id="cfg-aux1-port" style="flex:1; padding:4px;">
                        <option value="">AUTO</option><option value="OFF" ${sel(aux1.port, 'OFF')}>OFF</option></select>
                    <select id="cfg-aux1-baud" style="width:100px; padding:4px;">
                        <option value="9600" ${sel(aux1.baud, '9600')}>9600</option>
                        <option value="115200" ${sel(aux1.baud, '115200')}>115200</option></select>
                </div>
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:50px; font-weight:bold;">AUX2</span>
                    <select id="cfg-aux2-port" style="flex:1; padding:4px;">
                        <option value="">AUTO</option><option value="OFF" ${sel(aux2.port, 'OFF')}>OFF</option></select>
                    <select id="cfg-aux2-baud" style="width:100px; padding:4px;">
                        <option value="9600" ${sel(aux2.baud, '9600')}>9600</option></select>
                </div>
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:50px; font-weight:bold;">RDT</span>
                    <select id="cfg-rdt-port" style="flex:1; padding:4px;">
                        <option value="">AUTO</option><option value="OFF" ${sel(rdt.port, 'OFF')}>OFF</option></select>
                    <select id="cfg-rdt-baud" style="width:100px; padding:4px;">
                        <option value="9600" ${sel(rdt.baud, '9600')}>9600</option></select>
                </div>
            </div>
        </details>`;
}

function renderOutputSection() {
    const t = i18n.t.bind(i18n);
    return `
        <details>
            <summary style="font-weight:bold; color:#4a90e2; cursor:pointer; margin-bottom:8px;">${t('outputTitle')}</summary>
            <div style="display:flex; flex-direction:column; gap:8px; margin-left:10px;">
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:60px;">${t('serialOutput')}:</span>
                    <select id="cfg-outs-port" style="flex:1; padding:4px;"><option value="OFF">OFF</option></select>
                    <select id="cfg-outs-baud" style="width:100px; padding:4px;"><option value="9600">9600</option></select>
                </div>
                <div style="display:flex; gap:8px; align-items:center;">
                    <span style="width:60px;">${t('udpOutput')}:</span>
                    <input type="text" id="cfg-outu-addr" placeholder="255.255.255.255:28128" style="flex:1; padding:4px;">
                </div>
                <label><input type="checkbox" id="cfg-psimssb"> ${t('psimssbOutput')}</label>
            </div>
        </details>`;
}

function renderPositionSection() {
    const t = i18n.t.bind(i18n);
    return `
        <details>
            <summary style="font-weight:bold; color:#4a90e2; cursor:pointer; margin-bottom:8px;">${t('positionTitle')}</summary>
            <div style="display:flex; flex-direction:column; gap:8px; margin-left:10px;">
                <div style="color:#666; font-size:11px;">${t('offsets')}:</div>
                <div style="display:flex; gap:8px;">
                    X:<input type="number" id="cfg-ofsx" value="${currentSettings.offsX || '0.0'}" step="0.01" style="width:70px; padding:4px;">
                    Y:<input type="number" id="cfg-ofsy" value="${currentSettings.offsY || '0.0'}" step="0.01" style="width:70px; padding:4px;">
                    φ°:<input type="number" id="cfg-ofsphi" value="${currentSettings.offsPhi || '0.0'}" step="0.1" style="width:70px; padding:4px;">
                </div>
                <div style="color:#666; font-size:11px; margin-top:4px;">${t('locationOverride')}:</div>
                <div style="font-size:10px; color:#999;">${t('overrideHint')}</div>
                <div style="display:flex; gap:8px;">
                    <input type="number" id="cfg-lat" placeholder="${t('lat')}" step="0.000001" style="flex:1; padding:4px;">
                    <input type="number" id="cfg-lon" placeholder="${t('lon')}" step="0.000001" style="flex:1; padding:4px;">
                    <input type="number" id="cfg-hdg" placeholder="${t('heading')}" min="0" max="360" style="width:100px; padding:4px;">
                </div>
                <button onclick="sendCommand('LHOV',{}); closeSettings();" style="padding:4px 10px; background:#dc3545; color:#fff; border:none; border-radius:4px; width:fit-content;">${t('disableOverride')}</button>
            </div>
        </details>`;
}

function renderTransceiverSection() {
    const t = i18n.t.bind(i18n);
    return `
        <details open>
            <summary style="font-weight:bold; color:#4a90e2; cursor:pointer; margin-bottom:8px;">${t('transceiverTitle')}</summary>
            <div style="display:flex; flex-direction:column; gap:8px; margin-left:10px;">
                <div style="font-size:10px; color:#999;">${t('transceiverHint')}</div>

                <!-- Маска адреса -->
                <div style="display:flex; gap:8px; align-items:flex-start;">
                    <span style="width:50px; font-weight:bold; padding-top:4px;">${t('mask')}:</span>
                    <div style="flex:1;">
                        <input type="number" id="cfg-msk" value="${currentSettings.mask || '1'}"
                               min="0" max="65535" style="width:100px; padding:4px;"
                               onchange="onMaskInputChanged()" oninput="onMaskInputChanged()">
                        <span id="cfg-msk-addrs" style="font-size:10px; color:#4ec9b0; margin-left:8px;"></span>
                        <div id="cfg-addr-checkboxes" style="display:grid; grid-template-columns: repeat(8, 1fr); gap:2px; margin-top:6px; max-width:360px;"></div>
                    </div>
                </div>

                <div style="display:flex; gap:8px;">
                    ${t('salinity')}:<input type="number" id="cfg-sln" value="${currentSettings.salinity || '0.0'}" step="0.1" min="0" max="40" style="width:100px; padding:4px;">
                </div>
                <div style="display:flex; gap:8px;">
                    ${t('maxDist')}:<input type="number" id="cfg-mdst" value="${currentSettings.maxDist || '1000'}" step="10" min="500" max="5500" style="width:100px; padding:4px;">
                    ${t('soundSpeed')}:<input type="number" id="cfg-sos" value="${currentSettings.soundSpeed || ''}" placeholder="Auto" step="0.1" style="width:100px; padding:4px;">
                </div>
                <button onclick="applyTransceiverAndRestart(); return false;" style="padding:6px 12px; background:#4a90e2; color:#fff; border:none; border-radius:4px; width:fit-content; font-weight:bold;">${t('applyRestart')}</button>
            </div>
        </details>`;
}

function fillPortSelectors() {
    const ports = ['COM1', 'COM2', 'COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9', 'COM10'];
    const ids = ['cfg-azm-port', 'cfg-aux1-port', 'cfg-aux2-port', 'cfg-rdt-port', 'cfg-outs-port'];
    ids.forEach(id => {
        const sel = document.getElementById(id);
        if (!sel) return;
        ports.forEach(p => {
            if (!sel.querySelector(`option[value="${p}"]`)) {
                sel.innerHTML += `<option value="${p}">${p}</option>`;
            }
        });
    });

    buildAddressCheckboxes();
}

// ========== ВИЗУАЛЬНЫЙ ВЫБОР МАСКИ АДРЕСА ==========

function buildAddressCheckboxes() {
    const container = document.getElementById('cfg-addr-checkboxes');
    const maskInput = document.getElementById('cfg-msk');
    if (!container || !maskInput) return;

    const currentMask = parseInt(maskInput.value) || 0;

    let html = '';
    for (let addr = 0; addr < 16; addr++) {
        const bit = 1 << addr;
        const checked = (currentMask & bit) !== 0;
        html += `
            <label style="display:flex; align-items:center; gap:2px; font-size:10px; color:#ccc; cursor:pointer; padding:1px 3px; border-radius:3px; ${checked ? 'background:#1e3d5e;' : ''}">
                <input type="checkbox" value="${addr}" ${checked ? 'checked' : ''}
                       onchange="onAddrCheckboxChanged()" style="width:12px; height:12px; cursor:pointer;">
                ${addr}
            </label>`;
    }

    container.innerHTML = html;
    updateMaskAddrsLabel();
}

function onAddrCheckboxChanged() {
    const container = document.getElementById('cfg-addr-checkboxes');
    const maskInput = document.getElementById('cfg-msk');
    if (!container || !maskInput) return;

    const checkboxes = container.querySelectorAll('input[type="checkbox"]');
    let mask = 0;
    checkboxes.forEach(cb => {
        if (cb.checked) {
            mask |= 1 << parseInt(cb.value);
        }
    });

    maskInput.value = mask;
    updateMaskAddrsLabel();
    highlightAddrCheckboxes();
}

function onMaskInputChanged() {
    updateMaskAddrsLabel();
    syncCheckboxesFromMask();
}

function syncCheckboxesFromMask() {
    const container = document.getElementById('cfg-addr-checkboxes');
    const maskInput = document.getElementById('cfg-msk');
    if (!container || !maskInput) return;

    const mask = parseInt(maskInput.value) || 0;
    const checkboxes = container.querySelectorAll('input[type="checkbox"]');

    checkboxes.forEach(cb => {
        const addr = parseInt(cb.value);
        const bit = 1 << addr;
        cb.checked = (mask & bit) !== 0;
    });

    highlightAddrCheckboxes();
}

function highlightAddrCheckboxes() {
    const container = document.getElementById('cfg-addr-checkboxes');
    if (!container) return;

    const labels = container.querySelectorAll('label');
    labels.forEach(label => {
        const cb = label.querySelector('input[type="checkbox"]');
        if (cb && cb.checked) {
            label.style.background = '#1e3d5e';
            label.style.color = '#4ec9b0';
        } else {
            label.style.background = 'transparent';
            label.style.color = '#ccc';
        }
    });
}

function updateMaskAddrsLabel() {
    const maskInput = document.getElementById('cfg-msk');
    const label = document.getElementById('cfg-msk-addrs');
    if (!maskInput || !label) return;

    const mask = parseInt(maskInput.value) || 0;
    if (mask === 0) {
        label.textContent = i18n.t('noAddresses');
        return;
    }

    const addrs = [];
    for (let addr = 0; addr < 16; addr++) {
        if (mask & (1 << addr)) {
            addrs.push(addr);
        }
    }

    label.textContent = addrs.length <= 6
        ? i18n.t('addresses') + addrs.join(', ')
        : i18n.t('addressesCount') + addrs.length;
}

// ========== ОТПРАВКА НАСТРОЕК ==========

async function applySettings() {
    const commands = [];

    // AZM
    const azmPort = document.getElementById('cfg-azm-port')?.value;
    const azmBaud = document.getElementById('cfg-azm-baud')?.value;
    if (azmPort !== undefined) {
        commands.push({ cmd: 'AZM', params: { port: azmPort || 'AUTO', baud: azmBaud } });
    }

    // AUX1
    const aux1Proto = document.getElementById('cfg-aux1-proto')?.value;
    const aux1Port = document.getElementById('cfg-aux1-port')?.value;
    const aux1Baud = document.getElementById('cfg-aux1-baud')?.value;
    if (aux1Port === 'OFF') {
        commands.push({ cmd: 'AUX1', params: { port: 'OFF' } });
    } else if (aux1Proto) {
        commands.push({ cmd: 'AUX1', params: { proto: aux1Proto, port: aux1Port || 'AUTO', baud: aux1Baud } });
    }

    // AUX2
    const aux2Port = document.getElementById('cfg-aux2-port')?.value;
    const aux2Baud = document.getElementById('cfg-aux2-baud')?.value;
    if (aux2Port === 'OFF') {
        commands.push({ cmd: 'AUX2', params: { port: 'OFF' } });
    } else if (aux2Port !== undefined) {
        commands.push({ cmd: 'AUX2', params: { port: aux2Port || 'AUTO', baud: aux2Baud } });
    }

    // RDT
    const rdtPort = document.getElementById('cfg-rdt-port')?.value;
    const rdtBaud = document.getElementById('cfg-rdt-baud')?.value;
    if (rdtPort === 'OFF') {
        commands.push({ cmd: 'RDT', params: { port: 'OFF' } });
    } else if (rdtPort !== undefined) {
        commands.push({ cmd: 'RDT', params: { port: rdtPort || 'AUTO', baud: rdtBaud } });
    }

    // Output
    const outsPort = document.getElementById('cfg-outs-port')?.value;
    const outsBaud = document.getElementById('cfg-outs-baud')?.value;
    if (outsPort) commands.push({ cmd: 'OUTS', params: { port: outsPort, baud: outsBaud } });

    const outuAddr = document.getElementById('cfg-outu-addr')?.value;
    if (outuAddr) commands.push({ cmd: 'OUTU', params: { addr: outuAddr || 'OFF' } });

    const psimssb = document.getElementById('cfg-psimssb')?.checked;
    commands.push({ cmd: 'PSIMSSB', params: { on: psimssb ? 'TRUE' : 'FALSE' } });

    for (const c of commands) {
        await sendCommandAsync(c.cmd, c.params);
        await sleep(200);
    }

    closeSettings();
}

async function applyTransceiverAndRestart() {
    const msk = document.getElementById('cfg-msk')?.value;
    const sln = document.getElementById('cfg-sln')?.value;
    const mdst = document.getElementById('cfg-mdst')?.value;
    const sos = document.getElementById('cfg-sos')?.value;

    if (msk) await sendCommandAsync('MSK', { mask: msk });
    if (sln) await sendCommandAsync('SLN', { val: sln });
    if (mdst) await sendCommandAsync('MDST', { val: mdst });
    if (sos) await sendCommandAsync('SOS', { val: sos });
    await sendCommandAsync('RITG', {});

    showCommandStatus('Transceiver updated & restarted', 'success');
    closeSettings();
}

async function saveAsDefaultSettings() {
    if (!confirm(i18n.t('saveInitConfirm'))) return;

    const statusEl = document.getElementById('settings-service-status');
    statusEl.textContent = i18n.t('savingInit');
    statusEl.style.color = '#4a90e2';

    try {
        await sendCommandAsync('SAVEINIT', {});
        statusEl.textContent = i18n.t('initSaved');
        statusEl.style.color = '#28a745';
    } catch (e) {
        statusEl.textContent = i18n.t('saveFailed');
        statusEl.style.color = '#dc3545';
    }

    setTimeout(() => { statusEl.textContent = ''; }, 5000);
}

async function resetDefaultSettings() {
    if (!confirm(i18n.t('resetInitConfirm'))) return;

    const statusEl = document.getElementById('settings-service-status');
    statusEl.textContent = i18n.t('deletingInit');
    statusEl.style.color = '#ffc107';

    try {
        await sendCommandAsync('RESETINIT', {});
        statusEl.textContent = i18n.t('initDeleted');
        statusEl.style.color = '#28a745';
    } catch (e) {
        statusEl.textContent = i18n.t('deleteFailed');
        statusEl.style.color = '#dc3545';
    }

    setTimeout(() => { statusEl.textContent = ''; }, 5000);
}

function sendCommandAsync(command, params = {}) {
    return new Promise((resolve) => {
        const originalHandler = ws.onmessage;

        ws.onmessage = function (event) {
            const line = event.data.trim();
            addLogEntry(line);

            if (line.startsWith(command) && (line.includes(',OK') || line.includes(',ERR'))) {
                handleCommandResponse(line);
                ws.onmessage = originalHandler;
                resolve();
            } else if (line.startsWith('@')) {
                handleAzimuthLine(line);
            }
        };

        let line = command;
        const paramParts = Object.entries(params)
            .filter(([_, v]) => v !== null && v !== undefined && v !== '')
            .map(([k, v]) => `${k}=${v}`);
        if (paramParts.length > 0) line += ',' + paramParts.join(',');

        addLogEntry(`>> ${line}`);
        showCommandStatus(`${i18n.t('sending')} ${line}...`, 'info');
        ws.send(line);

        setTimeout(() => {
            ws.onmessage = originalHandler;
            resolve();
        }, 5000);
    });
}

function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}



// ========== INTELLISENSE ДЛЯ КОМАНДНОЙ СТРОКИ ==========

function initCommandAutocomplete() {
    const input = document.getElementById('cmd-input');
    if (!input) return;

    loadCmdHistory();
    let historyIndex = -1;

    // Подсказки при вводе
    input.addEventListener('input', () => {
        showCommandSuggestions(input);
    });

    // Клавиатура
    input.addEventListener('keydown', (e) => {
        const dropdown = document.getElementById('cmd-autocomplete');
        const visible = dropdown && dropdown.style.display !== 'none';

        switch (e.key) {
            case 'ArrowUp':
                e.preventDefault();
                if (visible) {
                    navigateDropdown(-1, input);
                } else {
                    const cmd = navigateCmdHistory(input, 1);
                    if (cmd !== null) input.value = cmd;
                }
                break;

            case 'ArrowDown':
                e.preventDefault();
                if (visible) {
                    navigateDropdown(1, input);
                } else {
                    const cmd = navigateCmdHistory(input, -1);
                    if (cmd !== null) input.value = cmd;
                }
                break;

            case 'Tab':
                if (visible) {
                    e.preventDefault();
                    applyActiveSuggestion(input);
                }
                break;

            case 'Escape':
                removeCommandDropdown();
                break;

            case 'Enter':
                if (visible) {
                    const active = dropdown.querySelector('.ac-active');
                    if (active) {
                        e.preventDefault();
                        applyActiveSuggestion(input);
                        return;
                    }
                }
                // Enter — сохраняем в историю
                const cmd = input.value.trim();
                if (cmd) saveCmdToHistory(cmd);
                removeCommandDropdown();
                break;
        }
    });

    // Фокус
    input.addEventListener('focus', () => {
        if (input.value.trim()) showCommandSuggestions(input);
    });

    // Клик мимо
    document.addEventListener('click', (e) => {
        if (e.target !== input && !e.target.closest('#cmd-autocomplete')) {
            removeCommandDropdown();
        }
    });

    console.log('[Autocomplete] Initialized');
}

// === История ===

function loadCmdHistory() {
    try {
        const saved = localStorage.getItem('cmdHistory');
        if (saved) cmdHistory = JSON.parse(saved);
    } catch (e) {
        cmdHistory = [];
    }
}

function saveCmdToHistory(cmd) {
    cmd = cmd.trim();
    if (!cmd) return;
    if (cmdHistory.length > 0 && cmdHistory[0] === cmd) return;
    cmdHistory.unshift(cmd);
    if (cmdHistory.length > 100) cmdHistory.pop();
    try {
        localStorage.setItem('cmdHistory', JSON.stringify(cmdHistory));
    } catch (e) { /* ignore */ }
}

function navigateCmdHistory(input, direction) {
    // direction: +1 = вверх (старее), -1 = вниз (новее)
    // используем замыкание через input.dataset
    let idx = parseInt(input.dataset.histIndex || '-1');

    const newIdx = idx + direction;
    if (newIdx < -1) {
        input.dataset.histIndex = '-1';
        return '';
    }
    if (newIdx >= cmdHistory.length) {
        input.dataset.histIndex = String(cmdHistory.length - 1);
        return cmdHistory[cmdHistory.length - 1];
    }

    input.dataset.histIndex = String(newIdx);
    if (newIdx === -1) return '';
    return cmdHistory[newIdx];
}

// === Выпадающий список ===

function getOrCreateDropdown() {
    let dd = document.getElementById('cmd-autocomplete');
    if (dd) return dd;

    dd = document.createElement('div');
    dd.id = 'cmd-autocomplete';
    // Позиционируем относительно всего document, а не command-panel
    dd.style.cssText = `
        position: fixed;
        z-index: 2000;
        font-family: Consolas, monospace;
        font-size: 11px;
        background: #1a1a1a;
        border: 1px solid #4a90e2;
        border-radius: 0 0 6px 6px;
        max-height: 180px;
        max-width: 400px;
        overflow-y: auto;
        overflow-x: hidden;
        display: none;
    `;
    document.body.appendChild(dd);

    // Стили для элементов (один раз)
    if (!document.getElementById('ac-styles')) {
        const st = document.createElement('style');
        st.id = 'ac-styles';
        st.textContent = `
        .ac-item {
            padding: 5px 10px;
            cursor: pointer;
            color: #e0e0e0;
            border-bottom: 1px solid #2a2a2a;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }
        .ac-item:hover {
            background: #1e3d5e;
        }
        .ac-item.ac-active {
            background: #4a90e2;
            color: #fff;
            font-weight: bold;
        }
        .ac-param {
            color: #4ec9b0;
        }
        .ac-item.ac-active .ac-param {
            color: #b5f0e0;
        }
        .ac-desc {
            color: #999;
            font-size: 10px;
            margin-left: 8px;
            opacity: 0.8;
        }
        .ac-item.ac-active .ac-desc {
            color: #d0e8ff;
            opacity: 1;
        }
    `;
        document.head.appendChild(st);
    }

    return dd;
}

function removeCommandDropdown() {
    const dd = document.getElementById('cmd-autocomplete');
    if (dd) {
        dd.style.display = 'none';
    }
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function showCommandSuggestions(input) {
    const dropdown = getOrCreateDropdown();
    const val = input.value.trim();

    if (!val || !commandSchema) {
        dropdown.style.display = 'none';
        return;
    }

    const commaIdx = val.indexOf(',');
    let items = [];
    let isCommand = false;

    if (commaIdx === -1) {
        // Автодополнение имени команды
        isCommand = true;
        const upper = val.toUpperCase();
        const commands = commandSchema.commands.map(c => c.id);
        const matches = commands.filter(c => c.startsWith(upper));

        items = matches.map(cmdId => {
            const cmd = commandSchema.commands.find(c => c.id === cmdId);
            const desc = cmd ? cmd.description : '';
            return {
                text: cmdId,
                html: `${cmdId}<span class="ac-desc">${escapeHtml(desc)}</span>`
            };
        });
    } else if (commaIdx === val.length - 1) {
        const cmdPart = val.substring(0, commaIdx);
        const cmd = commandSchema.commands.find(c => c.id === cmdPart.toUpperCase());
        if (cmd && cmd.paramsParsed) {
            items = cmd.paramsParsed.map(p => ({
                text: p.name + '=',
                html: `<span class="ac-param">${p.name}</span>= <span class="ac-desc">${p.type}</span>`
            }));
        }
    } else {
        const cmdPart = val.substring(0, commaIdx);
        const paramPart = val.substring(commaIdx + 1);

        const cmd = commandSchema.commands.find(c => c.id === cmdPart.toUpperCase());
        if (!cmd || !cmd.paramsParsed) {
            dropdown.style.display = 'none';
            return;
        }

        const lastComma = paramPart.lastIndexOf(',');
        const currentParam = lastComma >= 0
            ? paramPart.substring(lastComma + 1).trim()
            : paramPart.trim();

        const usedParams = new Set(
            paramPart.split(',').map(p => p.split('=')[0].trim().toLowerCase()).filter(Boolean)
        );

        const available = cmd.paramsParsed.filter(p => !usedParams.has(p.name.toLowerCase()));
        const upperParam = currentParam.toUpperCase();

        const matching = currentParam === ''
            ? available
            : available.filter(p => p.name.toUpperCase().startsWith(upperParam));

        items = matching.map(p => ({
            text: p.name + '=',
            html: `<span class="ac-param">${p.name}</span>= <span class="ac-desc">${p.type}</span>`
        }));
    }

    if (items.length === 0) {
        dropdown.style.display = 'none';
        return;
    }

    let activeIdx = parseInt(dropdown.dataset.activeIdx || '0');
    if (activeIdx >= items.length) activeIdx = 0;
    dropdown.dataset.activeIdx = String(activeIdx);

    dropdown.innerHTML = items.map((item, i) => `
        <div class="ac-item ${i === activeIdx ? 'ac-active' : ''}"
             data-index="${i}" data-text="${escapeHtml(item.text)}">
            ${item.html}
        </div>
    `).join('');

    // === ПОЗИЦИОНИРУЕМ ПОД INPUT'ОМ ===
    const rect = input.getBoundingClientRect();
    dropdown.style.top = (rect.bottom + 2) + 'px';
    dropdown.style.left = rect.left + 'px';
    dropdown.style.width = rect.width + 'px';

    dropdown.style.display = 'block';

    // Клик — применить
    dropdown.querySelectorAll('.ac-item').forEach(el => {
        el.addEventListener('mousedown', (e) => {
            e.preventDefault();
            const text = el.dataset.text;
            applySuggestionText(input, text, isCommand);
            removeCommandDropdown();
        });
    });
}
function navigateDropdown(direction, input) {
    const dropdown = document.getElementById('cmd-autocomplete');
    if (!dropdown || dropdown.style.display === 'none') return;

    const items = dropdown.querySelectorAll('.ac-item');
    if (items.length === 0) return;

    let idx = parseInt(dropdown.dataset.activeIdx || '0');
    idx += direction;
    if (idx < 0) idx = items.length - 1;
    if (idx >= items.length) idx = 0;

    dropdown.dataset.activeIdx = String(idx);
    items.forEach((el, i) => el.classList.toggle('ac-active', i === idx));

    // Скролл к активному
    items[idx].scrollIntoView({ block: 'nearest' });
}

function applyActiveSuggestion(input) {
    const dropdown = document.getElementById('cmd-autocomplete');
    if (!dropdown || dropdown.style.display === 'none') return;

    const active = dropdown.querySelector('.ac-active');
    if (!active) return;

    const isCommand = !input.value.includes(',');
    applySuggestionText(input, active.dataset.text, isCommand);
    removeCommandDropdown();

    // Сразу показать параметры
    if (isCommand) {
        setTimeout(() => showCommandSuggestions(input), 10);
    }
}

function applySuggestionText(input, text, isCommand) {
    const val = input.value.trim();

    if (isCommand) {
        input.value = text + ',';
    } else {
        const commaIdx = val.indexOf(',');
        if (commaIdx === -1) {
            input.value = val + text;
        } else {
            const base = val.substring(0, commaIdx + 1);
            const rest = val.substring(commaIdx + 1);
            const lastComma = rest.lastIndexOf(',');

            if (lastComma >= 0) {
                input.value = base + rest.substring(0, lastComma + 1) + text;
            } else {
                input.value = base + text;
            }
        }
    }

    input.focus();
    input.setSelectionRange(input.value.length, input.value.length);
}




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
        console.log('==================');
    }
};