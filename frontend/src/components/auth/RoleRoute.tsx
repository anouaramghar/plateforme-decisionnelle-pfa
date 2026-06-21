import { Navigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

export function RoleRoute({ roles, children }: { roles: string[]; children: React.ReactNode }) {
  const { user } = useAuth()
  return user && roles.includes(user.role)
    ? <>{children}</>
    : <Navigate to="/dashboard" replace />
}
