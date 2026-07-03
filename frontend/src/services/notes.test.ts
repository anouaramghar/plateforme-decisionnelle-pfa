import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'
import { buildNotePayload, fetchModules, modulesForStudent, upsertNote } from './notes'

afterEach(() => vi.restoreAllMocks())

describe('notes service', () => {
  it('fetches the real module catalogue', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [{ id: 7, code: 'GI01', nom: 'Architecture', filiereId: 2, niveau: 'CI1', semestre: 'S1' }], total: 1 } })

    await expect(fetchModules()).resolves.toHaveLength(1)
    expect(api.get).toHaveBeenCalledWith('/modules?page=1&pageSize=100')
  })

  it('pages through the catalogue until total is reached', async () => {
    const makeModule = (id: number) => ({ id, code: `M${id}`, nom: `Module ${id}`, filiereId: 1, niveau: 'CI1', semestre: 'S1' })
    vi.spyOn(api, 'get').mockImplementation(async (url: string) => {
      const page = Number(new URLSearchParams(url.split('?')[1]).get('page'))
      const items = page === 1
        ? Array.from({ length: 100 }, (_, i) => makeModule(i + 1))
        : Array.from({ length: 88 }, (_, i) => makeModule(100 + i + 1))
      return { data: { items, total: 188 } }
    })

    await expect(fetchModules()).resolves.toHaveLength(188)
    expect(api.get).toHaveBeenCalledTimes(2)
  })

  it('filters modules by student filiere and level', () => {
    const modules = [
      { id: 1, code: 'GI01', nom: 'GI', filiereId: 2, niveau: 'CI1', semestre: 'S1' },
      { id: 2, code: 'IA01', nom: 'IA', filiereId: 3, niveau: 'CI1', semestre: 'S1' },
    ]

    expect(modulesForStudent(modules, 2, 'CI1').map(module => module.id)).toEqual([1])
  })

  it('uses the idempotent upsert endpoint', async () => {
    vi.spyOn(api, 'put').mockResolvedValue({ data: { id: 9, created: false } })
    const payload = { etudiantId: 1, moduleId: 2, noteTD: null, noteTP: null, noteExamen: null, noteFinal: 12, semestre: 'S1', annee: '2025/2026' }

    await expect(upsertNote(payload)).resolves.toEqual({ id: 9, created: false })
    expect(api.put).toHaveBeenCalledWith('/notes/upsert', payload)
  })

  it('builds note payload with the selected module semester', () => {
    const payload = buildNotePayload({
      etudiantId: 1,
      moduleId: 2,
      noteTD: '',
      noteTP: '',
      noteExamen: '',
      noteFinal: '12',
      semestre: 'S1',
      annee: '2025/2026',
      modules: [
        { id: 2, code: 'ML', nom: 'ML Ops', filiereId: 3, niveau: 'CI3', semestre: 'S7' },
      ],
    })

    expect(payload).toMatchObject({ moduleId: 2, semestre: 'S7', noteFinal: 12 })
  })
})
