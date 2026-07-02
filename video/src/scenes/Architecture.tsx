import React from 'react';
import {interpolate, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Heading, Icon, pop, fadeIn} from '../ui';

export const ARCHI_DURATION = 300;

type P = [number, number];

// Position le long d'une polyligne, t ∈ [0,1].
const along = (pts: P[], t: number): P => {
  const lens: number[] = [];
  let total = 0;
  for (let i = 1; i < pts.length; i++) {
    const l = Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
    lens.push(l);
    total += l;
  }
  let d = t * total;
  for (let i = 0; i < lens.length; i++) {
    if (d <= lens[i]) {
      const k = d / lens[i];
      return [
        pts[i][0] + (pts[i + 1][0] - pts[i][0]) * k,
        pts[i][1] + (pts[i + 1][1] - pts[i][1]) * k,
      ];
    }
    d -= lens[i];
  }
  return pts[pts.length - 1];
};

const Packet: React.FC<{path: P[]; delay: number; period: number; color: string}> = ({
  path,
  delay,
  period,
  color,
}) => {
  const frame = useCurrentFrame();
  if (frame < delay) {
    return null;
  }
  const t = ((frame - delay) % period) / period;
  const [x, y] = along(path, t);
  const o = Math.min(1, Math.min(t, 1 - t) * 8);
  return (
    <>
      <circle cx={x} cy={y} r={7} fill={color} opacity={o} />
      <circle cx={x} cy={y} r={13} fill={color} opacity={o * 0.25} />
    </>
  );
};

const Zone: React.FC<{
  x: number;
  y: number;
  w: number;
  h: number;
  label: string;
  color: string;
  delay: number;
}> = ({x, y, w, h, label, color, delay}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const s = pop(frame, fps, delay, 16);
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: w,
        height: h,
        borderRadius: 22,
        border: `1.5px dashed ${color}77`,
        background: `${color}09`,
        opacity: s,
        transform: `scale(${0.96 + s * 0.04})`,
      }}
    >
      <div
        style={{
          position: 'absolute',
          top: -17,
          left: 26,
          padding: '5px 16px',
          borderRadius: 999,
          background: T.page,
          border: `1px solid ${color}88`,
          color,
          fontSize: 18,
          fontWeight: 700,
          letterSpacing: 1,
        }}
      >
        {label}
      </div>
    </div>
  );
};

const Node: React.FC<{
  x: number;
  y: number;
  w: number;
  icon: React.ComponentProps<typeof Icon>['name'];
  title: string;
  sub: string;
  accent: string;
  delay: number;
}> = ({x, y, w, icon, title, sub, accent, delay}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const s = pop(frame, fps, delay, 13);
  return (
    <Card
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: w,
        padding: '16px 22px',
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        opacity: s,
        transform: `translateY(${(1 - s) * 26}px)`,
        boxSizing: 'border-box',
      }}
    >
      <div
        style={{
          width: 52,
          height: 52,
          borderRadius: 13,
          background: `${accent}1c`,
          border: `1px solid ${accent}55`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        <Icon name={icon} color={accent} size={28} />
      </div>
      <div>
        <div style={{color: T.ink, fontSize: 24, fontWeight: 800}}>{title}</div>
        <div style={{color: T.muted, fontSize: 17, marginTop: 2}}>{sub}</div>
      </div>
    </Card>
  );
};

// Chemins des liens (coordonnées écran).
const L_IN: P[] = [[960, 208], [960, 292]];
const L_FRONT: P[] = [[900, 360], [520, 424]];
const L_BACK: P[] = [[960, 360], [960, 424]];
const L_COPILOT: P[] = [[1020, 360], [1400, 424]];
const L_ML: P[] = [[880, 512], [520, 668]];
const L_DB: P[] = [[1040, 512], [1400, 668]];
const L_NIM: P[] = [[1520, 424], [1660, 330]];

