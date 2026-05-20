import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Icon, type IconName } from '../components/ui/Icon'
import { Pill } from '../components/ui/Pill'
import { Field } from '../components/ui/Field'
import { SectionHeader } from '../components/ui/SectionHeader'
import { ChartBars } from '../components/charts'
import { api } from '../services/api'

type Format = 'PDF' | 'XLSX' | 'CSV'

interface Template {
  id: string
  title: string
  desc: string
  icon: IconName
  format: Format
}

const TEMPLATES: Template[] = [
  { id: 'perf-globale', title: 'Performance globale',  desc: 'KPIs + graphiques par filière',     icon: 'trend', format: 'PDF' },
  { id: 'risque',       title: 'Étudiants à risque',   desc: 'Liste + scores ML + recommandations', icon: 'brain', format: 'PDF' },
  { id: 'notes',        title: 'Notes par module',     desc: 'Détail CC/TP/Examen/Finale',       icon: 'doc',   format: 'XLSX' },
  { id: 'absences',     title: 'Absences hebdomadaires', desc: 'Suivi cumulatif par classe',     icon: 'clock', format: 'CSV' },
  { id: 'alertes',      title: 'Journal des alertes',  desc: 'Toutes les alertes du semestre',   icon: 'bell',  format: 'XLSX' },
  { id: 'bilan-ml',     title: 'Bilan modèle ML',      desc: 'Métriques + drift + features',     icon: 'spark', format: 'PDF' },
]

interface RapportRow {
  id: number
  templateId: string
  titre: string
  format: Format
  filiereCode: string
  periode: string
  auteurNom: string
  taille: number
  creeLe: string
}

const FILIERES = ['TOUS', 'TCP', 'GI', 'IA', 'ROC', 'IRSI']
const PERIODES = ['Semestre 2 · 2025/2026', 'Semestre 1 · 2025/2026', 'Année 2024/2025']
const FORMATS: Format[] = ['PDF', 'XLSX', 'CSV']

async function fetchRapports(): Promise<RapportRow[]> {
  const res = await api.get<RapportRow[]>('/rapports')
  return res.data
}

async function generateRapport(input: {
  templateId: string
  format: Format
  filiereCode: string
  periode: string
}): Promise<RapportRow> {
  const res = await api.post<RapportRow>('/rapports', input)
  return res.data
}

async function downloadRapport(row: RapportRow): Promise<void> {
  // Auth-aware download: pull the bytes through axios so the JWT is attached,
  // then save via a synthetic <a> click. Plain href= would 401.
  const res = await api.get<Blob>(`/rapports/${row.id}/download`, { responseType: 'blob' })
  const ext = row.format.toLowerCase()
  const cleanTitle = row.titre.replace(/[^a-zA-Z0-9-_ ]/g, '').trim().replace(/\s+/g, '-')
  const filename = `${cleanTitle || 'rapport'}-${row.id}.${ext}`
  const url = URL.createObjectURL(res.data)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  try {
    a.click()
  } finally {
    document.body.removeChild(a)
    // Defer the revoke: Firefox aborts the download if the blob URL is freed
    // in the same task as the click(). A short timeout is long enough for the
    // browser to capture the blob and short enough that we don't leak memory.
    setTimeout(() => URL.revokeObjectURL(url), 4_000)
  }
}

