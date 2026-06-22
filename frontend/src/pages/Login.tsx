import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '../context/AuthContext'
import { Field } from '../components/ui/Field'
import { Icon } from '../components/ui/Icon'
import { PasswordInput } from '../components/ui/PasswordInput'

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
          backgroundImage: 'url(/ecole-eniad.avif)',
          backgroundSize: 'cover',
          backgroundPosition: 'center',
          color: '#fff',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        {/* dark overlay for readability */}
        <div
          style={{
            position: 'absolute',
            inset: 0,
            background: 'linear-gradient(165deg, rgba(12,10,9,0.78) 0%, rgba(28,25,23,0.72) 60%, rgba(41,37,36,0.65) 100%)',
          }}
        />
        {/* amber glow accent */}
        <div
          style={{
            position: 'absolute',
            bottom: 0,
            left: 0,
            right: 0,
            height: '40%',
            background: 'linear-gradient(to top, rgba(249,115,22,0.18) 0%, transparent 100%)',
          }}
        />

        <div className="relative z-10 flex justify-start">
          <img
            src="/eniad-logo.png"
            alt="ENIAD"
            style={{ height: 110, width: 'auto', objectFit: 'contain' }}
          />
        </div>

        <div className="relative z-10 max-w-[440px]">
          <div
            className="text-[11px] uppercase tracking-[0.18em] mb-3"
            style={{ color: 'var(--accent-300)', fontWeight: 500 }}
          >
            Plateforme décisionnelle · 2025/2026
          </div>
          <h1 className="text-[42px] font-bold leading-[1.08]" style={{ letterSpacing: '-0.03em' }}>
            Décider, anticiper, accompagner —{' '}
            <span style={{ color: 'var(--accent-400)' }}>chaque étudiant compte</span>.
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
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="w-full max-w-[400px]"
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border-2)',
            borderRadius: 20,
            padding: '40px 36px',
            boxShadow:
              '0 0 0 1px rgba(0,0,0,0.04), 0 2px 4px rgba(0,0,0,0.04), 0 8px 24px -4px rgba(0,0,0,0.08), 0 32px 64px -12px rgba(0,0,0,0.10)',
          }}
        >
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
                placeholder="prenom.nom@eniad.ma"
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
              <PasswordInput
                {...register('password')}
                autoComplete="current-password"
              />
            </Field>
            {/* No "Remember me" checkbox: tokens are intentionally kept in memory
                only (XSS hardening, see AuthContext). A persistent checkbox here
                would lie to the user — the session always ends on hard reload. */}
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
