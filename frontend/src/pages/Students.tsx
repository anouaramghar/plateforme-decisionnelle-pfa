import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useCopilotReadable, useCopilotAction } from '@copilotkit/react-core'
import { useSearchParams } from 'react-router-dom'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar, RiskPill } from '../components/ui/RiskBar'
import { Select } from '../components/ui/Select'
import { SectionHeader } from '../components/ui/SectionHeader'
import { ChartRadial } from '../components/charts'
import { api } from '../services/api'
import { ConfirmCard } from '../components/copilot/ConfirmCard'
import type { ConfirmData } from '../components/copilot/ConfirmCard'

// Match the seed data — TCP, GI, IA, ROC, IRSI — and ENIAD's CP/CI ladder.
const FILIERES = ['Tous', 'TCP', 'GI', 'IA', 'ROC', 'IRSI']
const NIVEAUX  = ['Tous', 'CP1', 'CP2', 'CI1', 'CI2', 'CI3']
const RISQUES  = ['Tous', 'faible', 'modere', 'eleve']

type Risque = 'faible' | 'modere' | 'eleve'
type Statut = 'Régulier' | 'Suivi' | 'En difficulté'

interface EtudiantRow {
  id: number
  matricule: string
  nom: string
  prenom: string
  nomComplet: string
  filiereCode: string
  filiereIntitule: string
  niveau: string
  moyenne: number
  absences: number
  modulesValides: number
  modulesTotal: number
  scoreRisque: number
  risque: Risque
  statut: Statut
}

interface NoteRow {
  moduleId: number
  code: string
  nom: string
  semestre: string
  coef: number
  cc: number | null
  tp: number | null
  exam: number | null
  finale: number | null
  valide: boolean
}

async function fetchStudents(): Promise<EtudiantRow[]> {
  const res = await api.get<EtudiantRow[]>('/etudiants/with-stats')
  return res.data
}

async function fetchStudentNotes(id: number): Promise<NoteRow[]> {
  const res = await api.get<NoteRow[]>(`/etudiants/${id}/notes`)
  return res.data
}

