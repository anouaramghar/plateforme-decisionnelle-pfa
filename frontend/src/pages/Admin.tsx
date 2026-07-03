import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import type { AxiosError } from 'axios'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../context/AuthContext'
import { Icon, type IconName } from '../components/ui/Icon'
import { Pill } from '../components/ui/Pill'
import { Avatar } from '../components/ui/Avatar'
import { PasswordInput } from '../components/ui/PasswordInput'
import { api } from '../services/api'

// ── Types ────────────────────────────────────────────────────────────────────

interface Utilisateur {
  id: number
  nom: string
  prenom: string
  email: string
  role: string
}

interface PaginatedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

interface ApiErrorPayload {
  errors?: Record<string, string[]>
  message?: string
}

type Tab = 'users' | 'etudiants' | 'dw'
type Role = 'Admin' | 'Responsable' | 'Enseignant'

const ROLE_PILL: Record<string, 'bad' | 'warn' | 'ok' | 'info' | 'neutral'> = {
  Admin:       'bad',
  Responsable: 'warn',
  Enseignant:  'ok',
}

// ── API calls ────────────────────────────────────────────────────────────────

async function fetchUsers(page: number): Promise<PaginatedResult<Utilisateur>> {
  const res = await api.get<PaginatedResult<Utilisateur>>(
    `/utilisateurs?page=${page}&pageSize=20`
  )
  return res.data
}

async function createUser(data: {
  nom: string
  prenom: string
  email: string
  role: string
  motDePasse: string
  moduleId?: number | null
}): Promise<Utilisateur> {
  const res = await api.post<Utilisateur>('/utilisateurs', data)
  return res.data
}

async function syncDw(): Promise<{ message: string; timestamp: string }> {
  const res = await api.post<{ message: string; timestamp: string }>('/admin/sync-dw')
  return res.data
}

async function generateAlerts(): Promise<{ message: string; notes: number; absences: number }> {
  const res = await api.post<{ message: string; notes: number; absences: number }>('/admin/generate-alerts')
  return res.data
}

interface Filiere { id: number; code: string; intitule: string }
interface EtudiantRow {
  id: number; matricule: string; nom: string; prenom: string; email?: string
  filiereId: number; filiereCode: string; niveau: string; annee: string
  moyenne: number; scoreRisque: number
}

async function fetchFilieres(): Promise<Filiere[]> {
  const res = await api.get<{ items: Filiere[] }>('/filieres?pageSize=20')
  return res.data.items
}

interface ModuleItem { id: number; code: string; nom: string; filiereCode: string; niveau: string }
async function fetchModules(): Promise<ModuleItem[]> {
  // /modules caps pageSize at 100 server-side, but the school now has 188+
  // modules across 5 filières — page through all of them so the "module
  // enseigné" dropdown isn't silently missing whichever filières land past
  // the first page (was hiding GINF/IRSI entirely).
  const items: ModuleItem[] = []
  let page = 1
  while (true) {
    const res = await api.get<{ items: ModuleItem[]; total: number }>(`/modules?page=${page}&pageSize=100`)
    items.push(...res.data.items)
    if (items.length >= res.data.total || res.data.items.length === 0) break
    page += 1
  }
  return items
}

type EtudiantPayload = {
  matricule: string; nom: string; prenom: string; email: string
  filiereId: number; niveau: string; annee: string
}

async function createEtudiant(data: EtudiantPayload): Promise<void> {
  await api.post('/etudiants', data)
}

async function updateEtudiant({ id, data }: { id: number; data: EtudiantPayload }): Promise<void> {
  await api.put(`/etudiants/${id}`, data)
}

async function deleteEtudiant(id: number): Promise<void> {
  await api.delete(`/etudiants/${id}`)
}

// ── Component ────────────────────────────────────────────────────────────────

