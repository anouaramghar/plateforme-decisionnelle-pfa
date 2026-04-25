import { useQuery } from '@tanstack/react-query'
import { api } from '../services/api'
import { useAuth } from '../context/AuthContext'

interface Alerte {
  id: number
  etudiantId: number
  type: string
  niveau: string
  message: string
  resolue: boolean
  creeLe: string
}

export default function Alerts() {
  const { token } = useAuth()
  const { data: alertes = [], isLoading, isError } = useQuery<Alerte[]>({
    queryKey: ['alertes'],
    queryFn: () => api.get('/alertes').then(r => r.data),
    refetchInterval: 30_000,
    // Don't fire until the JWT is available — avoids a guaranteed 401 on first render.
    enabled: !!token,
  })

  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold text-gray-800 mb-4">Alertes</h1>
      {isLoading && <p className="text-gray-400">Chargement…</p>}
      {isError && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2 mb-4">
          Impossible de charger les alertes. Veuillez réessayer.
        </p>
      )}
      <ul className="space-y-2">
        {alertes.map(a => (
          <li key={a.id} className="bg-white border rounded-lg px-4 py-3 flex items-center justify-between shadow-sm">
            <div>
              <span className="font-medium text-sm text-gray-800">{a.type}</span>
              <span className="ml-2 text-xs text-gray-500">{a.message}</span>
            </div>
            <span className={`text-xs font-semibold px-2 py-1 rounded-full ${
              a.niveau === 'Critique' ? 'bg-red-100 text-red-700' :
              a.niveau === 'Eleve'    ? 'bg-orange-100 text-orange-700' :
              a.niveau === 'Moyen'   ? 'bg-yellow-100 text-yellow-700' :
                                       'bg-green-100 text-green-700'
            }`}>{a.niveau}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