export const Architecture: React.FC = () => {
  const frame = useCurrentFrame();
  const drawn = interpolate(frame, [30, 75], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const line = (pts: P[], dashed = false) => {
    const d = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x},${y}`).join(' ');
    let len = 0;
    for (let i = 1; i < pts.length; i++) {
      len += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
    }
    return (
      <path
        d={d}
        fill="none"
        stroke={T.muted}
        strokeWidth={2.5}
        strokeDasharray={dashed ? '7 8' : len}
        strokeDashoffset={dashed ? 0 : len * (1 - drawn)}
        opacity={dashed ? drawn * 0.8 : 0.8}
      />
    );
  };

  return (
    <Backdrop glow="aqua">
      <Heading
        kicker="Architecture"
        title="Micro-services Docker, données cloisonnées"
      />
      <svg style={{position: 'absolute', inset: 0}} width={1920} height={1080} viewBox="0 0 1920 1080">
        {line(L_IN)}
        {line(L_FRONT)}
        {line(L_BACK)}
        {line(L_COPILOT)}
        {line(L_ML)}
        {line(L_DB)}
        {line(L_NIM, true)}
        <Packet path={L_IN} delay={70} period={55} color={T.blue} />
        <Packet path={L_FRONT} delay={95} period={70} color={T.blue} />
        <Packet path={L_BACK} delay={85} period={60} color={T.blue} />
        <Packet path={L_ML} delay={110} period={75} color={T.aqua} />
        <Packet path={L_ML} delay={148} period={75} color={T.aqua} />
        <Packet path={L_DB} delay={100} period={75} color={T.aqua} />
        <Packet path={L_DB} delay={138} period={75} color={T.aqua} />
        <Packet path={L_COPILOT} delay={120} period={80} color={T.violet} />
        <Packet path={L_NIM} delay={140} period={80} color={T.violet} />
      </svg>

      {/* Internet, en amont */}
      <div
        style={{
          position: 'absolute',
          left: 850,
          top: 168,
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          color: T.ink2,
          fontSize: 21,
          opacity: fadeIn(frame, 8, 16),
        }}
      >
        <Icon name="globe" color={T.ink2} size={26} />
        Internet — utilisateurs
      </div>

      <Zone x={130} y={252} w={1450} h={330} label="pfa_public" color={T.blue} delay={10} />
      <Zone x={130} y={636} w={700} h={210} label="pfa_internal_ml · internal: true" color={T.aqua} delay={22} />
      <Zone x={880} y={636} w={700} h={210} label="pfa_internal_db · internal: true" color={T.aqua} delay={28} />

      <Node x={790} y={292} w={340} icon="globe" title="nginx" sub="proxy unique · 20 r/s" accent={T.yellow} delay={16} />
      <Node x={190} y={424} w={430} icon="monitor" title="Frontend" sub="React · Vite · ApexCharts" accent={T.blue} delay={40} />
      <Node x={745} y={424} w={430} icon="api" title="Backend API" sub="ASP.NET Core 8 · EF Core · JWT" accent={T.blue} delay={48} />
      <Node x={1300} y={424} w={430} icon="sparkle" title="Copilot" sub="assistant IA — optionnel" accent={T.violet} delay={56} />
      <Node x={220} y={692} w={520} icon="brain" title="Service ML" sub="FastAPI · scikit-learn · XGBoost" accent={T.aqua} delay={66} />
      <Node x={970} y={692} w={520} icon="db" title="SQL Server" sub="PFA_DB (OLTP) · PFA_DW (étoile)" accent={T.aqua} delay={74} />

      {/* LLM externe */}
      <div
        style={{
          position: 'absolute',
          left: 1600,
          top: 276,
          opacity: fadeIn(frame, 60, 18),
        }}
      >
        <Card style={{padding: '12px 20px', display: 'flex', alignItems: 'center', gap: 12}}>
          <Icon name="cloud" color={T.violet} size={26} />
          <div>
            <div style={{color: T.ink, fontSize: 19, fontWeight: 700}}>NVIDIA NIM</div>
            <div style={{color: T.muted, fontSize: 15}}>LLM externe</div>
          </div>
        </Card>
      </div>

      <div
        style={{
          position: 'absolute',
          bottom: 84,
          left: 130,
          right: 130,
          textAlign: 'center',
          color: T.ink2,
          fontSize: 26,
          lineHeight: 1.5,
          opacity: fadeIn(frame, 150, 22),
        }}
      >
        La base de données et le service ML sont{' '}
        <span style={{color: T.ink, fontWeight: 800}}>injoignables depuis Internet</span>
        {' '}— et le backend se connecte avec un compte SQL à privilèges minimaux.
      </div>
    </Backdrop>
  );
};
