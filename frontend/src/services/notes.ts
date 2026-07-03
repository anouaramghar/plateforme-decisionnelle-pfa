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

interface BuildNotePayloadInput {
  etudiantId: number
  moduleId: number
  noteTD: string
  noteTP: string
  noteExamen: string
  noteFinal: string
  semestre: string
  annee: string
  modules: ModuleOption[]
}

const toNullableNumber = (value: string): number | null =>
  value === '' ? null : Number(value)

export function buildNotePayload(input: BuildNotePayloadInput): NoteUpsertPayload {
  const module = input.modules.find(m => m.id === Number(input.moduleId))
  return {
    etudiantId: input.etudiantId,
    moduleId: input.moduleId,
    noteTD: toNullableNumber(input.noteTD),
    noteTP: toNullableNumber(input.noteTP),
    noteExamen: toNullableNumber(input.noteExamen),
    noteFinal: toNullableNumber(input.noteFinal),
    semestre: module?.semestre ?? input.semestre,
    annee: input.annee,
  }
}

export async function fetchModules(): Promise<ModuleOption[]> {
  // /modules caps pageSize at 100 server-side; page through all of them so
  // filières whose modules land past the first page aren't silently dropped
  // from the note-entry module picker.
  const items: ModuleOption[] = []
  let page = 1
  while (true) {
    const response = await api.get<{ items: ModuleOption[]; total: number }>(`/modules?page=${page}&pageSize=100`)
    items.push(...response.data.items)
    if (items.length >= response.data.total || response.data.items.length === 0) break
    page += 1
  }
  return items
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