export default function Admin() {
  const { user } = useAuth()
  const location = useLocation()
  const isAdmin = user?.role === 'Admin'
  const isResponsable = user?.role === 'Responsable'
  const [tab, setTab] = useState<Tab>(() =>
    isResponsable ? 'etudiants' : (location.state as { tab?: Tab } | null)?.tab ?? 'users')

  if (!isAdmin && !isResponsable) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-4">
        <Icon name="alert" size={40} style={{ color: 'var(--bad)' }} />
        <h2 className="text-[18px] font-semibold">Accès refusé</h2>
        <p className="cap">Cette section est réservée aux administrateurs.</p>
      </div>
    )
  }

  return (
    <div className="space-y-5">
      <div>
        <div className="cap mb-1">Système</div>
        <h1 className="text-[22px] font-semibold tracking-tight">Administration</h1>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1" style={{ borderBottom: '1px solid var(--border)' }}>
        {(
          [
            { id: 'users',     label: 'Utilisateurs',   icon: 'students' },
            { id: 'etudiants', label: 'Étudiants',      icon: 'user'     },
            { id: 'dw',        label: 'Data Warehouse', icon: 'database' },
          ] as { id: Tab; label: string; icon: IconName }[]
        ).filter(item => isAdmin || item.id === 'etudiants'
        ).map(t => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className="px-4 py-2.5 text-[12.5px] flex items-center gap-2"
            style={{
              color: tab === t.id ? 'var(--text)' : 'var(--text-3)',
              fontWeight: tab === t.id ? 500 : 400,
              borderBottom: tab === t.id ? '2px solid var(--accent-500)' : '2px solid transparent',
              marginBottom: -1,
            }}
          >
            <Icon name={t.icon} size={13} />
            {t.label}
          </button>
        ))}
      </div>

      {isAdmin && tab === 'users' && <UsersTab />}
      {tab === 'etudiants' && <EtudiantsTab initialOpenCreate={!!(location.state as { openCreate?: boolean } | null)?.openCreate} />}
      {isAdmin && tab === 'dw' && <DwTab />}
    </div>
  )
}

// ── Users tab ─────────────────────────────────────────────────────────────────

function UsersTab() {
  const [page, setPage] = useState(1)
  const [showCreate, setShowCreate] = useState(false)
  const queryClient = useQueryClient()

  const { data, isLoading, isError } = useQuery({
    queryKey: ['utilisateurs', page],
    queryFn: () => fetchUsers(page),
  })

  const createMutation = useMutation({
    mutationFn: createUser,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['utilisateurs'] })
      setShowCreate(false)
    },
  })

  const totalPages = data ? Math.ceil(data.total / 20) : 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="cap">
          {data ? `${data.total} utilisateur${data.total > 1 ? 's' : ''} enregistré${data.total > 1 ? 's' : ''}` : '…'}
        </p>
        <button className="btn btn-sm btn-accent" onClick={() => setShowCreate(true)}>
          <Icon name="plus" size={13} strokeWidth={2.4} />
          Nouvel utilisateur
        </button>
      </div>

      {showCreate && (
        <CreateUserForm
          onSubmit={data => createMutation.mutate(data)}
          onCancel={() => setShowCreate(false)}
          isPending={createMutation.isPending}
          error={createMutation.error
            ? (() => {
                const r = (createMutation.error as AxiosError<ApiErrorPayload>)?.response?.data
                if (!r) return createMutation.error.message
                if (r.errors) return Object.values(r.errors).flat().join(' ')
                return r.message || createMutation.error.message
              })()
            : undefined}
        />
      )}

      <div className="card overflow-x-auto" role="region" aria-label="Utilisateurs" tabIndex={0}>
        {isLoading && (
          <div className="px-4 py-8 text-center cap">Chargement…</div>
        )}
        {isError && (
          <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>
            Impossible de charger les utilisateurs.
          </div>
        )}
        {!isLoading && !isError && (
          <>
            {/* Header */}
            <div
              className="grid min-w-[640px] px-4 py-2.5 text-[11px] uppercase tracking-wider"
              style={{
                gridTemplateColumns: '1fr 1.5fr 1fr',
                color: 'var(--text-3)',
                borderBottom: '1px solid var(--border)',
                fontWeight: 500,
              }}
            >
              <span>Utilisateur</span>
              <span>Email</span>
              <span>Rôle</span>
            </div>

            {data?.items.map((u, i) => (
              <div
                key={u.id}
                className="grid min-w-[640px] items-center px-4 py-3"
                style={{
                  gridTemplateColumns: '1fr 1.5fr 1fr',
                  borderBottom: i < (data.items.length - 1) ? '1px solid var(--border)' : 'none',
                }}
              >
                <div className="flex items-center gap-2.5">
                  <Avatar name={`${u.prenom} ${u.nom}`} size={26} />
                  <span className="text-[13px] font-medium">
                    {u.prenom} {u.nom}
                  </span>
                </div>
                <span className="text-[12.5px]" style={{ color: 'var(--text-2)' }}>
                  {u.email}
                </span>
                <Pill tone={ROLE_PILL[u.role] ?? 'neutral'}>{u.role}</Pill>
              </div>
            ))}
          </>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <button
            className="btn btn-sm"
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            <Icon name="chevLeft" size={13} />
            Précédent
          </button>
          <span className="text-[12.5px]" style={{ color: 'var(--text-3)' }}>
            Page {page} / {totalPages}
          </span>
          <button
            className="btn btn-sm"
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
          >
            Suivant
            <Icon name="chevRight" size={13} />
          </button>
        </div>
      )}
    </div>
  )
}

