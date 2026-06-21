import { useState, forwardRef, type InputHTMLAttributes } from 'react'
import { Icon } from './Icon'

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>

export const PasswordInput = forwardRef<HTMLInputElement, Props>(
  function PasswordInput({ className, style, ...rest }, ref) {
  const [show, setShow] = useState(false)
  return (
    <div className="relative">
      <input
        {...rest}
        ref={ref}
        type={show ? 'text' : 'password'}
        className={`input ${className ?? ''}`}
        style={{ paddingRight: '2.5rem', ...style }}
      />
      <button
        type="button"
        tabIndex={-1}
        onClick={() => setShow(v => !v)}
        style={{
          position: 'absolute',
          right: 10,
          top: '50%',
          transform: 'translateY(-50%)',
          background: 'none',
          border: 'none',
          padding: 0,
          cursor: 'pointer',
          color: 'var(--text-3)',
          display: 'flex',
          alignItems: 'center',
        }}
        aria-label={show ? 'Masquer le mot de passe' : 'Afficher le mot de passe'}
      >
        <Icon name={show ? 'eyeOff' : 'eye'} size={15} />
      </button>
    </div>
  )
})
