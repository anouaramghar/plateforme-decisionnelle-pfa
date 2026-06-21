import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import * as XLSX from 'xlsx'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar, RiskPill } from '../components/ui/RiskBar'
import { Select } from '../components/ui/Select'
import { SectionHeader } from '../components/ui/SectionHeader'
import { ChartRadial, ChartShap } from '../components/charts'
import type { ShapExplainData } from '../components/charts'
import { api } from '../services/api'

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
  annee: string
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

async function fetchShap(id: number): Promise<ShapExplainData> {
  const res = await api.get<ShapExplainData>(`/predictions/explain/${id}`)
  return res.data
}

export default function Students() {
  const { data: all = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['etudiants-with-stats'],
    queryFn: fetchStudents,
    refetchInterval: 60_000,
  })

  const navigate = useNavigate()
  const csvRef = useRef<HTMLInputElement>(null)

  const [q, setQ] = useState('')
  const [niveau, setNiveau] = useState('Tous')
  const [risque, setRisque] = useState('Tous')
  const [filiere, setFiliere] = useState('Tous')
  const [statut, setStatut] = useState('Tous')
  const [showExtra, setShowExtra] = useState(false)
  const [importing, setImporting] = useState(false)
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
        if (statut !== 'Tous' && e.statut !== statut) return false
        if (q && !(e.nomComplet.toLowerCase().includes(q.toLowerCase()) || e.matricule.toLowerCase().includes(q.toLowerCase())))
          return false
        return true
      }),
    [all, filiere, niveau, risque, statut, q],
  )
  useEffect(() => {
    setPage(1)
  }, [filiere, niveau, risque, statut, q])

  const handleExport = () => {
    const rows = filtered.map(e => ({
      Matricule: e.matricule,
      Nom: e.nom,
      Prénom: e.prenom,
      Filière: e.filiereCode,
      Niveau: e.niveau,
      Moyenne: e.moyenne,
      Absences: `${e.absences}h`,
      'Modules validés': `${e.modulesValides}/${e.modulesTotal}`,
      'Score risque': `${Math.round(e.scoreRisque * 100)}%`,
      Statut: e.statut,
    }))
    const ws = XLSX.utils.json_to_sheet(rows)
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Étudiants')
    XLSX.writeFile(wb, `etudiants-${new Date().toISOString().slice(0, 10)}.xlsx`)
  }

  const handleImportFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setImporting(true)
    try {
      const text = await file.text()
      const lines = text.split('\n').map(l => l.trim()).filter(Boolean)
      if (lines.length < 2) return
      const header = lines[0].split(',').map(h => h.trim().toLowerCase())
      const filRes = await api.get<{ items: { id: number; code: string }[] }>('/filieres?pageSize=50')
      const filMap = Object.fromEntries(filRes.data.items.map(f => [f.code.toLowerCase(), f.id]))
      for (const line of lines.slice(1)) {
        const cols = line.split(',').map(c => c.trim())
        const row = Object.fromEntries(header.map((h, i) => [h, cols[i] ?? '']))
        const filiereId = filMap[(row.filierecode ?? row.filiere ?? '').toLowerCase()]
        if (!filiereId || !row.matricule) continue
        await api.post('/etudiants', {
          matricule: row.matricule,
          nom: row.nom ?? '',
          prenom: row.prenom ?? '',
          email: row.email ?? '',
          niveau: row.niveau ?? 'CP1',
          annee: row.annee ?? '2025/2026',
          filiereId,
        })
      }
      refetch()
    } finally {
      setImporting(false)
      e.target.value = ''
    }
  }
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE))
  const slice = filtered.slice((page - 1) * PAGE, page * PAGE)

  const [searchParams] = useSearchParams()

  // Pre-fill search from URL ?q= param (used by navigate_to_student copilot action)
  useEffect(() => {
    const qParam = searchParams.get('q')
    if (qParam) setQ(qParam)
  }, [searchParams])

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
          <input ref={csvRef} type="file" accept=".csv" className="hidden" onChange={handleImportFile} />
          <button className="btn btn-sm" onClick={() => csvRef.current?.click()} disabled={importing}>
            <Icon name="upload" size={13} />
            {importing ? 'Import…' : 'Importer CSV'}
          </button>
          <button className="btn btn-sm" onClick={handleExport}>
            <Icon name="download" size={13} />
            Exporter
          </button>
          <button
            className="btn btn-sm btn-accent"
            onClick={() => navigate('/admin', { state: { tab: 'etudiants', openCreate: true } })}
          >
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
            className="input"
            style={{ paddingLeft: '2.25rem' }}
          />
        </div>
        <Select value={filiere} onChange={setFiliere} options={FILIERES} label="Filière" />
        <Select value={niveau} onChange={setNiveau} options={NIVEAUX} label="Niveau" />
        <Select value={risque} onChange={setRisque} options={RISQUES} label="Risque" />
        {showExtra && (
          <Select value={statut} onChange={setStatut} options={['Tous', 'Régulier', 'Suivi', 'En difficulté']} label="Statut" />
        )}
        <button
          className="btn btn-sm btn-ghost"
          onClick={() => setShowExtra(v => !v)}
          style={{ color: showExtra ? 'var(--accent-700)' : undefined }}
        >
          <Icon name="filter" size={13} />
          Plus de filtres
        </button>
        {(q || filiere !== 'Tous' || niveau !== 'Tous' || risque !== 'Tous' || statut !== 'Tous') && (
          <button
            onClick={() => {
              setQ('')
              setFiliere('Tous')
              setNiveau('Tous')
              setRisque('Tous')
              setStatut('Tous')
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

      {selected && (
        <StudentDrawer
          student={selected}
          onClose={() => setSelected(null)}
          onAlert={(severite, message) => setPendingAlert({ student: selected, severite, message })}
        />
      )}
    </div>
  )
}

function StudentDrawer({
  student,
  onClose,
  onAlert,
}: {
  student: EtudiantRow
  onClose: () => void
  onAlert?: (severite: string, message: string) => void
}) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: notes = [], isLoading } = useQuery({
    queryKey: ['etudiant-notes', student.id],
    queryFn: () => fetchStudentNotes(student.id),
  })

  const { data: shap, isLoading: shapLoading } = useQuery({
    queryKey: ['etudiant-shap', student.id],
    queryFn: () => fetchShap(student.id),
    retry: false,
  })

  // ── Note form ──────────────────────────────────────────────────────────────
  const [showNoteForm, setShowNoteForm] = useState(false)
  const [noteForm, setNoteForm] = useState({
    moduleId: 0, noteTD: '', noteTP: '', noteExamen: '', noteFinal: '', semestre: 'S1', annee: '2025/2026',
  })
  const [savingNote, setSavingNote]   = useState(false)
  const [noteSuccess, setNoteSuccess] = useState(false)

  const submitNote = async () => {
    if (!noteForm.moduleId) return
    setSavingNote(true)
    try {
      await api.post('/notes', {
        etudiantId:  student.id,
        moduleId:    noteForm.moduleId,
        noteTD:      noteForm.noteTD      ? parseFloat(noteForm.noteTD)      : null,
        noteTP:      noteForm.noteTP      ? parseFloat(noteForm.noteTP)      : null,
        noteExamen:  noteForm.noteExamen  ? parseFloat(noteForm.noteExamen)  : null,
        noteFinal:   noteForm.noteFinal   ? parseFloat(noteForm.noteFinal)   : null,
        semestre:    noteForm.semestre,
        annee:       noteForm.annee,
      })
      queryClient.invalidateQueries({ queryKey: ['etudiant-notes', student.id] })
      queryClient.invalidateQueries({ queryKey: ['etudiants-with-stats'] })
      setShowNoteForm(false)
      setNoteSuccess(true)
      setTimeout(() => setNoteSuccess(false), 3000)
    } finally {
      setSavingNote(false)
    }
  }

  // ── Alert actions ──────────────────────────────────────────────────────────
  const [sendingEntretien, setSendingEntretien] = useState(false)
  const [entretienSent, setEntretienSent]       = useState(false)

  const handleEnvoyerCoordination = () => {
    onAlert?.(
      'eleve',
      `${student.nomComplet} (${student.filiereCode} ${student.niveau}) — score ML ${Math.round(student.scoreRisque * 100)}%, moyenne ${student.moyenne.toFixed(2)}/20. Suivi requis.`,
    )
    onClose()
  }

  const handlePlanifierEntretien = async () => {
    setSendingEntretien(true)
    try {
      await api.post('/alertes', {
        etudiantId: student.id,
        type:       'RisqueEchec',
        niveau:     'Eleve',
        message:    `Entretien à planifier — ${student.nomComplet} · ${student.filiereCode} ${student.niveau} · Score ML : ${Math.round(student.scoreRisque * 100)}%`,
      })
      setEntretienSent(true)
      setTimeout(() => setEntretienSent(false), 3000)
    } finally {
      setSendingEntretien(false)
    }
  }

  const moyenne = student.moyenne
  const riskColor =
    student.risque === 'eleve' ? '#dc2626' : student.risque === 'modere' ? '#f59e0b' : '#16a34a'

  const trend = useMemo(() => {
    if (!notes || notes.length === 0) return null;
    const notesWithFinal = notes.filter(n => n.finale !== null);
    if (notesWithFinal.length === 0) return null;

    const getSemSortKey = (annee: string, semestre: string) => {
      const startYear = parseInt(annee.split('/')[0]) || 0;
      const semNum = semestre === 'S2' ? 2 : 1;
      return startYear * 2 + semNum;
    };

    const semData: { [key: number]: { sum: number; count: number } } = {};
    notesWithFinal.forEach(n => {
      const key = getSemSortKey(n.annee, n.semestre);
      if (!semData[key]) {
        semData[key] = { sum: 0, count: 0 };
      }
      semData[key].sum += n.finale!;
      semData[key].count += 1;
    });

    const sortedKeys = Object.keys(semData)
      .map(Number)
      .sort((a, b) => b - a);

    if (sortedKeys.length < 2) return null;

    const avgRecent = semData[sortedKeys[0]].sum / semData[sortedKeys[0]].count;
    const avgPrev = semData[sortedKeys[1]].sum / semData[sortedKeys[1]].count;
    return avgRecent - avgPrev;
  }, [notes]);

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
                <button className="btn btn-sm" onClick={() => setShowNoteForm(v => !v)}>
                  <Icon name="plus" size={12} />
                  {showNoteForm ? 'Fermer' : 'Saisir'}
                </button>
              }
            />
            {noteSuccess && (
              <div className="mb-2 px-3 py-2 rounded-lg text-[12.5px] font-medium" style={{ background: 'color-mix(in oklch, var(--ok) 12%, transparent)', color: 'var(--ok)' }}>
                Note enregistrée avec succès.
              </div>
            )}
            {showNoteForm && (
              <div className="mb-3 p-4 rounded-xl border space-y-3" style={{ background: 'var(--surface-2)', borderColor: 'var(--border)' }}>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <div className="cap mb-1">Module</div>
                    {notes.length > 0 ? (
                      <select
                        className="input"
                        value={noteForm.moduleId}
                        onChange={e => setNoteForm(f => ({ ...f, moduleId: parseInt(e.target.value) }))}
                      >
                        <option value={0}>Choisir un module…</option>
                        {notes.map(n => (
                          <option key={n.moduleId} value={n.moduleId}>{n.code} — {n.nom}</option>
                        ))}
                      </select>
                    ) : (
                      <input className="input" type="number" placeholder="ID du module" value={noteForm.moduleId || ''}
                        onChange={e => setNoteForm(f => ({ ...f, moduleId: parseInt(e.target.value) || 0 }))} />
                    )}
                  </div>
                  <div>
                    <div className="cap mb-1">Semestre</div>
                    <select className="input" value={noteForm.semestre} onChange={e => setNoteForm(f => ({ ...f, semestre: e.target.value }))}>
                      <option value="S1">S1</option>
                      <option value="S2">S2</option>
                    </select>
                  </div>
                </div>
                <div className="grid grid-cols-4 gap-2">
                  {(['noteTD', 'noteTP', 'noteExamen', 'noteFinal'] as const).map((field, i) => (
                    <div key={field}>
                      <div className="cap mb-1 text-[11px]">{['CC', 'TP', 'Examen', 'Finale'][i]}</div>
                      <input className="input" type="number" min={0} max={20} step={0.01} placeholder="/20"
                        value={noteForm[field]}
                        onChange={e => setNoteForm(f => ({ ...f, [field]: e.target.value }))} />
                    </div>
                  ))}
                </div>
                <div className="flex justify-end gap-2">
                  <button className="btn btn-sm" onClick={() => setShowNoteForm(false)}>Annuler</button>
                  <button className="btn btn-sm btn-accent" onClick={submitNote} disabled={savingNote || !noteForm.moduleId}>
                    {savingNote ? 'Enregistrement…' : 'Enregistrer'}
                  </button>
                </div>
              </div>
            )}
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
              title="Analyse prédictive (SHAP)"
              subtitle="Contribution réelle de chaque facteur — calculée par le modèle XGBoost"
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
              <div className="flex-1">
                {shapLoading && (
                  <div className="text-[12px]" style={{ color: 'var(--text-4)' }}>
                    Calcul SHAP en cours…
                  </div>
                )}
                {shap && <ChartShap data={shap} />}
                {!shapLoading && !shap && (
                  <div className="space-y-2">
                    {[
                      {
                        f: 'Tendance trimestrielle',
                        w: trend !== null ? Math.min(Math.abs(trend) / 5.0, 1.0) : 0,
                        neg: trend !== null && trend < 0,
                        display: trend !== null ? (trend >= 0 ? `+${trend.toFixed(1)}` : trend.toFixed(1)) : '—',
                      },
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
                      {row.display}
                    </span>
                  </div>
                ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        <div
          className="p-4 flex items-center justify-between"
          style={{ borderTop: '1px solid var(--border)', background: 'var(--surface-2)' }}
        >
          <button
            className="btn btn-sm"
            onClick={() => { navigate(`/students/${student.id}`); onClose() }}
          >
            <Icon name="ext" size={12} />
            Ouvrir la fiche complète
          </button>
          <div className="flex items-center gap-2">
            <button className="btn btn-sm" onClick={handleEnvoyerCoordination}>
              Envoyer à la coordination
            </button>
            <button
              className="btn btn-sm btn-accent"
              onClick={handlePlanifierEntretien}
              disabled={sendingEntretien}
            >
              {entretienSent ? 'Alerte créée ✓' : sendingEntretien ? 'Envoi…' : 'Planifier un entretien'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
