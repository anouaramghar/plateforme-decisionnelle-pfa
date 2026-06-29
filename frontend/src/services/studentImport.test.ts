import { describe, expect, it } from 'vitest'
import { parseStudentCsv } from './studentImport'

describe('parseStudentCsv', () => {
  it('normalizes BOM headers and preserves quoted comma values', async () => {
    const csv = '\uFEFFmatricule,nom,prenom,filiere,niveau,annee\nE1,"Doe, Jr",Jane,GINF,CI1,2025/2026'

    expect(await parseStudentCsv(csv)).toEqual([
      {
        rowNumber: 2,
        matricule: 'E1',
        nom: 'Doe, Jr',
        prenom: 'Jane',
        email: '',
        filiereCode: 'GINF',
        niveau: 'CI1',
        annee: '2025/2026',
      },
    ])
  })

  it('parses semicolon-delimited CSV', async () => {
    const csv = 'matricule;nom;prenom;filière;niveau;annee\nE2;Ali;Sara;IA;CI2;2025/2026'

    expect((await parseStudentCsv(csv))[0]).toMatchObject({
      matricule: 'E2',
      filiereCode: 'IA',
      niveau: 'CI2',
    })
  })
})
