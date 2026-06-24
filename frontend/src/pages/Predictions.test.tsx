import { render, screen } from '@testing-library/react'
import { vi, test, expect } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import Predictions from './Predictions'

// Mock useTheme
vi.mock('../context/ThemeContext', () => ({
  useTheme: () => ({
    theme: 'light',
    setTheme: vi.fn(),
  }),
}))

// Mock useAuth
vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 1, nomComplet: 'Admin Admin', role: 'Admin' },
  }),
}))

// Mock useNavigate
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => vi.fn(),
  }
})

// Mock react-apexcharts
vi.mock('react-apexcharts', () => ({
  default: () => <div data-testid="mock-chart" />,
}))

// Mock Copilot hooks (useCopilotReadable/useCopilotAction live on the main
// entry, not /v2 — see Students.tsx for the same import).
vi.mock('@copilotkit/react-core', () => ({
  useCopilotReadable: vi.fn(),
  useCopilotAction: vi.fn(),
}))

// Mock API calls via react-query useQuery
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query')
  return {
    ...actual,
    useQuery: ({ queryKey }: { queryKey: string[] }) => {
      if (queryKey[0] === 'predictions-summary') {
        return {
          data: {
            kpis: { evalues: 10, risqueEleve: 2, risqueModere: 3, auc: 0.85 },
            scatter: [],
            topARisque: [],
            runs: [],
          },
          isLoading: false,
          isError: false,
        }
      }
      if (queryKey[0] === 'ml-metrics') {
        return {
          data: {
            risk: {
              auc: 0.85,
              dataSource: 'synthetic',
              splitStrategy: 'grouped_student',
              modelVersion: '1.7.0',
              nSamples: 200,
              trainedAt: '2026-06-22T02:00:00Z',
              source: 'computed',
            },
            forecast: {
              mae: 1.2,
              dataSource: 'synthetic',
              splitStrategy: 'grouped_student',
              modelVersion: '1.7.0',
              nSamples: 200,
              trainedAt: '2026-06-22T02:00:00Z',
              source: 'computed',
            },
            auc: 0.85,
            f1: 0.8,
            precision: 0.78,
            recall: 0.82,
            nSamples: 200,
            modelVersion: '1.7.0',
            trainedAt: '2026-06-22T02:00:00Z',
            source: 'computed',
            dataSource: 'synthetic',
            splitStrategy: 'grouped_student',
          },
          isLoading: false,
          isError: false,
        }
      }
      return { data: null, isLoading: false, isError: false }
    },
    useMutation: () => ({
      mutate: vi.fn(),
      isPending: false,
    }),
  }
})

test('renders synthetic data warning when metrics data source is synthetic', () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <Predictions />
      </MemoryRouter>
    </QueryClientProvider>
  )

  expect(
    screen.getByText(
      /Modèle de démonstration entraîné sur des données synthétiques — ne pas utiliser pour une décision pédagogique/i
    )
  ).toBeInTheDocument()
})
