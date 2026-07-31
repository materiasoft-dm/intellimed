// command-palette.js
// Registers a document-level Ctrl+K / Cmd+K listener that toggles the global command palette
// via a callback into the Blazor component. Same IIFE + window-namespace convention as
// resizable-table.js / chart-interop.js.

(function () {
    'use strict';

    let dotNetRef = null;
    let listenerAttached = false;

    function onKeyDown(e) {
        const isToggleCombo = (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k';
        if (!isToggleCombo) return;

        e.preventDefault();
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('ToggleFromJs');
        }
    }

    window.IntelliMedCommandPalette = {
        register: function (ref) {
            dotNetRef = ref;
            if (!listenerAttached) {
                document.addEventListener('keydown', onKeyDown);
                listenerAttached = true;
            }
        },
        focusSearchInput: function (elementId) {
            const el = document.getElementById(elementId);
            if (el) el.focus();
        },
        unregister: function () {
            if (listenerAttached) {
                document.removeEventListener('keydown', onKeyDown);
                listenerAttached = false;
            }
            dotNetRef = null;
        }
    };
})();
