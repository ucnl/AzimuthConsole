// autocomplete.js — IntelliSense для командной строки

const CommandAutocomplete = {
    schema: null,
    history: [],
    historyIndex: -1,
    currentSuggestion: 0,
    dropdown: null,
    styleEl: null,

    /* ───────── Загрузка схемы ───────── */

    loadSchema(schemaJson) {
        try {
            this.schema = typeof schemaJson === 'string'
                ? JSON.parse(schemaJson)
                : schemaJson;
            console.log(`[Autocomplete] Loaded ${this.schema.commands.length} commands`);
        } catch (e) {
            console.error('[Autocomplete] Failed to parse schema:', e);
        }
    },

    getCommands() {
        if (!this.schema) return [];
        return this.schema.commands.map(c => c.id);
    },

    getCommand(id) {
        if (!this.schema) return null;
        return this.schema.commands.find(c => c.id === id.toUpperCase());
    },

    getParams(cmdId) {
        const cmd = this.getCommand(cmdId);
        if (!cmd || !cmd.paramsParsed) return [];
        return cmd.paramsParsed;
    },

    /* ───────── Выпадающий список ───────── */

    getOrCreateDropdown() {
        if (this.dropdown) return this.dropdown;

        this.dropdown = document.createElement('div');
        this.dropdown.id = 'cmd-autocomplete';
        this.dropdown.style.cssText = `
            position: absolute;
            top: 100%;
            left: 0;
            right: 0;
            max-height: 180px;
            overflow-y: auto;
            background: #1a1a1a;
            border: 1px solid #4a90e2;
            border-top: none;
            border-radius: 0 0 6px 6px;
            z-index: 2000;
            font-family: Consolas, monospace;
            font-size: 11px;
            display: none;
        `;

        const panel = document.getElementById('command-panel');
        panel.style.position = 'relative';
        panel.appendChild(this.dropdown);

        // Стили для элементов списка (один раз)
        if (!this.styleEl) {
            this.styleEl = document.createElement('style');
            this.styleEl.textContent = `
                .ac-item {
                    padding: 5px 10px;
                    cursor: pointer;
                    color: #ccc;
                    border-bottom: 1px solid #2a2a2a;
                    white-space: nowrap;
                }
                .ac-item:hover { background: #1e3d5e; }
                .ac-item.ac-active { background: #4a90e2; color: #fff; }
                .ac-item .ac-param { color: #4ec9b0; }
                .ac-item .ac-desc {
                    color: #888;
                    font-size: 10px;
                    margin-left: 8px;
                }
            `;
            document.head.appendChild(this.styleEl);
        }

        return this.dropdown;
    },

    removeDropdown() {
        if (this.dropdown) {
            this.dropdown.remove();
            this.dropdown = null;
        }
        this.currentSuggestion = 0;
    },

    /* ───────── Показ подсказок ───────── */

    showSuggestions(input) {
        const dropdown = this.getOrCreateDropdown();
        const val = input.value.trim();

        if (!val) {
            dropdown.style.display = 'none';
            return;
        }

        const commaIdx = val.indexOf(',');
        let items = [];
        let isCommand = false;

        if (commaIdx === -1) {
            // === Автодополнение имени команды ===
            isCommand = true;
            const upper = val.toUpperCase();
            const all = this.getCommands();
            const matches = all.filter(c => c.startsWith(upper));

            // Добавляем описание из схемы
            items = matches.map(cmdId => {
                const cmd = this.getCommand(cmdId);
                const desc = cmd ? cmd.description : '';
                return {
                    text: cmdId,
                    html: `${cmdId}<span class="ac-desc">${this.escapeHtml(desc)}</span>`
                };
            });
        }
        else if (commaIdx === val.length - 1) {
            // Курсор сразу после запятой — показываем все параметры
            const cmdPart = val.substring(0, commaIdx);
            const params = this.getParams(cmdPart);
            items = params.map(p => ({
                text: `${p.name}=`,
                html: `<span class="ac-param">${p.name}</span>= <span class="ac-desc">${p.type}</span>`
            }));
        }
        else {
            // === Автодополнение параметра ===
            const cmdPart = val.substring(0, commaIdx);
            const paramPart = val.substring(commaIdx + 1);

            const lastComma = paramPart.lastIndexOf(',');
            const currentParam = lastComma >= 0
                ? paramPart.substring(lastComma + 1).trim()
                : paramPart.trim();

            const params = this.getParams(cmdPart);
            if (params.length === 0) {
                dropdown.style.display = 'none';
                return;
            }

            // Какие параметры уже использованы
            const usedParams = new Set(
                paramPart.split(',')
                    .map(p => p.split('=')[0].trim().toLowerCase())
                    .filter(Boolean)
            );

            const upperParam = currentParam.toUpperCase();
            const available = params.filter(p => !usedParams.has(p.name.toLowerCase()));

            if (currentParam === '') {
                // Показываем все доступные
                items = available.map(p => ({
                    text: `${p.name}=`,
                    html: `<span class="ac-param">${p.name}</span>= <span class="ac-desc">${p.type}</span>`
                }));
            } else {
                // Фильтруем по вводу
                const matching = available.filter(p =>
                    p.name.toUpperCase().startsWith(upperParam)
                );
                items = matching.map(p => ({
                    text: `${p.name}=`,
                    html: `<span class="ac-param">${p.name}</span>= <span class="ac-desc">${p.type}</span>`
                }));
            }
        }

        if (items.length === 0) {
            dropdown.style.display = 'none';
            return;
        }

        // Ограничиваем currentSuggestion
        this.currentSuggestion = Math.min(this.currentSuggestion, items.length - 1);

        dropdown.innerHTML = items.map((item, i) => `
            <div class="ac-item ${i === this.currentSuggestion ? 'ac-active' : ''}"
                 data-index="${i}"
                 data-text="${this.escapeHtml(item.text)}">
                ${item.html}
            </div>
        `).join('');

        dropdown.style.display = 'block';

        // Обработчики клика
        dropdown.querySelectorAll('.ac-item').forEach(el => {
            el.addEventListener('mousedown', (e) => {
                // mousedown раньше blur на input — не теряем фокус
                e.preventDefault();
                const text = el.dataset.text;
                this.applySuggestion(input, text, isCommand);
                this.removeDropdown();
            });
        });
    },

    escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    },

    /* ───────── Применение подсказки ───────── */

    applySuggestion(input, text, isCommand) {
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
    },

    /* ───────── История команд ───────── */

    addToHistory(cmd) {
        cmd = cmd.trim();
        if (!cmd) return;
        // Не дублируем подряд
        if (this.history.length > 0 && this.history[0] === cmd) return;

        this.history.unshift(cmd);
        if (this.history.length > 100) this.history.pop();
        this.historyIndex = -1;

        try {
            localStorage.setItem('cmdHistory', JSON.stringify(this.history));
        } catch (e) { /* quota exceeded — игнорируем */ }
    },

    loadHistory() {
        try {
            const saved = localStorage.getItem('cmdHistory');
            if (saved) {
                this.history = JSON.parse(saved);
            }
        } catch (e) {
            this.history = [];
        }
    },

    navigateHistory(input, direction) {
        if (this.history.length === 0) return null;

        const newIndex = this.historyIndex + direction;

        if (newIndex < -1) {
            this.historyIndex = -1;
            return '';
        }

        if (newIndex >= this.history.length) {
            this.historyIndex = this.history.length - 1;
            return this.history[this.historyIndex];
        }

        this.historyIndex = newIndex;

        if (this.historyIndex === -1) {
            return '';
        }

        return this.history[this.historyIndex];
    },

    /* ───────── Инициализация ───────── */

    init(inputId) {
        const input = document.getElementById(inputId);
        if (!input) {
            console.warn(`[Autocomplete] Input #${inputId} not found`);
            return;
        }

        this.loadHistory();

        // Ввод текста
        input.addEventListener('input', () => {
            this.showSuggestions(input);
        });

        // Клавиши
        input.addEventListener('keydown', (e) => {
            const dropdown = this.dropdown;
            const dropdownVisible = dropdown && dropdown.style.display !== 'none';

            switch (e.key) {
                case 'ArrowUp':
                    e.preventDefault();
                    if (dropdownVisible) {
                        const items = dropdown.querySelectorAll('.ac-item');
                        if (items.length > 0) {
                            this.currentSuggestion = Math.max(0, this.currentSuggestion - 1);
                            this.showSuggestions(input);
                        }
                    } else {
                        const histCmd = this.navigateHistory(input, 1);
                        if (histCmd !== null) {
                            input.value = histCmd;
                            input.setSelectionRange(input.value.length, input.value.length);
                        }
                    }
                    break;

                case 'ArrowDown':
                    e.preventDefault();
                    if (dropdownVisible) {
                        const items = dropdown.querySelectorAll('.ac-item');
                        if (items.length > 0) {
                            this.currentSuggestion = Math.min(items.length - 1, this.currentSuggestion + 1);
                            this.showSuggestions(input);
                        }
                    } else {
                        const histCmd = this.navigateHistory(input, -1);
                        if (histCmd !== null) {
                            input.value = histCmd;
                            input.setSelectionRange(input.value.length, input.value.length);
                        }
                    }
                    break;

                case 'Tab':
                    if (dropdownVisible) {
                        e.preventDefault();
                        const activeItem = dropdown.querySelector('.ac-active');
                        if (activeItem) {
                            const isCommand = !input.value.includes(',');
                            this.applySuggestion(input, activeItem.dataset.text, isCommand);
                            this.removeDropdown();
                            this.showSuggestions(input); // сразу показать параметры
                        }
                    }
                    break;

                case 'Escape':
                    this.removeDropdown();
                    break;

                case 'Enter':
                    if (dropdownVisible) {
                        const activeItem = dropdown.querySelector('.ac-active');
                        if (activeItem) {
                            e.preventDefault();
                            const isCommand = !input.value.includes(',');
                            this.applySuggestion(input, activeItem.dataset.text, isCommand);
                            this.removeDropdown();
                            return;
                        }
                    }
                    // Enter без активного элемента — отправка, сохраняем в историю
                    const cmd = input.value.trim();
                    if (cmd) {
                        this.addToHistory(cmd);
                    }
                    this.removeDropdown();
                    break;
            }
        });

        // Фокус — показать подсказки
        input.addEventListener('focus', () => {
            if (input.value.trim()) {
                this.showSuggestions(input);
            }
        });

        // Клик мимо — закрыть
        document.addEventListener('click', (e) => {
            if (e.target !== input && !e.target.closest('#cmd-autocomplete')) {
                this.removeDropdown();
            }
        });

        console.log('[Autocomplete] Initialized');
    }
};