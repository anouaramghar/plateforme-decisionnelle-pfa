import React from 'react';
import {
  AbsoluteFill,
  interpolate,
  spring,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import {T} from './theme';

export const pop = (
  frame: number,
  fps: number,
  delay: number,
  damping = 13,
): number => spring({frame: frame - delay, fps, config: {damping}});

export const fadeIn = (frame: number, from: number, dur = 20): number =>
  interpolate(frame, [from, from + dur], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

const GLOWS: Record<string, [string, string]> = {
  blue: [T.blue, T.aqua],
  aqua: [T.aqua, T.blue],
  violet: [T.violet, T.blue],
  yellow: [T.yellow, T.blue],
};

// Fond commun : lueurs dérivantes + grille estompée + vignette.
export const Backdrop: React.FC<{
  glow?: keyof typeof GLOWS;
  children?: React.ReactNode;
}> = ({glow = 'blue', children}) => {
  const frame = useCurrentFrame();
  const [c1, c2] = GLOWS[glow];
  const dx = Math.sin(frame / 70) * 40;
  const dy = Math.cos(frame / 90) * 30;
  return (
    <AbsoluteFill style={{backgroundColor: T.page, fontFamily: T.font}}>
      <div
        style={{
          position: 'absolute',
          width: 900,
          height: 900,
          borderRadius: '50%',
          background: c1,
          opacity: 0.13,
          filter: 'blur(160px)',
          left: -220 + dx,
          top: -260 + dy,
        }}
      />
      <div
        style={{
          position: 'absolute',
          width: 800,
          height: 800,
          borderRadius: '50%',
          background: c2,
          opacity: 0.1,
          filter: 'blur(160px)',
          right: -200 - dx,
          bottom: -260 - dy,
        }}
      />
      <AbsoluteFill
        style={{
          backgroundImage: `linear-gradient(${T.grid}44 1px, transparent 1px),
            linear-gradient(90deg, ${T.grid}44 1px, transparent 1px)`,
          backgroundSize: '120px 120px',
          maskImage:
            'radial-gradient(ellipse at center, black 25%, transparent 80%)',
          WebkitMaskImage:
            'radial-gradient(ellipse at center, black 25%, transparent 80%)',
        }}
      />
      {children}
      <AbsoluteFill
        style={{
          background:
            'radial-gradient(ellipse at center, transparent 58%, rgba(0,0,0,0.45) 100%)',
          pointerEvents: 'none',
        }}
      />
    </AbsoluteFill>
  );
};

// Titre de scène : kicker + titre, avec note facultative à droite.
export const Heading: React.FC<{kicker: string; title: string; note?: string}> = ({
  kicker,
  title,
  note,
}) => {
  const frame = useCurrentFrame();
  const o = fadeIn(frame, 0, 18);
  const y = interpolate(frame, [0, 18], [22, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  return (
    <div
      style={{
        position: 'absolute',
        top: 68,
        left: 110,
        right: 110,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-end',
        opacity: o,
        transform: `translateY(${y}px)`,
      }}
    >
      <div>
        <div
          style={{
            color: T.blue,
            fontSize: 24,
            fontWeight: 800,
            letterSpacing: 5,
            textTransform: 'uppercase',
          }}
        >
          {kicker}
        </div>
        <div style={{color: T.ink, fontSize: 54, fontWeight: 800, marginTop: 8}}>
          {title}
        </div>
      </div>
      {note ? (
        <div style={{color: T.muted, fontSize: 21, paddingBottom: 8}}>{note}</div>
      ) : null}
    </div>
  );
};

// Révélation mot à mot (ressort + translation).
export const WordReveal: React.FC<{
  text: string;
  delay: number;
  per?: number;
  style?: React.CSSProperties;
}> = ({text, delay, per = 4, style}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  return (
    <div style={style}>
      {text.split(' ').map((w, i) => {
        const s = pop(frame, fps, delay + i * per, 15);
        return (
          <span
            key={`${w}-${i}`}
            style={{
              display: 'inline-block',
              opacity: s,
              transform: `translateY(${(1 - s) * 34}px)`,
              marginRight: '0.28em',
            }}
          >
            {w}
          </span>
        );
      })}
    </div>
  );
};

// Ligne de titre en dégradé — rendue en SVG (background-clip: text est
// peu fiable dans Chromium headless avec des enfants transformés).
export const GradientLine: React.FC<{
  text: string;
  delay: number;
  fontSize: number;
  id: string;
}> = ({text, delay, fontSize, id}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const s = pop(frame, fps, delay, 15);
  const w = 1500;
  const h = fontSize * 1.35;
  return (
    <div
      style={{
        opacity: s,
        transform: `translateY(${(1 - s) * 34}px)`,
        lineHeight: 0,
      }}
    >
      <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`}>
        <defs>
          <linearGradient id={id} x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stopColor={T.blue} />
            <stop offset="100%" stopColor={T.aqua} />
          </linearGradient>
        </defs>
        <text
          x={w / 2}
          y={fontSize * 1.02}
          textAnchor="middle"
          fill={`url(#${id})`}
          fontSize={fontSize}
          fontWeight={800}
          fontFamily={T.font}
        >
          {text}
        </text>
      </svg>
    </div>
  );
};

export const Chip: React.FC<{
  children: React.ReactNode;
  color?: string;
  style?: React.CSSProperties;
}> = ({children, color = T.muted, style}) => (
  <span
    style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: 10,
      padding: '8px 20px',
      borderRadius: 999,
      border: `1px solid ${color}55`,
      background: `${T.surface}cc`,
      color: T.ink2,
      fontSize: 21,
      ...style,
    }}
  >
    {children}
  </span>
);

export const Card: React.FC<{
  style?: React.CSSProperties;
  children?: React.ReactNode;
}> = ({style, children}) => (
  <div
    style={{
      background: `linear-gradient(180deg, ${T.surface}f2, ${T.surface}d8)`,
      border: `1px solid ${T.border}`,
      borderRadius: 18,
      boxShadow: '0 24px 60px rgba(0,0,0,0.35)',
      ...style,
    }}
  >
    {children}
  </div>
);

// Pastille de statut : icône + libellé — la couleur ne porte jamais seule.
export const RiskChip: React.FC<{level: 'low' | 'mid' | 'high'; size?: number}> = ({
  level,
  size = 22,
}) => {
  const conf = {
    low: {color: T.good, label: 'Faible', icon: 'check' as const},
    mid: {color: T.warning, label: 'Modéré', icon: 'minus' as const},
    high: {color: T.critical, label: 'Élevé', icon: 'tri' as const},
  }[level];
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        padding: `${size * 0.28}px ${size * 0.75}px`,
        borderRadius: 999,
        background: `${conf.color}1f`,
        border: `1px solid ${conf.color}66`,
        color: conf.color,
        fontSize: size,
        fontWeight: 700,
      }}
    >
      <StatusIcon kind={conf.icon} color={conf.color} size={size * 0.8} />
      {conf.label}
    </span>
  );
};

