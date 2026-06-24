import { useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar, RiskPill } from '../components/ui/RiskBar'
import { useAuth } from '../context/AuthContext'
import { useFiliere } from '../context/FiliereContext'
import { api } from '../services/api'
import { fetchInterventionKpis } from '../services/interventions'

interface EtudiantRow {
  id: number; matricule: string; nomComplet: string; nom: string; prenom: string
  filiereCode: string; filiereIntitule: string; niveau: string
  moyenne: number; absences: number; modulesValides: number; modulesTotal: number
  scoreRisque: number; risque: 'faible' | 'modere' | 'eleve'; statut: string
}

interface AlerteRow {
  id: number; type: string; niveau: string; message: string; resolue: boolean
  creeLe: string; etudiantId: number
  etudiant: { nomComplet: string; matricule: string; filiere?: { code: string } }
}

async function fetchStudents(): Promise<EtudiantRow[]> {
  const res = await api.get<EtudiantRow[]>('/etudiants/with-stats')
  return res.data
}

async function fetchAlertes(): Promise<AlerteRow[]> {
  const res = await api.get<{ items: AlerteRow[]; total: number }>('/alertes?pageSize=50&resolue=false')
  return res.data.items
}

export default function Responsable() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  // Scope follows the single global "Périmètre" picker in the sidebar — no
  // second filière control that can drift out of sync with the rest of the app.
  const { filiere: filterFiliere } = useFiliere()

  const { data: students = [], isLoading: loadingStudents } = useQuery({
    queryKey: ['etudiants-with-stats'], queryFn: fetchStudents, refetchInterval: 60_000,
  })
  const { data: alertes = [], isLoading: loadingAlertes } = useQuery({
    queryKey: ['responsable-alertes'], queryFn: fetchAlertes, refetchInterval: 30_000,
  })
  const { data: kpis } = useQuery({
    queryKey: ['dashboard', 'interventions'], queryFn: fetchInterventionKpis, refetchInterval: 60_000,
  })

  const resolveMutation = useMutation({
    mutationFn: (id: number) => api.patch(`/alertes/${id}/resoudre`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['responsable-alertes'] })
      queryClient.invalidateQueries({ queryKey: ['alertes'] })
    },
  })

  const scopedStudents = useMemo(
    () => filterFiliere === 'TOUS' ? students : students.filter(e => e.filiereCode === filterFiliere),
    [students, filterFiliere],
  )

  const atRisk    = useMemo(() => scopedStudents.filter(e => e.risque === 'eleve'), [scopedStudents])
  const moyGlobal = useMemo(() => scopedStudents.length > 0 ? scopedStudents.reduce((s, e) => s + e.moyenne, 0) / scopedStudents.length : 0, [scopedStudents])
  const tauxReussite = useMemo(() => scopedStudents.length > 0 ? scopedStudents.filter(e => e.moyenne >= 10).length / scopedStudents.length * 100 : 0, [scopedStudents])

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">Espace Responsable</div>
          <h1 className="text-[22px] font-semibold tracking-tight">
            Bonjour, <span className="display-serif" style={{ fontSize: '1.15em' }}>{user?.nom || 'Responsable'}</span>
          </h1>
        </div>
        <div className="cap">
          Périmètre : {filterFiliere === 'TOUS' ? 'Toutes filières' : `Filière ${filterFiliere}`}
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { l: 'Étudiants suivis',  v: scopedStudents.length, s: '',     t: '' },
          { l: 'Moyenne globale',   v: moyGlobal.toFixed(2),  s: '/20',  t: moyGlobal >= 10 ? 'ok' : 'warn' },
          { l: 'Taux de réussite',  v: tauxReussite.toFixed(1), s: '%',  t: tauxReussite >= 50 ? 'ok' : 'warn' },
          { l: 'Alertes actives',   v: alertes.length,         s: '',    t: alertes.length > 0 ? 'bad' : 'ok' },
        ].map(m => (
          <div key={m.l} className="card p-4">
            <div className="cap mb-1">{m.l}</div>
            <div className="flex items-baseline gap-1">
              <span className="num text-[22px] font-semibold"
                style={{ color: m.t === 'ok' ? 'var(--ok)' : m.t === 'warn' ? 'var(--warn)' : m.t === 'bad' ? 'var(--bad)' : 'var(--text)' }}>
                {m.v}
              </span>
              {m.s && <span className="text-[12px]" style={{ color: 'var(--text-3)' }}>{m.s}</span>}
            </div>
          </div>
        ))}
      </div>

      {/* Outreach funnel */}
      {kpis && (
        <div className="card p-4">
          <div className="flex items-center justify-between mb-3">
            <div className="text-[14px] font-semibold">File d'intervention</div>
            <button className="btn btn-sm btn-ghost" onClick={() => navigate('/cases')}>
              Voir les interventions <Icon name="arrowRight" size={12} />
            </button>
          </div>
          <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
            {([
              { l: 'À contacter',     v: kpis.needsContact,      t: 'bad' },
              { l: 'Email préparé',   v: kpis.emailPrepared,     t: 'warn' },
              { l: 'Entretien planifié', v: kpis.meetingsScheduled, t: 'info' },
              { l: 'Entretien réalisé',  v: kpis.meetingsHeld,    t: 'ok' },
            ] as { l: string; v: number; t: string }[]).map(s => (
              <div key={s.l} className="text-center py-2 px-1 rounded-md" style={{ background: 'var(--surface-2)' }}>
                <div className="num text-[22px] font-semibold" style={{
                  color: s.t === 'ok' ? 'var(--ok)' : s.t === 'bad' ? 'var(--bad)' : s.t === 'warn' ? 'var(--warn)' : 'var(--accent-600)',
                }}>{s.v}</div>
                <div className="cap mt-0.5">{s.l}</div>
              </div>
            ))}
          </div>
          <div className="mt-3 grid grid-cols-1 gap-3 pt-3 sm:grid-cols-2 xl:grid-cols-4" style={{ borderTop: '1px solid var(--border)' }}>
            {([
              { l: 'Étudiants contactés', v: `${kpis.contactedEligiblePercent.toFixed(0)}%`, hint: 'des étudiants à risque' },
              { l: 'Livraison email',      v: `${kpis.emailDeliverySuccessPercent.toFixed(0)}%`, hint: 'Sent / (Sent + Failed)' },
              { l: 'Entretiens tenus',     v: `${kpis.meetingHeldPercent.toFixed(0)}%`, hint: 'tenus / planifiés' },
              { l: 'Délai médian',          v: `${kpis.medianHoursRiskToEmail.toFixed(1)}h`, hint: 'alerte → email' },
            ] as { l: string; v: string; hint: string }[]).map(s => (
              <div key={s.l}>
                <div className="num text-[16px] font-semibold">{s.v}</div>
                <div className="cap mt-0.5">{s.l}</div>
                <div className="cap" style={{ color: 'var(--text-3)', fontSize: 10.5 }}>{s.hint}</div>
              </div>
            ))}
          </div>
          {kpis.duplicateCasesPrevented > 0 && (
            <div className="cap mt-2" style={{ color: 'var(--text-3)' }}>
              {kpis.duplicateCasesPrevented} doublon(s) d'intervention évité(s)
            </div>
          )}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
        {/* At-risk students */}
        <div className="card overflow-hidden xl:col-span-7">
          <div className="p-4 flex items-center justify-between" style={{ borderBottom: '1px solid var(--border)' }}>
            <div>
              <div className="text-[14px] font-semibold">Étudiants à risque élevé</div>
              <div className="cap mt-0.5">Score ML ≥ 60% · action requise</div>
            </div>
            <Pill tone="bad">{atRisk.length}</Pill>
          </div>
          {loadingStudents ? (
            <div className="p-6 cap text-center">Chargement…</div>
          ) : atRisk.length === 0 ? (
            <div className="p-8 cap text-center" style={{ color: 'var(--ok)' }}>
              <Icon name="check" size={24} style={{ margin: '0 auto 8px' }} />
              Aucun étudiant à risque élevé
            </div>
          ) : (
            <table className="tbl">
              <thead>
                <tr>
                  <th>Étudiant</th>
                  <th>Filière</th>
                  <th>Moy.</th>
                  <th>Absences</th>
                  <th>Score ML</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {atRisk.slice(0, 10).map(e => (
                  <tr key={e.id} style={{ cursor: 'pointer' }} onClick={() => navigate(`/students/${e.id}`)}>
                    <td>
                      <div className="flex items-center gap-2.5">
                        <Avatar name={e.nomComplet} size={26} />
                        <div>
                          <div className="font-medium text-[12.5px]">{e.nomComplet}</div>
                          <div className="cap font-mono text-[10.5px]">{e.matricule}</div>
                        </div>
                      </div>
                    </td>
                    <td><Pill tone="neutral">{e.filiereCode} · {e.niveau}</Pill></td>
                    <td className="num font-medium" style={{ color: e.moyenne < 10 ? 'var(--bad)' : 'var(--text)' }}>
                      {e.moyenne.toFixed(2)}
                    </td>
                    <td className="num" style={{ color: e.absences > 30 ? 'var(--warn)' : 'var(--text)' }}>
                      {e.absences}h
                    </td>
                    <td><RiskBar score={e.scoreRisque} width={80} /></td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-sm btn-ghost"
                        aria-label={`Voir la fiche de ${e.nomComplet}`}
                        onClick={() => navigate(`/students/${e.id}`)}
                      >
                        <Icon name="chevRight" size={13} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {atRisk.length > 10 && (
            <div className="px-4 py-2 cap text-center" style={{ borderTop: '1px solid var(--border)' }}>
              +{atRisk.length - 10} autres — <button className="text-accent-600" onClick={() => navigate('/students')}>voir tous</button>
            </div>
          )}
        </div>

        {/* Alerts panel */}
        <div className="card overflow-hidden xl:col-span-5">
          <div className="p-4 flex items-center justify-between" style={{ borderBottom: '1px solid var(--border)' }}>
            <div>
              <div className="text-[14px] font-semibold">Alertes en attente</div>
              <div className="cap mt-0.5">Non résolues</div>
            </div>
            <button className="btn btn-sm btn-ghost" onClick={() => navigate('/alerts')}>
              Voir toutes <Icon name="arrowRight" size={12} />
            </button>
          </div>
          <div className="divide-y" style={{ borderColor: 'var(--border)' }}>
            {loadingAlertes ? (
              <div className="p-6 cap text-center">Chargement…</div>
            ) : alertes.length === 0 ? (
              <div className="p-8 cap text-center" style={{ color: 'var(--ok)' }}>
                <Icon name="check" size={24} style={{ margin: '0 auto 8px' }} />
                Aucune alerte active
              </div>
            ) : alertes.slice(0, 8).map(a => (
              <div key={a.id} className="p-3 flex items-start gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <RiskPill risque={a.niveau === 'Eleve' ? 'eleve' : a.niveau === 'Moyen' ? 'modere' : 'faible'} />
                    <span className="text-[12px] font-medium truncate">{a.etudiant?.nomComplet}</span>
                  </div>
                  <div className="text-[11.5px] truncate" style={{ color: 'var(--text-3)' }}>{a.message}</div>
                </div>
                <button
                  type="button"
                  className="btn btn-sm btn-ghost flex-shrink-0"
                  onClick={() => resolveMutation.mutate(a.id)}
                  disabled={resolveMutation.isPending}
                  title="Marquer comme résolue"
                  aria-label={`Marquer l'alerte de ${a.etudiant?.nomComplet ?? 'cet étudiant'} comme résolue`}
                >
                  <Icon name="check" size={13} />
                </button>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Performance par filière */}
      <div className="card overflow-hidden">
        <div className="p-4 text-[14px] font-semibold" style={{ borderBottom: '1px solid var(--border)' }}>
          Performance par filière
        </div>
        <table className="tbl">
          <thead>
            <tr><th>Filière</th><th>Étudiants</th><th>Moyenne</th><th>Taux réussite</th><th>À risque</th></tr>
          </thead>
          <tbody>
            {Array.from(new Set(students.map(e => e.filiereCode))).sort().map(code => {
              const group = students.filter(e => e.filiereCode === code)
              const avg   = group.reduce((s, e) => s + e.moyenne, 0) / group.length
              const rate  = group.filter(e => e.moyenne >= 10).length / group.length * 100
              const risk  = group.filter(e => e.risque === 'eleve').length
              return (
                <tr key={code}>
                  <td><Pill tone="neutral">{code}</Pill></td>
                  <td className="num">{group.length}</td>
                  <td className="num font-medium" style={{ color: avg >= 10 ? 'var(--ok)' : 'var(--bad)' }}>{avg.toFixed(2)}</td>
                  <td className="num">{rate.toFixed(1)}%</td>
                  <td>
                    {risk > 0
                      ? <Pill tone="bad">{risk} à risque</Pill>
                      : <Pill tone="ok">Aucun</Pill>}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
