import { useMemo, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar, RiskPill } from '../components/ui/RiskBar'
import { ChartRadial, ChartShap } from '../components/charts'
import type { ShapExplainData } from '../components/charts'
import { api } from '../services/api'
import { fetchModules, modulesForStudent, upsertNote } from '../services/notes'
import type { AxiosError } from 'axios'
import { canCreateAlerts, canEnterNotes } from '../auth/roles'
import { useAuth } from '../context/AuthContext'

interface EtudiantRow {
  id: number; matricule: string; nom: string; prenom: string; nomComplet: string
  filiereId: number; filiereCode: string; filiereIntitule: string; niveau: string
  moyenne: number; absences: number; modulesValides: number; modulesTotal: number
  scoreRisque: number; risque: 'faible' | 'modere' | 'eleve'; statut: string
}

interface NoteRow {
  moduleId: number; code: string; nom: string; semestre: string; annee: string
  coef: number; cc: number | null; tp: number | null; exam: number | null
  finale: number | null; valide: boolean
}

async function fetchStudents(): Promise<EtudiantRow[]> {
  const res = await api.get<EtudiantRow[]>('/etudiants/with-stats')
  return res.data
}

async function fetchNotes(id: number): Promise<NoteRow[]> {
  const res = await api.get<NoteRow[]>(`/etudiants/${id}/notes`)
  return res.data
}

async function fetchShap(id: number): Promise<ShapExplainData> {
  const res = await api.get<ShapExplainData>(`/predictions/explain/${id}`)
  return res.data
}

