/**
 * Chart.js interop for Blazor WebAssembly
 */
window.IntelliMedCharts = {
    instances: {},

    /**
     * Render (or re-render) a single-series bar chart in the given <canvas> element.
     * @param {string} canvasId - Element id of the <canvas>
     * @param {string[]} labels - X-axis category labels
     * @param {number[]} data - Bar values, one per label
     * @param {string} accentColor - CSS color for the bars
     */
    renderBarChart: function (canvasId, labels, data, accentColor) {
        this.destroy(canvasId);

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const chart = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: accentColor || '#4272d7',
                    borderRadius: 6,
                    maxBarThickness: 40
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    x: { grid: { display: false } },
                    y: { beginAtZero: true, ticks: { precision: 0 } }
                }
            }
        });

        this.instances[canvasId] = chart;
    },

    destroy: function (canvasId) {
        const existing = this.instances[canvasId];
        if (existing) {
            existing.destroy();
            delete this.instances[canvasId];
        }
    }
};

window.IntelliMedRenderBarChart = function (canvasId, labels, data, accentColor) {
    window.IntelliMedCharts.renderBarChart(canvasId, labels, data, accentColor);
};
