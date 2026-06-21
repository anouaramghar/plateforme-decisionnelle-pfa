export type RolePage = 'enseignant' | 'responsable'

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
