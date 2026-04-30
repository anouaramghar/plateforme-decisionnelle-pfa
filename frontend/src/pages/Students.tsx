import { useMemo, useState } from 'react'
import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { RiskBar, RiskPill } from '../components/ui/RiskBar'
import { Select } from '../components/ui/Select'
import { SectionHeader } from '../components/ui/SectionHeader'
import { ChartRadial } from '../components/charts'
import { ETUDIANTS, genNotesFor, type Etudiant } from '../data/mock'

export default function Students() {
  const all = ETUDIANTS
  const [q, setQ] = useState('')
  const [niveau, setNiveau] = useState('Tous')
  const [risque, setRisque] = useState('Tous')
  const [filiere, setFiliere] = useState('Tous')
  const [selected, setSelected] = useState<Etudiant | null>(null)
  const [page, setPage] = useState(1)
  const PAGE = 14

  const filtered = useMemo(
    () =>
      all.filter(e => {
        if (filiere !== 'Tous' && e.filiere !== filiere) return false
        if (niveau !== 'Tous' && e.niveau !== niveau) return false
        if (risque !== 'Tous' && e.risque !== risque) return false
        if (q && !(e.nomComplet.toLowerCase().includes(q.toLowerCase()) || e.matricule.toLowerCase().includes(q.toLowerCase())))
          return false
        return true
      }),
    [all, filiere, niveau, risque, q],
  )
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE))
  const slice = filtered.slice((page - 1) * PAGE, page * PAGE)

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">
            {filtered.length} résultat{filtered.length > 1 ? 's' : ''} · {all.length} au total
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Étudiants</h1>
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">
            <Icon name="upload" size={13} />
            Importer CSV
          </button>
          <button className="btn btn-sm">
            <Icon name="download" size={13} />
            Exporter
          </button>
          <button className="btn btn-sm btn-accent">
            <Icon name="plus" size={13} strokeWidth={2.2} />
            Inscrire
          </button>
        </div>
      </div>

      <div className="card p-3 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[260px]">
          <Icon
            name="search"
            size={14}
            className="absolute left-3 top-1/2 -translate-y-1/2"
            style={{ color: 'var(--text-3)' }}
          />
          <input
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Rechercher par nom ou matricule…"
            className="input pl-9"
          />
        </div>
        <Select value={filiere} onChange={setFiliere} options={['Tous', 'GI', 'GE', 'GC', 'GIND', 'DATA']} label="Filière" />
        <Select value={niveau} onChange={setNiveau} options={['Tous', '1A', '2A', '3A']} label="Niveau" />
        <Select value={risque} onChange={setRisque} options={['Tous', 'faible', 'modere', 'eleve']} label="Risque" />
        <button className="btn btn-sm btn-ghost">
          <Icon name="filter" size={13} />
          Plus de filtres
        </button>
        {(q || filiere !== 'Tous' || niveau !== 'Tous' || risque !== 'Tous') && (
          <button
            onClick={() => {
              setQ('')
              setFiliere('Tous')
              setNiveau('Tous')
              setRisque('Tous')
            }}
            className="btn btn-sm btn-ghost"
            style={{ color: 'var(--accent-700)' }}
          >
            Réinitialiser
          </button>
        )}
      </div>

      <div className="card overflow-hidden">
        <table className="tbl">
          <thead>
            <tr>
              <th style={{ width: 32 }}>
                <input type="checkbox" className="rounded border-stone-300" />
              </th>
              <th>Étudiant</th>
              <th>Matricule</th>
              <th>Filière</th>
              <th>Niveau</th>
              <th>Moy.</th>
              <th>Modules</th>
              <th>Absences</th>
              <th>Score ML</th>
              <th>Statut</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {slice.map(e => (
              <tr key={e.id} onClick={() => setSelected(e)} style={{ cursor: 'pointer' }}>
                <td onClick={ev => ev.stopPropagation()}>
                  <input type="checkbox" className="rounded border-stone-300" />
                </td>
                <td>
                  <div className="flex items-center gap-2.5">
                    <Avatar name={e.nomComplet} />
                    <div className="font-medium">{e.nomComplet}</div>
                  </div>
                </td>
                <td className="font-mono cap">{e.matricule}</td>
                <td>
                  <Pill tone="neutral">{e.filiere}</Pill>
                </td>
                <td className="font-mono">{e.niveau}</td>
                <td className="num font-medium">{e.moyenne.toFixed(2)}</td>
                <td className="num">
                  <span style={{ color: e.modulesValides >= 4 ? 'var(--ok)' : 'var(--warn)' }}>
                    {e.modulesValides}
                  </span>
                  <span style={{ color: 'var(--text-4)' }}>/{e.modulesTotal}</span>
                </td>
                <td className="num">{e.absences}h</td>
                <td>
                  <RiskBar score={e.scoreRisque} width={64} />
                </td>
                <td>
                  <Pill
                    tone={e.statut === 'Régulier' ? 'ok' : e.statut === 'Suivi' ? 'warn' : 'bad'}
                    dot
                  >
                    {e.statut}
                  </Pill>
                </td>
                <td>
                  <button className="btn btn-sm btn-ghost">
                    <Icon name="chevRight" size={13} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div
          className="flex items-center justify-between px-4 py-2.5"
          style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--border)' }}
        >
          <div className="cap">
            Affichage {(page - 1) * PAGE + 1}–{Math.min(page * PAGE, filtered.length)} sur{' '}
            {filtered.length}
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(Math.max(1, page - 1))}
              className="btn btn-sm btn-ghost"
            >
              <Icon name="chevLeft" size={13} />
            </button>
            <span className="px-2 text-[12px]">
              {page} / {pages}
            </span>
            <button
              onClick={() => setPage(Math.min(pages, page + 1))}
              className="btn btn-sm btn-ghost"
            >
              <Icon name="chevRight" size={13} />
            </button>
          </div>
        </div>
      </div>

      {selected && <StudentDrawer student={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

function StudentDrawer({ student, onClose }: { student: Etudiant; onClose: () => void }) {
  const notes = genNotesFor(student)
  const moyenne = student.moyenne
  const riskColor =
    student.risque === 'eleve' ? '#dc2626' : student.risque === 'modere' ? '#f59e0b' : '#16a34a'

  return (
    <div
      className="fixed inset-0 z-40"
      onClick={onClose}
      style={{ background: 'rgba(12,10,9,0.32)' }}
    >
      <div
        onClick={e => e.stopPropagation()}
        className="drawer absolute right-0 top-0 bottom-0 w-[640px] flex flex-col"
        style={{ background: 'var(--surface)' }}
      >
        <div
          className="flex items-center gap-3 p-5"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <Avatar name={student.nomComplet} size={44} ring />
          <div className="flex-1">
            <div className="text-[16px] font-semibold tracking-tight">{student.nomComplet}</div>
            <div className="flex items-center gap-2 mt-1">
              <span className="cap font-mono">{student.matricule}</span>
              <span style={{ color: 'var(--text-4)' }}>•</span>
              <Pill tone="neutral">
                {student.filiere} · {student.niveau}
              </Pill>
              <RiskPill risque={student.risque} />
            </div>
          </div>
          <button onClick={onClose} className="btn btn-sm btn-ghost">
            <Icon name="x" size={14} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto scroll-thin p-5 space-y-5">
          <div className="grid grid-cols-4 gap-3">
            {[
              { l: 'Moyenne', v: moyenne.toFixed(2), s: '/20', t: moyenne >= 10 ? 'ok' : 'bad' },
              { l: 'Modules validés', v: student.modulesValides, s: `/${student.modulesTotal}` },
              { l: 'Absences', v: student.absences, s: 'h', t: student.absences > 20 ? 'warn' : '' },
              {
                l: 'Score ML',
                v: Math.round(student.scoreRisque * 100),
                s: '%',
                t:
                  student.risque === 'eleve'
                    ? 'bad'
                    : student.risque === 'modere'
                    ? 'warn'
                    : 'ok',
              },
            ].map(m => (
              <div
                key={m.l}
                className="p-3 rounded-lg"
                style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
              >
                <div className="cap mb-1">{m.l}</div>
                <div className="flex items-baseline gap-1">
                  <span
                    className="num text-[20px] font-medium"
                    style={{
                      color:
                        m.t === 'ok'
                          ? 'var(--ok)'
                          : m.t === 'warn'
                          ? 'var(--warn)'
                          : m.t === 'bad'
                          ? 'var(--bad)'
                          : 'var(--text)',
                    }}
                  >
                    {m.v}
                  </span>
                  <span className="text-[11.5px]" style={{ color: 'var(--text-3)' }}>
                    {m.s}
                  </span>
                </div>
              </div>
            ))}
          </div>

          <div>
            <SectionHeader
              title="Notes par module"
              subtitle="Semestre 5 — 2025/2026"
              right={
                <button className="btn btn-sm">
                  <Icon name="plus" size={12} />
                  Saisir
                </button>
              }
            />
            <div className="card overflow-hidden">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Module</th>
                    <th>CC</th>
                    <th>TP</th>
                    <th>Examen</th>
                    <th>Finale</th>
                    <th>Statut</th>
                  </tr>
                </thead>
                <tbody>
                  {notes.map(n => (
                    <tr key={n.code}>
                      <td className="font-mono cap">{n.code}</td>
                      <td className="font-medium">{n.nom}</td>
                      <td className="num">{n.cc.toFixed(1)}</td>
                      <td className="num">{n.tp.toFixed(1)}</td>
                      <td className="num">{n.exam.toFixed(1)}</td>
                      <td
                        className="num font-medium"
                        style={{ color: n.finale >= 10 ? 'var(--ok)' : 'var(--bad)' }}
                      >
                        {n.finale.toFixed(2)}
                      </td>
                      <td>
                        {n.valide ? (
                          <Pill tone="ok" dot>Validé</Pill>
                        ) : (
                          <Pill tone="bad" dot>Non validé</Pill>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="card p-4">
            <SectionHeader
              title="Analyse prédictive"
              subtitle="Facteurs de risque détectés par le modèle"
            />
            <div className="flex items-center gap-5">
              <div style={{ width: 130, flexShrink: 0 }}>
                <ChartRadial
                  value={student.scoreRisque}
                  label="Risque"
                  color={riskColor}
                  height={140}
                />
              </div>
              <div className="flex-1 space-y-2">
                {[
                  {
                    f: 'Moyenne basse',
                    w: student.moyenne < 10 ? 0.82 : student.moyenne < 12 ? 0.42 : 0.12,
                    neg: student.moyenne < 12,
                  },
                  {
                    f: 'Absences élevées',
                    w: Math.min(0.95, student.absences / 30),
                    neg: student.absences > 15,
                  },
                  {
                    f: 'Modules non validés',
                    w: 1 - student.modulesValides / student.modulesTotal,
                    neg: student.modulesValides < 4,
                  },
                  { f: 'Tendance trimestrielle', w: 0.34, neg: false },
                ].map(row => (
                  <div key={row.f} className="flex items-center gap-3">
                    <span className="text-[12px]" style={{ minWidth: 160 }}>
                      {row.f}
                    </span>
                    <div
                      className="flex-1 h-1.5 rounded-full"
                      style={{ background: 'var(--surface-3)' }}
                    >
                      <div
                        style={{
                          width: `${row.w * 100}%`,
                          height: '100%',
                          background: row.neg ? 'var(--bad)' : 'var(--ok)',
                          borderRadius: 999,
                        }}
                      />
                    </div>
                    <span
                      className="num text-[11.5px] w-10 text-right"
                      style={{ color: 'var(--text-3)' }}
                    >
                      {Math.round(row.w * 100)}%
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div
          className="p-4 flex items-center justify-between"
          style={{ borderTop: '1px solid var(--border)', background: 'var(--surface-2)' }}
        >
          <button className="btn btn-sm">
            <Icon name="ext" size={12} />
            Ouvrir la fiche complète
          </button>
          <div className="flex items-center gap-2">
            <button className="btn btn-sm">Envoyer à la coordination</button>
            <button className="btn btn-sm btn-accent">Planifier un entretien</button>
          </div>
        </div>
      </div>
    </div>
  )
}
