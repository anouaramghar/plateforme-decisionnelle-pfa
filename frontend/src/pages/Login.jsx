import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Mail, Lock, Eye, EyeOff, AlertCircle, BarChart3, ShieldCheck, Brain } from 'lucide-react';

export default function Login() {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (!email || !password) {
      setError('Veuillez remplir tous les champs.');
      return;
    }

    setLoading(true);
    try {
      await login(email, password);
    } catch (err) {
      const msg =
        err.response?.data?.message ||
        'Email ou mot de passe incorrect.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-layout">
      {/* Left Brand Panel */}
      <div className="auth-brand-panel">
        <div className="brand-content">
          <div className="brand-logo">
            <BarChart3 />
          </div>
          <h2 className="brand-title">Plateforme Décisionnelle</h2>
          <p className="brand-subtitle">
            Analysez les performances étudiantes, détectez les risques et prenez
            des décisions éclairées grâce à l'intelligence artificielle.
          </p>

          <div className="brand-features">
            <div className="brand-feature">
              <div className="brand-feature-icon blue">
                <BarChart3 size={18} />
              </div>
              <span>Tableaux de bord interactifs et visualisations avancées</span>
            </div>
            <div className="brand-feature">
              <div className="brand-feature-icon green">
                <ShieldCheck size={18} />
              </div>
              <span>Alertes intelligentes et suivi de présence en temps réel</span>
            </div>
            <div className="brand-feature">
              <div className="brand-feature-icon purple">
                <Brain size={18} />
              </div>
              <span>Prédictions ML pour identifier les étudiants à risque</span>
            </div>
          </div>
        </div>
      </div>

      {/* Right Form Panel */}
      <div className="auth-form-panel">
        <div className="auth-form-container">
          <div className="auth-form-header">
            <h1>Connexion</h1>
            <p>Accédez à votre espace de gestion</p>
          </div>

          {error && (
            <div className="alert alert-error">
              <AlertCircle className="alert-icon" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} id="login-form">
            <div className="form-group">
              <label className="form-label" htmlFor="login-email">
                Adresse email
              </label>
              <div className="form-input-wrapper">
                <input
                  id="login-email"
                  type="email"
                  className="form-input"
                  placeholder="exemple@universite.ma"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="email"
                />
                <Mail className="input-icon" />
              </div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="login-password">
                Mot de passe
              </label>
              <div className="form-input-wrapper">
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  className="form-input"
                  placeholder="Entrez votre mot de passe"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                />
                <Lock className="input-icon" />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label="Afficher/masquer le mot de passe"
                >
                  {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
              id="login-submit"
            >
              {loading ? (
                <>
                  <span className="btn-spinner" />
                  Connexion en cours…
                </>
              ) : (
                'Se connecter'
              )}
            </button>
          </form>

          <div className="auth-footer">
            Pas encore de compte ?{' '}
            <Link to="/register">Créer un compte</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
