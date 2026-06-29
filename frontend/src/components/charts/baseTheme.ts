import type { ApexOptions } from 'apexcharts'

/** Reads CSS variables off :root so charts repaint when theme/accent change. */
export function readCssVar(name: string, fallback = ''): string {
  if (typeof window === 'undefined') return fallback
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
}

export function isDark(): boolean {
  return typeof document !== 'undefined' && document.documentElement.classList.contains('dark')
}

function prefersReducedMotion(): boolean {
  return typeof window !== 'undefined'
    && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
}

export function baseTheme(): ApexOptions {
  return {
    chart: {
      fontFamily: 'Geist, sans-serif',
      foreColor: readCssVar('--text-3', '#6b7872'),
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: { enabled: !prefersReducedMotion(), speed: 320 },
    },
    grid: {
      borderColor: readCssVar('--border', '#e7e5e4'),
      strokeDashArray: 3,
      padding: { left: 0, right: 0 },
    },
    legend: {
      fontSize: '11.5px',
      labels: { colors: readCssVar('--text-2', '#3f4c47') },
    },
    tooltip: { theme: isDark() ? 'dark' : 'light' },
    dataLabels: { enabled: false },
  }
}
