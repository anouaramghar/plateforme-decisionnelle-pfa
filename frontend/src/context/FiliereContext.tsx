import { createContext, useContext, useState } from 'react'

interface FiliereCtx {
  filiere: string
  setFiliere: (f: string) => void
}

const Ctx = createContext<FiliereCtx>({ filiere: 'TOUS', setFiliere: () => {} })

export function FiliereProvider({ children }: { children: React.ReactNode }) {
  const [filiere, setFiliereState] = useState<string>(
    () => localStorage.getItem('pfa_filiere') ?? 'TOUS'
  )

  const setFiliere = (f: string) => {
    setFiliereState(f)
    localStorage.setItem('pfa_filiere', f)
  }

  return <Ctx.Provider value={{ filiere, setFiliere }}>{children}</Ctx.Provider>
}

export const useFiliere = () => useContext(Ctx)
