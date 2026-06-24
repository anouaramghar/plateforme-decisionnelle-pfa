export interface StudentImportRow {
  rowNumber: number
  matricule: string
  nom: string
  prenom: string
  email: string
  filiereCode: string
  niveau: string
  annee: string
}

export interface StudentImportError {
  rowNumber: number
  field: string
  message: string
}

export interface StudentImportResult {
  imported: number
  errors: StudentImportError[]
}

const HEADER_ALIASES: Record<string, keyof Omit<StudentImportRow, 'rowNumber'>> = {
  matricule: 'matricule',
  nom: 'nom',
  prenom: 'prenom',
  email: 'email',
  filiere: 'filiereCode',
  filierecode: 'filiereCode',
  niveau: 'niveau',
  annee: 'annee',
}

function normalizeHeader(value: unknown): string {
  return String(value ?? '')
    .replace(/^\uFEFF/, '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[\s_-]+/g, '')
}

export async function parseStudentCsv(text: string): Promise<StudentImportRow[]> {
  const XLSX = await import('xlsx')
  const workbook = XLSX.read(text, { type: 'string' })
  const sheetName = workbook.SheetNames[0]
  if (!sheetName) return []
  const table = XLSX.utils.sheet_to_json<unknown[]>(workbook.Sheets[sheetName], {
    header: 1,
    defval: '',
    raw: false,
  })
  if (table.length < 2) return []

  const headers = table[0].map(value => HEADER_ALIASES[normalizeHeader(value)])
  return table.slice(1).flatMap((values, index) => {
    if (values.every(value => String(value ?? '').trim() === '')) return []
    const row: StudentImportRow = {
      rowNumber: index + 2,
      matricule: '',
      nom: '',
      prenom: '',
      email: '',
      filiereCode: '',
      niveau: '',
      annee: '',
    }
    headers.forEach((field, column) => {
      if (field) row[field] = String(values[column] ?? '').trim()
    })
    return [row]
  })
}
