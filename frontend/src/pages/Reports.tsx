import { useState } from 'react'
import { Icon, type IconName } from '../components/ui/Icon'
import { Pill } from '../components/ui/Pill'
import { Field } from '../components/ui/Field'
import { SectionHeader } from '../components/ui/SectionHeader'
import { ChartBars } from '../components/charts'
import { notesByFiliere, RAPPORTS } from '../data/mock'

interface Template {
  id: string
  title: string
  desc: string
  icon: IconName
  format: 'PDF' | 'XLSX' | 'CSV'
}

const TEMPLATES: Template[] = [
  { id: 'perf-globale', title: 'Performance globale',  desc: 'KPIs + graphiques par filière',     icon: 'trend', format: 'PDF' },
  { id: 'risque',       title: 'Étudiants à risque',   desc: 'Liste + scores ML + recommandations', icon: 'brain', format: 'PDF' },
  { id: 'notes',        title: 'Notes par module',     desc: 'Détail CC/TP/Examen/Finale',       icon: 'doc',   format: 'XLSX' },
  { id: 'absences',     title: 'Absences hebdomadaires', desc: 'Suivi cumulatif par classe',     icon: 'clock', format: 'CSV' },
  { id: 'alertes',      title: 'Journal des alertes',  desc: 'Toutes les alertes du semestre',   icon: 'bell',  format: 'XLSX' },
  { id: 'bilan-ml',     title: 'Bilan modèle ML',      desc: 'Métriques + drift + features',     icon: 'spark', format: 'PDF' },
]

