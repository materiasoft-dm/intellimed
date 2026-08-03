// calendar-context-menu.js
// Delegated right-click handling for the RadzenScheduler on the Appointment Calendar page.
// Radzen's own Attributes-splatting (AppointmentRender/SlotRender) can attach plain data-* attributes
// and event *handlers*, but not the @oncontextmenu:preventDefault compiler directive, so the native
// browser menu has to be suppressed here in JS before invoking back into the Blazor component.
// Same IIFE + window-namespace convention as resizable-table.js / command-palette.js.

(function () {
    'use strict';

    let container = null;
    let dotNetRef = null;

    function onContextMenu(e) {
        const apptEl = e.target.closest('[data-appt-id]');
        const slotEl = !apptEl ? e.target.closest('[data-slot-start]') : null;

        if (apptEl) {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnAppointmentContextMenu', parseInt(apptEl.getAttribute('data-appt-id'), 10), e.clientX, e.clientY);
        } else if (slotEl) {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnSlotContextMenu', slotEl.getAttribute('data-slot-start'), slotEl.getAttribute('data-slot-end'), e.clientX, e.clientY);
        }
    }

    window.IntelliMedCalendarContextMenu = {
        init: function (containerEl, ref) {
            if (container) {
                container.removeEventListener('contextmenu', onContextMenu);
            }
            container = containerEl;
            dotNetRef = ref;
            if (container) {
                container.addEventListener('contextmenu', onContextMenu);
            }
        },
        dispose: function () {
            if (container) {
                container.removeEventListener('contextmenu', onContextMenu);
            }
            container = null;
            dotNetRef = null;
        }
    };
})();