function formatTaille(bytes: number): string {
  if (bytes < 1024) return `${bytes} o`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} Ko`
  return `${(bytes / (1024 * 1024)).toFixed(2)} Mo`
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}

export default function Reports() {
  const [picked, setPicked] = useState('perf-globale')
  const [periode, setPeriode] = useState(PERIODES[0])
  const [filiere, setFiliere] = useState('TOUS')
  const [format, setFormat] = useState<Format>('PDF')
  const queryClient = useQueryClient()

  const { data: rapports = [], isLoading: listLoading } = useQuery({
    queryKey: ['rapports'],
    queryFn: fetchRapports,
    refetchInterval: 60_000,
  })

  const generateMutation = useMutation({
    mutationFn: () =>
      generateRapport({ templateId: picked, format, filiereCode: filiere, periode }),
    onSuccess: row => {
      queryClient.invalidateQueries({ queryKey: ['rapports'] })
      // Auto-download immediately so the user gets feedback the report ran.
      downloadRapport(row).catch(() => {/* user can retry from the table */})
    },
  })

  const downloadMutation = useMutation({
    mutationFn: downloadRapport,
  })

  // Static preview chart — keeps the cover artwork without round-tripping for
  // numbers the cover doesn't really need.
  const previewChartData = [
    { filiere: 'GI',   moyenne: 13.06, color: '#f97316' },
    { filiere: 'IA',   moyenne: 13.29, color: '#ec4899' },
    { filiere: 'IRSI', moyenne: 12.83, color: '#0ea5e9' },
    { filiere: 'ROC',  moyenne: 13.15, color: '#a855f7' },
    { filiere: 'TCP',  moyenne: 12.95, color: '#94a3b8' },
  ]

  const pickedTemplate = TEMPLATES.find(t => t.id === picked) ?? TEMPLATES[0]

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">Rapports automatiques · Conseil pédagogique</div>
          <h1 className="text-[22px] font-semibold tracking-tight">Rapports</h1>
        </div>
        {/* The canonical "generate" action lives in the parameters panel below
            ("Générer le rapport"). A duplicate header button would let two
            clicks race the same mutation and produce two simultaneous file
            downloads. */}
      </div>

      {generateMutation.isError && (
        <div className="card p-3 text-[12.5px]" style={{ color: 'var(--bad)' }}>
          Échec de la génération — réessayez ou changez de format.
        </div>
      )}

      <div className="grid grid-cols-3 gap-3">
        {TEMPLATES.map(t => (
          <button
            key={t.id}
            onClick={() => {
              setPicked(t.id)
              setFormat(t.format)
            }}
            className="card p-4 text-left transition"
            style={{
              borderColor: picked === t.id ? 'var(--accent-500)' : 'var(--border)',
              boxShadow:
                picked === t.id
                  ? '0 0 0 3px color-mix(in oklch, var(--accent-500) 14%, transparent)'
                  : 'none',
            }}
          >
            <div className="flex items-center gap-3 mb-2">
              <div
                className="w-9 h-9 rounded-lg flex items-center justify-center"
                style={{
                  background: picked === t.id ? 'var(--accent-100)' : 'var(--surface-2)',
                  color: picked === t.id ? 'var(--accent-700)' : 'var(--text-2)',
                }}
              >
                <Icon name={t.icon} size={16} />
              </div>
              <Pill tone={t.format === 'PDF' ? 'bad' : t.format === 'XLSX' ? 'ok' : 'info'}>
                {t.format}
              </Pill>
            </div>
            <div className="text-[13px] font-semibold tracking-tight">{t.title}</div>
            <div className="cap mt-1">{t.desc}</div>
          </button>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4 col-span-1">
          <SectionHeader title="Paramètres" subtitle="Configurez le rapport avant export" />
          <div className="space-y-3">
            <Field label="Période">
              <select className="input" value={periode} onChange={e => setPeriode(e.target.value)}>
                {PERIODES.map(p => <option key={p}>{p}</option>)}
              </select>
            </Field>
            <Field label="Filière">
              <select className="input" value={filiere} onChange={e => setFiliere(e.target.value)}>
                {FILIERES.map(f => <option key={f} value={f}>{f === 'TOUS' ? 'Toutes' : f}</option>)}
              </select>
            </Field>
            <Field label="Format de sortie">
              <div className="flex gap-1.5">
                {FORMATS.map(f => (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setFormat(f)}
                    className="flex-1 px-3 py-1.5 rounded-md text-[12px] transition"
                    style={{
                      border: `1px solid ${format === f ? 'var(--accent-500)' : 'var(--border-2)'}`,
                      background: format === f ? 'var(--accent-50)' : 'var(--surface)',
                      color: format === f ? 'var(--accent-700)' : 'var(--text-2)',
                      fontWeight: format === f ? 500 : 400,
                    }}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </Field>
            <button
              className="btn btn-accent btn-lg w-full mt-2"
              onClick={() => generateMutation.mutate()}
              disabled={generateMutation.isPending}
            >
              <Icon name="download" size={14} />
              {generateMutation.isPending ? 'Génération en cours…' : 'Générer le rapport'}
            </button>
          </div>
        </div>

        {/* preview */}
        <div className="col-span-2">
          <SectionHeader title="Aperçu" subtitle="Couverture du document — généré à la demande" />
          <div
            className="card p-0 overflow-hidden"
            style={{ aspectRatio: '1/1.414', background: 'var(--surface)' }}
          >
            <div
              className="h-full flex flex-col p-8"
              style={{
                background:
                  'linear-gradient(180deg, var(--surface) 0%, var(--surface) 60%, var(--surface-2) 100%)',
              }}
            >
              <div className="flex items-center justify-between mb-6">
                <div className="flex items-center gap-2">
                  <div
                    style={{
                      width: 24,
                      height: 24,
                      borderRadius: 6,
                      background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: '#fff',
                      fontWeight: 700,
                      fontSize: 11,
                    }}
                  >
                    E
                  </div>
                  <span className="text-[11px] font-semibold tracking-wider">
                    ENIAD · BERKANE
                  </span>
                </div>
                <span className="cap font-mono">
                  {format} · {filiere}
                </span>
              </div>
              <div
                className="text-[10px] uppercase tracking-[0.18em] mb-3"
                style={{ color: 'var(--accent-700)', fontWeight: 500 }}
              >
                Rapport
              </div>
              <h2
                className="font-serif text-[36px] leading-[1.05]"
                style={{ letterSpacing: '-0.015em' }}
              >
                {pickedTemplate.title}
                <br />
                {periode}
              </h2>
              <p className="mt-3 text-[12px] max-w-[420px]" style={{ color: 'var(--text-2)' }}>
                {pickedTemplate.desc}
              </p>

              <div
                className="flex-1 mt-5 rounded-md p-4"
                style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
              >
                <div className="text-[11px] font-semibold mb-2">Moyennes par filière (illustration)</div>
                <ChartBars data={previewChartData} height={160} />
              </div>

              <div
                className="mt-auto flex items-center justify-between pt-3"
                style={{ borderTop: '1px solid var(--border)', color: 'var(--text-3)', fontSize: 10 }}
              >
                <span>Plateforme décisionnelle ENIAD · données en direct</span>
                <span className="font-mono">aperçu</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div
          className="px-4 py-3 flex items-center justify-between"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div>
            <div className="text-[13px] font-semibold">Rapports récents</div>
            <div className="cap mt-0.5">{rapports.length} document{rapports.length > 1 ? 's' : ''} généré{rapports.length > 1 ? 's' : ''}</div>
          </div>
        </div>
        <table className="tbl">
          <thead>
            <tr>
              <th>ID</th>
              <th>Titre</th>
              <th>Filière</th>
              <th>Format</th>
              <th>Taille</th>
              <th>Auteur</th>
              <th>Date</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {listLoading ? (
              <tr><td colSpan={8} className="cap text-center py-6">Chargement…</td></tr>
            ) : rapports.length === 0 ? (
              <tr>
                <td colSpan={8} className="cap text-center py-6">
                  Aucun rapport encore — choisissez un modèle et cliquez sur « Générer ».
                </td>
              </tr>
            ) : (
              rapports.map(r => (
                <tr key={r.id}>
                  <td className="font-mono cap">RP-{String(r.id).padStart(3, '0')}</td>
                  <td className="font-medium">{r.titre}</td>
                  <td>
                    <Pill tone="neutral">{r.filiereCode}</Pill>
                  </td>
                  <td>
                    <Pill tone={r.format === 'PDF' ? 'bad' : r.format === 'XLSX' ? 'ok' : 'info'}>
                      {r.format}
                    </Pill>
                  </td>
                  <td className="num cap">{formatTaille(r.taille)}</td>
                  <td>{r.auteurNom}</td>
                  <td className="cap">{formatDate(r.creeLe)}</td>
                  <td>
                    <div className="flex items-center gap-1">
                      <button
                        className="btn btn-sm btn-ghost"
                        title="Télécharger"
                        onClick={() => downloadMutation.mutate(r)}
                        disabled={downloadMutation.isPending}
                      >
                        <Icon name="download" size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
