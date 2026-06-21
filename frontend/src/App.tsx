import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import { AppShell } from './layout/AppShell'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Students from './pages/Students'
import StudentProfile from './pages/StudentProfile'
import Alerts from './pages/Alerts'
import Reports from './pages/Reports'
import Predictions from './pages/Predictions'
import Admin from './pages/Admin'
import Settings from './pages/Settings'
import Enseignant from './pages/Enseignant'
import Responsable from './pages/Responsable'
import { RoleRoute } from './components/auth/RoleRoute'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  return token ? <>{children}</> : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <AppShell />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="students" element={<Students />} />
          <Route path="students/:id" element={<StudentProfile />} />
          <Route path="alerts" element={<RoleRoute roles={['Admin', 'Responsable']}><Alerts /></RoleRoute>} />
          <Route path="predictions" element={<Predictions />} />
          <Route path="reports" element={<Reports />} />
          <Route path="admin" element={<RoleRoute roles={['Admin', 'Responsable']}><Admin /></RoleRoute>} />
          <Route path="enseignant" element={<RoleRoute roles={['Admin', 'Enseignant']}><Enseignant /></RoleRoute>} />
          <Route path="responsable" element={<RoleRoute roles={['Admin', 'Responsable']}><Responsable /></RoleRoute>} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
