import { Icon } from '../components/ui/Icon'
import { Avatar } from '../components/ui/Avatar'
import { Pill } from '../components/ui/Pill'
import { KpiCard } from '../components/ui/KpiCard'
import { RiskBar } from '../components/ui/RiskBar'
import { SectionHeader } from '../components/ui/SectionHeader'
import { Select } from '../components/ui/Select'
import { ChartScatter } from '../components/charts'
import { ETUDIANTS, predictionRuns, topARisque } from '../data/mock'

export default function Predictions() {
  const runs = predictionRuns()
  const top = topARisque(10)
  const points = ETUDIANTS.map(e => ({ x: e.absences, y: e.moyenne, r: e.risque }))

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between">
        <div>
          <div className="cap mb-1">
            Modèle XGBoost v1.4 · AUC 0.87 · ré-entraîné il y a 1 jour
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight">Prédictions ML</h1>
        </div>
        <div className="flex items-center gap-2">
          <button className="btn btn-sm">
            <Icon name="refresh" size={13} />
            Ré-entraîner
          </button>
          <button className="btn btn-sm btn-accent">
            <Icon name="spark" size={13} />
            Lancer une prédiction batch
          </button>
        </div>
      </div>

      <div
        className="card p-5"
        style={{
          background: 'linear-gradient(135deg, var(--surface) 0%, var(--accent-50) 100%)',
          borderColor: 'var(--accent-200)',
        }}
      >
        <div className="flex items-center gap-5">
          <div
            className="w-12 h-12 rounded-xl flex items-center justify-center"
            style={{
              background: 'linear-gradient(135deg, var(--accent-500), var(--accent-700))',
              color: '#fff',
              boxShadow:
                'inset 0 1px 0 rgba(255,255,255,.2), 0 8px 24px -8px rgba(249,115,22,.5)',
            }}
          >
            <Icon name="brain" size={22} />
          </div>
          <div className="flex-1">
            <div className="text-[14px] font-semibold tracking-tight">
              Évaluer le risque d'une cohorte
            </div>
            <div className="text-[12px] mt-1" style={{ color: 'var(--text-2)' }}>
              Sélectionnez la filière puis le niveau. Le service ML calcule un score 0–1 et
              déclenche les alertes pour les étudiants au-dessus du seuil.
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Select
              value="GI"
              onChange={() => {}}
              options={['GI', 'GE', 'GC', 'GIND', 'DATA', 'TOUS']}
              label="Filière"
            />
            <Select
              value="3A"
              onChange={() => {}}
              options={['Toutes', '1A', '2A', '3A']}
              label="Niveau"
            />
            <button className="btn btn-accent">Lancer · 142 étu.</button>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-4 gap-3">
        <KpiCard label="Étudiants évalués" value="452" hint="Dernière semaine" />
        <KpiCard label="Risque élevé" value="51" suffix="(11%)" delta={8} deltaTone="bad" />
        <KpiCard label="Risque modéré" value="98" suffix="(22%)" delta={-3} deltaTone="ok" />
        <KpiCard
          label="AUC du modèle"
          value="0.87"
          delta={2.4}
          deltaTone="ok"
          hint="vs version précédente"
        />
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="card p-4 col-span-2">
          <SectionHeader
            title="Cartographie du risque"
            subtitle="Moyenne /20 vs heures d'absence — chaque point = 1 étudiant"
            right={
              <>
                <Pill tone="ok" dot>Faible</Pill>
                <Pill tone="warn" dot>Modéré</Pill>
                <Pill tone="bad" dot>Élevé</Pill>
              </>
            }
          />
          <ChartScatter points={points} height={300} />
        </div>
        <div className="card p-4">
          <SectionHeader title="Top à risque" subtitle="Score le plus élevé" />
          <div className="space-y-2.5">
            {top.map((e, i) => (
              <div key={e.id} className="flex items-center gap-2.5">
                <span className="num text-[11px] w-5" style={{ color: 'var(--text-4)' }}>
                  {String(i + 1).padStart(2, '0')}
                </span>
                <Avatar name={e.nomComplet} size={26} />
                <div className="flex-1 min-w-0">
                  <div className="text-[12.5px] font-medium truncate">{e.nomComplet}</div>
                  <div className="cap font-mono">
                    {e.filiere} · {e.niveau}
                  </div>
                </div>
                <RiskBar score={e.scoreRisque} width={50} />
              </div>
            ))}
          </div>
          <button
            className="btn btn-sm w-full mt-3"
            style={{ background: 'var(--surface-2)', border: 'none' }}
          >
            Générer le rapport · {top.length} étudiants
          </button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <div
          className="px-4 py-3 flex items-center justify-between"
          style={{ borderBottom: '1px solid var(--border)' }}
        >
          <div>
            <div className="text-[13px] font-semibold">Historique des prédictions</div>
            <div className="cap mt-0.5">
              Toutes les exécutions de batch — les 30 derniers jours
            </div>
          </div>
          <button className="btn btn-sm btn-ghost">
            Voir tout <Icon name="arrowRight" size={12} />
          </button>
        </div>
        <table className="tbl">
          <thead>
            <tr>
              <th>ID</th>
              <th>Date</th>
              <th>Filière</th>
              <th>Étudiants</th>
              <th>Risque élevé</th>
              <th>Modèle</th>
              <th>AUC</th>
              <th>Statut</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {runs.map(r => (
              <tr key={r.id}>
                <td className="font-mono cap">{r.id}</td>
                <td>{r.date}</td>
                <td>
                  <Pill tone="neutral">{r.filiere}</Pill>
                </td>
                <td className="num">{r.etudiants}</td>
                <td className="num font-medium" style={{ color: 'var(--bad)' }}>
                  {r.risqueEleve}
                </td>
                <td className="font-mono cap">{r.modele}</td>
                <td className="num">{r.auc}</td>
                <td>
                  {r.statut === 'OK' ? (
                    <Pill tone="ok" dot>OK</Pill>
                  ) : (
                    <Pill tone="warn" dot>Dégradé</Pill>
                  )}
                </td>
                <td>
                  <button className="btn btn-sm btn-ghost">
                    <Icon name="more" size={14} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
