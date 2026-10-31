import { useEffect, useRef } from 'react';
import { BarController, BarElement, CategoryScale, Chart, LinearScale, Tooltip } from 'chart.js';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip);
Chart.defaults.font.family = "'Inter Variable', 'Inter', 'Helvetica Neue', Helvetica, Arial, sans-serif";
Chart.defaults.color = '#78716c';

/** Single-series bar chart (what chart-interop.js rendered for Blazor). */
export function BarChart({ labels, data, color = '#10b981', height = 260, label }: {
  labels: string[];
  data: number[];
  color?: string;
  height?: number;
  label?: string;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (!canvas.current) return;
    const chart = new Chart(canvas.current, {
      type: 'bar',
      data: {
        labels,
        datasets: [{ label: label ?? '', data, backgroundColor: color, hoverBackgroundColor: '#059669', borderRadius: 6, maxBarThickness: 40 }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 500 },
        plugins: {
          legend: { display: false },
          tooltip: { backgroundColor: '#0c0a09', padding: 10, cornerRadius: 8, displayColors: false },
        },
        scales: {
          x: { grid: { display: false }, border: { display: false } },
          y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#f0efed' }, border: { display: false } },
        },
      },
    });
    return () => chart.destroy();
  }, [labels, data, color, label]);

  return (
    <div style={{ height }}>
      <canvas ref={canvas} role="img" aria-label={label ?? 'Bar chart'} />
    </div>
  );
}
