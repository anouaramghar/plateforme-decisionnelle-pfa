import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '../context/AuthContext'
import { Field } from '../components/ui/Field'
import { Icon } from '../components/ui/Icon'

const schema = z.object({
  email: z.string().email('Email invalide'),
  password: z.string().min(1, 'Mot de passe requis'),
})
type FormValues = z.infer<typeof schema>

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = async ({ email, password }: FormValues) => {
    try {
      setError(null)
      await login(email, password)
      navigate('/dashboard')
    } catch {
      setError('Email ou mot de passe incorrect.')
    }
  }

  return (
    <div className="vh-full flex" style={{ background: 'var(--bg)' }}>
      {/* Left brand panel */}
      <div
        className="hidden lg:flex flex-col justify-between p-10 flex-1"
        style={{
          background: 'linear-gradient(165deg, #0c0a09 0%, #1c1917 60%, #292524 100%)',
          color: '#fff',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        {/* ambient amber glow */}
        <div
          style={{
            position: 'absolute',
            top: '-20%',
            right: '-15%',
            width: 520,
            height: 520,
            borderRadius: '50%',
            background: 'radial-gradient(circle, rgba(249,115,22,0.32) 0%, rgba(249,115,22,0) 60%)',
            filter: 'blur(20px)',
          }}
        />
        <div
          style={{
            position: 'absolute',
            bottom: '-10%',
            left: '-10%',
            width: 420,
            height: 420,
            borderRadius: '50%',
            background: 'radial-gradient(circle, rgba(249,115,22,0.12) 0%, rgba(0,0,0,0) 70%)',
          }}
        />

        {/* grid lines */}
        <div
          style={{
            position: 'absolute',
            inset: 0,
            backgroundImage:
              'linear-gradient(rgba(255,255,255,.04) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.04) 1px, transparent 1px)',
            backgroundSize: '48px 48px',
            maskImage: 'radial-gradient(ellipse at center, black 30%, transparent 80%)',
            WebkitMaskImage: 'radial-gradient(ellipse at center, black 30%, transparent 80%)',
          }}
        />

        <div className="relative z-10 flex items-center gap-3">
          <div
            style={{
              width: 36,
              height: 36,
              borderRadius: 9,
              background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontWeight: 700,
              fontSize: 17,
              boxShadow: 'inset 0 1px 0 rgba(255,255,255,.2), 0 8px 24px -8px rgba(249,115,22,.5)',
            }}
          >
            E
          </div>
          <div>
            <div className="text-[14px] font-semibold leading-none">ENIAD</div>
            <div className="text-[11px] mt-1" style={{ color: 'rgba(255,255,255,0.5)' }}>
              École Nationale d'Intelligence Artificielle
            </div>
          </div>
        </div>

        <div className="relative z-10 max-w-[440px]">
          <div
            className="text-[11px] uppercase tracking-[0.18em] mb-3"
            style={{ color: 'var(--accent-300)', fontWeight: 500 }}
          >
            Plateforme décisionnelle · 2025/2026
          </div>
          <h1 className="font-serif text-[44px] leading-[1.05]" style={{ letterSpacing: '-0.02em' }}>
            Décider, anticiper, accompagner —{' '}
            <em style={{ color: 'var(--accent-400)' }}>chaque étudiant compte</em>.
          </h1>
          <p
            className="mt-4 text-[13.5px] leading-relaxed"
            style={{ color: 'rgba(255,255,255,0.65)' }}
          >
            BI académique et analyse prédictive du risque de décrochage. Tableaux de bord en temps
            réel, alertes automatisées, rapports prêts pour le conseil pédagogique.
          </p>

          <div className="mt-7 grid grid-cols-3 gap-4">
            {[
              { v: '452', l: 'Étudiants suivis' },
              { v: '5',   l: 'Filières' },
              { v: '0.87', l: 'AUC modèle ML' },
            ].map(s => (
              <div key={s.l} className="border-l-2 pl-3" style={{ borderColor: 'rgba(249,115,22,.6)' }}>
                <div className="num text-[22px]" style={{ color: '#fff', fontWeight: 500, letterSpacing: '-0.02em' }}>
                  {s.v}
                </div>
                <div className="text-[10.5px] mt-0.5" style={{ color: 'rgba(255,255,255,.5)' }}>
                  {s.l}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div
          className="relative z-10 flex items-center justify-between text-[11px]"
          style={{ color: 'rgba(255,255,255,.4)' }}
        >
          <span>© 2026 ENIAD · Berkane · Maroc</span>
          <span className="font-mono">v0.4.2</span>
        </div>
      </div>

      {/* Right form */}
      <div
        className="flex-1 flex flex-col items-center justify-center p-8 lg:p-12"
        style={{ background: 'var(--surface)' }}
      >
        <form onSubmit={handleSubmit(onSubmit)} className="w-full max-w-[360px]">
          <div className="mb-7">
            <div
              className="text-[11px] uppercase tracking-wider mb-2"
              style={{ color: 'var(--text-3)', fontWeight: 500, letterSpacing: '0.08em' }}
            >
              Connexion
            </div>
            <h2 className="text-[24px] font-semibold tracking-tight">Bon retour parmi nous</h2>
            <p className="text-[12.5px] mt-1.5" style={{ color: 'var(--text-3)' }}>
              Connectez-vous pour accéder au tableau de bord du conseil pédagogique.
            </p>
          </div>

          {error && (
            <div
              className="mb-3 text-[12.5px] rounded-md px-3 py-2"
              style={{
                background: 'color-mix(in oklch, var(--bad) 8%, transparent)',
                color: 'var(--bad)',
                border: '1px solid color-mix(in oklch, var(--bad) 25%, transparent)',
              }}
            >
              {error}
            </div>
          )}

          <div className="space-y-3.5">
            <Field label="Adresse e-mail ENIAD" required error={errors.email?.message}>
              <input
                {...register('email')}
                type="email"
                autoComplete="email"
                placeholder="prenom.nom@eniad.dz"
                className="input"
              />
            </Field>
            <Field
              label="Mot de passe"
              hint={
                <a className="hover:underline" style={{ color: 'var(--accent-600)' }}>
                  Mot de passe oublié&nbsp;?
                </a>
              }
              required
              error={errors.password?.message}
            >
              <input
                {...register('password')}
                type="password"
                autoComplete="current-password"
                className="input"
              />
            </Field>
            <label className="flex items-center gap-2 text-[12px]" style={{ color: 'var(--text-2)' }}>
              <input
                type="checkbox"
                defaultChecked
                className="rounded border-stone-300 text-orange-600 focus:ring-orange-500"
              />
              Se souvenir de cet appareil
            </label>
          </div>

          <button type="submit" disabled={isSubmitting} className="btn btn-accent btn-lg w-full mt-5">
            {isSubmitting ? (
              'Connexion en cours…'
            ) : (
              <>
                Se connecter <Icon name="arrowRight" size={14} strokeWidth={2} />
              </>
            )}
          </button>

          <div className="mt-5 flex items-center gap-3">
            <div className="flex-1 h-px" style={{ background: 'var(--border)' }} />
            <span className="text-[11px]" style={{ color: 'var(--text-4)' }}>
              OU
            </span>
            <div className="flex-1 h-px" style={{ background: 'var(--border)' }} />
          </div>

          <button
            type="button"
            className="btn btn-lg w-full mt-4"
            style={{ background: 'var(--surface)', borderColor: 'var(--border-2)' }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24">
              <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" />
              <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
              <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" />
              <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" />
            </svg>
            Se connecter avec Google Workspace
          </button>

          <p className="mt-6 text-[11.5px] text-center" style={{ color: 'var(--text-3)' }}>
            Pas de compte ?{' '}
            <a className="hover:underline" style={{ color: 'var(--accent-600)', fontWeight: 500 }}>
              Demander un accès au responsable
            </a>
          </p>
        </form>
      </div>
    </div>
  )
}