export default function StudentProfile() {
  const { user } = useAuth()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const studentId = parseInt(id ?? '0')

  const { data: student, isLoading: loadingStudent } = useQuery({
    queryKey: ['etudiants-with-stats'],
    queryFn: fetchStudents,
    select: (list) => list.find(s => s.id === studentId),
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
  })

  const { data: notes = [], isLoading: loadingNotes } = useQuery({
    queryKey: ['etudiant-notes', studentId],
    queryFn: () => fetchNotes(studentId),
    enabled: !!studentId,
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
  })
  const { data: modules = [] } = useQuery({ queryKey: ['modules'], queryFn: fetchModules, staleTime: 10 * 60 * 1000 })

  const { data: shap, isLoading: shapLoading } = useQuery({
    queryKey: ['etudiant-shap', studentId],
    queryFn: () => fetchShap(studentId),
    retry: false,
    enabled: !!studentId,
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
  })

  // ── Note form ──────────────────────────────────────────────────────────────
  const [showNoteForm, setShowNoteForm] = useState(false)
  const [noteForm, setNoteForm] = useState({
    moduleId: 0, noteTD: '', noteTP: '', noteExamen: '', noteFinal: '', semestre: 'S1', annee: '2025/2026',
  })
  const [savingNote, setSavingNote]   = useState(false)
  const [noteSuccess, setNoteSuccess] = useState(false)
  const [noteError, setNoteError]     = useState<string | null>(null)

  const submitNote = async () => {
    if (!noteForm.moduleId || !student) return
    setSavingNote(true)
    try {
      await upsertNote({
        etudiantId:  student.id,
        moduleId:    noteForm.moduleId,
        noteTD:      noteForm.noteTD     ? parseFloat(noteForm.noteTD)     : null,
        noteTP:      noteForm.noteTP     ? parseFloat(noteForm.noteTP)     : null,
        noteExamen:  noteForm.noteExamen ? parseFloat(noteForm.noteExamen) : null,
        noteFinal:   noteForm.noteFinal  ? parseFloat(noteForm.noteFinal)  : null,
        semestre:    noteForm.semestre,
        annee:       noteForm.annee,
      })
      queryClient.invalidateQueries({ queryKey: ['etudiant-notes', studentId] })
      queryClient.invalidateQueries({ queryKey: ['etudiants-with-stats'] })
      setShowNoteForm(false)
      setNoteError(null)
      setNoteSuccess(true)
      setTimeout(() => setNoteSuccess(false), 3000)
    } catch (error) {
      const message = (error as AxiosError<{ message?: string }>).response?.data?.message
      setNoteError(message ?? 'Impossible d’enregistrer la note.')
    } finally {
      setSavingNote(false)
    }
  }

  // ── Alert ──────────────────────────────────────────────────────────────────
  const [sendingAlert, setSendingAlert] = useState(false)
  const [alertSent, setAlertSent]       = useState(false)

  const handlePlanifierEntretien = async () => {
    if (!student) return
    setSendingAlert(true)
    try {
      await api.post('/alertes', {
        etudiantId: student.id,
        type:       'RisqueEchec',
        niveau:     'Eleve',
        message:    `Entretien à planifier — ${student.nomComplet} · ${student.filiereCode} ${student.niveau} · Score ML : ${Math.round(student.scoreRisque * 100)}%`,
      })
      setAlertSent(true)
      setTimeout(() => setAlertSent(false), 4000)
    } finally {
      setSendingAlert(false)
    }
  }

  // ── Trend ──────────────────────────────────────────────────────────────────
  const trend = useMemo(() => {
    if (!notes.length) return null
    const withFinal = notes.filter(n => n.finale !== null)
    if (withFinal.length === 0) return null
    const key = (annee: string, sem: string) => parseInt(annee.split('/')[0]) * 2 + (sem === 'S2' ? 2 : 1)
    const byKey: Record<number, { sum: number; n: number }> = {}
    withFinal.forEach(n => {
      const k = key(n.annee, n.semestre)
      if (!byKey[k]) byKey[k] = { sum: 0, n: 0 }
      byKey[k].sum += n.finale!
      byKey[k].n++
    })
    const sorted = Object.keys(byKey).map(Number).sort((a, b) => b - a)
    if (sorted.length < 2) return null
    return byKey[sorted[0]].sum / byKey[sorted[0]].n - byKey[sorted[1]].sum / byKey[sorted[1]].n
  }, [notes])

  if (loadingStudent) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
        Chargement de la fiche étudiant…
      </div>
    )
  }

  if (!student) {
    return (
      <div className="card p-8 text-center" style={{ color: 'var(--bad)' }}>
        Étudiant introuvable.
        <div className="mt-3">
          <button className="btn btn-sm" onClick={() => navigate('/students')}>Retour</button>
        </div>
      </div>
    )
  }

  const riskColor = student.risque === 'eleve' ? '#dc2626' : student.risque === 'modere' ? '#f59e0b' : '#16a34a'
  const availableModules = modulesForStudent(modules, student.filiereId, student.niveau)

  return (
    <div className="space-y-5 max-w-4xl mx-auto">

      {/* Back + actions */}
      <div className="flex items-center justify-between">
        <button className="btn btn-sm btn-ghost" onClick={() => navigate(-1)}>
          <Icon name="chevLeft" size={13} />
          Retour
        </button>
        <div className="flex items-center gap-2">
          {canCreateAlerts(user?.role) && <button
            className="btn btn-sm btn-accent"
            onClick={handlePlanifierEntretien}
            disabled={sendingAlert}
          >
            {alertSent ? 'Alerte créée ✓' : sendingAlert ? 'Envoi…' : 'Planifier un entretien'}
          </button>}
        </div>
      </div>

      {/* Header card */}
      <div className="card p-6">
        <div className="flex items-center gap-4">
          <Avatar name={student.nomComplet} size={56} ring />
          <div className="flex-1">
            <h1 className="text-[22px] font-semibold tracking-tight">{student.nomComplet}</h1>
            <div className="flex items-center gap-2 mt-1.5 flex-wrap">
              <span className="cap font-mono">{student.matricule}</span>
              <span style={{ color: 'var(--text-4)' }}>•</span>
              <Pill tone="neutral">{student.filiereCode} · {student.niveau}</Pill>
              <RiskPill risque={student.risque} />
              <Pill tone={student.statut === 'Régulier' ? 'ok' : student.statut === 'Suivi' ? 'warn' : 'bad'} dot>
                {student.statut}
              </Pill>
            </div>
            <div className="text-[12.5px] mt-1" style={{ color: 'var(--text-3)' }}>{student.filiereIntitule}</div>
          </div>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { l: 'Moyenne générale', v: student.moyenne.toFixed(2), s: '/20', t: student.moyenne >= 10 ? 'ok' : 'bad' },
          { l: 'Modules validés',  v: student.modulesValides,     s: `/${student.modulesTotal}` },
          { l: 'Absences totales', v: student.absences,           s: 'h',  t: student.absences > 20 ? 'warn' : '' },
          { l: 'Score de risque ML', v: Math.round(student.scoreRisque * 100), s: '%', t: student.risque === 'eleve' ? 'bad' : student.risque === 'modere' ? 'warn' : 'ok' },
        ].map(m => (
          <div key={m.l} className="card p-4">
            <div className="cap mb-1">{m.l}</div>
            <div className="flex items-baseline gap-1">
              <span
                className="num text-[24px] font-semibold"
                style={{ color: m.t === 'ok' ? 'var(--ok)' : m.t === 'warn' ? 'var(--warn)' : m.t === 'bad' ? 'var(--bad)' : 'var(--text)' }}
              >
                {m.v}
              </span>
              <span className="text-[12px]" style={{ color: 'var(--text-3)' }}>{m.s}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Notes */}
      <div className="card overflow-hidden">
        <div className="flex items-center justify-between p-5 pb-3">
          <div>
            <div className="text-[15px] font-semibold tracking-tight">Notes par module</div>
            <div className="cap mt-0.5">{student.niveau} — {student.filiereIntitule}</div>
          </div>
          {canEnterNotes(user?.role) && <button className="btn btn-sm" onClick={() => setShowNoteForm(v => !v)}>
            <Icon name="plus" size={12} />
            {showNoteForm ? 'Fermer' : 'Saisir'}
          </button>}
        </div>

        {noteSuccess && (
          <div className="mx-5 mb-3 px-3 py-2 rounded-lg text-[12.5px] font-medium" style={{ background: 'color-mix(in oklch, var(--ok) 12%, transparent)', color: 'var(--ok)' }}>
            Note enregistrée avec succès.
          </div>
        )}
        {noteError && (
          <div className="mx-5 mb-3 px-3 py-2 rounded-lg text-[12.5px]" style={{ color: 'var(--bad)', background: 'color-mix(in oklch, var(--bad) 8%, transparent)' }}>
            {noteError}
          </div>
        )}

        {showNoteForm && (
          <div className="mx-5 mb-4 p-4 rounded-xl border space-y-3" style={{ background: 'var(--surface-2)', borderColor: 'var(--border)' }}>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <div className="cap mb-1">Module</div>
                {availableModules.length > 0 ? (
                  <select className="input" value={noteForm.moduleId}
                    onChange={e => setNoteForm(f => ({ ...f, moduleId: parseInt(e.target.value) }))}
                  >
                    <option value={0}>Choisir un module…</option>
                    {availableModules.map(module => <option key={module.id} value={module.id}>{module.code} — {module.nom}</option>)}
                  </select>
                ) : (
                  <div className="cap py-2">Aucun module configuré pour cette filière et ce niveau.</div>
                )}
              </div>
              <div>
                <div className="cap mb-1">Semestre</div>
                <select className="input" value={noteForm.semestre}
                  onChange={e => setNoteForm(f => ({ ...f, semestre: e.target.value }))}>
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

        <table className="tbl">
          <thead>
            <tr>
              <th>Code</th>
              <th>Module</th>
              <th>CC</th>
              <th>TP</th>
              <th>Examen</th>
              <th>Finale</th>
              <th>Coef.</th>
              <th>Statut</th>
            </tr>
          </thead>
          <tbody>
            {loadingNotes ? (
              <tr><td colSpan={8} className="cap text-center py-6">Chargement…</td></tr>
            ) : notes.length === 0 ? (
              <tr><td colSpan={8} className="cap text-center py-6">Aucune note enregistrée</td></tr>
            ) : notes.map(n => (
              <tr key={n.moduleId}>
                <td className="font-mono cap">{n.code}</td>
                <td className="font-medium">{n.nom}</td>
                <td className="num">{n.cc != null ? n.cc.toFixed(1) : '—'}</td>
                <td className="num">{n.tp != null ? n.tp.toFixed(1) : '—'}</td>
                <td className="num">{n.exam != null ? n.exam.toFixed(1) : '—'}</td>
                <td className="num font-medium" style={{ color: n.finale != null && n.finale >= 10 ? 'var(--ok)' : 'var(--bad)' }}>
                  {n.finale != null ? n.finale.toFixed(2) : '—'}
                </td>
                <td className="num">{n.coef}</td>
                <td>
                  {n.valide
                    ? <Pill tone="ok" dot>Validé</Pill>
                    : <Pill tone="bad" dot>Non validé</Pill>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* SHAP */}
      <div className="card p-5">
        <div className="text-[15px] font-semibold tracking-tight mb-0.5">Analyse prédictive (SHAP)</div>
        <div className="cap mb-4">Contribution réelle de chaque facteur — calculée par le modèle XGBoost</div>
        <div className="flex items-center gap-6">
          <div style={{ width: 150, flexShrink: 0 }}>
            <ChartRadial value={student.scoreRisque} label="Risque" color={riskColor} height={160} />
          </div>
          <div className="flex-1">
            {shapLoading && <div className="cap">Calcul SHAP en cours…</div>}
            {shap && <ChartShap data={shap} />}
            {!shapLoading && !shap && (
              <div className="space-y-2">
                {[
                  { f: 'Tendance trimestrielle', w: trend !== null ? Math.min(Math.abs(trend) / 5, 1) : 0, neg: trend !== null && trend < 0, display: trend !== null ? (trend >= 0 ? `+${trend.toFixed(1)}` : trend.toFixed(1)) : '—' },
                ].map(row => (
                  <div key={row.f} className="flex items-center gap-3">
                    <span className="text-[12px]" style={{ minWidth: 180 }}>{row.f}</span>
                    <div className="flex-1 h-1.5 rounded-full" style={{ background: 'var(--surface-3)' }}>
                      <div style={{ width: `${row.w * 100}%`, height: '100%', background: row.neg ? 'var(--bad)' : 'var(--ok)', borderRadius: 999 }} />
                    </div>
                    <span className="num text-[11.5px] w-10 text-right" style={{ color: 'var(--text-3)' }}>{row.display}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Risk bar full */}
      <div className="card p-5">
        <div className="text-[15px] font-semibold tracking-tight mb-4">Score de risque de décrochage</div>
        <RiskBar score={student.scoreRisque} width={600} />
        <div className="mt-3 cap">{Math.round(student.scoreRisque * 100)}% de probabilité de décrochage selon le modèle ML</div>
      </div>

    </div>
  )
}
