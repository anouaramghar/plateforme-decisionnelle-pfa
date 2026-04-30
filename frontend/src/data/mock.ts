/**
 * Mock data — realistic ENIAD context, seeded RNG so the layout is stable
 * across renders. Used by every screen except Alerts (live polling against
 * the real /api/alertes endpoint).
 */

export type RiskLevel = 'faible' | 'modere' | 'eleve'

export interface Filiere {
  id: string
  nom: string
  short: string
  niveaux: string[]
  etudiants: number
  color: string
}

export interface Module {
  code: string
  nom: string
  semestre: string
  coef: number
  ects: number
}

export interface Etudiant {
  id: string
  matricule: string
  prenom: string
  nom: string
  nomComplet: string
  sexe: 'F' | 'M'
  filiere: string
  filiereNom: string
  niveau: string
  moyenne: number
  absences: number
  modulesValides: number
  modulesTotal: number
  scoreRisque: number
  risque: RiskLevel
  statut: 'Régulier' | 'Suivi' | 'En difficulté'
  dateNaissance: string
  ville: string
}

export interface NoteRow extends Module {
  cc: number
  tp: number
  exam: number
  finale: number
  valide: boolean
}

export interface Alerte {
  id: string
  type: 'NoteFaible' | 'AbsenceExcessive' | 'RisqueEleve' | 'ModuleNonValide' | 'RetardRecurrent'
  etudiantId: string
  etudiant: string
  filiere: string
  message: string
  date: string
  statut: 'active' | 'resolue'
}

export const FILIERES: Filiere[] = [
  { id: 'GI',   nom: 'Génie Informatique',  short: 'GI',   niveaux: ['1A', '2A', '3A'], etudiants: 142, color: '#f97316' },
  { id: 'GE',   nom: 'Génie Électrique',    short: 'GE',   niveaux: ['1A', '2A', '3A'], etudiants: 98,  color: '#0ea5e9' },
  { id: 'GC',   nom: 'Génie Civil',         short: 'GC',   niveaux: ['1A', '2A', '3A'], etudiants: 86,  color: '#14b8a6' },
  { id: 'GIND', nom: 'Génie Industriel',    short: 'GIND', niveaux: ['1A', '2A', '3A'], etudiants: 74,  color: '#a855f7' },
  { id: 'DATA', nom: 'Data Science & IA',   short: 'DATA', niveaux: ['2A', '3A'],       etudiants: 52,  color: '#ec4899' },
]

const PRENOMS_M = ['Mehdi', 'Youssef', 'Hamza', 'Reda', 'Adam', 'Anas', 'Othmane', 'Yassine', 'Karim', 'Salah-Eddine', 'Ilyas', 'Marouane', 'Anouar', 'Walid', 'Achraf', 'Soufiane', 'Bilal', 'Imad']
const PRENOMS_F = ['Salma', 'Imane', 'Yasmine', 'Hajar', 'Nada', 'Khadija', 'Sara', 'Oumaima', 'Lina', 'Maryam', 'Chaimae', 'Fatima-Zahra', 'Aya', 'Inès', 'Manal', 'Houda', 'Zineb', 'Asma']
const NOMS = ['Alaoui', 'Bennani', 'Tazi', 'El Amrani', 'Berrada', 'Cherkaoui', 'Idrissi', 'El Fassi', 'Benjelloun', 'Laraki', 'Ait Ali', 'Amghar', 'Engar', 'Lhiadi', 'Bouhlal', 'Naciri', 'El Khattabi', 'Sefrioui', 'Kabbaj', 'Lazrak', 'Sqalli', 'Ouazzani', 'Tahiri', 'El Mansouri', 'Belhaj', 'Ziani', 'Filali', 'Slaoui']

export const MODULES_BY_FIL: Record<string, Module[]> = {
  GI: [
    { code: 'INF301', nom: 'Algorithmique avancée',     semestre: 'S5', coef: 4, ects: 6 },
    { code: 'INF302', nom: 'Bases de données',          semestre: 'S5', coef: 3, ects: 5 },
    { code: 'INF303', nom: 'Réseaux & Sécurité',        semestre: 'S5', coef: 3, ects: 4 },
    { code: 'INF304', nom: "Systèmes d'exploitation",   semestre: 'S5', coef: 3, ects: 4 },
    { code: 'INF305', nom: 'Génie logiciel',            semestre: 'S5', coef: 2, ects: 3 },
    { code: 'INF306', nom: 'Anglais technique',         semestre: 'S5', coef: 1, ects: 2 },
    { code: 'INF401', nom: 'Intelligence artificielle', semestre: 'S6', coef: 4, ects: 6 },
    { code: 'INF402', nom: 'Cloud & DevOps',            semestre: 'S6', coef: 3, ects: 5 },
  ],
  GE:   [{ code: 'ELC301', nom: 'Électronique numérique', semestre: 'S5', coef: 4, ects: 6 }, { code: 'ELC302', nom: 'Automatique', semestre: 'S5', coef: 3, ects: 5 }],
  GC:   [{ code: 'CIV301', nom: 'Béton armé',              semestre: 'S5', coef: 4, ects: 6 }],
  GIND: [{ code: 'IND301', nom: 'Recherche opérationnelle', semestre: 'S5', coef: 3, ects: 5 }],
  DATA: [{ code: 'DAT301', nom: 'Machine Learning',         semestre: 'S5', coef: 4, ects: 6 }, { code: 'DAT302', nom: 'Statistique inférentielle', semestre: 'S5', coef: 3, ects: 5 }],
}

