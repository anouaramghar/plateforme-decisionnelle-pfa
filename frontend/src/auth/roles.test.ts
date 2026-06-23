import { describe, expect, it } from 'vitest'
import { canAccessRolePage, canCreateAlerts, canDeleteStudents, canEnterNotes, canManageStudents, homeFor } from './roles'

describe('role policies', () => {
  it('allows responsables to manage but not delete students', () => {
    expect(canManageStudents('Responsable')).toBe(true)
    expect(canDeleteStudents('Responsable')).toBe(false)
  })

  it('restricts role pages to their role and admins', () => {
    expect(canAccessRolePage('Enseignant', 'enseignant')).toBe(true)
    expect(canAccessRolePage('Responsable', 'enseignant')).toBe(false)
    expect(canAccessRolePage('Admin', 'enseignant')).toBe(true)
  })

  it('matches backend authorization for notes and alerts', () => {
    expect(canEnterNotes('Enseignant')).toBe(true)
    expect(canEnterNotes('Responsable')).toBe(false)
    expect(canCreateAlerts('Admin')).toBe(true)
    expect(canCreateAlerts('Responsable')).toBe(false)
  })

  it('lands each role on its own home', () => {
    expect(homeFor('Enseignant')).toBe('/enseignant')
    expect(homeFor('Responsable')).toBe('/responsable')
    expect(homeFor('Admin')).toBe('/dashboard')
    expect(homeFor(undefined)).toBe('/dashboard')
  })
})