export default function Reports() {
  const [picked, setPicked] = useState('perf-globale')

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">Rapports automatiques · Conseil pédagogique</div>
          <h1 className="text-[22px] font-semibold tracking-tight">Rapports</h1>
        </div>
        <button className="btn btn-sm btn-accent">
          <Icon name="plus" size={13} strokeWidth={2.2} />
          Nouveau rapport
        </button>
      </div>

      <div className="grid grid-cols-3 gap-3">
        {TEMPLATES.map(t => (
          <button
            key={t.id}
            onClick={() => setPicked(t.id)}
            className="card p-4 text-left transition"
            style={{
              borderColor: picked === t.id ? 'var(--accent-500)' : 'var(--border)',
              boxShadow:
                picked === t.id
                  ? '0 0 0 3px color-mix(in oklch, var(--accent-500) 14%, transparent)'
                  : 'none',
            }}
          >
            <div className="flex items-center gap-3 mb-2">
              <div
                className="w-9 h-9 rounded-lg flex items-center justify-center"
                style={{
                  background: picked === t.id ? 'var(--accent-100)' : 'var(--surface-2)',
                  color: picked === t.id ? 'var(--accent-700)' : 'var(--text-2)',
                }}
              >
                <Icon name={t.icon} size={16} />
              </div>
              <Pill tone={t.format === 'PDF' ? 'bad' : t.format === 'XLSX' ? 'ok' : 'info'}>
                {t.format}
              </Pill>
            </div>
            <div className="text-[13px] font-semibold tracking-tight">{t.title}</div>
            <div className="cap mt-1">{t.desc}</div>
          </button>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4 col-span-1">
          <SectionHeader title="Paramètres" subtitle="Configurez le rapport avant export" />
          <div className="space-y-3">
            <Field label="Période">
              <select className="input">
                <option>Semestre 2 · 2025/2026</option>
                <option>Semestre 1 · 2025/2026</option>
                <option>Année 2024/2025</option>
              </select>
            </Field>
            <Field label="Filière(s)">
              <select className="input">
                <option>Toutes</option>
                <option>Génie Informatique</option>
                <option>Génie Électrique</option>
              </select>
            </Field>
            <Field label="Niveaux">
              <div className="flex gap-1.5">
                {['1A', '2A', '3A'].map((n, i) => (
                  <label
                    key={n}
                    className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-md cursor-pointer text-[12px]"
                    style={{
                      border: `1px solid ${i < 2 ? 'var(--accent-500)' : 'var(--border-2)'}`,
                      background: i < 2 ? 'var(--accent-50)' : 'var(--surface)',
                      color: i < 2 ? 'var(--accent-700)' : 'var(--text-2)',
                      fontWeight: i < 2 ? 500 : 400,
                    }}
                  >
                    <input type="checkbox" defaultChecked={i < 2} className="rounded" />
                    {n}
                  </label>
                ))}
              </div>
            </Field>
            <Field label="Inclure">
              <div className="space-y-1.5">
                {[
                  'KPIs principaux',
                  'Graphiques (notes, absences, risque)',
                  'Liste des étudiants',
                  'Recommandations ML',
                  'Annexes méthodologiques',
                ].map((opt, i) => (
                  <label key={opt} className="flex items-center gap-2 text-[12.5px]">
                    <input type="checkbox" defaultChecked={i < 4} className="rounded" />
                    {opt}
                  </label>
                ))}
              </div>
            </Field>
            <Field label="Format de sortie">
              <div className="flex gap-1.5">
                {['PDF', 'XLSX', 'CSV'].map((f, i) => (
                  <button
                    key={f}
                    className="flex-1 px-3 py-1.5 rounded-md text-[12px]"
                    style={{
                      border: `1px solid ${i === 0 ? 'var(--accent-500)' : 'var(--border-2)'}`,
                      background: i === 0 ? 'var(--accent-50)' : 'var(--surface)',
                      color: i === 0 ? 'var(--accent-700)' : 'var(--text-2)',
                      fontWeight: i === 0 ? 500 : 400,
                    }}
                  >
                    {f}
                  </button>
                ))}
              </div>
            </Field>
            <button className="btn btn-accent btn-lg w-full mt-2">
              <Icon name="download" size={14} />
              Générer le rapport
            </button>
          </div>
        </div>

        {/* preview */}
        <div className="col-span-2">
          <SectionHeader title="Aperçu" subtitle="Couverture du document — page 1 / 18" />
          <div
            className="card p-0 overflow-hidden"
            style={{ aspectRatio: '1/1.414', background: 'var(--surface)' }}
          >
            <div
              className="h-full flex flex-col p-8"
              style={{
                background:
                  'linear-gradient(180deg, var(--surface) 0%, var(--surface) 60%, var(--surface-2) 100%)',
              }}
            >
              <div className="flex items-center justify-between mb-6">
                <div className="flex items-center gap-2">
                  <div
                    style={{
                      width: 24,
                      height: 24,
                      borderRadius: 6,
                      background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: '#fff',
                      fontWeight: 700,
                      fontSize: 11,
                    }}
                  >
                    E
                  </div>
                  <span className="text-[11px] font-semibold tracking-wider">
                    ENIAD · BERKANE
                  </span>
                </div>
                <span className="cap font-mono">RP-105 · 25 avr. 2026</span>
              </div>
              <div
                className="text-[10px] uppercase tracking-[0.18em] mb-3"
                style={{ color: 'var(--accent-700)', fontWeight: 500 }}
              >
                Rapport semestriel
              </div>
              <h2
                className="font-serif text-[36px] leading-[1.05]"
                style={{ letterSpacing: '-0.015em' }}
              >
                Performance globale
                <br />
                Semestre 2 · 2025/2026
              </h2>
              <p
                className="mt-3 text-[12px] max-w-[420px]"
                style={{ color: 'var(--text-2)' }}
              >
                Synthèse pour le conseil pédagogique du 5 mai 2026 — pilotée par la plateforme
                décisionnelle ENIAD.
              </p>

              <div className="mt-6 grid grid-cols-4 gap-3">
                {[
                  { l: 'Étudiants', v: '452' },
                  { l: 'Moyenne',   v: '12.4' },
                  { l: 'Réussite',  v: '74%' },
                  { l: 'À risque',  v: '51' },
                ].map(s => (
                  <div
                    key={s.l}
                    className="p-3 rounded-md"
                    style={{ background: 'var(--surface-2)', border: '1px solid var(--border)' }}
                  >
                    <div className="cap">{s.l}</div>
                    <div className="num text-[20px] font-medium mt-0.5">{s.v}</div>
                  </div>
                ))}
              </div>

              <div
                className="flex-1 mt-5 rounded-md p-4"
                style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
              >
                <div className="text-[11px] font-semibold mb-2">Moyennes par filière</div>
                <ChartBars data={notesByFiliere()} height={160} />
              </div>

              <div
                className="mt-auto flex items-center justify-between pt-3"
                style={{ borderTop: '1px solid var(--border)', color: 'var(--text-3)', fontSize: 10 }}
              >
                <span>Préparé par AMGHAR Anouar · Encadré par Prof. LHIADI</span>
                <span className="font-mono">Page 1 / 18</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div
          className="px-4 py-3 flex items-center justify-between"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div>
            <div className="text-[13px] font-semibold">Rapports récents</div>
            <div className="cap mt-0.5">Générés au cours des 7 derniers jours</div>
          </div>
        </div>
        <table className="tbl">
          <thead>
            <tr>
              <th>ID</th>
              <th>Titre</th>
              <th>Filière</th>
              <th>Format</th>
              <th>Taille</th>
              <th>Auteur</th>
              <th>Date</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {RAPPORTS.map(r => (
              <tr key={r.id}>
                <td className="font-mono cap">{r.id}</td>
                <td className="font-medium">{r.titre}</td>
                <td>
                  <Pill tone="neutral">{r.filiere}</Pill>
                </td>
                <td>
                  <Pill tone={r.format === 'PDF' ? 'bad' : r.format === 'XLSX' ? 'ok' : 'info'}>
                    {r.format}
                  </Pill>
                </td>
                <td className="num cap">{r.taille}</td>
                <td>{r.auteur}</td>
                <td className="cap">{r.date}</td>
                <td>
                  <div className="flex items-center gap-1">
                    <button className="btn btn-sm btn-ghost">
                      <Icon name="eye" size={13} />
                    </button>
                    <button className="btn btn-sm btn-ghost">
                      <Icon name="download" size={13} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