// ── Create user form ──────────────────────────────────────────────────────────

interface CreateUserFormProps {
  onSubmit: (data: { nom: string; prenom: string; email: string; role: string; motDePasse: string; moduleId?: number | null }) => void
  onCancel: () => void
  isPending: boolean
  error?: string
}

function CreateUserForm({ onSubmit, onCancel, isPending, error }: CreateUserFormProps) {
  const [form, setForm] = useState({
    prenom: '',
    nom: '',
    email: '',
    role: 'Enseignant' as Role,
    motDePasse: '',
    moduleId: null as number | null,
  })

  const { data: modules = [] } = useQuery({
    queryKey: ['modules-all'],
    queryFn: fetchModules,
  })

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm(f => ({ ...f, [k]: e.target.value }))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    onSubmit({
      ...form,
      moduleId: form.role === 'Enseignant' ? form.moduleId : null,
    })
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="card p-5 space-y-4"
      style={{ borderColor: 'var(--accent-400)' }}
    >
      <div className="flex items-center justify-between">
        <h3 className="text-[14px] font-semibold">Nouvel utilisateur</h3>
        <button type="button" className="btn btn-sm btn-ghost" onClick={onCancel}>
          <Icon name="x" size={13} />
        </button>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1">
          <span className="cap">Prénom</span>
          <input className="input" placeholder="Mohamed" value={form.prenom} onChange={set('prenom')} required />
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Nom</span>
          <input className="input" placeholder="Ait Ali" value={form.nom} onChange={set('nom')} required />
        </label>
      </div>

      <label className="flex flex-col gap-1">
        <span className="cap">Email</span>
        <input className="input" type="email" placeholder="m.aitali@eniad.ma" value={form.email} onChange={set('email')} required />
      </label>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1">
          <span className="cap">Rôle</span>
          <select className="input" value={form.role} onChange={set('role')}>
            <option value="Enseignant">Enseignant</option>
            <option value="Responsable">Responsable</option>
            <option value="Admin">Admin</option>
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Mot de passe temporaire</span>
          <PasswordInput
            placeholder="Min. 12 caractères"
            value={form.motDePasse}
            onChange={set('motDePasse')}
            required
            minLength={12}
          />
        </label>
      </div>

      {form.role === 'Enseignant' && (
        <label className="flex flex-col gap-1">
          <span className="cap">Module enseigné</span>
          <select
            className="input"
            value={form.moduleId ?? ''}
            onChange={e => setForm(f => ({ ...f, moduleId: e.target.value ? Number(e.target.value) : null }))}
            required
          >
            <option value="">Sélectionner un module…</option>
            {modules.map(m => (
              <option key={m.id} value={m.id}>
                {m.code} — {m.nom} ({m.filiereCode} · {m.niveau})
              </option>
            ))}
          </select>
        </label>
      )}

      {error && (
        <p className="text-[12px]" style={{ color: 'var(--bad)' }}>{error}</p>
      )}

      <div className="flex items-center gap-2 justify-end">
        <button type="button" className="btn btn-sm" onClick={onCancel}>Annuler</button>
        <button type="submit" className="btn btn-sm btn-accent" disabled={isPending}>
          {isPending ? 'Création…' : 'Créer l\'utilisateur'}
        </button>
      </div>
    </form>
  )
}

