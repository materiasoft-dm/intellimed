/**
 * File-download interop for Blazor WebAssembly — triggers a browser download with a real filename
 * (the data-URI + window.open pattern used elsewhere in this app for CSV export doesn't let the
 * caller control the saved filename, which matters for backup files).
 */
window.IntelliMedDownload = {
    fromBase64: function (fileName, base64) {
        const link = document.createElement('a');
        link.href = 'data:application/octet-stream;base64,' + base64;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};
