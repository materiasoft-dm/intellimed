/*
 * Tri-state checkbox support for Blazor WebAssembly.
 *
 * A checkbox's "partly ticked" look comes from the DOM property `indeterminate`, which has no HTML
 * attribute, so Razor markup cannot set it. Parents in the Role Configuration tree render
 * data-tri="on|mixed|off" and call sync() after each render to mirror that onto the property.
 */
window.IntelliMedTri = {
    sync: function () {
        document.querySelectorAll('input[data-tri]').forEach(function (el) {
            var mixed = el.getAttribute('data-tri') === 'mixed';
            if (el.indeterminate !== mixed) {
                el.indeterminate = mixed;
            }
        });
    }
};
