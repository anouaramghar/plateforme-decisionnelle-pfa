import Chart from 'react-apexcharts'
import type { ApexOptions } from 'apexcharts'
import { baseTheme, readCssVar } from './baseTheme'
import { useTheme } from '../../context/ThemeContext'

/** Forces ApexCharts options to recompute when the user toggles theme/accent. */
function useChartKey(extra?: string) {
  const { theme, accent } = useTheme()
  return `${theme}-${accent}-${extra ?? ''}`
}

interface ChartBarsProps {
  data: { filiere?: string; label?: string; moyenne?: number; n?: number; color?: string }[]
  height?: number
  label?: string
}

export function ChartBars({ data, height = 240, label = 'Moyenne' }: ChartBarsProps) {
  const k = useChartKey()
  const isMoyenne = data[0]?.moyenne != null
  const options: ApexOptions = {
    ...baseTheme(),
    chart: { ...baseTheme().chart, type: 'bar', height },
    xaxis: {
      categories: data.map(d => d.filiere ?? d.label ?? ''),
      axisBorder: { show: false },
      axisTicks: { show: false },
      labels: { style: { fontSize: '11px' } },
    },
    yaxis: {
      labels: { style: { fontSize: '11px' }, formatter: v => Number(v).toFixed(0) },
      max: isMoyenne ? 20 : undefined,
    },
    plotOptions: {
      bar: { columnWidth: '52%', borderRadius: 4, distributed: !!data[0]?.color },
    },
    colors: data.map(d => d.color ?? readCssVar('--accent-500', '#14b8a6')),
    fill: { opacity: 1 },
    tooltip: {
      ...baseTheme().tooltip,
      y: { formatter: v => (isMoyenne ? `${v}/20` : `${v} étudiants`) },
    },
  }
  const series = [{ name: label, data: data.map(d => d.moyenne ?? d.n ?? 0) }]
  return <Chart key={k} options={options} series={series} type="bar" height={height} />
}

interface ChartAreaProps {
  data: { semaine: string; heures: number; nbEtudiants?: number }[]
  height?: number
  mode?: 'heures' | 'etudiants' | 'pct'
  totalEtudiants?: number
}

export function ChartArea({ data, height = 240, mode = 'heures', totalEtudiants = 1 }: ChartAreaProps) {
  const k = useChartKey(mode)

  const values = data.map(d => {
    if (mode === 'heures') return d.heures
    if (mode === 'etudiants') return d.nbEtudiants ?? 0
    return totalEtudiants > 0 ? Math.round(((d.nbEtudiants ?? 0) / totalEtudiants) * 100) : 0
  })

  const yFormatter = (v: number) =>
    mode === 'heures' ? `${Math.round(v)} h`
    : mode === 'etudiants' ? `${Math.round(v)} étud.`
    : `${Math.round(v)} %`

  const seriesName =
    mode === 'heures' ? "Heures d'absence"
    : mode === 'etudiants' ? 'Étudiants absents'
    : '% étudiants absents'

  const options: ApexOptions = {
    ...baseTheme(),
    chart: { ...baseTheme().chart, type: 'area', height, sparkline: { enabled: false } },
    xaxis: {
      categories: data.map(d => d.semaine),
      axisBorder: { show: false },
      axisTicks: { show: false },
      labels: { style: { fontSize: '10.5px' } },
    },
    yaxis: {
      labels: { style: { fontSize: '10.5px' }, formatter: yFormatter },
      ...(mode === 'pct' ? { min: 0, max: 100 } : {}),
    },
    stroke: { curve: 'smooth', width: 2 },
    fill: {
      type: 'gradient',
      gradient: { shadeIntensity: 0.6, opacityFrom: 0.35, opacityTo: 0.02, stops: [0, 90] },
    },
    colors: [readCssVar('--accent-500', '#14b8a6')],
    markers: { size: 0, hover: { size: 4 } },
    tooltip: { ...baseTheme().tooltip, y: { formatter: yFormatter } },
  }

  const series = [{ name: seriesName, data: values }]
  return <Chart key={k} options={options} series={series} type="area" height={height} />
}

interface ChartHistogramProps {
  data: { label: string; n: number }[]
  height?: number
}

export function ChartHistogram({ data, height = 200 }: ChartHistogramProps) {
  const k = useChartKey()
  const options: ApexOptions = {
    ...baseTheme(),
    chart: { ...baseTheme().chart, type: 'bar', height },
    xaxis: {
      categories: data.map(d => d.label),
      axisBorder: { show: false },
      axisTicks: { show: false },
      labels: { style: { fontSize: '10.5px' } },
    },
    yaxis: { labels: { style: { fontSize: '10.5px' } } },
    plotOptions: { bar: { columnWidth: '42%', borderRadius: 3, distributed: true } },
    colors: ['#ccfbf1', '#99f6e4', '#5eead4', '#14b8a6', '#0d9488', '#115e59'],
    legend: { show: false },
    fill: { opacity: 1 },
  }
  const series = [{ name: 'Étudiants', data: data.map(d => d.n) }]
  return <Chart key={k} options={options} series={series} type="bar" height={height} />
}

