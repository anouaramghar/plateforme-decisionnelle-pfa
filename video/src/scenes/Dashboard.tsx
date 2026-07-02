import React from 'react';
import {interpolate, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Heading, Icon, pop, fadeIn} from '../ui';
import {LineArea, Donut, Sparkline} from '../charts';

export const DASHBOARD_DURATION = 280;

const NAV = [
  {label: 'Tableau de bord', active: true},
  {label: 'Étudiants', active: false},
  {label: 'Prédictions', active: false},
  {label: 'Alertes', active: false, badge: '37'},
  {label: 'Exports', active: false},
];

const KPIS = [
  {label: 'Étudiants suivis', value: 1248, unit: '', color: T.blue, spark: [1180, 1195, 1210, 1222, 1240, 1248]},
  {label: 'Modules analysés', value: 42, unit: '', color: T.aqua, spark: [36, 38, 38, 40, 41, 42]},
  {label: 'Taux de réussite', value: 78, unit: ' %', color: T.yellow, spark: [71, 74, 72, 76, 78, 78], delta: '+3 pts'},
  {label: 'Alertes actives', value: 37, unit: '', color: T.critical, spark: [52, 48, 45, 41, 39, 37]},
];

const MENTIONS = [
  {label: 'Très bien', value: 14, color: T.blue},
  {label: 'Bien', value: 27, color: T.aqua},
  {label: 'Assez bien', value: 37, color: T.yellow},
  {label: 'Passable', value: 22, color: T.violet},
];

