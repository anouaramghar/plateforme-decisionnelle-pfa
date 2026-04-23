import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  Mail,
  Lock,
  Eye,
  EyeOff,
  AlertCircle,
  CheckCircle2,
  User,
  UserCircle,
  BarChart3,
  ShieldCheck,
  Brain,
  Shield,
} from 'lucide-react';

const ROLES = [
  { value: '', label: 'Sélectionnez un rôle' },
  { value: 'Admin', label: 'Administrateur' },
  { value: 'Responsable', label: 'Responsable de filière' },
  { value: 'Enseignant', label: 'Enseignant' },
];

export default function Register() {
  const { register } = useAuth();
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    nom: '',
    prenom: '',
    email: '',
    motDePasse: '',
    confirmMotDePasse: '',
    role: '',
  });
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const validate = () => {
    if (
      !formData.nom ||
      !formData.prenom ||
      !formData.email ||
      !formData.motDePasse ||
      !formData.role
    ) {
      return 'Veuillez remplir tous les champs.';
    }
    if (formData.motDePasse.length < 6) {
      return 'Le mot de passe doit contenir au moins 6 caractères.';
    }
    if (formData.motDePasse !== formData.confirmMotDePasse) {
      return 'Les mots de passe ne correspondent pas.';
    }
    return null;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      await register(
        formData.nom,
        formData.prenom,
        formData.email,
        formData.motDePasse,
        formData.role
      );
      setSuccess('Compte créé avec succès ! Redirection vers la connexion…');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err) {
      const msg =
        err.response?.data?.message ||
        err.response?.data?.title ||
        "Erreur lors de la création du compte.";
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
            Rejoignez la plateforme pour accéder aux outils d'analyse et de
            suivi des performances académiques.
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
            <h1>Créer un compte</h1>
            <p>Remplissez les informations pour vous inscrire</p>
          </div>

          {error && (
            <div className="alert alert-error">
              <AlertCircle className="alert-icon" />
              <span>{error}</span>
            </div>
          )}

          {success && (
            <div className="alert alert-success">
              <CheckCircle2 className="alert-icon" />
              <span>{success}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} id="register-form">
            <div className="form-row">
              <div className="form-group">
                <label className="form-label" htmlFor="register-nom">
                  Nom
                </label>
                <div className="form-input-wrapper">
                  <input
                    id="register-nom"
                    type="text"
                    name="nom"
                    className="form-input"
                    placeholder="Dupont"
                    value={formData.nom}
                    onChange={handleChange}
                  />
                  <User className="input-icon" />
                </div>
              </div>

              <div className="form-group">
                <label className="form-label" htmlFor="register-prenom">
                  Prénom
                </label>
                <div className="form-input-wrapper">
                  <input
                    id="register-prenom"
                    type="text"
                    name="prenom"
                    className="form-input"
                    placeholder="Jean"
                    value={formData.prenom}
                    onChange={handleChange}
                  />
                  <UserCircle className="input-icon" />
                </div>
              </div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="register-email">
                Adresse email
              </label>
              <div className="form-input-wrapper">
                <input
                  id="register-email"
                  type="email"
                  name="email"
                  className="form-input"
                  placeholder="exemple@universite.ma"
                  value={formData.email}
                  onChange={handleChange}
                  autoComplete="email"
                />
                <Mail className="input-icon" />
              </div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="register-role">
                Rôle
              </label>
              <div className="form-input-wrapper">
                <select
                  id="register-role"
                  name="role"
                  className="form-select"
                  value={formData.role}
                  onChange={handleChange}
                >
                  {ROLES.map((r) => (
                    <option key={r.value} value={r.value}>
                      {r.label}
                    </option>
                  ))}
                </select>
                <Shield className="input-icon" />
              </div>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="register-password">
                Mot de passe
              </label>
              <div className="form-input-wrapper">
                <input
                  id="register-password"
                  type={showPassword ? 'text' : 'password'}
                  name="motDePasse"
                  className="form-input"
                  placeholder="Min. 6 caractères"
                  value={formData.motDePasse}
                  onChange={handleChange}
                  autoComplete="new-password"
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

            <div className="form-group">
              <label className="form-label" htmlFor="register-confirm">
                Confirmer le mot de passe
              </label>
              <div className="form-input-wrapper">
                <input
                  id="register-confirm"
                  type={showConfirm ? 'text' : 'password'}
                  name="confirmMotDePasse"
                  className="form-input"
                  placeholder="Retapez votre mot de passe"
                  value={formData.confirmMotDePasse}
                  onChange={handleChange}
                  autoComplete="new-password"
                />
                <Lock className="input-icon" />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowConfirm((v) => !v)}
                  aria-label="Afficher/masquer la confirmation"
                >
                  {showConfirm ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
              id="register-submit"
            >
              {loading ? (
                <>
                  <span className="btn-spinner" />
                  Création en cours…
                </>
              ) : (
                "Créer mon compte"
              )}
            </button>
          </form>

          <div className="auth-footer">
            Vous avez déjà un compte ?{' '}
            <Link to="/login">Se connecter</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
