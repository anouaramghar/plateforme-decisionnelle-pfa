import { Pill } from '../components/ui/Pill'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { RiskBar } from '../components/ui/RiskBar'
import { SectionHeader } from '../components/ui/SectionHeader'
import { MiniKpi } from '../components/ui/KpiCard'
import { ChartArea, ChartBars, ChartHistogram } from '../components/charts'
import { ACTIVITY, absenceTrend, kpis, moyenneDistribution, notesByFiliere, riskBreakdown, topARisque } from '../data/mock'

const SPARK = [22, 28, 30, 26, 32, 38, 42, 40, 46, 52, 48, 55, 62, 58]

const ML_METRICS: { l: string; v: string; t: 'ok' | 'warn'; bar: number }[] = [
  { l: 'AUC',       v: '0.87', t: 'ok',   bar: 87 },
  { l: 'F1-score',  v: '0.81', t: 'ok',   bar: 81 },
  { l: 'Précision', v: '0.83', t: 'ok',   bar: 83 },
  { l: 'Rappel',    v: '0.79', t: 'warn', bar: 79 },
]

export default function Dashboard() {
  const k = kpis()
  const notesByFil = notesByFiliere()
  const moyDist = moyenneDistribution()
  const risk = riskBreakdown()
  const absTrend = absenceTrend()
  const top = topARisque(6)
  const filiereCtx = 'GI'

  const today = new Date().toLocaleDateString('fr-FR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  })

  const totalRisk = risk.reduce((a, b) => a + b.n, 0)

  return (
    <div className="space-y-6">
      {/* Hero header */}
      <div className="flex items-end justify-between gap-8 pb-2">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-3">
            <span className="pill pill-accent" style={{ padding: '2px 8px' }}>
              <span className="pill-dot live-dot" style={{ background: 'currentColor' }} />
              Filière {filiereCtx}
            </span>
            <span className="cap">Semestre 2 · 2025/2026</span>
            <span style={{ color: 'var(--text-4)' }}>·</span>
            <span className="cap">{today}</span>
          </div>
          <h1
            className="tracking-tight"
            style={{ fontSize: 32, fontWeight: 500, letterSpacing: '-0.025em', lineHeight: 1.05 }}
          >
            Pilotage{' '}
            <span className="display-serif" style={{ color: 'var(--accent-700)' }}>
              pédagogique
            </span>
          </h1>
          <p
            className="mt-2 max-w-[60ch]"
            style={{ color: 'var(--text-3)', fontSize: 13.5, lineHeight: 1.55 }}
          >
            Vue d'ensemble en temps réel des indicateurs clés, du risque ML, et des actions à
            prioriser cette semaine.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div
            className="flex items-center rounded-lg overflow-hidden"
            style={{ border: '1px solid var(--border-2)', background: 'var(--surface)' }}
          >
            {['7j', '30j', 'S2', 'Année'].map((p, i) => (
              <button
                key={p}
                className="px-3 py-1.5 text-[12px] transition"
                style={{
                  background: i === 2 ? 'var(--text)' : 'transparent',
                  color: i === 2 ? 'var(--bg)' : 'var(--text-3)',
                  fontWeight: i === 2 ? 500 : 400,
                  borderRight: i < 3 ? '1px solid var(--border)' : 'none',
                }}
              >
                {p}
              </button>
            ))}
          </div>
          <button className="btn btn-sm btn-ghost" title="Actualiser">
            <Icon name="refresh" size={13} />
          </button>
          <button className="btn btn-sm">
            <Icon name="download" size={13} />
            Exporter
          </button>
        </div>
      </div>

      {/* Hero metric + supporting KPIs */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card-feature col-span-5 p-6 relative overflow-hidden">
          <div
            className="absolute inset-0 grid-bg pointer-events-none"
            style={{
              maskImage: 'linear-gradient(180deg, transparent 0%, black 60%)',
              WebkitMaskImage: 'linear-gradient(180deg, transparent 0%, black 60%)',
            }}
          />
          <div className="relative">
            <div className="flex items-center justify-between mb-5">
              <div
                className="text-[11px] uppercase tracking-[0.08em] font-medium"
                style={{ color: 'var(--text-3)' }}
              >
                Étudiants suivis
              </div>
              <Pill tone="ok">
                <Icon name="trend" size={11} strokeWidth={2.2} />
                +3.2%
              </Pill>
            </div>
            <div className="flex items-baseline gap-3">
              <span className="num num-hero">{k.nbEtudiants}</span>
              <span className="display-serif text-[20px]" style={{ color: 'var(--text-3)' }}>
                actifs
              </span>
            </div>
            <div className="mt-5 flex items-end gap-6">
              <div className="flex-1">
                <div className="sparkbar" style={{ height: 38 }}>
                  {SPARK.map((v, i) => (
                    <div
                      key={i}
                      style={{
                        height: `${Math.max(15, v)}%`,
                        background: 'var(--accent-500)',
                        opacity: 0.45 + (i / SPARK.length) * 0.55,
                        borderRadius: 1.5,
                      }}
                    />
                  ))}
                </div>
                <div className="mt-2 flex items-center justify-between cap">
                  <span>il y a 14 sem.</span>
                  <span>aujourd'hui</span>
                </div>
              </div>
              <div className="text-right">
                <div className="cap">cette semaine</div>
                <div className="num text-[15px] font-medium" style={{ color: 'var(--ok)' }}>
                  +14
                </div>
                <div className="cap mt-2">retraits</div>
                <div className="num text-[15px] font-medium" style={{ color: 'var(--text-3)' }}>
                  −2
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="col-span-7 grid grid-cols-3 gap-4">
          <MiniKpi label="Moyenne globale" value={k.moyGlobale} suffix="/20" delta={-0.4} deltaTone="warn" hint="vs S1 — 11.6" />
          <MiniKpi label="Taux de réussite" value={k.tauxReussite} suffix="%" delta={1.8} deltaTone="ok" hint="modules ≥ 10/20" />
          <MiniKpi label="Alertes actives" value={k.alertesActives} delta={12} deltaTone="bad" hint="dont 8 risque ML élevé" emphasized />

          <div className="card p-4 col-span-3">
            <div className="flex items-center justify-between mb-3">
              <div>
                <div className="text-[12.5px] font-medium">Répartition du risque</div>
                <div className="cap">Score ML actuel · {totalRisk} étudiants</div>
              </div>
              <div className="flex items-center gap-3 text-[11px]">
                {risk.map(r => (
                  <span key={r.label} className="flex items-center gap-1.5" style={{ color: 'var(--text-2)' }}>
                    <span style={{ width: 8, height: 8, borderRadius: 2, background: r.color }} />
                    {r.label}
                    <span className="num font-medium" style={{ color: 'var(--text)' }}>
                      {r.n}
                    </span>
                  </span>
                ))}
              </div>
            </div>
            <div className="flex h-2.5 rounded-full overflow-hidden" style={{ background: 'var(--surface-2)' }}>
              {risk.map(r => (
                <div
                  key={r.label}
                  style={{ width: `${(r.n / Math.max(1, totalRisk)) * 100}%`, background: r.color }}
                  title={`${r.label}: ${r.n}`}
                />
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Charts */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card col-span-8 p-5">
          <SectionHeader
            title="Moyennes par filière"
            subtitle="Comparaison /20 — semestre courant"
            right={
              <>
                <Pill tone="neutral" dot>Moyenne</Pill>
                <button className="btn btn-sm btn-ghost"><Icon name="more" size={14} /></button>
              </>
            }
          />
          <ChartBars data={notesByFil} height={232} />
        </div>

        <div className="card col-span-4 p-5">
          <SectionHeader title="Distribution des moyennes" subtitle="Effectifs par tranche /20" />
          <ChartHistogram data={moyDist} height={232} />
        </div>
      </div>

      {/* Absence trend */}
      <div className="card p-5">
        <SectionHeader
          title="Heures d'absence cumulées"
          subtitle="14 dernières semaines — toutes filières"
          right={
            <>
              <Pill tone="warn" dot>Pic semaine 12</Pill>
              <div
                className="flex items-center rounded-md overflow-hidden ml-2"
                style={{ border: '1px solid var(--border)' }}
              >
                {['Heures', 'Étudiants', '%'].map((p, i) => (
                  <button
                    key={p}
                    className="px-2.5 py-1 text-[11px]"
                    style={{
                      background: i === 0 ? 'var(--surface-2)' : 'transparent',
                      color: i === 0 ? 'var(--text)' : 'var(--text-3)',
                      fontWeight: i === 0 ? 500 : 400,
                      borderRight: i < 2 ? '1px solid var(--border)' : 'none',
                    }}
                  >
                    {p}
                  </button>
                ))}
              </div>
            </>
          }
        />
        <ChartArea data={absTrend} height={200} />
      </div>

      {/* Bottom — table + activity */}
      <div className="grid grid-cols-12 gap-4">
        <div className="card col-span-8 overflow-hidden">
          <div className="flex items-end justify-between p-5 pb-4">
            <div>
              <h2 className="text-[15px] font-semibold tracking-tight">Étudiants à surveiller</h2>
              <div className="cap mt-0.5">
                Score de risque ML le plus élevé · mis à jour il y a 2h
              </div>
            </div>
            <button className="btn btn-sm btn-ghost" style={{ color: 'var(--accent-700)' }}>
              Voir tous <Icon name="arrowRight" size={12} />
            </button>
          </div>
          <table className="tbl">
            <thead>
              <tr>
                <th>Étudiant</th>
                <th>Filière</th>
                <th>Niveau</th>
                <th>Moy.</th>
                <th>Absences</th>
                <th style={{ minWidth: 140 }}>Score ML</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {top.map(e => (
                <tr key={e.id}>
                  <td>
                    <div className="flex items-center gap-2.5">
                      <Avatar name={e.nomComplet} size={28} />
                      <div>
                        <div className="font-medium">{e.nomComplet}</div>
                        <div className="cap font-mono">{e.matricule}</div>
                      </div>
                    </div>
                  </td>
                  <td>
                    <Pill tone="neutral">{e.filiere}</Pill>
                  </td>
                  <td className="font-mono">{e.niveau}</td>
                  <td className="num font-medium">{e.moyenne.toFixed(2)}</td>
                  <td className="num">{e.absences}h</td>
                  <td>
                    <RiskBar score={e.scoreRisque} />
                  </td>
                  <td>
                    <button className="btn btn-sm btn-ghost">
                      <Icon name="chevRight" size={13} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card col-span-4 p-5">
          <SectionHeader title="Activité récente" subtitle="Audit de la plateforme" />
          <div className="space-y-3 relative">
            <div
              className="absolute left-[13px] top-3 bottom-3 w-px"
              style={{ background: 'var(--border)' }}
            />
            {ACTIVITY.map((a, i) => (
              <div key={i} className="flex items-start gap-3 relative">
                <div
                  className="w-7 h-7 rounded-md flex items-center justify-center flex-shrink-0 relative z-10"
                  style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
                >
                  {a.icon}
                </div>
                <div className="flex-1 min-w-0 pt-0.5">
                  <div className="text-[12.5px] leading-snug">
                    <span className="font-medium">{a.who}</span>{' '}
                    <span style={{ color: 'var(--text-2)' }}>{a.text}</span>
                  </div>
                  <div className="cap mt-1">{a.when}</div>
                </div>
              </div>
            ))}
          </div>
          <button
            className="btn btn-sm w-full mt-4"
            style={{ background: 'var(--surface-2)', border: 'none', color: 'var(--text-2)' }}
          >
            Voir le journal complet
          </button>
        </div>
      </div>

      {/* ML model status */}
      <div className="card-feature p-5 flex items-center gap-6 relative overflow-hidden">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{
              background: 'var(--accent-100)',
              color: 'var(--accent-700)',
              border: '1px solid color-mix(in oklch, var(--accent-300) 40%, transparent)',
            }}
          >
            <Icon name="brain" size={18} />
          </div>
          <div className="min-w-0">
            <div className="text-[13px] font-medium flex items-center gap-2">
              XGBoost <span className="font-mono cap">v1.4</span>
              <Pill tone="ok" dot>En production</Pill>
            </div>
            <div className="cap mt-0.5">Ré-entraîné le 24 avr. 2026 · 6 482 lignes du DW</div>
          </div>
        </div>
        <div style={{ width: 1, height: 36, background: 'var(--border)' }} />
        <div className="flex-1 grid grid-cols-4 gap-6">
          {ML_METRICS.map(m => (
            <div key={m.l}>
              <div className="cap mb-1">{m.l}</div>
              <div
                className="num text-[20px] font-medium"
                style={{ color: m.t === 'ok' ? 'var(--ok)' : 'var(--warn)' }}
              >
                {m.v}
              </div>
              <div className="mt-1.5 h-[3px] rounded-full" style={{ background: 'var(--surface-2)' }}>
                <div
                  style={{
                    width: `${m.bar}%`,
                    height: '100%',
                    background: m.t === 'ok' ? 'var(--ok)' : 'var(--warn)',
                    borderRadius: 999,
                  }}
                />
              </div>
            </div>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">Voir les features</button>
          <button className="btn btn-sm btn-primary">Ré-entraîner</button>
        </div>
      </div>
    </div>
  )
}
