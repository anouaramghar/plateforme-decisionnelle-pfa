import React from 'react';
import {interpolate, spring, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Scene, SceneTitle} from '../Scene';

export const PREDICTION_DURATION = 270;

const RISK = 0.72; // exemple illustratif

const InputRow: React.FC<{label: string; value: string; delay: number}> = ({
  label,
  value,
  delay,
}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const pop = spring({frame: frame - delay, fps, config: {damping: 14}});
  return (
    <div
      style={{
        background: T.surface,
        border: `1px solid ${T.border}`,
        borderRadius: 14,
        padding: '22px 30px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        transform: `translateX(${(1 - pop) * -80}px)`,
        opacity: pop,
      }}
    >
      <span style={{color: T.ink2, fontSize: 26}}>{label}</span>
      <span style={{color: T.ink, fontSize: 30, fontWeight: 700}}>{value}</span>
    </div>
  );
};

// Jauge semi-circulaire : arc de fond + arc de valeur qui balaie jusqu'au risque.
const Gauge: React.FC = () => {
  const frame = useCurrentFrame();
  const p = interpolate(frame, [60, 130], [0, RISK], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const cx = 260;
  const cy = 260;
  const r = 200;
  const circ = Math.PI * r; // demi-cercle
  const color = p < 0.4 ? T.good : p < 0.6 ? T.warning : T.critical;
  const label = p < 0.4 ? 'Risque faible' : p < 0.6 ? 'Risque modéré' : 'Risque élevé';
  return (
    <div style={{position: 'relative', width: 520, height: 330}}>
      <svg width={520} height={330} viewBox="0 0 520 330">
        <path
          d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
          fill="none"
          stroke={T.grid}
          strokeWidth={30}
          strokeLinecap="round"
        />
        <path
          d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
          fill="none"
          stroke={color}
          strokeWidth={30}
          strokeLinecap="round"
          strokeDasharray={circ}
          strokeDashoffset={circ * (1 - p)}
        />
      </svg>
      <div
        style={{
          position: 'absolute',
          top: 150,
          left: 0,
          right: 0,
          textAlign: 'center',
        }}
      >
        <div style={{color: T.ink, fontSize: 84, fontWeight: 800}}>
          {Math.round(p * 100)}
          <span style={{fontSize: 44, color: T.ink2}}> %</span>
        </div>
        <div
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 10,
            marginTop: 10,
            color: T.ink2,
            fontSize: 28,
          }}
        >
          {/* icône + libellé : la couleur ne porte jamais seule le statut */}
          <svg width={26} height={26} viewBox="0 0 24 24">
            <path
              d="M12 2 L23 21 H1 Z"
              fill="none"
              stroke={color}
              strokeWidth={2.5}
              strokeLinejoin="round"
            />
            <line x1={12} y1={9} x2={12} y2={15} stroke={color} strokeWidth={2.5} />
            <circle cx={12} cy={18} r={1.4} fill={color} />
          </svg>
          {label}
        </div>
      </div>
    </div>
  );
};

export const Prediction: React.FC = () => {
  const frame = useCurrentFrame();
  const arrow = interpolate(frame, [45, 65], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const note = interpolate(frame, [150, 175], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Scene durationInFrames={PREDICTION_DURATION}>
      <SceneTitle kicker="Machine Learning" title="Alerte précoce sur le risque d'échec" />
      <div
        style={{
          position: 'absolute',
          top: 330,
          left: 130,
          width: 520,
          display: 'flex',
          flexDirection: 'column',
          gap: 24,
        }}
      >
        <InputRow label="Moyenne générale" value="9,4 / 20" delay={10} />
        <InputRow label="Taux d'absence" value="18 %" delay={20} />
        <InputRow label="Modules inscrits" value="7" delay={30} />
      </div>

      <div
        style={{
          position: 'absolute',
          top: 460,
          left: 700,
          width: 320,
          textAlign: 'center',
          opacity: arrow,
        }}
      >
        <div style={{color: T.aqua, fontSize: 24, fontWeight: 700}}>
          FastAPI · XGBoost
        </div>
        <svg width={320} height={40} viewBox="0 0 320 40">
          <line x1={10} y1={20} x2={290} y2={20} stroke={T.aqua} strokeWidth={3} />
          <path d="M 290 10 L 310 20 L 290 30 Z" fill={T.aqua} />
        </svg>
        <div style={{color: T.muted, fontSize: 20}}>POST /predict</div>
      </div>

      <div style={{position: 'absolute', top: 300, left: 1130}}>
        <Gauge />
        <div style={{color: T.muted, fontSize: 22, textAlign: 'center', marginTop: 8}}>
          Probabilité d'échec au semestre suivant
        </div>
      </div>

      <div
        style={{
          position: 'absolute',
          bottom: 90,
          left: 130,
          right: 130,
          color: T.ink2,
          fontSize: 25,
          lineHeight: 1.5,
          opacity: note,
          textAlign: 'center',
        }}
      >
        Le modèle apprend sur la période courante pour signaler, dès aujourd'hui,
        les étudiants à accompagner au semestre suivant — un outil d'alerte
        précoce au service des équipes pédagogiques.
      </div>
    </Scene>
  );
};