// Seeded RNG — stable across renders so the same student always sits in the
// same risk bucket between hot reloads (helps both the demo and the eyeballs).
function mulberry32(seed: number) {
  return () => {
    let t = (seed += 0x6d2b79f5)
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}
const rnd = mulberry32(20260425)
const pick = <T>(arr: T[]): T => arr[Math.floor(rnd() * arr.length)]
const randInt = (a: number, b: number) => a + Math.floor(rnd() * (b - a + 1))
const randFloat = (a: number, b: number, d = 2) => parseFloat((a + rnd() * (b - a)).toFixed(d))

function generateEtudiants(): Etudiant[] {
  const list: Etudiant[] = []
  let id = 1
  FILIERES.forEach(fil => {
    const target = Math.min(fil.etudiants, 36) // cap for mock perf
    for (let i = 0; i < target; i++) {
      const isF = rnd() > 0.42
      const prenom = isF ? pick(PRENOMS_F) : pick(PRENOMS_M)
      const nom = pick(NOMS)
      const niveau = pick(fil.niveaux)
      const moy = Math.max(4, Math.min(18.5, randFloat(7, 17, 2)))
      const absences = Math.max(0, Math.round((20 - moy) * randFloat(1.2, 2.6) + randInt(-4, 8)))
      const score = Math.min(
        0.98,
        Math.max(
          0.02,
          (moy < 10 ? 0.55 : 0.12) + (absences > 18 ? 0.22 : 0) + randFloat(-0.18, 0.22),
        ),
      )
      const risque: RiskLevel = score >= 0.65 ? 'eleve' : score >= 0.35 ? 'modere' : 'faible'
      const yearOffset = niveau === '3A' ? 0 : niveau === '2A' ? 1 : 2
      list.push({
        id: `ETU-${String(id).padStart(4, '0')}`,
        matricule: `EN${2023 + yearOffset}${String(id).padStart(3, '0')}`,
        prenom,
        nom,
        nomComplet: `${prenom} ${nom}`,
        sexe: isF ? 'F' : 'M',
        filiere: fil.id,
        filiereNom: fil.nom,
        niveau,
        moyenne: moy,
        absences,
        modulesValides: randInt(2, 6),
        modulesTotal: 6,
        scoreRisque: parseFloat(score.toFixed(3)),
        risque,
        statut: moy < 10 ? 'En difficulté' : moy < 12 ? 'Suivi' : 'Régulier',
        dateNaissance: `${randInt(2002, 2006)}-${String(randInt(1, 12)).padStart(2, '0')}-${String(randInt(1, 28)).padStart(2, '0')}`,
        ville: pick(['Casablanca', 'Rabat', 'Fès', 'Marrakech', 'Tanger', 'Oujda', 'Berkane', 'Nador', 'Agadir', 'Meknès', 'Salé', 'Tétouan']),
      })
      id++
    }
  })
  return list
}

export const ETUDIANTS = generateEtudiants()

export function genNotesFor(etu: Etudiant): NoteRow[] {
  const mods = MODULES_BY_FIL[etu.filiere] ?? MODULES_BY_FIL.GI
  return mods.map(m => {
    const cc = parseFloat((etu.moyenne + randFloat(-3, 3)).toFixed(2))
    const tp = parseFloat((etu.moyenne + randFloat(-2, 3)).toFixed(2))
    const exam = parseFloat((etu.moyenne + randFloat(-3.5, 2.5)).toFixed(2))
    const finale = parseFloat((cc * 0.3 + tp * 0.2 + exam * 0.5).toFixed(2))
    return {
      ...m,
      cc: Math.max(0, Math.min(20, cc)),
      tp: Math.max(0, Math.min(20, tp)),
      exam: Math.max(0, Math.min(20, exam)),
      finale: Math.max(0, Math.min(20, finale)),
      valide: finale >= 10,
    }
  })
}

export const ALERT_TYPES: Record<Alerte['type'], { label: string; severity: string; pill: 'bad' | 'warn' | 'info' }> = {
  NoteFaible:       { label: 'Note faible',         severity: 'haute',   pill: 'bad'  },
  AbsenceExcessive: { label: 'Absences excessives', severity: 'moyenne', pill: 'warn' },
  RisqueEleve:      { label: 'Risque élevé (ML)',   severity: 'haute',   pill: 'bad'  },
  ModuleNonValide:  { label: 'Module non validé',   severity: 'moyenne', pill: 'warn' },
  RetardRecurrent:  { label: 'Retards récurrents',  severity: 'basse',   pill: 'info' },
}

function generateAlertes(): Alerte[] {
  const out: Alerte[] = []
  let id = 1
  ETUDIANTS.forEach(etu => {
    if (etu.moyenne < 10) {
      out.push({
        id: `AL-${id++}`,
        type: 'NoteFaible',
        etudiantId: etu.id,
        etudiant: etu.nomComplet,
        filiere: etu.filiere,
        message: `Note < 10 sur module ${pick(MODULES_BY_FIL[etu.filiere] ?? MODULES_BY_FIL.GI).code}`,
        date: `2026-04-${String(randInt(10, 25)).padStart(2, '0')}T${String(randInt(8, 18)).padStart(2, '0')}:${String(randInt(0, 59)).padStart(2, '0')}`,
        statut: rnd() > 0.55 ? 'active' : 'resolue',
      })
    }
    if (etu.absences > 20) {
      out.push({
        id: `AL-${id++}`,
        type: 'AbsenceExcessive',
        etudiantId: etu.id,
        etudiant: etu.nomComplet,
        filiere: etu.filiere,
        message: `${etu.absences}h d'absences non justifiées`,
        date: `2026-04-${String(randInt(10, 25)).padStart(2, '0')}T${String(randInt(8, 18)).padStart(2, '0')}:${String(randInt(0, 59)).padStart(2, '0')}`,
        statut: rnd() > 0.4 ? 'active' : 'resolue',
      })
    }
    if (etu.scoreRisque > 0.7) {
      out.push({
        id: `AL-${id++}`,
        type: 'RisqueEleve',
        etudiantId: etu.id,
        etudiant: etu.nomComplet,
        filiere: etu.filiere,
        message: `Score de risque ML : ${(etu.scoreRisque * 100).toFixed(0)}%`,
        date: `2026-04-${String(randInt(15, 25)).padStart(2, '0')}T${String(randInt(8, 18)).padStart(2, '0')}:${String(randInt(0, 59)).padStart(2, '0')}`,
        statut: rnd() > 0.6 ? 'active' : 'resolue',
      })
    }
  })
  return out.sort((a, b) => b.date.localeCompare(a.date)).slice(0, 80)
}
export const ALERTES = generateAlertes()

export function kpis() {
  const n = ETUDIANTS.length
  const aRisque = ETUDIANTS.filter(e => e.risque === 'eleve').length
  const moyGlobale = ETUDIANTS.reduce((s, e) => s + e.moyenne, 0) / n
  const tauxReussite = ETUDIANTS.filter(e => e.moyenne >= 10).length / n
  const alertesActives = ALERTES.filter(a => a.statut === 'active').length
  return {
    nbEtudiants: n,
    nbARisque: aRisque,
    moyGlobale: parseFloat(moyGlobale.toFixed(2)),
    tauxReussite: parseFloat((tauxReussite * 100).toFixed(1)),
    alertesActives,
  }
}

export function notesByFiliere() {
  const map: Record<string, { sum: number; n: number }> = {}
  FILIERES.forEach(f => {
    map[f.id] = { sum: 0, n: 0 }
  })
  ETUDIANTS.forEach(e => {
    map[e.filiere].sum += e.moyenne
    map[e.filiere].n += 1
  })
  return FILIERES.map(f => ({
    filiere: f.short,
    moyenne: parseFloat((map[f.id].sum / Math.max(1, map[f.id].n)).toFixed(2)),
    color: f.color,
  }))
}

export function moyenneDistribution() {
  const buckets = [
    { label: '<8',    min: 0,  max: 8,  n: 0 },
    { label: '8-10',  min: 8,  max: 10, n: 0 },
    { label: '10-12', min: 10, max: 12, n: 0 },
    { label: '12-14', min: 12, max: 14, n: 0 },
    { label: '14-16', min: 14, max: 16, n: 0 },
    { label: '16+',   min: 16, max: 21, n: 0 },
  ]
  ETUDIANTS.forEach(e => {
    const b = buckets.find(b => e.moyenne >= b.min && e.moyenne < b.max)
    if (b) b.n++
  })
  return buckets
}

export function riskBreakdown() {
  const counts = { faible: 0, modere: 0, eleve: 0 }
  ETUDIANTS.forEach(e => {
    counts[e.risque]++
  })
  return [
    { label: 'Faible', n: counts.faible, color: '#16a34a' },
    { label: 'Modéré', n: counts.modere, color: '#f59e0b' },
    { label: 'Élevé',  n: counts.eleve,  color: '#dc2626' },
  ]
}

export function absenceTrend() {
  const weeks: { semaine: string; heures: number }[] = []
  let base = 8
  for (let i = 0; i < 14; i++) {
    base += randFloat(-1.5, 1.8)
    base = Math.max(2, Math.min(22, base))
    weeks.push({ semaine: `S${i + 1}`, heures: parseFloat(base.toFixed(1)) })
  }
  return weeks
}

export function topARisque(n = 8): Etudiant[] {
  return [...ETUDIANTS].sort((a, b) => b.scoreRisque - a.scoreRisque).slice(0, n)
}

export const ACTIVITY = [
  { kind: 'pred',  who: 'Système ML',       text: 'Prédiction batch lancée — 142 étudiants GI', when: 'il y a 12 min', icon: '⚡' },
  { kind: 'note',  who: 'Prof. LHIADI',     text: 'Notes du contrôle INF301 publiées',          when: 'il y a 1 h',    icon: '📝' },
  { kind: 'alert', who: 'Système',          text: '3 nouvelles alertes de risque élevé',        when: 'il y a 2 h',    icon: '🔔' },
  { kind: 'sync',  who: 'AIT ALI Marouane', text: 'Synchronisation DW exécutée (243 lignes)',   when: 'il y a 4 h',    icon: '🔄' },
  { kind: 'login', who: 'AMGHAR Anouar',    text: 'Export PDF — Rapport S5 / GI',                when: 'il y a 6 h',    icon: '📄' },
  { kind: 'note',  who: 'Prof. CHERKAOUI',  text: 'Absences mises à jour pour la semaine 14',   when: 'hier',          icon: '📊' },
]

export interface PredictionRun {
  id: string
  date: string
  filiere: string
  etudiants: number
  risqueEleve: number
  modele: string
  auc: number
  statut: 'OK' | 'Dégradé'
}

export function predictionRuns(): PredictionRun[] {
  return [
    { id: 'PR-082', date: '25 avr. 2026, 14:32', filiere: 'GI',   etudiants: 142, risqueEleve: 18, modele: 'XGBoost v1.4',     auc: 0.87, statut: 'OK' },
    { id: 'PR-081', date: '25 avr. 2026, 11:08', filiere: 'DATA', etudiants: 52,  risqueEleve: 7,  modele: 'XGBoost v1.4',     auc: 0.84, statut: 'OK' },
    { id: 'PR-080', date: '24 avr. 2026, 16:45', filiere: 'GE',   etudiants: 98,  risqueEleve: 12, modele: 'XGBoost v1.4',     auc: 0.86, statut: 'OK' },
    { id: 'PR-079', date: '24 avr. 2026, 09:21', filiere: 'TOUS', etudiants: 452, risqueEleve: 51, modele: 'XGBoost v1.4',     auc: 0.85, statut: 'OK' },
    { id: 'PR-078', date: '23 avr. 2026, 15:02', filiere: 'GC',   etudiants: 86,  risqueEleve: 9,  modele: 'RandomForest v0.9', auc: 0.79, statut: 'Dégradé' },
    { id: 'PR-077', date: '23 avr. 2026, 10:11', filiere: 'GIND', etudiants: 74,  risqueEleve: 6,  modele: 'XGBoost v1.4',     auc: 0.86, statut: 'OK' },
  ]
}

export const RAPPORTS = [
  { id: 'RP-104', titre: 'Performance globale — S1 2025/2026',     filiere: 'TOUS', format: 'PDF',  taille: '2.1 MB', date: '25 avr. 2026', auteur: 'AMGHAR A.' },
  { id: 'RP-103', titre: 'Notes détaillées — GI 3A',               filiere: 'GI',   format: 'XLSX', taille: '412 KB', date: '24 avr. 2026', auteur: 'AIT ALI M.' },
  { id: 'RP-102', titre: 'Étudiants à risque — Filière DATA',      filiere: 'DATA', format: 'PDF',  taille: '870 KB', date: '23 avr. 2026', auteur: 'AMGHAR A.' },
  { id: 'RP-101', titre: 'Absences hebdomadaires — toutes filières', filiere: 'TOUS', format: 'CSV', taille: '88 KB', date: '22 avr. 2026', auteur: 'Système' },
  { id: 'RP-100', titre: 'Bilan de prédictions ML — Avril',         filiere: 'TOUS', format: 'PDF', taille: '1.4 MB', date: '21 avr. 2026', auteur: 'ENGAR' },
  { id: 'RP-099', titre: 'Liste des alertes résolues — S2',         filiere: 'TOUS', format: 'XLSX', taille: '318 KB', date: '20 avr. 2026', auteur: 'AMGHAR A.' },
]
