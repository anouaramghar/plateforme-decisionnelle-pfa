import React from 'react';
import {
  interpolate,
  spring,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import {T} from '../theme';
import {Scene, SceneTitle} from '../Scene';

export const KPIS_DURATION = 270;

const TILES = [
  {label: 'Étudiants suivis', value: 1248, suffix: '', color: T.blue},
  {label: 'Modules analysés', value: 42, suffix: '', color: T.aqua},
  {label: 'Taux de réussite', value: 78, suffix: '%', color: T.yellow},
  {label: 'Alertes actives', value: 37, suffix: '', color: T.critical},
];

const BARS = [
  {label: 'Algo', v: 12.4},
  {label: 'BD', v: 13.1},
  {label: 'Web', v: 11.2},
  {label: 'IA', v: 14.6},
  {label: 'Réseaux', v: 10.8},
  {label: 'Maths', v: 9.7},
];

const StatTile: React.FC<{
  label: string;
  value: number;
  suffix: string;
  color: string;
  delay: number;
}> = ({label, value, suffix, color, delay}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const pop = spring({frame: frame - delay, fps, config: {damping: 13}});
  const n = Math.round(
    interpolate(frame - delay, [0, 45], [0, value], {
      extrapolateLeft: 'clamp',
      extrapolateRight: 'clamp',
    }),
  );
  return (
    <div
      style={{
        background: T.surface,
        border: `1px solid ${T.border}`,
        borderRadius: 18,
        padding: '34px 40px',
        width: 330,
        transform: `scale(${pop})`,
      }}
    >
      <div style={{display: 'flex', alignItems: 'center', gap: 12}}>
        <div style={{width: 12, height: 12, borderRadius: 6, background: color}} />
        <div style={{color: T.ink2, fontSize: 24}}>{label}</div>
      </div>
      <div style={{color: T.ink, fontSize: 64, fontWeight: 800, marginTop: 14}}>
        {n.toLocaleString('fr-FR')}
        {suffix}
      </div>
    </div>
  );
};

export const Kpis: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const chartIn = interpolate(frame, [60, 80], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Scene durationInFrames={KPIS_DURATION}>
      <SceneTitle kicker="Tableau de bord" title="Les indicateurs clés, en un coup d'œil" />
      <div
        style={{
          position: 'absolute',
          top: 290,
          left: 110,
          right: 110,
          display: 'flex',
          gap: 30,
        }}
      >
        {TILES.map((t, i) => (
          <StatTile key={t.label} {...t} delay={10 + i * 8} />
        ))}
      </div>

      {/* Moyenne par module — barres fines, extrémités arrondies 4px, écart 2px+ */}
      <div
        style={{
          position: 'absolute',
          top: 560,
          left: 110,
          right: 110,
          background: T.surface,
          border: `1px solid ${T.border}`,
          borderRadius: 18,
          padding: '30px 44px 26px',
          opacity: chartIn,
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'baseline',
            marginBottom: 22,
          }}
        >
          <div style={{color: T.ink2, fontSize: 24}}>
            Moyenne générale par module — S1
          </div>
          <div style={{color: T.muted, fontSize: 20, display: 'flex', alignItems: 'center', gap: 10}}>
            <span style={{width: 10, height: 10, borderRadius: 5, background: T.critical, display: 'inline-block'}} />
            sous le seuil de réussite (10 / 20)
          </div>
        </div>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-end',
            height: 230,
            borderBottom: `2px solid ${T.grid}`,
          }}
        >
          {BARS.map((b, i) => {
            const grow = spring({
              frame: frame - (70 + i * 5),
              fps,
              config: {damping: 15},
            });
            const h = (b.v / 20) * 190 * grow;
            const failing = b.v < 10;
            return (
              <div
                key={b.label}
                style={{
                  width: 90,
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'flex-end',
                }}
              >
                <div
                  style={{
                    color: failing ? T.critical : T.ink,
                    fontSize: 22,
                    fontWeight: 700,
                    marginBottom: 8,
                    opacity: grow,
                  }}
                >
                  {b.v.toLocaleString('fr-FR')}
                </div>
                <div
                  style={{
                    width: 46,
                    height: Math.max(h, 2),
                    background: failing ? T.critical : T.blue,
                    borderRadius: '4px 4px 0 0',
                  }}
                />
              </div>
            );
          })}
        </div>
        <div style={{display: 'flex', justifyContent: 'space-between', marginTop: 10}}>
          {BARS.map((b) => (
            <div
              key={b.label}
              style={{width: 90, textAlign: 'center', color: T.muted, fontSize: 20}}
            >
              {b.label}
            </div>
          ))}
        </div>
      </div>
    </Scene>
  );
};
