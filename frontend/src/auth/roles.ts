export type RolePage = 'enseignant' | 'responsable'

// Each role's real home — the page that matches what they actually do.
// Teachers enter grades/absences; responsables steer a cohort; admins pilot.
export function homeFor(role?: string | null): string {
  return role === 'Enseignant' ? '/enseignant'
    : role === 'Responsable' ? '/responsable'
    : '/dashboard'
}

export function canManageStudents(role?: string | null): boolean {
  return role === 'Admin' || role === 'Responsable'
}

export function canDeleteStudents(role?: string | null): boolean {
  return role === 'Admin'
}

export function canEnterNotes(role?: string | null): boolean {
  return role === 'Admin' || role === 'Enseignant'
}

export function canCreateAlerts(role?: string | null): boolean {
  return role === 'Admin'
}

export function canAccessRolePage(role: string | null | undefined, page: RolePage): boolean {
  if (role === 'Admin') return true
  return page === 'enseignant' ? role === 'Enseignant' : role === 'Responsable'
}

export function canCreateStudent(role?: string | null): boolean {
  return role === 'Admin' || role === 'Responsable'
}

export function canRunBatchPredictions(role?: string | null): boolean {
  return role === 'Admin' || role === 'Responsable'
}

export function canUseCopilot(role?: string | null): boolean {
  return role === 'Admin' || role === 'Responsable'
}