export default function Students() {
  const { data: all = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['etudiants-with-stats'],
    queryFn: fetchStudents,
    refetchInterval: 60_000,
  })

  const [q, setQ] = useState('')
  const [niveau, setNiveau] = useState('Tous')
  const [risque, setRisque] = useState('Tous')
  const [filiere, setFiliere] = useState('Tous')
  const [selected, setSelected] = useState<EtudiantRow | null>(null)
  const [page, setPage] = useState(1)
  const [pendingAlert, setPendingAlert] = useState<{
    student: EtudiantRow; severite: string; message: string
  } | null>(null)
  const PAGE = 14

  const filtered = useMemo(
    () =>
      all.filter(e => {
        if (filiere !== 'Tous' && e.filiereCode !== filiere) return false
        if (niveau !== 'Tous' && e.niveau !== niveau) return false
        if (risque !== 'Tous' && e.risque !== risque) return false
        if (q && !(e.nomComplet.toLowerCase().includes(q.toLowerCase()) || e.matricule.toLowerCase().includes(q.toLowerCase())))
          return false
        return true
      }),
    [all, filiere, niveau, risque, q],
  )
  // Reset to page 1 whenever filters change, otherwise the user can land on
  // an empty page (e.g. page 3 of a result set that now only has 1 page).
  useEffect(() => {
    setPage(1)
  }, [filiere, niveau, risque, q])
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE))
  const slice = filtered.slice((page - 1) * PAGE, page * PAGE)

  const [searchParams] = useSearchParams()

  // Pre-fill search from URL ?q= param (used by navigate_to_student copilot action)
  useEffect(() => {
    const qParam = searchParams.get('q')
    if (qParam) setQ(qParam)
  }, [searchParams])

  useCopilotReadable({
    description: 'Currently visible students after filters — name, matricule, filière, niveau, risk score',
    value: slice.map(s => ({
      matricule: s.matricule,
      name: s.nomComplet,
      filiere: s.filiereCode,
      niveau: s.niveau,
      averageScore: s.moyenne,
      absences: s.absences,
      riskScore: s.scoreRisque,
      riskLevel: s.risque,
    })),
  })

  useCopilotAction({
    name: 'filter_students',
    description: 'Filter the student table by filière, niveau, risk level, or search text.',
    parameters: [
      { name: 'filiere', type: 'string', description: 'Filière code: TCP, GI, IA, ROC, IRSI, or Tous to clear.', required: false },
      { name: 'niveau', type: 'string', description: 'Academic level: CP1, CP2, CI1, CI2, CI3, or Tous.', required: false },
      { name: 'risque', type: 'string', description: 'Risk level: faible, modere, eleve, or Tous.', required: false },
      { name: 'search', type: 'string', description: 'Free text search on name or matricule.', required: false },
    ],
    handler: async ({ filiere: f, niveau: n, risque: r, search: s }: { filiere?: string; niveau?: string; risque?: string; search?: string }) => {
      if (f !== undefined) setFiliere(f)
      if (n !== undefined) setNiveau(n)
      if (r !== undefined) setRisque(r)
      if (s !== undefined) setQ(s)
      return 'Filtres appliqués'
    },
  })

  useCopilotAction({
    name: 'open_student',
    description: 'Open the detail panel for a specific student by matricule.',
    parameters: [
      { name: 'matricule', type: 'string', description: 'Student matricule to open.', required: true },
    ],
    handler: async ({ matricule }: { matricule: string }) => {
      const student = all.find(s => s.matricule.toLowerCase() === matricule.toLowerCase())
      if (!student) return `Étudiant ${matricule} introuvable`
      setSelected(student)
      return `Profil de ${student.nomComplet} ouvert`
    },
  })

  useCopilotAction({
    name: 'draft_alert',
    description:
      'Propose creating an alert for a student at risk. This only drafts the alert — ' +
      'the user must click "Confirmer" in the UI before anything is saved. ' +
      'Severity values: high, medium, low.',
    parameters: [
      { name: 'matricule', type: 'string', description: 'Student matricule', required: true },
      { name: 'severity', type: 'string', description: 'Alert severity: high, medium, or low', required: true },
      { name: 'message', type: 'string', description: 'Alert message content', required: true },
    ],
    renderAndWaitForResponse: ({ args, status, respond }: { args: { matricule?: string; severity?: string; message?: string }; status: string; respond?: (r: unknown) => void }) => {
      const student = all.find(s => s.matricule.toLowerCase() === (args.matricule ?? '').toLowerCase())
      
      const confirmData: ConfirmData = {
        draftId: 0,
        preview: {
          student_name: student?.nomComplet ?? args.matricule ?? '',
          matricule: args.matricule ?? '',
          severity: args.severity ?? 'medium',
          message: args.message ?? '',
        },
        tool: 'draft_alert',
        state: status === 'complete' ? 'confirmed' : 'pending',
      }

      const VALID_SEVERITES: Record<string, string> = { high: 'eleve', medium: 'modere', low: 'faible' }
      const NIVEAU_MAP: Record<string, string> = { eleve: 'Eleve', modere: 'Moyen', faible: 'Faible' }

      return (
        <ConfirmCard
          data={confirmData}
          onConfirm={async () => {
            const severite = VALID_SEVERITES[args.severity ?? 'medium']
            if (!severite || !student) {
              respond?.({ confirmed: false })
              return
            }
            await api.post('/alertes', {
              etudiantId: student.id,
              type: 'RisqueEchec',
              niveau: NIVEAU_MAP[severite] ?? 'Moyen',
              message: args.message ?? '',
            })
            respond?.({ confirmed: true })
          }}
          onDismiss={() => respond?.({ confirmed: false })}
        />
      )
    },
  })

  async function confirmAlert() {
    if (!pendingAlert) return
    const NIVEAU_MAP: Record<string, string> = { eleve: 'Eleve', modere: 'Moyen', faible: 'Faible' }
    await api.post('/alertes', {
      etudiantId: pendingAlert.student.id,
      type: 'RisqueEchec',
      niveau: NIVEAU_MAP[pendingAlert.severite] ?? 'Moyen',
      message: pendingAlert.message,
    })
    setPendingAlert(null)
  }

  return (
    <div className="space-y-4">
      {pendingAlert && (
        <div
          className="card p-4 flex items-start gap-4"
          style={{ borderColor: 'var(--warn)', background: 'color-mix(in oklch, var(--warn) 8%, transparent)' }}
        >
          <div className="flex-1 min-w-0">
            <div className="text-[13px] font-semibold mb-0.5">Brouillon d'alerte — confirmation requise</div>
            <div className="text-[12.5px]" style={{ color: 'var(--text-2)' }}>
              <span className="font-medium">{pendingAlert.student.nomComplet}</span>
              {' · '}sévérité <span className="font-medium">{pendingAlert.severite}</span>
            </div>
            <div className="text-[12px] mt-1" style={{ color: 'var(--text-3)' }}>{pendingAlert.message}</div>
          </div>
          <div className="flex items-center gap-2 flex-shrink-0">
            <button className="btn btn-sm" onClick={() => setPendingAlert(null)}>Annuler</button>
            <button className="btn btn-sm btn-primary" onClick={confirmAlert}>Confirmer l'envoi</button>
          </div>
        </div>
      )}
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">
            {filtered.length} résultat{filtered.length > 1 ? 's' : ''} · {all.length} au total
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Étudiants</h1>
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">
            <Icon name="upload" size={13} />
            Importer CSV
          </button>
          <button className="btn btn-sm">
            <Icon name="download" size={13} />
            Exporter
          </button>
          <button className="btn btn-sm btn-accent">
            <Icon name="plus" size={13} strokeWidth={2.2} />
            Inscrire
          </button>
        </div>
      </div>

      <div className="card p-3 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[260px]">
          <Icon
            name="search"
            size={14}
            className="absolute left-3 top-1/2 -translate-y-1/2"
            style={{ color: 'var(--text-3)' }}
          />
          <input
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Rechercher par nom ou matricule…"
            className="input pl-9"
          />
        </div>
        <Select value={filiere} onChange={setFiliere} options={FILIERES} label="Filière" />
        <Select value={niveau} onChange={setNiveau} options={NIVEAUX} label="Niveau" />
        <Select value={risque} onChange={setRisque} options={RISQUES} label="Risque" />
        <button className="btn btn-sm btn-ghost">
          <Icon name="filter" size={13} />
          Plus de filtres
        </button>
        {(q || filiere !== 'Tous' || niveau !== 'Tous' || risque !== 'Tous') && (
          <button
            onClick={() => {
              setQ('')
              setFiliere('Tous')
              setNiveau('Tous')
              setRisque('Tous')
            }}
            className="btn btn-sm btn-ghost"
            style={{ color: 'var(--accent-700)' }}
          >
            Réinitialiser
          </button>
        )}
      </div>

      {isLoading ? (
        <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
          Chargement des étudiants…
        </div>
      ) : isError ? (
        <div className="card p-8 text-center" style={{ color: 'var(--bad)' }}>
          Impossible de charger la liste.
          <div className="mt-3">
            <button className="btn btn-sm" onClick={() => refetch()}>Réessayer</button>
          </div>
        </div>
      ) : (
      <div className="card overflow-hidden">
        <table className="tbl">
          <thead>
            <tr>
              <th style={{ width: 32 }}>
                <input type="checkbox" className="rounded border-stone-300" />
              </th>
              <th>Étudiant</th>
              <th>Matricule</th>
              <th>Filière</th>
              <th>Niveau</th>
              <th>Moy.</th>
              <th>Modules</th>
              <th>Absences</th>
              <th>Score ML</th>
              <th>Statut</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {slice.map(e => (
              <tr key={e.id} onClick={() => setSelected(e)} style={{ cursor: 'pointer' }}>
                <td onClick={ev => ev.stopPropagation()}>
                  <input type="checkbox" className="rounded border-stone-300" />
                </td>
                <td>
                  <div className="flex items-center gap-2.5">
                    <Avatar name={e.nomComplet} />
                    <div className="font-medium">{e.nomComplet}</div>
                  </div>
                </td>
                <td className="font-mono cap">{e.matricule}</td>
                <td>
                  <Pill tone="neutral">{e.filiereCode}</Pill>
                </td>
                <td className="font-mono">{e.niveau}</td>
                <td className="num font-medium">{e.moyenne.toFixed(2)}</td>
                <td className="num">
                  <span style={{ color: e.modulesValides >= Math.max(1, Math.ceil(e.modulesTotal / 2)) ? 'var(--ok)' : 'var(--warn)' }}>
                    {e.modulesValides}
                  </span>
                  <span style={{ color: 'var(--text-4)' }}>/{e.modulesTotal}</span>
                </td>
                <td className="num">{e.absences}h</td>
                <td>
                  <RiskBar score={e.scoreRisque} width={64} />
                </td>
                <td>
                  <Pill
                    tone={e.statut === 'Régulier' ? 'ok' : e.statut === 'Suivi' ? 'warn' : 'bad'}
                    dot
                  >
                    {e.statut}
                  </Pill>
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
        <div
          className="flex items-center justify-between px-4 py-2.5"
          style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--border)' }}
        >
          <div className="cap">
            {filtered.length === 0
              ? 'Aucun résultat'
              : `Affichage ${(page - 1) * PAGE + 1}–${Math.min(page * PAGE, filtered.length)} sur ${filtered.length}`}
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(Math.max(1, page - 1))}
              className="btn btn-sm btn-ghost"
            >
              <Icon name="chevLeft" size={13} />
            </button>
            <span className="px-2 text-[12px]">
              {page} / {pages}
            </span>
            <button
              onClick={() => setPage(Math.min(pages, page + 1))}
              className="btn btn-sm btn-ghost"
            >
              <Icon name="chevRight" size={13} />
            </button>
          </div>
        </div>
      </div>
      )}

      {selected && <StudentDrawer student={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

function StudentDrawer({ student, onClose }: { student: EtudiantRow; onClose: () => void }) {
  const { data: notes = [], isLoading } = useQuery({
    queryKey: ['etudiant-notes', student.id],
    queryFn: () => fetchStudentNotes(student.id),
  })

  const moyenne = student.moyenne
  const riskColor =
    student.risque === 'eleve' ? '#dc2626' : student.risque === 'modere' ? '#f59e0b' : '#16a34a'

  // Accessibility for a modal drawer: ESC closes it, focus jumps inside on
  // open (so a screen reader announces the dialog), focus returns to the
  // trigger row on close, and Tab is trapped within the panel so users can't
  // tab-out behind the backdrop.
  const panelRef = useRef<HTMLDivElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null
    closeRef.current?.focus()

    const onKey = (ev: KeyboardEvent) => {
      if (ev.key === 'Escape') {
        ev.stopPropagation()
        onClose()
        return
      }
      if (ev.key === 'Tab' && panelRef.current) {
        const focusables = panelRef.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        )
        if (focusables.length === 0) return
        const first = focusables[0]
        const last  = focusables[focusables.length - 1]
        if (ev.shiftKey && document.activeElement === first) {
          last.focus()
          ev.preventDefault()
        } else if (!ev.shiftKey && document.activeElement === last) {
          first.focus()
          ev.preventDefault()
        }
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      previouslyFocused?.focus?.()
    }
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-40"
      onClick={onClose}
      style={{ background: 'rgba(12,10,9,0.32)' }}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={`Fiche étudiant — ${student.nomComplet}`}
        onClick={e => e.stopPropagation()}
        className="drawer absolute right-0 top-0 bottom-0 w-[640px] flex flex-col"
        style={{ background: 'var(--surface)' }}
      >
        <div
          className="flex items-center gap-3 p-5"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <Avatar name={student.nomComplet} size={44} ring />
          <div className="flex-1">
            <div className="text-[16px] font-semibold tracking-tight">{student.nomComplet}</div>
            <div className="flex items-center gap-2 mt-1">
              <span className="cap font-mono">{student.matricule}</span>
              <span style={{ color: 'var(--text-4)' }}>•</span>
              <Pill tone="neutral">
                {student.filiereCode} · {student.niveau}
              </Pill>
              <RiskPill risque={student.risque} />
            </div>
          </div>
          <button
            ref={closeRef}
            onClick={onClose}
            aria-label="Fermer la fiche étudiant"
            className="btn btn-sm btn-ghost"
          >
            <Icon name="x" size={14} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto scroll-thin p-5 space-y-5">
          <div className="grid grid-cols-4 gap-3">
            {[
              { l: 'Moyenne', v: moyenne.toFixed(2), s: '/20', t: moyenne >= 10 ? 'ok' : 'bad' },
              { l: 'Modules validés', v: student.modulesValides, s: `/${student.modulesTotal}` },
              { l: 'Absences', v: student.absences, s: 'h', t: student.absences > 20 ? 'warn' : '' },
              {
                l: 'Score ML',
                v: Math.round(student.scoreRisque * 100),
                s: '%',
                t:
                  student.risque === 'eleve'
                    ? 'bad'
                    : student.risque === 'modere'
                    ? 'warn'
                    : 'ok',
              },
            ].map(m => (
              <div
                key={m.l}
                className="p-3 rounded-lg"
                style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
              >
                <div className="cap mb-1">{m.l}</div>
                <div className="flex items-baseline gap-1">
                  <span
                    className="num text-[20px] font-medium"
                    style={{
                      color:
                        m.t === 'ok'
                          ? 'var(--ok)'
                          : m.t === 'warn'
                          ? 'var(--warn)'
                          : m.t === 'bad'
                          ? 'var(--bad)'
                          : 'var(--text)',
                    }}
                  >
                    {m.v}
                  </span>
                  <span className="text-[11.5px]" style={{ color: 'var(--text-3)' }}>
                    {m.s}
                  </span>
                </div>
              </div>
            ))}
          </div>

          <div>
            <SectionHeader
              title="Notes par module"
              subtitle={`${student.niveau} — ${student.filiereIntitule}`}
              right={
                <button className="btn btn-sm">
                  <Icon name="plus" size={12} />
                  Saisir
                </button>
              }
            />
            <div className="card overflow-hidden">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Module</th>
                    <th>CC</th>
                    <th>TP</th>
                    <th>Examen</th>
                    <th>Finale</th>
                    <th>Statut</th>
                  </tr>
                </thead>
                <tbody>
                  {isLoading ? (
                    <tr>
                      <td colSpan={7} className="cap text-center py-4">Chargement…</td>
                    </tr>
                  ) : notes.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="cap text-center py-4">Aucune note enregistrée</td>
                    </tr>
                  ) : (
                    notes.map(n => (
                      <tr key={n.moduleId}>
                        <td className="font-mono cap">{n.code}</td>
                        <td className="font-medium">{n.nom}</td>
                        <td className="num">{n.cc != null ? n.cc.toFixed(1) : '—'}</td>
                        <td className="num">{n.tp != null ? n.tp.toFixed(1) : '—'}</td>
                        <td className="num">{n.exam != null ? n.exam.toFixed(1) : '—'}</td>
                        <td
                          className="num font-medium"
                          style={{ color: n.finale != null && n.finale >= 10 ? 'var(--ok)' : 'var(--bad)' }}
                        >
                          {n.finale != null ? n.finale.toFixed(2) : '—'}
                        </td>
                        <td>
                          {n.valide ? (
                            <Pill tone="ok" dot>Validé</Pill>
                          ) : (
                            <Pill tone="bad" dot>Non validé</Pill>
                          )}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="card p-4">
            <SectionHeader
              title="Analyse prédictive"
              subtitle="Facteurs de risque détectés par le modèle"
            />
            <div className="flex items-center gap-5">
              <div style={{ width: 130, flexShrink: 0 }}>
                <ChartRadial
                  value={student.scoreRisque}
                  label="Risque"
                  color={riskColor}
                  height={140}
                />
              </div>
              <div className="flex-1 space-y-2">
                {[
                  {
                    f: 'Moyenne basse',
                    w: student.moyenne < 10 ? 0.82 : student.moyenne < 12 ? 0.42 : 0.12,
                    neg: student.moyenne < 12,
                  },
                  {
                    f: 'Absences élevées',
                    w: Math.min(0.95, student.absences / 30),
                    neg: student.absences > 15,
                  },
                  {
                    f: 'Modules non validés',
                    w: student.modulesTotal > 0
                      ? 1 - student.modulesValides / student.modulesTotal
                      : 0,
                    neg: student.modulesTotal > 0
                      && student.modulesValides < Math.ceil(student.modulesTotal / 2),
                  },
                  { f: 'Tendance trimestrielle', w: 0.34, neg: false },
                ].map(row => (
                  <div key={row.f} className="flex items-center gap-3">
                    <span className="text-[12px]" style={{ minWidth: 160 }}>
                      {row.f}
                    </span>
                    <div
                      className="flex-1 h-1.5 rounded-full"
                      style={{ background: 'var(--surface-3)' }}
                    >
                      <div
                        style={{
                          width: `${row.w * 100}%`,
                          height: '100%',
                          background: row.neg ? 'var(--bad)' : 'var(--ok)',
                          borderRadius: 999,
                        }}
                      />
                    </div>
                    <span
                      className="num text-[11.5px] w-10 text-right"
                      style={{ color: 'var(--text-3)' }}
                    >
                      {Math.round(row.w * 100)}%
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div
          className="p-4 flex items-center justify-between"
          style={{ borderTop: '1px solid var(--border)', background: 'var(--surface-2)' }}
        >
          <button className="btn btn-sm">
            <Icon name="ext" size={12} />
            Ouvrir la fiche complète
          </button>
          <div className="flex items-center gap-2">
            <button className="btn btn-sm">Envoyer à la coordination</button>
            <button className="btn btn-sm btn-accent">Planifier un entretien</button>
          </div>
        </div>
      </div>
    </div>
  )
}
