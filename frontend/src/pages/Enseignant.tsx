import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'
import { upsertNote } from '../services/notes'
import type { AxiosError } from 'axios'

// ── Types ────────────────────────────────────────────────────────────────────

interface MonModule {
  moduleId: number
  moduleCode: string
  moduleNom: string
  semestre: string
  coefficient: number
  filiereId: number
  filiereCode: string
  filiereIntitule: string
  niveau: string
}

interface EtudiantRow {
  id: number
  matricule: string
  nomComplet: string
  nom: string
  prenom: string
  filiereId: number
  niveau: string
  annee: string
}

interface NoteRow {
  moduleId: number; code: string; nom: string; semestre: string; annee: string
  coef: number; cc: number | null; tp: number | null; exam: number | null
  finale: number | null; valide: boolean
}

interface AbsenceRow {
  id: number; etudiantId: number; moduleId: number
  nombreHeures: number; justifiee: boolean; dateAbsence: string
}

// ── API ──────────────────────────────────────────────────────────────────────

async function fetchMonModule(): Promise<MonModule | null> {
  const res = await api.get<MonModule | null>('/enseignant/mon-module')
  return res.data
}

async function fetchMesEtudiants(): Promise<EtudiantRow[]> {
  const res = await api.get<EtudiantRow[]>('/enseignant/mes-etudiants')
  return res.data
}

async function fetchNotes(etudiantId: number): Promise<NoteRow[]> {
  const res = await api.get<NoteRow[]>(`/etudiants/${etudiantId}/notes`)
  return res.data
}

async function fetchAbsences(etudiantId: number): Promise<AbsenceRow[]> {
  const res = await api.get<AbsenceRow[]>(`/absences/etudiant/${etudiantId}`)
  return res.data
}

// ── Component ────────────────────────────────────────────────────────────────

type ActiveTab = 'notes' | 'absences'

