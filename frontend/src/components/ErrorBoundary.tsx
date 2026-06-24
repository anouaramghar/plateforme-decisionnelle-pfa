import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('ErrorBoundary caught an error:', error, info)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: '100vh',
            gap: 16,
            padding: 24,
            fontFamily: 'system-ui, -apple-system, sans-serif',
            color: '#1c1917',
            textAlign: 'center',
          }}
        >
          <div style={{ fontSize: 22, fontWeight: 600 }}>
            Une erreur inattendue est survenue
          </div>
          <div style={{ fontSize: 14, color: '#57534e', maxWidth: 420 }}>
            La plateforme a rencontré un problème. Vous pouvez recharger la page pour reprendre.
          </div>
          <button
            onClick={() => window.location.reload()}
            style={{
              padding: '8px 16px',
              borderRadius: 8,
              border: '1px solid #d6d3d1',
              background: 'var(--surface, #fff)',
              cursor: 'pointer',
              fontSize: 13,
              fontWeight: 500,
            }}
          >
            Recharger
          </button>
        </div>
      )
    }
    return this.props.children
  }
}