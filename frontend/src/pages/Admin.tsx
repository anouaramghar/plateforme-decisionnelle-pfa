import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../context/AuthContext'
import { Icon } from '../components/ui/Icon'
import { Pill } from '../components/ui/Pill'
import { Avatar } from '../components/ui/Avatar'
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

type Tab = 'users' | 'dw'
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
}): Promise<Utilisateur> {
  const res = await api.post<Utilisateur>('/utilisateurs', data)
  return res.data
}

async function syncDw(): Promise<{ message: string; timestamp: string }> {
  const res = await api.post<{ message: string; timestamp: string }>('/admin/sync-dw')
  return res.data
}

// ── Component ────────────────────────────────────────────────────────────────

export default function Admin() {
  const { user } = useAuth()
  const [tab, setTab] = useState<Tab>('users')

  if (user?.role !== 'Admin') {
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
            { id: 'users', label: 'Utilisateurs', icon: 'students' },
            { id: 'dw',    label: 'Data Warehouse', icon: 'database' },
          ] as { id: Tab; label: string; icon: string }[]
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

      {tab === 'users' && <UsersTab />}
      {tab === 'dw'    && <DwTab />}
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
          error={createMutation.error?.message}
        />
      )}

      <div className="card overflow-hidden">
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
              className="grid px-4 py-2.5 text-[11px] uppercase tracking-wider"
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
                className="grid items-center px-4 py-3"
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
  onSubmit: (data: { nom: string; prenom: string; email: string; role: string; motDePasse: string }) => void
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
  })

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm(f => ({ ...f, [k]: e.target.value }))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    onSubmit(form)
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

      <div className="grid grid-cols-2 gap-3">
        <label className="flex flex-col gap-1">
          <span className="cap">Prénom</span>
          <input
            className="input"
            placeholder="Mohamed"
            value={form.prenom}
            onChange={set('prenom')}
            required
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="cap">Nom</span>
          <input
            className="input"
            placeholder="Ait Ali"
            value={form.nom}
            onChange={set('nom')}
            required
          />
        </label>
      </div>

      <label className="flex flex-col gap-1">
        <span className="cap">Email</span>
        <input
          className="input"
          type="email"
          placeholder="m.aitali@eniad.dz"
          value={form.email}
          onChange={set('email')}
          required
        />
      </label>

      <div className="grid grid-cols-2 gap-3">
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
          <input
            className="input"
            type="password"
            placeholder="Min. 8 caractères"
            value={form.motDePasse}
            onChange={set('motDePasse')}
            required
            minLength={8}
          />
        </label>
      </div>

      {error && (
        <p className="text-[12px]" style={{ color: 'var(--bad)' }}>{error}</p>
      )}

      <div className="flex items-center gap-2 justify-end">
        <button type="button" className="btn btn-sm" onClick={onCancel}>
          Annuler
        </button>
        <button type="submit" className="btn btn-sm btn-accent" disabled={isPending}>
          {isPending ? 'Création…' : 'Créer l\'utilisateur'}
        </button>
      </div>
    </form>
  )
}

// ── DW sync tab ───────────────────────────────────────────────────────────────

function DwTab() {
  const [lastSync, setLastSync] = useState<string | null>(null)
  const [syncError, setSyncError] = useState<string | null>(null)

  const syncMutation = useMutation({
    mutationFn: syncDw,
    onSuccess: (data) => {
      setLastSync(data.timestamp)
      setSyncError(null)
    },
    onError: (err: Error) => {
      setSyncError(err.message)
    },
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

      {/* Info grid */}
      <div className="grid grid-cols-2 gap-3">
        {[
          {
            title: 'PFA_DB (OLTP)',
            desc: 'Base opérationnelle — données quotidiennes (étudiants, notes, modules, absences)',
            icon: 'doc',
          },
          {
            title: 'PFA_DW (Data Warehouse)',
            desc: 'Schéma en étoile — FaitNotes + DimEtudiant + DimModule + DimTemps',
            icon: 'database',
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