// ── Étudiants tab ────────────────────────────────────────────────────────────

const NIVEAUX = ['CP1', 'CP2', 'CI1', 'CI2', 'CI3']
const ANNEE_RE = /^\d{4}\/\d{4}$/

function EtudiantsTab({ initialOpenCreate = false }: { initialOpenCreate?: boolean }) {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const queryClient = useQueryClient()

  const [showCreate, setShowCreate]         = useState(initialOpenCreate)
  const [editingStudent, setEditingStudent] = useState<EtudiantRow | null>(null)
  const [deletingId, setDeletingId]         = useState<number | null>(null)
  const [search, setSearch]                 = useState('')
  const [filterFiliere, setFilterFiliere]   = useState('')
  const [filterNiveau, setFilterNiveau]     = useState('')
  const [successMsg, setSuccessMsg]         = useState<string | null>(null)

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['admin-etudiants'] })
    queryClient.invalidateQueries({ queryKey: ['etudiants'] })
  }

  const { data: filieres = [], isLoading: loadingFil } = useQuery({
    queryKey: ['filieres'],
    queryFn: fetchFilieres,
  })

  const { data: students = [], isLoading, isError } = useQuery({
    queryKey: ['admin-etudiants'],
    queryFn: async () => {
      const res = await api.get<EtudiantRow[]>('/etudiants/with-stats')
      return res.data
    },
  })

  const filtered = students.filter(s => {
    const q = search.trim().toLowerCase()
    const matchQ = !q || [s.prenom, s.nom, s.matricule, s.email ?? ''].some(v => v.toLowerCase().includes(q))
    const matchF = !filterFiliere || s.filiereCode === filterFiliere
    const matchN = !filterNiveau  || s.niveau === filterNiveau
    return matchQ && matchF && matchN
  })

  const createMutation = useMutation({
    mutationFn: createEtudiant,
    onSuccess: (_result, vars) => {
      invalidate()
      setShowCreate(false)
      const filCode = filieres.find(f => f.id === vars.filiereId)?.code ?? vars.filiereId
      setSuccessMsg(`${vars.prenom} ${vars.nom} a été inscrit(e) en ${filCode} — ${vars.niveau}.`)
      setTimeout(() => setSuccessMsg(null), 5000)
    },
  })

  const updateMutation = useMutation({
    mutationFn: updateEtudiant,
    onSuccess: (_result, vars) => {
      invalidate()
      setEditingStudent(null)
      const filCode = filieres.find(f => f.id === vars.data.filiereId)?.code ?? vars.data.filiereId
      setSuccessMsg(`Les informations de ${vars.data.prenom} ${vars.data.nom} ont été mises à jour (${filCode} — ${vars.data.niveau}).`)
      setTimeout(() => setSuccessMsg(null), 5000)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteEtudiant,
    onSuccess: () => { invalidate(); setDeletingId(null) },
  })

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex items-center gap-3 flex-wrap">
        {/* Search input */}
        <div className="flex items-center gap-2 flex-1 min-w-[200px]" style={{ position: 'relative' }}>
          <Icon name="search" size={13} style={{ position: 'absolute', left: 10, color: 'var(--text-3)', pointerEvents: 'none' }} />
          <input
            className="input"
            style={{ paddingLeft: 32 }}
            placeholder="Nom, prénom, matricule…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          {search && (
            <button
              className="btn btn-sm btn-ghost"
              style={{ position: 'absolute', right: 4 }}
              onClick={() => setSearch('')}
            >
              <Icon name="x" size={12} />
            </button>
          )}
        </div>

        {/* Filière filter */}
        <select
          className="input"
          style={{ width: 'auto', minWidth: 130 }}
          value={filterFiliere}
          onChange={e => setFilterFiliere(e.target.value)}
        >
          <option value="">Toutes filières</option>
          {filieres.map(f => <option key={f.id} value={f.code}>{f.code}</option>)}
        </select>

        {/* Niveau filter */}
        <select
          className="input"
          style={{ width: 'auto', minWidth: 110 }}
          value={filterNiveau}
          onChange={e => setFilterNiveau(e.target.value)}
        >
          <option value="">Tous niveaux</option>
          {NIVEAUX.map(n => <option key={n} value={n}>{n}</option>)}
        </select>

        <p className="cap" style={{ whiteSpace: 'nowrap' }}>
          {isLoading ? '…' : `${filtered.length} / ${students.length}`}
        </p>

        <button className="btn btn-sm btn-accent" style={{ marginLeft: 'auto' }} onClick={() => { setShowCreate(true); setEditingStudent(null) }}>
          <Icon name="plus" size={13} strokeWidth={2.4} />
          Nouvel étudiant
        </button>
      </div>

      {/* Success banner */}
      {successMsg && (
        <div
          className="flex items-center gap-3 px-4 py-3 rounded-lg text-[13px]"
          style={{
            background: 'color-mix(in oklch, var(--ok) 10%, transparent)',
            border: '1px solid color-mix(in oklch, var(--ok) 30%, transparent)',
            color: 'var(--ok)',
          }}
        >
          <Icon name="check" size={15} style={{ flexShrink: 0 }} />
          <span className="flex-1 font-medium">{successMsg}</span>
          <button className="btn btn-sm btn-ghost" style={{ color: 'var(--ok)' }} onClick={() => setSuccessMsg(null)}>
            <Icon name="x" size={12} />
          </button>
        </div>
      )}

      {showCreate && (
        <EtudiantForm
          title="Nouvel étudiant"
          filieres={filieres}
          loadingFil={loadingFil}
          onSubmit={data => createMutation.mutate(data)}
          onCancel={() => setShowCreate(false)}
          isPending={createMutation.isPending}
          error={createMutation.error?.message}
        />
      )}

      {editingStudent && (
        <EtudiantForm
          title={`Modifier — ${editingStudent.prenom} ${editingStudent.nom}`}
          filieres={filieres}
          loadingFil={loadingFil}
          initial={editingStudent}
          onSubmit={data => updateMutation.mutate({ id: editingStudent.id, data })}
          onCancel={() => setEditingStudent(null)}
          isPending={updateMutation.isPending}
          error={updateMutation.error?.message}
        />
      )}

      <div className="card overflow-hidden">
        {isLoading && <div className="px-4 py-8 text-center cap">Chargement…</div>}
        {isError   && <div className="px-4 py-8 text-center cap" style={{ color: 'var(--bad)' }}>Impossible de charger les étudiants.</div>}
        {!isLoading && !isError && (
          <>
            <div
              className="grid px-4 py-2.5 text-[11px] uppercase tracking-wider"
              style={{ gridTemplateColumns: '2fr 1fr 1fr 1fr 1fr auto', color: 'var(--text-3)', borderBottom: '1px solid var(--border)', fontWeight: 500 }}
            >
              <span>Nom</span>
              <span>Matricule</span>
              <span>Filière</span>
              <span>Niveau</span>
              <span>Moyenne</span>
              <span />
            </div>

            {filtered.length === 0 && (
              <div className="px-4 py-8 text-center cap">Aucun étudiant ne correspond à votre recherche.</div>
            )}
            {filtered.slice(0, 100).map((s, i) => (
              <div key={s.id}>
                <div
                  className="grid items-center px-4 py-2.5"
                  style={{
                    gridTemplateColumns: '2fr 1fr 1fr 1fr 1fr auto',
                    borderBottom: deletingId === s.id
                      ? 'none'
                      : i < filtered.length - 1 ? '1px solid var(--border)' : 'none',
                  }}
                >
                  <span className="text-[13px] font-medium">{s.prenom} {s.nom}</span>
                  <span className="cap">{s.matricule}</span>
                  <span className="cap">{s.filiereCode}</span>
                  <span className="cap">{s.niveau}</span>
                  <span className="text-[12.5px] num" style={{ color: s.moyenne >= 10 ? 'var(--ok)' : 'var(--bad)' }}>
                    {s.moyenne.toFixed(2)}/20
                  </span>
                  <div className="flex items-center gap-1">
                    <button
                      className="btn btn-sm btn-ghost"
                      title="Modifier"
                      onClick={() => { setEditingStudent(s); setShowCreate(false); setDeletingId(null) }}
                    >
                      <Icon name="edit" size={13} />
                    </button>
                    {isAdmin && (
                      <button
                        className="btn btn-sm btn-ghost"
                        title="Supprimer"
                        onClick={() => { setDeletingId(s.id); setEditingStudent(null); setShowCreate(false) }}
                        style={{ color: 'var(--bad)' }}
                      >
                        <Icon name="trash" size={13} />
                      </button>
                    )}
                  </div>
                </div>

                {/* Inline delete confirmation */}
                {deletingId === s.id && (
                  <div
                    className="px-4 py-3 flex items-center gap-3"
                    style={{ background: 'color-mix(in oklch, var(--bad) 6%, transparent)', borderBottom: i < filtered.length - 1 ? '1px solid var(--border)' : 'none' }}
                  >
                    <Icon name="alert" size={14} style={{ color: 'var(--bad)', flexShrink: 0 }} />
                    <span className="text-[12.5px] flex-1" style={{ color: 'var(--bad)' }}>
                      Supprimer <strong>{s.prenom} {s.nom}</strong> ? Cette action est irréversible.
                    </span>
                    <button className="btn btn-sm" onClick={() => setDeletingId(null)}>Annuler</button>
                    <button
                      className="btn btn-sm"
                      style={{ background: 'var(--bad)', color: '#fff', borderColor: 'var(--bad)' }}
                      disabled={deleteMutation.isPending}
                      onClick={() => deleteMutation.mutate(s.id)}
                    >
                      {deleteMutation.isPending ? 'Suppression…' : 'Confirmer'}
                    </button>
                  </div>
                )}
              </div>
            ))}

            {filtered.length > 100 && (
              <div className="px-4 py-2 text-center cap" style={{ borderTop: '1px solid var(--border)' }}>
                {filtered.length - 100} autres résultats — affinez votre recherche
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}

// ── Étudiant form (shared create / edit) ─────────────────────────────────────

interface EtudiantFormProps {
  title: string
  filieres: Filiere[]
  loadingFil: boolean
  initial?: EtudiantRow
  onSubmit: (data: EtudiantPayload) => void
  onCancel: () => void
  isPending: boolean
  error?: string
}

function EtudiantForm({ title, filieres, loadingFil, initial, onSubmit, onCancel, isPending, error }: EtudiantFormProps) {
  const currentYear = new Date().getFullYear()

  const [form, setForm] = useState({
    matricule: initial?.matricule ?? '',
    prenom:    initial?.prenom    ?? '',
    nom:       initial?.nom       ?? '',
    email:     initial?.email     ?? '',
    filiereId: initial?.filiereId != null ? String(initial.filiereId) : '',
    niveau:    initial?.niveau    ?? 'CP1',
    annee:     initial?.annee     ?? `${currentYear - 1}/${currentYear}`,
  })

  const [anneeError, setAnneeError] = useState('')

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm(f => ({ ...f, [k]: e.target.value }))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!ANNEE_RE.test(form.annee)) {
      setAnneeError('Format requis : YYYY/YYYY (ex : 2025/2026)')
      return
    }
    setAnneeError('')
    onSubmit({ ...form, filiereId: Number(form.filiereId), email: form.email || '' })
  }

  const isEdit = !!initial

  return (
    <form
      onSubmit={handleSubmit}
      className="card p-5 space-y-4"
      style={{ borderColor: isEdit ? 'var(--text-3)' : 'var(--accent-400)' }}
    >
      <div className="flex items-center justify-between">
        <h3 className="text-[14px] font-semibold">{title}</h3>
        <button type="button" className="btn btn-sm btn-ghost" onClick={onCancel}>
          <Icon name="x" size={13} />
        </button>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1">
          <span className="cap">Prénom</span>
          <input className="input" placeholder="Amine" value={form.prenom} onChange={set('prenom')} required />
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Nom</span>
          <input className="input" placeholder="Benali" value={form.nom} onChange={set('nom')} required />
        </label>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1">
          <span className="cap">Matricule</span>
          <input className="input" placeholder="ENI-2025-001" value={form.matricule} onChange={set('matricule')} required maxLength={20} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Email (optionnel)</span>
          <input className="input" type="email" placeholder="a.benali@eniad.ma" value={form.email} onChange={set('email')} />
        </label>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <label className="flex flex-col gap-1">
          <span className="cap">Filière</span>
          <select className="input" value={form.filiereId} onChange={set('filiereId')} required disabled={loadingFil}>
            <option value="">-- Choisir --</option>
            {filieres.map(f => (
              <option key={f.id} value={f.id}>{f.code} — {f.intitule}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Niveau</span>
          <select className="input" value={form.niveau} onChange={set('niveau')} required>
            {NIVEAUX.map(n => <option key={n} value={n}>{n}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Année scolaire</span>
          <input className="input" placeholder="2025/2026" value={form.annee} onChange={set('annee')} required maxLength={9} />
          {anneeError && <span className="text-[11px]" style={{ color: 'var(--bad)' }}>{anneeError}</span>}
        </label>
      </div>

      {error && <p className="text-[12px]" style={{ color: 'var(--bad)' }}>{error}</p>}

      <div className="flex items-center gap-2 justify-end">
        <button type="button" className="btn btn-sm" onClick={onCancel}>Annuler</button>
        <button type="submit" className="btn btn-sm btn-accent" disabled={isPending || !form.filiereId}>
          {isPending
            ? (isEdit ? 'Enregistrement…' : 'Création…')
            : (isEdit ? 'Enregistrer' : "Ajouter l'étudiant")}
        </button>
      </div>
    </form>
  )
}

// ── DW sync tab ───────────────────────────────────────────────────────────────

function DwTab() {
  const [lastSync, setLastSync]       = useState<string | null>(null)
  const [syncError, setSyncError]     = useState<string | null>(null)
  const [alertsMsg, setAlertsMsg]     = useState<string | null>(null)
  const [alertsError, setAlertsError] = useState<string | null>(null)

  const syncMutation = useMutation({
    mutationFn: syncDw,
    onSuccess: (data) => { setLastSync(data.timestamp); setSyncError(null) },
    onError: (err: Error) => { setSyncError(err.message) },
  })

  const alertsMutation = useMutation({
    mutationFn: generateAlerts,
    onSuccess: (data) => {
      setAlertsMsg(data.message)
      setAlertsError(null)
      setTimeout(() => setAlertsMsg(null), 6000)
    },
    onError: (err: Error) => { setAlertsError(err.message) },
  })

  return (
    <div className="space-y-4">
      {/* Status card */}
      <div className="card p-5 space-y-4">
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-[14px] font-semibold">Synchronisation OLTP → Data Warehouse</h3>
            <p className="cap mt-1">
              Exécute les 4 instructions MERGE (DimEtudiant, DimModule, DimTemps, FaitNotes).
              À lancer après chaque import de notes ou modification de données.
            </p>
          </div>
          <Icon name="database" size={22} style={{ color: 'var(--accent-500)', flexShrink: 0 }} />
        </div>

        <div
          className="rounded-lg px-4 py-3 flex items-center gap-3"
          style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
        >
          <div
            className="w-8 h-8 rounded-md flex items-center justify-center flex-shrink-0"
            style={{ background: 'color-mix(in oklch, var(--accent-500) 14%, transparent)' }}
          >
            <Icon name="refresh" size={15} style={{ color: 'var(--accent-600)' }} />
          </div>
          <div className="flex-1">
            <div className="text-[12.5px] font-medium">Dernière synchronisation</div>
            <div className="cap mt-0.5">
              {lastSync
                ? new Date(lastSync).toLocaleString('fr-FR', {
                    dateStyle: 'medium',
                    timeStyle: 'short',
                  })
                : 'Aucune synchronisation durant cette session'}
            </div>
          </div>
          {lastSync && <Pill tone="ok" dot>Succès</Pill>}
        </div>

        {syncError && (
          <div
            className="rounded-lg px-4 py-3 text-[12.5px]"
            style={{
              background: 'color-mix(in oklch, var(--bad) 8%, transparent)',
              border: '1px solid color-mix(in oklch, var(--bad) 25%, transparent)',
              color: 'var(--bad)',
            }}
          >
            <strong>Erreur :</strong> {syncError}
          </div>
        )}

        <button
          className="btn btn-accent"
          onClick={() => syncMutation.mutate()}
          disabled={syncMutation.isPending}
          style={{ width: '100%', justifyContent: 'center' }}
        >
          <Icon name={syncMutation.isPending ? 'refresh' : 'refresh'} size={14} />
          {syncMutation.isPending ? 'Synchronisation en cours…' : 'Lancer la synchronisation'}
        </button>
      </div>

      {/* Generate alerts card */}
      <div className="card p-5 space-y-4">
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-[14px] font-semibold">Génération des alertes</h3>
            <p className="cap mt-1">
              Analyse tous les étudiants et crée les alertes manquantes (Note faible, Absences excessives).
              À lancer après un seeding ou un import bulk.
            </p>
          </div>
          <Icon name="bell" size={22} style={{ color: 'var(--warn)', flexShrink: 0 }} />
        </div>

        {alertsMsg && (
          <div className="rounded-lg px-4 py-3 text-[12.5px]" style={{ background: 'color-mix(in oklch, var(--ok) 10%, transparent)', border: '1px solid color-mix(in oklch, var(--ok) 30%, transparent)', color: 'var(--ok)' }}>
            <Icon name="check" size={13} style={{ display: 'inline', marginRight: 6 }} />
            {alertsMsg}
          </div>
        )}
        {alertsError && (
          <div className="rounded-lg px-4 py-3 text-[12.5px]" style={{ background: 'color-mix(in oklch, var(--bad) 8%, transparent)', border: '1px solid color-mix(in oklch, var(--bad) 25%, transparent)', color: 'var(--bad)' }}>
            <strong>Erreur :</strong> {alertsError}
          </div>
        )}

        <button
          className="btn btn-accent"
          onClick={() => alertsMutation.mutate()}
          disabled={alertsMutation.isPending}
          style={{ width: '100%', justifyContent: 'center' }}
        >
          <Icon name="bell" size={14} />
          {alertsMutation.isPending ? 'Scan en cours…' : 'Générer les alertes manquantes'}
        </button>
      </div>

      {/* Info grid */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {[
          {
            title: 'PFA_DB (OLTP)',
            desc: 'Base opérationnelle — données quotidiennes (étudiants, notes, modules, absences)',
            icon: 'doc' as IconName,
          },
          {
            title: 'PFA_DW (Data Warehouse)',
            desc: 'Schéma en étoile — FaitNotes + DimEtudiant + DimModule + DimTemps',
            icon: 'database' as IconName,
          },
        ].map(c => (
          <div key={c.title} className="card p-4 flex gap-3">
            <div
              className="w-8 h-8 rounded-md flex items-center justify-center flex-shrink-0"
              style={{ background: 'var(--surface-2)' }}
            >
              <Icon name={c.icon} size={15} style={{ color: 'var(--text-2)' }} />
            </div>
            <div>
              <div className="text-[13px] font-medium">{c.title}</div>
              <div className="cap mt-1">{c.desc}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