interface ChartScatterProps {
  points: { x: number; y: number; r: 'faible' | 'modere' | 'eleve' }[]
  height?: number
}

export function ChartScatter({ points, height = 260 }: ChartScatterProps) {
  const k = useChartKey()
  const options: ApexOptions = {
    ...baseTheme(),
    chart: { ...baseTheme().chart, type: 'scatter', height },
    xaxis: {
      type: 'numeric',
      min: 0,
      max: 50,
      title: { text: "Heures d'absence", style: { fontSize: '11px', fontWeight: 500 } },
      labels: { style: { fontSize: '10.5px' } },
    },
    yaxis: {
      min: 0,
      max: 20,
      title: { text: 'Moyenne /20', style: { fontSize: '11px', fontWeight: 500 } },
      labels: { style: { fontSize: '10.5px' } },
    },
    colors: ['#16a34a', '#f59e0b', '#dc2626'],
    markers: { size: 5, strokeWidth: 0, hover: { size: 7 } },
    legend: { position: 'top', horizontalAlign: 'left', fontSize: '11.5px' },
  }
  const series = [
    { name: 'Faible', data: points.filter(p => p.r === 'faible').map(p => [p.x, p.y]) },
    { name: 'Modéré', data: points.filter(p => p.r === 'modere').map(p => [p.x, p.y]) },
    { name: 'Élevé',  data: points.filter(p => p.r === 'eleve').map(p => [p.x, p.y]) },
  ]
  return <Chart key={k} options={options} series={series} type="scatter" height={height} />
}

// ─── SHAP feature-importance chart ───────────────────────────────────────────

export interface ShapContribution {
  feature: string
  label: string
  value: number   // log-odds; positive = increases risk
  pct: number     // abs(value) / sum(abs) — relative importance 0-1
}

export interface ShapExplainData {
  contributions: ShapContribution[]
  baseValue: number
  probability: number
}

export function ChartShap({ data }: { data: ShapExplainData }) {
  const sorted = [...data.contributions].sort((a, b) => Math.abs(b.value) - Math.abs(a.value))
  const maxPct = Math.max(...sorted.map(c => c.pct), 0.01)

  return (
    <div className="space-y-2.5">
      {sorted.map(c => {
        const isRisk = c.value > 0
        const barWidth = Math.round((c.pct / maxPct) * 100)
        return (
          <div key={c.feature} className="flex items-center gap-3">
            <span className="text-[11.5px]" style={{ minWidth: 136, color: 'var(--text-2)' }}>
              {c.label}
            </span>
            <div className="flex-1 h-2 rounded-full overflow-hidden" style={{ background: 'var(--surface-3)' }}>
              <div
                style={{
                  width: `${barWidth}%`,
                  height: '100%',
                  borderRadius: '9999px',
                  background: isRisk ? 'var(--bad)' : 'var(--ok)',
                }}
              />
            </div>
            <span
              className="text-[11px] font-medium num"
              style={{ minWidth: 38, textAlign: 'right', color: isRisk ? 'var(--bad)' : 'var(--ok)' }}
            >
              {isRisk ? '+' : '−'}{(c.pct * 100).toFixed(0)}%
            </span>
          </div>
        )
      })}
      <div className="flex items-center gap-4 pt-0.5 text-[10.5px]" style={{ color: 'var(--text-4)' }}>
        <span>Probabilité ML : <strong className="num">{(data.probability * 100).toFixed(0)}%</strong></span>
        <span style={{ color: 'var(--bad)' }}>■</span><span>Augmente le risque</span>
        <span style={{ color: 'var(--ok)' }}>■</span><span>Réduit le risque</span>
      </div>
    </div>
  )
}

interface ChartRadialProps {
  value: number // 0..1
  label: string
  height?: number
  color?: string
}

export function ChartRadial({ value, label, height = 180, color = '#14b8a6' }: ChartRadialProps) {
  const k = useChartKey(label)
  const options: ApexOptions = {
    ...baseTheme(),
    chart: { ...baseTheme().chart, type: 'radialBar', height, sparkline: { enabled: true }, animations: { enabled: false } },
    colors: [color],
    plotOptions: {
      radialBar: {
        hollow: { size: '62%' },
        track: { background: readCssVar('--surface-3', '#e7e5e4'), strokeWidth: '100%' },
        dataLabels: {
          name: { fontSize: '10.5px', color: readCssVar('--text-3'), offsetY: 16 },
          value: {
            fontSize: '22px',
            fontWeight: 500,
            color: readCssVar('--text'),
            offsetY: -8,
            formatter: v => `${v}%`,
          },
        },
      },
    },
    labels: [label],
    stroke: { lineCap: 'round' },
  }
  const series = [Math.round(value * 100)]
  return <Chart key={k} options={options} series={series} type="radialBar" height={height} />
}
