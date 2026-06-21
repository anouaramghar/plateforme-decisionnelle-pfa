import { api } from './api'

export interface ModuleOption {
  id: number
  code: string
  nom: string
  filiereId: number
  niveau: string
  semestre: string
}

export interface NoteUpsertPayload {
  etudiantId: number
  moduleId: number
  noteTD: number | null
  noteTP: number | null
  noteExamen: number | null
  noteFinal: number | null
  semestre: string
  annee: string
}

export interface NoteUpsertResult {
  id: number
  created: boolean
}

export async function fetchModules(): Promise<ModuleOption[]> {
  const response = await api.get<{ items: ModuleOption[] }>('/modules?pageSize=100')
  return response.data.items
}

export function modulesForStudent(
  modules: ModuleOption[],
  filiereId: number,
  niveau: string,
): ModuleOption[] {
  return modules.filter(module => module.filiereId === filiereId && module.niveau === niveau)
}

export async function upsertNote(payload: NoteUpsertPayload): Promise<NoteUpsertResult> {
  const response = await api.put<NoteUpsertResult>('/notes/upsert', payload)
  return response.data
}