export const Dashboard: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const frameIn = pop(frame, fps, 4, 15);
  const chartP = interpolate(frame, [66, 150], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const donutP = interpolate(frame, [92, 168], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Backdrop glow="blue">
      <Heading
        kicker="Tableau de bord"
        title="Toute la performance étudiante, en direct"
        note="aperçu illustratif"
      />
      <div
        style={{
          position: 'absolute',
          left: 130,
          top: 236,
          width: 1660,
          opacity: frameIn,
          transform: `translateY(${(1 - frameIn) * 60}px) scale(${0.96 + frameIn * 0.04})`,
        }}
      >
        <Card style={{overflow: 'hidden'}}>
          {/* barre de navigateur */}
          <div
            style={{
              height: 54,
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '0 24px',
              borderBottom: `1px solid ${T.border}`,
              background: `${T.surface2}aa`,
            }}
          >
            {[T.critical, T.warning, T.good].map((c) => (
              <div key={c} style={{width: 13, height: 13, borderRadius: 7, background: `${c}bb`}} />
            ))}
            <div
              style={{
                marginLeft: 18,
                padding: '7px 22px',
                borderRadius: 999,
                background: T.page,
                color: T.muted,
                fontSize: 18,
              }}
            >
              plateforme-eniad.local / tableau-de-bord
            </div>
          </div>
          <div style={{display: 'flex', height: 706}}>
            {/* barre latérale */}
            <div
              style={{
                width: 264,
                borderRight: `1px solid ${T.border}`,
                padding: '26px 18px',
                flexShrink: 0,
              }}
            >
              <div style={{display: 'flex', alignItems: 'center', gap: 12, padding: '0 12px', marginBottom: 28}}>
                <div
                  style={{
                    width: 38,
                    height: 38,
                    borderRadius: 10,
                    background: `linear-gradient(135deg, ${T.blue}, ${T.aqua})`,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: '#fff',
                    fontWeight: 800,
                    fontSize: 19,
                  }}
                >
                  E
                </div>
                <div>
                  <div style={{color: T.ink, fontSize: 20, fontWeight: 800}}>ENIAD</div>
                  <div style={{color: T.muted, fontSize: 14}}>Décisionnel</div>
                </div>
              </div>
              {NAV.map((n, i) => {
                const s = pop(frame, fps, 14 + i * 5, 15);
                return (
                  <div
                    key={n.label}
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      padding: '13px 14px',
                      borderRadius: 10,
                      marginBottom: 4,
                      background: n.active ? `${T.blue}26` : 'transparent',
                      color: n.active ? T.ink : T.ink2,
                      fontSize: 19,
                      fontWeight: n.active ? 700 : 400,
                      opacity: s,
                      transform: `translateX(${(1 - s) * -26}px)`,
                    }}
                  >
                    <span>{n.label}</span>
                    {n.badge ? (
                      <span
                        style={{
                          background: T.critical,
                          color: '#fff',
                          borderRadius: 999,
                          fontSize: 14,
                          fontWeight: 700,
                          padding: '2px 9px',
                        }}
                      >
                        {n.badge}
                      </span>
                    ) : null}
                  </div>
                );
              })}
            </div>
            {/* contenu */}
            <div style={{flex: 1, padding: 26}}>
              <div style={{display: 'flex', gap: 20}}>
                {KPIS.map((k, i) => {
                  const s = pop(frame, fps, 22 + i * 7, 14);
                  const n = Math.round(
                    interpolate(frame, [24 + i * 7, 70 + i * 7], [0, k.value], {
                      extrapolateLeft: 'clamp',
                      extrapolateRight: 'clamp',
                    }),
                  );
                  const sparkP = interpolate(frame, [40 + i * 7, 95 + i * 7], [0, 1], {
                    extrapolateLeft: 'clamp',
                    extrapolateRight: 'clamp',
                  });
                  return (
                    <div
                      key={k.label}
                      style={{
                        flex: 1,
                        background: `${T.surface2}88`,
                        border: `1px solid ${T.border}`,
                        borderRadius: 14,
                        padding: '18px 22px',
                        opacity: s,
                        transform: `translateY(${(1 - s) * 30}px)`,
                      }}
                    >
                      <div style={{display: 'flex', alignItems: 'center', gap: 9}}>
                        <div style={{width: 9, height: 9, borderRadius: 5, background: k.color}} />
                        <div style={{color: T.muted, fontSize: 17}}>{k.label}</div>
                      </div>
                      <div style={{display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between'}}>
                        <div style={{color: T.ink, fontSize: 42, fontWeight: 800, marginTop: 6}}>
                          {n.toLocaleString('fr-FR')}
                          <span style={{fontSize: 26, color: T.ink2}}>{k.unit}</span>
                        </div>
                        <Sparkline data={k.spark} color={k.color} progress={sparkP} w={104} h={34} />
                      </div>
                      {k.delta ? (
                        <div style={{color: T.good, fontSize: 16, fontWeight: 700, marginTop: 2}}>
                          ↑ {k.delta} vs S1
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </div>
              <div style={{display: 'flex', gap: 20, marginTop: 20}}>
                <div
                  style={{
                    flex: 1.65,
                    background: `${T.surface2}88`,
                    border: `1px solid ${T.border}`,
                    borderRadius: 14,
                    padding: '20px 26px',
                    opacity: fadeIn(frame, 52, 18),
                  }}
                >
                  <div style={{color: T.ink2, fontSize: 21, marginBottom: 14}}>
                    Taux de réussite par semestre
                  </div>
                  <LineArea
                    id="lg1"
                    data={[71, 74, 72, 76, 78, 81]}
                    labels={['S1 ’23', 'S2 ’23', 'S1 ’24', 'S2 ’24', 'S1 ’25', 'S2 ’25']}
                    w={790}
                    h={356}
                    min={60}
                    max={90}
                    gridValues={[70, 80, 90]}
                    progress={chartP}
                    unit=" %"
                  />
                </div>
                <div
                  style={{
                    flex: 1,
                    background: `${T.surface2}88`,
                    border: `1px solid ${T.border}`,
                    borderRadius: 14,
                    padding: '20px 26px',
                    opacity: fadeIn(frame, 66, 18),
                  }}
                >
                  <div style={{color: T.ink2, fontSize: 21, marginBottom: 18}}>
                    Répartition des mentions — S2
                  </div>
                  <div style={{display: 'flex', alignItems: 'center', gap: 24}}>
                    <div style={{position: 'relative', width: 224, height: 224, flexShrink: 0}}>
                      <Donut segments={MENTIONS} r={96} stroke={29} progress={donutP} />
                      <div
                        style={{
                          position: 'absolute',
                          inset: 0,
                          display: 'flex',
                          flexDirection: 'column',
                          alignItems: 'center',
                          justifyContent: 'center',
                          opacity: donutP > 0.95 ? 1 : 0,
                        }}
                      >
                        <div style={{color: T.ink, fontSize: 40, fontWeight: 800}}>1 248</div>
                        <div style={{color: T.muted, fontSize: 16}}>étudiants</div>
                      </div>
                    </div>
                    <div style={{display: 'flex', flexDirection: 'column', gap: 14}}>
                      {MENTIONS.map((m, i) => (
                        <div
                          key={m.label}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 10,
                            opacity: donutP > (i + 1) / 5 ? 1 : 0.15,
                          }}
                        >
                          <div style={{width: 11, height: 11, borderRadius: 6, background: m.color, flexShrink: 0}} />
                          <div style={{color: T.ink2, fontSize: 17, width: 100, whiteSpace: 'nowrap'}}>{m.label}</div>
                          <div style={{color: T.ink, fontSize: 18, fontWeight: 700, whiteSpace: 'nowrap'}}>{m.value} %</div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </Card>
      </div>
      <div
        style={{
          position: 'absolute',
          bottom: 34,
          left: 130,
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          color: T.muted,
          fontSize: 20,
          opacity: fadeIn(frame, 100, 20),
        }}
      >
        <Icon name="monitor" color={T.muted} size={24} />
        React · TanStack Query · ApexCharts — données rafraîchies automatiquement
      </div>
    </Backdrop>
  );
};
