import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar } from '../components/ui/RiskBar'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'

interface EtudiantRow {
  id: number; matricule: string; nomComplet: string; nom: string; prenom: string
  filiereCode: string; filiereIntitule: string; niveau: string
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

export default function Enseignant() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [q, setQ] = useState('')
  const [activeStudent, setActiveStudent] = useState<EtudiantRow | null>(null)
  const [noteForm, setNoteForm] = useState({ moduleId: 0, noteTD: '', noteTP: '', noteExamen: '', noteFinal: '', semestre: 'S1', annee: '2025/2026' })
  const [saving, setSaving]     = useState(false)
  const [success, setSuccess]   = useState(false)

  const { data: students = [], isLoading } = useQuery({ queryKey: ['etudiants-with-stats'], queryFn: fetchStudents, refetchInterval: 60_000 })
  const { data: notes = [] } = useQuery({
    queryKey: ['etudiant-notes', activeStudent?.id],
    queryFn: () => fetchNotes(activeStudent!.id),
    enabled: !!activeStudent,
  })

  const filtered = useMemo(() => {
    if (!q) return students
    const ql = q.toLowerCase()
    return students.filter(e => e.nomComplet.toLowerCase().includes(ql) || e.matricule.toLowerCase().includes(ql))
  }, [students, q])

  const atRisk = useMemo(() => students.filter(e => e.risque === 'eleve').length, [students])

