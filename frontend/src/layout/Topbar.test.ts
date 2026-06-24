import { describe, expect, it } from 'vitest'
import { breadcrumbFor } from './Topbar'

describe('breadcrumbFor', () => {
  it.each([
    ['/cases', ['Pilotage', 'Interventions']],
    ['/cases/42', ['Interventions', 'Détail du cas']],
    ['/students/7', ['Étudiants', 'Fiche étudiant']],
    ['/enseignant', ['Enseignement', 'Mon espace']],
    ['/responsable', ['Pilotage', 'Mon espace']],
  ])('maps %s to route metadata', (path, expected) => {
    expect(breadcrumbFor(path)).toEqual(expected)
  })
})
