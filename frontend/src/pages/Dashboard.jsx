import { useAuth } from '../context/AuthContext';
import { LogOut, BarChart3, Shield } from 'lucide-react';

export default function Dashboard() {
  const { user, logout } = useAuth();

  return (
    <div className="dashboard-layout">
      <header className="dashboard-header">
        <h1>
          <BarChart3 size={22} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 8 }} />
          Plateforme Décisionnelle
        </h1>
        <div className="dashboard-user-info">
          <span>{user?.email}</span>
          <span className="role-badge">
            <Shield size={12} />
            {user?.role}
          </span>
          <button className="btn-logout" onClick={logout} id="logout-btn">
            <LogOut size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} />
            Déconnexion
          </button>
        </div>
      </header>

      <main className="dashboard-content">
        <div className="dashboard-welcome">
          <h2>Bienvenue 👋</h2>
          <p>
            Vous êtes connecté en tant que <strong>{user?.role}</strong>.
            <br />
            Le tableau de bord complet avec Recharts sera intégré prochainement.
          </p>
          <div className="role-badge" style={{ marginTop: '1.5rem' }}>
            <Shield size={14} />
            Rôle : {user?.role}
          </div>
        </div>
      </main>
    </div>
  );
}