export const StatusIcon: React.FC<{
  kind: 'check' | 'minus' | 'tri';
  color: string;
  size: number;
}> = ({kind, color, size}) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    {kind === 'check' ? (
      <path
        d="M4 12.5 10 18 20 6"
        stroke={color}
        strokeWidth={3}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    ) : kind === 'minus' ? (
      <path d="M5 12h14" stroke={color} strokeWidth={3} strokeLinecap="round" />
    ) : (
      <>
        <path
          d="M12 3 22 20 H2 Z"
          stroke={color}
          strokeWidth={2.4}
          strokeLinejoin="round"
        />
        <line x1={12} y1={9.5} x2={12} y2={14.5} stroke={color} strokeWidth={2.4} />
        <circle cx={12} cy={17.2} r={1.2} fill={color} />
      </>
    )}
  </svg>
);

// Petites icônes filaires pour les nœuds et cartes.
export const Icon: React.FC<{
  name:
    | 'globe'
    | 'monitor'
    | 'api'
    | 'brain'
    | 'db'
    | 'sparkle'
    | 'bell'
    | 'file'
    | 'chat'
    | 'cloud';
  color: string;
  size?: number;
}> = ({name, color, size = 30}) => {
  const s = {stroke: color, strokeWidth: 1.9, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, fill: 'none' as const};
  return (
    <svg width={size} height={size} viewBox="0 0 24 24">
      {name === 'globe' && (
        <>
          <circle cx={12} cy={12} r={9} {...s} />
          <path d="M3 12h18M12 3c3 3.5 3 14 0 18M12 3c-3 3.5-3 14 0 18" {...s} />
        </>
      )}
      {name === 'monitor' && (
        <>
          <rect x={3} y={4} width={18} height={12} rx={2} {...s} />
          <path d="M9 20h6M12 16v4" {...s} />
        </>
      )}
      {name === 'api' && (
        <>
          <rect x={3} y={3} width={7.5} height={7.5} rx={1.5} {...s} />
          <rect x={13.5} y={3} width={7.5} height={7.5} rx={1.5} {...s} />
          <rect x={3} y={13.5} width={7.5} height={7.5} rx={1.5} {...s} />
          <rect x={13.5} y={13.5} width={7.5} height={7.5} rx={1.5} {...s} />
        </>
      )}
      {name === 'brain' && (
        <>
          <rect x={5} y={5} width={14} height={14} rx={3} {...s} />
          <path d="M9 2v3M15 2v3M9 19v3M15 19v3M2 9h3M2 15h3M19 9h3M19 15h3" {...s} />
          <circle cx={12} cy={12} r={2.4} {...s} />
        </>
      )}
      {name === 'db' && (
        <>
          <ellipse cx={12} cy={5.5} rx={8} ry={3} {...s} />
          <path d="M4 5.5v13c0 1.7 3.6 3 8 3s8-1.3 8-3v-13M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3" {...s} />
        </>
      )}
      {name === 'sparkle' && (
        <path d="M12 2l2.2 6.3L21 10l-6.8 1.7L12 18l-2.2-6.3L3 10l6.8-1.7L12 2ZM19 15l1 3 3 1-3 1-1 3-1-3-3-1 3-1 1-3Z" {...s} />
      )}
      {name === 'bell' && (
        <>
          <path d="M6 9a6 6 0 1 1 12 0c0 4 1.5 5.5 2 6H4c.5-.5 2-2 2-6Z" {...s} />
          <path d="M10 18a2 2 0 0 0 4 0" {...s} />
        </>
      )}
      {name === 'file' && (
        <>
          <path d="M6 2h8l4 4v16H6Z" {...s} />
          <path d="M14 2v4h4M9 13h6M9 17h6" {...s} />
        </>
      )}
      {name === 'chat' && (
        <>
          <path d="M4 4h16v12H9l-5 5Z" {...s} />
          <circle cx={9} cy={10} r={1} fill={color} />
          <circle cx={12.5} cy={10} r={1} fill={color} />
          <circle cx={16} cy={10} r={1} fill={color} />
        </>
      )}
      {name === 'cloud' && (
        <path d="M7 18a4 4 0 0 1 0-8 5.5 5.5 0 0 1 10.7 1.4A3.6 3.6 0 0 1 17.5 18H7Z" {...s} />
      )}
    </svg>
  );
};
