import React from 'react';
import {interpolate, spring, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Scene, SceneTitle} from '../Scene';

export const ARCHI_DURATION = 300;

const Node: React.FC<{
  x: number;
  y: number;
  w: number;
  title: string;
  sub: string;
  accent: string;
  delay: number;
  badge?: string;
}> = ({x, y, w, title, sub, accent, delay, badge}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const pop = spring({frame: frame - delay, fps, config: {damping: 13}});
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: w,
        background: T.surface,
        border: `1px solid ${T.border}`,
        borderTop: `4px solid ${accent}`,
        borderRadius: 14,
        padding: '20px 26px',
        transform: `scale(${pop})`,
        boxSizing: 'border-box',
      }}
    >
      <div style={{color: T.ink, fontSize: 28, fontWeight: 700}}>{title}</div>
      <div style={{color: T.ink2, fontSize: 20, marginTop: 6}}>{sub}</div>
      {badge ? (
        <div
          style={{
            display: 'inline-block',
            marginTop: 10,
            padding: '4px 12px',
            borderRadius: 999,
            border: `1px solid ${T.border}`,
            color: T.muted,
            fontSize: 16,
          }}
        >
          {badge}
        </div>
      ) : null}
    </div>
  );
};

// Lien animé (trait qui se dessine) entre deux points.
const Link: React.FC<{
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  delay: number;
}> = ({x1, y1, x2, y2, delay}) => {
  const frame = useCurrentFrame();
  const len = Math.hypot(x2 - x1, y2 - y1);
  const drawn = interpolate(frame - delay, [0, 25], [len, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  return (
    <line
      x1={x1}
      y1={y1}
      x2={x2}
      y2={y2}
      stroke={T.muted}
      strokeWidth={3}
      strokeDasharray={len}
      strokeDashoffset={drawn}
    />
  );
};

export const Architecture: React.FC = () => {
  const frame = useCurrentFrame();
  const note = interpolate(frame, [150, 175], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Scene durationInFrames={ARCHI_DURATION}>
      <SceneTitle kicker="Architecture" title="Micro-services conteneurisés, réseaux isolés" />
      <svg
        style={{position: 'absolute', inset: 0}}
        width={1920}
        height={1080}
        viewBox="0 0 1920 1080"
      >
        {/* nginx → frontend / backend / copilot */}
        <Link x1={960} y1={415} x2={430} y2={555} delay={45} />
        <Link x1={960} y1={415} x2={960} y2={555} delay={55} />
        <Link x1={960} y1={415} x2={1490} y2={555} delay={65} />
        {/* backend → ml, db */}
        <Link x1={905} y1={690} x2={680} y2={830} delay={85} />
        <Link x1={1015} y1={690} x2={1240} y2={830} delay={95} />
      </svg>

      <Node
        x={760}
        y={290}
        w={400}
        title="nginx"
        sub="Point d'entrée unique — port 80"
        accent={T.yellow}
        delay={15}
        badge="pfa_public · rate-limit 20 r/s"
      />
      <Node
        x={210}
        y={555}
        w={440}
        title="Frontend"
        sub="React · Vite · TanStack Query · ApexCharts"
        accent={T.blue}
        delay={50}
      />
      <Node
        x={740}
        y={555}
        w={440}
        title="Backend API"
        sub="ASP.NET Core 8 · EF Core · JWT"
        accent={T.blue}
        delay={60}
      />
      <Node
        x={1270}
        y={555}
        w={440}
        title="Copilot Runtime"
        sub="Assistant IA optionnel — jamais bloquant"
        accent={T.aqua}
        delay={70}
      />
      <Node
        x={420}
        y={830}
        w={480}
        title="Service ML"
        sub="FastAPI · scikit-learn · XGBoost"
        accent={T.aqua}
        delay={95}
        badge="pfa_internal_ml · internal: true"
      />
      <Node
        x={1000}
        y={830}
        w={480}
        title="SQL Server"
        sub="PFA_DB (OLTP) + PFA_DW (étoile) · ETL MERGE"
        accent={T.aqua}
        delay={105}
        badge="pfa_internal_db · internal: true"
      />

      <div
        style={{
          position: 'absolute',
          right: 120,
          top: 320,
          width: 430,
          color: T.ink2,
          fontSize: 24,
          lineHeight: 1.5,
          opacity: note,
        }}
      >
        La base de données et le service ML sont{' '}
        <span style={{color: T.ink, fontWeight: 700}}>
          inaccessibles depuis Internet
        </span>{' '}
        — trois réseaux Docker isolés par conception.
      </div>
    </Scene>
  );
};
