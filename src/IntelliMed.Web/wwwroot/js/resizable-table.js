// resizable-table.js
// MIT License - Lightweight vanilla JS for resizable table columns
// Adds drag handles to <th> elements within a table and persists widths in localStorage.

(function () {
    'use strict';

    const STORAGE_PREFIX = 'intellimed-table-cols:';

    function getStorageKey(table) {
        return STORAGE_PREFIX + (table.id || table.dataset.tableKey || 'default');
    }

    function loadWidths(table) {
        try {
            const raw = localStorage.getItem(getStorageKey(table));
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function saveWidths(table, widths) {
        try {
            localStorage.setItem(getStorageKey(table), JSON.stringify(widths));
        } catch (e) {
            // ignore quota errors
        }
    }

    function applyWidths(table, widths) {
        if (!widths) return;
        const ths = table.querySelectorAll('thead th');
        ths.forEach((th, idx) => {
            if (widths[idx]) {
                th.style.width = widths[idx] + 'px';
                th.style.minWidth = widths[idx] + 'px';
            }
        });
    }

    function makeResizable(table) {
        if (!table || table.dataset.resizable === 'true') return;
        table.dataset.resizable = 'true';

        const ths = table.querySelectorAll('thead th');
        ths.forEach((th) => {
            // Skip if column has no-resize class
            if (th.classList.contains('no-resize')) return;

            // Create handle
            const handle = document.createElement('span');
            handle.className = 'col-resize-handle';
            th.style.position = 'relative';
            th.appendChild(handle);

            let startX = 0;
            let startWidth = 0;
            let thRef = null;
            let nextThRef = null;
            let tableRef = null;

            handle.addEventListener('mousedown', (e) => {
                e.preventDefault();
                e.stopPropagation();
                thRef = th;
                tableRef = table;
                startX = e.pageX;
                startWidth = th.offsetWidth;
                const idx = Array.prototype.indexOf.call(ths, th);
                nextThRef = ths[idx + 1] && !ths[idx + 1].classList.contains('no-resize') ? ths[idx + 1] : null;

                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';

                const onMove = (ev) => {
                    const dx = ev.pageX - startX;
                    const newWidth = Math.max(40, startWidth + dx);
                    thRef.style.width = newWidth + 'px';
                    thRef.style.minWidth = newWidth + 'px';
                    if (nextThRef) {
                        const nextWidth = Math.max(40, nextThRef.offsetWidth - dx);
                        nextThRef.style.width = nextWidth + 'px';
                        nextThRef.style.minWidth = nextWidth + 'px';
                    }
                };

                const onUp = () => {
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup', onUp);
                    document.body.style.cursor = '';
                    document.body.style.userSelect = '';

                    // Save widths
                    const widths = Array.from(tableRef.querySelectorAll('thead th')).map(t => t.offsetWidth);
                    saveWidths(tableRef, widths);
                };

                document.addEventListener('mousemove', onMove);
                document.addEventListener('mouseup', onUp);
            });
        });

        // Restore persisted widths
        const saved = loadWidths(table);
        if (saved) {
            applyWidths(table, saved);
        }
    }

    // Public API
    window.IntelliMedResizableTable = {
        init: function (selector) {
            const tables = typeof selector === 'string'
                ? document.querySelectorAll(selector)
                : [selector];
            tables.forEach(t => t && makeResizable(t));
        },
        reset: function (selector) {
            const tables = typeof selector === 'string'
                ? document.querySelectorAll(selector)
                : [selector];
            tables.forEach(t => {
                if (!t) return;
                try { localStorage.removeItem(getStorageKey(t)); } catch (e) {}
                t.querySelectorAll('thead th').forEach(th => {
                    th.style.width = '';
                    th.style.minWidth = '';
                });
            });
        }
    };
})();