export default function Enseignant() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [q, setQ]                       = useState('')
  const [activeStudent, setActiveStudent] = useState<EtudiantRow | null>(null)
  const [activeTab, setActiveTab]         = useState<ActiveTab>('notes')

  // Note form
  const [noteForm, setNoteForm] = useState({
    noteTD: '', noteTP: '', noteExamen: '', noteFinal: '',
    semestre: 'S1', annee: '2025/2026',
  })
  const [noteSaving, setNoteSaving] = useState(false)
  const [noteSuccess, setNoteSuccess] = useState(false)
  const [noteError, setNoteError] = useState<string | null>(null)

  // Absence form
  const [absForm, setAbsForm] = useState({
    nombreHeures: '1', justifiee: false, dateAbsence: new Date().toISOString().split('T')[0],
  })
  const [absSaving, setAbsSaving] = useState(false)
  const [absSuccess, setAbsSuccess] = useState(false)
  const [absError, setAbsError] = useState<string | null>(null)

  // ── Queries ────────────────────────────────────────────────────────────────

  const { data: monModule, isLoading: loadingModule } = useQuery({
    queryKey: ['enseignant-module'],
    queryFn: fetchMonModule,
  })

  const { data: etudiants = [], isLoading: loadingEtudiants } = useQuery({
    queryKey: ['enseignant-etudiants'],
    queryFn: fetchMesEtudiants,
    enabled: !!monModule,
  })

  const { data: notes = [] } = useQuery({
    queryKey: ['etudiant-notes', activeStudent?.id],
    queryFn: () => fetchNotes(activeStudent!.id),
    enabled: !!activeStudent,
  })

  const { data: absences = [] } = useQuery({
    queryKey: ['etudiant-absences', activeStudent?.id],
    queryFn: () => fetchAbsences(activeStudent!.id),
    enabled: !!activeStudent,
  })

  // ── Mutations ──────────────────────────────────────────────────────────────

  const submitNote = async () => {
    if (!activeStudent || !monModule) return
    setNoteSaving(true)
    try {
      await upsertNote({
        etudiantId:  activeStudent.id,
        moduleId:    monModule.moduleId,
        noteTD:      noteForm.noteTD     ? parseFloat(noteForm.noteTD)     : null,
        noteTP:      noteForm.noteTP     ? parseFloat(noteForm.noteTP)     : null,
        noteExamen:  noteForm.noteExamen ? parseFloat(noteForm.noteExamen) : null,
        noteFinal:   noteForm.noteFinal  ? parseFloat(noteForm.noteFinal)  : null,
        semestre:    noteForm.semestre,
        annee:       noteForm.annee,
      })
      queryClient.invalidateQueries({ queryKey: ['etudiant-notes', activeStudent.id] })
      setNoteForm(f => ({ ...f, noteTD: '', noteTP: '', noteExamen: '', noteFinal: '' }))
      setNoteSuccess(true)
      setNoteError(null)
      setTimeout(() => setNoteSuccess(false), 3000)
    } catch (error) {
      const message = (error as AxiosError<{ message?: string }>).response?.data?.message
      setNoteError(message ?? 'Impossible d’enregistrer la note.')
    } finally {
      setNoteSaving(false)
    }
  }

  const submitAbsence = async () => {
    if (!activeStudent || !monModule) return
    setAbsSaving(true)
    try {
      await api.post('/absences', {
        etudiantId:   activeStudent.id,
        moduleId:     monModule.moduleId,
        nombreHeures: parseInt(absForm.nombreHeures),
        justifiee:    absForm.justifiee,
        dateAbsence:  absForm.dateAbsence,
      })
      queryClient.invalidateQueries({ queryKey: ['etudiant-absences', activeStudent.id] })
      setAbsForm(f => ({ ...f, nombreHeures: '1', justifiee: false }))
      setAbsSuccess(true)
      setAbsError(null)
      setTimeout(() => setAbsSuccess(false), 3000)
    } catch (error) {
      const message = (error as AxiosError<{ message?: string }>).response?.data?.message
      setAbsError(message ?? 'Impossible d’enregistrer l’absence.')
    } finally {
      setAbsSaving(false)
    }
  }

  // ── Filter ────────────────────────────────────────────────────────────────

  const filtered = q
    ? etudiants.filter(e =>
        e.nomComplet.toLowerCase().includes(q.toLowerCase()) ||
        e.matricule.toLowerCase().includes(q.toLowerCase())
      )
    : etudiants

  // ── No module assigned ────────────────────────────────────────────────────

  if (!loadingModule && !monModule) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4">
        <Icon name="alert" size={40} style={{ color: 'var(--warn)' }} />
        <h2 className="text-[18px] font-semibold">Aucun module assigné</h2>
        <p className="cap text-center max-w-sm">
          Votre compte n'est pas encore lié à un module. Contactez l'administrateur pour vous assigner un module.
        </p>
      </div>
    )
  }

  // ── Module badge for the note that belongs to this teacher ───────────────

  const myNoteRow = notes.find(n => n.moduleId === monModule?.moduleId)

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">Espace Enseignant</div>
          <h1 className="text-[22px] font-semibold tracking-tight">
            Bonjour, <span className="display-serif" style={{ fontSize: '1.15em' }}>{user?.nom || 'Enseignant'}</span>
          </h1>
        </div>
        {monModule && (
          <div className="text-right">
            <div className="text-[12px] font-semibold" style={{ color: 'var(--text)' }}>
              {monModule.moduleCode} — {monModule.moduleNom}
            </div>
            <div className="cap">{monModule.filiereIntitule} · {monModule.niveau} · {monModule.semestre}</div>
          </div>
        )}
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { l: 'Module',       v: monModule?.moduleCode ?? '—',    sub: monModule?.semestre },
          { l: 'Filière',      v: monModule?.filiereCode ?? '—',    sub: monModule?.niveau },
          { l: 'Étudiants',    v: etudiants.length,                 sub: 'dans la filière' },
          { l: 'Coefficient',  v: monModule ? `×${monModule.coefficient}` : '—', sub: 'du module' },
        ].map(k => (
          <div key={k.l} className="card p-4">
            <div className="cap mb-1">{k.l}</div>
            <div className="num text-[20px] font-semibold">{k.v}</div>
            {k.sub && <div className="cap text-[10.5px] mt-0.5">{k.sub}</div>}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        {/* Students list */}
        <div className="card overflow-hidden lg:col-span-6">
          <div className="p-4 flex items-center gap-3" style={{ borderBottom: '1px solid var(--border)' }}>
            <div className="relative flex-1">
              <Icon name="search" size={13} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-3)' }} />
              <input
                className="input"
                value={q}
                onChange={e => setQ(e.target.value)}
                placeholder="Rechercher un étudiant…"
                style={{ paddingLeft: '2.25rem' }}
              />
            </div>
          </div>

          {loadingEtudiants ? (
            <div className="px-4 py-8 text-center cap">Chargement…</div>
          ) : filtered.length === 0 ? (
            <div className="px-4 py-8 text-center cap">Aucun étudiant trouvé.</div>
          ) : (
            <table className="tbl">
              <thead>
                <tr>
                  <th>Étudiant</th>
                  <th>Niveau</th>
                  <th>Année</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {filtered.map(e => (
                  <tr
                    key={e.id}
                    style={{
                      cursor: 'pointer',
                      background: activeStudent?.id === e.id
                        ? 'color-mix(in oklch, var(--accent-500) 6%, transparent)'
                        : undefined,
                    }}
                    onClick={() => { setActiveStudent(e); setActiveTab('notes') }}
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
                    <td><Pill tone="neutral">{e.niveau}</Pill></td>
                    <td className="cap">{e.annee}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn-sm btn-ghost"
                        onClick={ev => { ev.stopPropagation(); navigate(`/students/${e.id}`) }}
                        aria-label={`Voir la fiche de ${e.nomComplet}`}
                      >
                        <Icon name="ext" size={12} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Right panel */}
        <div className="space-y-4 lg:col-span-6">
          {!activeStudent ? (
            <div className="card p-8 text-center" style={{ color: 'var(--text-3)' }}>
              <Icon name="doc" size={32} style={{ margin: '0 auto 12px', opacity: 0.3 }} />
              <div className="text-[13px]">Sélectionnez un étudiant pour saisir notes ou absences</div>
            </div>
          ) : (
            <>
              {/* Student header */}
              <div className="card p-4">
                <div className="flex items-center gap-3 mb-3">
                  <Avatar name={activeStudent.nomComplet} size={36} ring />
                  <div>
                    <div className="font-semibold text-[14px]">{activeStudent.nomComplet}</div>
                    <div className="cap">{activeStudent.matricule} · {activeStudent.niveau} · {activeStudent.annee}</div>
                  </div>
                </div>

                {/* Tabs */}
                <div className="flex gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
                  {(['notes', 'absences'] as ActiveTab[]).map(t => (
                    <button
                      type="button"
                      key={t}
                      onClick={() => setActiveTab(t)}
                      aria-pressed={activeTab === t}
                      className="px-3 py-1.5 text-[12px] capitalize"
                      style={{
                        color: activeTab === t ? 'var(--text)' : 'var(--text-3)',
                        fontWeight: activeTab === t ? 500 : 400,
                        borderBottom: activeTab === t ? '2px solid var(--accent-500)' : '2px solid transparent',
                        marginBottom: -1,
                      }}
                    >
                      {t === 'notes' ? 'Notes' : 'Absences'}
                    </button>
                  ))}
                </div>
              </div>

              {/* ── Notes tab ────────────────────────────────────────────── */}
              {activeTab === 'notes' && (
                <div className="card p-4 space-y-3">
                  <div className="text-[13px] font-semibold">
                    Saisir une note — {monModule?.moduleCode}
                  </div>

                  {noteSuccess && (
                    <div className="px-3 py-2 rounded-lg text-[12.5px] font-medium"
                      style={{ background: 'color-mix(in oklch, var(--ok) 12%, transparent)', color: 'var(--ok)' }}>
                      Note enregistrée avec succès.
                    </div>
                  )}

                  {noteError && (
                    <div className="px-3 py-2 rounded-lg text-[12.5px] font-medium"
                      style={{ background: 'color-mix(in oklch, var(--bad) 8%, transparent)', color: 'var(--bad)' }}>
                      {noteError}
                    </div>
                  )}

                  <div className="grid grid-cols-2 gap-3">
                    <label className="flex flex-col gap-1">
                      <span className="cap">Semestre</span>
                      <select className="input" value={noteForm.semestre}
                        onChange={e => setNoteForm(f => ({ ...f, semestre: e.target.value }))}>
                        <option value="S1">S1</option>
                        <option value="S2">S2</option>
                      </select>
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="cap">Année</span>
                      <input className="input" value={noteForm.annee}
                        onChange={e => setNoteForm(f => ({ ...f, annee: e.target.value }))} />
                    </label>
                  </div>

                  <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                    {(['noteTD', 'noteTP', 'noteExamen', 'noteFinal'] as const).map((field, i) => (
                      <label key={field} className="flex flex-col gap-1">
                        <span className="cap text-[11px]">{['CC', 'TP', 'Examen', 'Finale'][i]}</span>
                        <input
                          className="input" type="number" min={0} max={20} step={0.01}
                          placeholder="/20" value={noteForm[field]}
                          onChange={e => setNoteForm(f => ({ ...f, [field]: e.target.value }))}
                        />
                      </label>
                    ))}
                  </div>

                  <button className="btn btn-sm btn-accent w-full" onClick={submitNote} disabled={noteSaving}>
                    {noteSaving ? 'Enregistrement…' : 'Enregistrer la note'}
                  </button>

                  {/* Existing note for this module */}
                  {myNoteRow && (
                    <div className="pt-2" style={{ borderTop: '1px solid var(--border)' }}>
                      <div className="cap mb-2">Note actuelle — {monModule?.moduleCode}</div>
                      <div className="grid grid-cols-2 gap-2 text-[12px] sm:grid-cols-4">
                        {[
                          { l: 'CC',     v: myNoteRow.cc },
                          { l: 'TP',     v: myNoteRow.tp },
                          { l: 'Examen', v: myNoteRow.exam },
                          { l: 'Finale', v: myNoteRow.finale },
                        ].map(n => (
                          <div key={n.l} className="card p-2 text-center">
                            <div className="cap text-[10px]">{n.l}</div>
                            <div className="num font-semibold"
                              style={{ color: n.l === 'Finale' && n.v != null
                                ? (n.v >= 10 ? 'var(--ok)' : 'var(--bad)')
                                : 'var(--text)' }}>
                              {n.v != null ? n.v.toFixed(2) : '—'}
                            </div>
                          </div>
                        ))}
                      </div>
                      {myNoteRow.finale != null && (
                        <div className="mt-2">
                          <Pill tone={myNoteRow.valide ? 'ok' : 'bad'} dot>
                            {myNoteRow.valide ? 'Module validé' : 'Non validé'}
                          </Pill>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}

              {/* ── Absences tab ──────────────────────────────────────────── */}
              {activeTab === 'absences' && (
                <div className="card p-4 space-y-3">
                  <div className="text-[13px] font-semibold">
                    Saisir une absence — {monModule?.moduleCode}
                  </div>

                  {absSuccess && (
                    <div className="px-3 py-2 rounded-lg text-[12.5px] font-medium"
                      style={{ background: 'color-mix(in oklch, var(--ok) 12%, transparent)', color: 'var(--ok)' }}>
                      Absence enregistrée avec succès.
                    </div>
                  )}

                  {absError && (
                    <div className="px-3 py-2 rounded-lg text-[12.5px] font-medium"
                      style={{ background: 'color-mix(in oklch, var(--bad) 8%, transparent)', color: 'var(--bad)' }}>
                      {absError}
                    </div>
                  )}

                  <div className="grid grid-cols-2 gap-3">
                    <label className="flex flex-col gap-1">
                      <span className="cap">Date</span>
                      <input type="date" className="input" value={absForm.dateAbsence}
                        onChange={e => setAbsForm(f => ({ ...f, dateAbsence: e.target.value }))} />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="cap">Nombre d'heures</span>
                      <input type="number" className="input" min={1} max={8}
                        value={absForm.nombreHeures}
                        onChange={e => setAbsForm(f => ({ ...f, nombreHeures: e.target.value }))} />
                    </label>
                  </div>

                  <label className="flex items-center gap-2 cursor-pointer">
                    <input type="checkbox" checked={absForm.justifiee}
                      onChange={e => setAbsForm(f => ({ ...f, justifiee: e.target.checked }))} />
                    <span className="text-[12.5px]">Absence justifiée</span>
                  </label>

                  <button className="btn btn-sm btn-accent w-full" onClick={submitAbsence} disabled={absSaving}>
                    {absSaving ? 'Enregistrement…' : 'Enregistrer l\'absence'}
                  </button>

                  {/* Absences history for this module */}
                  {absences.filter(a => a.moduleId === monModule?.moduleId).length > 0 && (
                    <div className="pt-2" style={{ borderTop: '1px solid var(--border)' }}>
                      <div className="cap mb-2">
                        Absences dans {monModule?.moduleCode}
                        {' '}({absences.filter(a => a.moduleId === monModule?.moduleId).reduce((s, a) => s + a.nombreHeures, 0)}h total)
                      </div>
                      <div className="space-y-1.5">
                        {absences
                          .filter(a => a.moduleId === monModule?.moduleId)
                          .slice(0, 10)
                          .map(a => (
                            <div key={a.id} className="flex items-center justify-between text-[12px] px-2 py-1.5 rounded"
                              style={{ background: 'var(--surface-2)' }}>
                              <span>{new Date(a.dateAbsence).toLocaleDateString('fr-FR')}</span>
                              <span className="font-medium">{a.nombreHeures}h</span>
                              <Pill tone={a.justifiee ? 'ok' : 'bad'} dot>
                                {a.justifiee ? 'Justifiée' : 'Non justifiée'}
                              </Pill>
                            </div>
                          ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  )
}