  const submitNote = async () => {
    if (!noteForm.moduleId || !activeStudent) return
    setSaving(true)
    try {
      await api.post('/notes', {
        etudiantId:  activeStudent.id,
        moduleId:    noteForm.moduleId,
        noteTD:      noteForm.noteTD     ? parseFloat(noteForm.noteTD)     : null,
        noteTP:      noteForm.noteTP     ? parseFloat(noteForm.noteTP)     : null,
        noteExamen:  noteForm.noteExamen ? parseFloat(noteForm.noteExamen) : null,
        noteFinal:   noteForm.noteFinal  ? parseFloat(noteForm.noteFinal)  : null,
        semestre:    noteForm.semestre,
        annee:       noteForm.annee,
      })
      queryClient.invalidateQueries({ queryKey: ['etudiant-notes', activeStudent.id] })
      queryClient.invalidateQueries({ queryKey: ['etudiants-with-stats'] })
      setNoteForm(f => ({ ...f, noteTD: '', noteTP: '', noteExamen: '', noteFinal: '', moduleId: 0 }))
      setSuccess(true)
      setTimeout(() => setSuccess(false), 3000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">Espace Enseignant</div>
          <h1 className="text-[22px] font-semibold tracking-tight">
            Bonjour, {user?.nom || 'Enseignant'}
          </h1>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-3 gap-4">
        {[
          { l: 'Étudiants suivis',  v: students.length,                    t: '' },
          { l: 'Étudiants à risque', v: atRisk,                             t: atRisk > 0 ? 'bad' : 'ok' },
          { l: 'Moyenne globale',   v: students.length > 0 ? (students.reduce((s, e) => s + e.moyenne, 0) / students.length).toFixed(2) : '—', s: '/20', t: '' },
        ].map(m => (
          <div key={m.l} className="card p-4">
            <div className="cap mb-1">{m.l}</div>
            <div className="flex items-baseline gap-1">
              <span className="num text-[22px] font-semibold" style={{ color: m.t === 'bad' ? 'var(--bad)' : m.t === 'ok' ? 'var(--ok)' : 'var(--text)' }}>{m.v}</span>
              {m.s && <span className="text-[12px]" style={{ color: 'var(--text-3)' }}>{m.s}</span>}
            </div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-12 gap-4">
        {/* Student list */}
        <div className="col-span-7 card overflow-hidden">
          <div className="p-4 flex items-center gap-3" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="relative flex-1">
              <Icon name="search" size={13} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-3)' }} />
              <input className="input" value={q} onChange={e => setQ(e.target.value)}
                placeholder="Rechercher un étudiant…" style={{ paddingLeft: '2.25rem' }} />
            </div>
          </div>
          <table className="tbl">
            <thead>
              <tr>
                <th>Étudiant</th>
                <th>Filière</th>
                <th>Moy.</th>
                <th>Score ML</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr><td colSpan={5} className="cap text-center py-6">Chargement…</td></tr>
              ) : filtered.slice(0, 20).map(e => (
                <tr key={e.id}
                  style={{ cursor: 'pointer', background: activeStudent?.id === e.id ? 'color-mix(in oklch, var(--accent-500) 6%, transparent)' : undefined }}
                  onClick={() => { setActiveStudent(e); setNoteForm(f => ({ ...f, moduleId: 0 })) }}
                >
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
                  <td className="num font-medium">{e.moyenne.toFixed(2)}</td>
                  <td><RiskBar score={e.scoreRisque} width={60} /></td>
                  <td>
                    <button className="btn btn-sm btn-ghost" onClick={ev => { ev.stopPropagation(); navigate(`/students/${e.id}`) }}>
                      <Icon name="ext" size={12} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length > 20 && (
            <div className="px-4 py-2 cap text-center" style={{ borderTop: '1px solid var(--border)' }}>
              {filtered.length - 20} autres résultats — affinez la recherche
            </div>
          )}
        </div>

        {/* Note entry panel */}
        <div className="col-span-5 space-y-4">
          {!activeStudent ? (
            <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
              <Icon name="doc" size={32} style={{ margin: '0 auto 12px', opacity: 0.3 }} />
              <div className="text-[13px]">Sélectionnez un étudiant pour saisir ses notes</div>
            </div>
          ) : (
            <>
              <div className="card p-4 space-y-4">
                <div className="flex items-center gap-3">
                  <Avatar name={activeStudent.nomComplet} size={36} ring />
                  <div>
                    <div className="font-semibold text-[14px]">{activeStudent.nomComplet}</div>
                    <div className="cap">{activeStudent.filiereCode} · {activeStudent.niveau} · {activeStudent.matricule}</div>
                  </div>
                </div>

                {success && (
                  <div className="px-3 py-2 rounded-lg text-[12.5px] font-medium" style={{ background: 'color-mix(in oklch, var(--ok) 12%, transparent)', color: 'var(--ok)' }}>
                    Note enregistrée avec succès.
                  </div>
                )}

                <div>
                  <div className="cap mb-1">Module</div>
                  <select className="input" value={noteForm.moduleId}
                    onChange={e => setNoteForm(f => ({ ...f, moduleId: parseInt(e.target.value) }))}>
                    <option value={0}>Choisir un module…</option>
                    {notes.map(n => <option key={n.moduleId} value={n.moduleId}>{n.code} — {n.nom}</option>)}
                  </select>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <div className="cap mb-1">Semestre</div>
                    <select className="input" value={noteForm.semestre}
                      onChange={e => setNoteForm(f => ({ ...f, semestre: e.target.value }))}>
                      <option value="S1">S1</option>
                      <option value="S2">S2</option>
                    </select>
                  </div>
                  <div>
                    <div className="cap mb-1">Année</div>
                    <input className="input" value={noteForm.annee}
                      onChange={e => setNoteForm(f => ({ ...f, annee: e.target.value }))} />
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

                <button className="btn btn-sm btn-accent w-full" onClick={submitNote}
                  disabled={saving || !noteForm.moduleId}>
                  {saving ? 'Enregistrement…' : 'Enregistrer la note'}
                </button>
              </div>

              {/* Existing notes */}
              <div className="card overflow-hidden">
                <div className="p-3 text-[13px] font-semibold" style={{ borderBottom: '1px solid var(--border)' }}>
                  Notes actuelles
                </div>
                <table className="tbl">
                  <thead>
                    <tr><th>Module</th><th>CC</th><th>Exam</th><th>Finale</th><th>Statut</th></tr>
                  </thead>
                  <tbody>
                    {notes.length === 0 ? (
                      <tr><td colSpan={5} className="cap text-center py-4">Aucune note</td></tr>
                    ) : notes.map(n => (
                      <tr key={n.moduleId}>
                        <td className="font-mono cap text-[11px]">{n.code}</td>
                        <td className="num text-[12px]">{n.cc != null ? n.cc.toFixed(1) : '—'}</td>
                        <td className="num text-[12px]">{n.exam != null ? n.exam.toFixed(1) : '—'}</td>
                        <td className="num text-[12px] font-medium" style={{ color: n.finale != null && n.finale >= 10 ? 'var(--ok)' : 'var(--bad)' }}>
                          {n.finale != null ? n.finale.toFixed(2) : '—'}
                        </td>
                        <td>{n.valide ? <Pill tone="ok" dot>Validé</Pill> : <Pill tone="bad" dot>Non validé</Pill>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
