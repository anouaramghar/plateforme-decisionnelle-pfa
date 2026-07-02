import React from 'react';
import {T} from './theme';

const pt = (
  data: number[],
  i: number,
  w: number,
  h: number,
  min: number,
  max: number,
): [number, number] => [
  (i / (data.length - 1)) * w,
  h - ((data[i] - min) / (max - min)) * h,
];

const pathLength = (pts: [number, number][]): number => {
  let l = 0;
  for (let i = 1; i < pts.length; i++) {
    l += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
  }
  return l;
};

export const Sparkline: React.FC<{
  data: number[];
  w?: number;
  h?: number;
  color?: string;
  progress: number;
}> = ({data, w = 116, h = 34, color = T.blue, progress}) => {
  const min = Math.min(...data);
  const max = Math.max(...data);
  const pts = data.map((_, i) => pt(data, i, w, h - 4, min, max));
  const d = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x},${y + 2}`).join(' ');
  const len = pathLength(pts);
  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`}>
      <path
        d={d}
        fill="none"
        stroke={color}
        strokeWidth={2.5}
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeDasharray={len}
        strokeDashoffset={len * (1 - progress)}
      />
    </svg>
  );
};

// Courbe + aire : tracé progressif, points, étiquettes sélectives (1re et dernière).
export const LineArea: React.FC<{
  data: number[];
  labels: string[];
  w: number;
  h: number;
  min: number;
  max: number;
  gridValues: number[];
  progress: number;
  id: string;
  unit?: string;
}> = ({data, labels, w, h, min, max, gridValues, progress, id, unit = ''}) => {
  const gutter = 56;
  const bottom = 34;
  const top = 18;
  const cw = w - gutter;
  const ch = h - bottom - top;
  const pts = data.map((_, i) => {
    const [x, y] = pt(data, i, cw, ch, min, max);
    return [x, y + top] as [number, number];
  });
  const line = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x},${y}`).join(' ');
  const len = pathLength(pts);
  const last = pts[pts.length - 1];
  const first = pts[0];
  return (
    <div style={{position: 'relative', width: w, height: h}}>
      <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`}>
        <defs>
          <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={T.blue} stopOpacity={0.32} />
            <stop offset="100%" stopColor={T.blue} stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <g transform={`translate(${gutter},0)`}>
          {gridValues.map((g) => {
            const y = top + ch - ((g - min) / (max - min)) * ch;
            return (
              <g key={g}>
                <line x1={0} y1={y} x2={cw} y2={y} stroke={T.grid} strokeWidth={1} />
                <text
                  x={-14}
                  y={y + 6}
                  textAnchor="end"
                  fill={T.muted}
                  fontSize={17}
                  fontFamily={T.font}
                >
                  {g}
                </text>
              </g>
            );
          })}
          <line x1={0} y1={top + ch} x2={cw} y2={top + ch} stroke={T.grid} strokeWidth={2} />
          {progress > 0.02 ? (
            <path
              d={`${line} L${last[0]},${top + ch} L0,${top + ch} Z`}
              fill={`url(#${id})`}
              opacity={Math.min(1, progress * 1.4)}
              clipPath={`url(#clip-${id})`}
            />
          ) : null}
          <clipPath id={`clip-${id}`}>
            <rect x={0} y={0} width={cw * progress} height={h} />
          </clipPath>
          <path
            d={line}
            fill="none"
            stroke={T.blue}
            strokeWidth={3.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeDasharray={len}
            strokeDashoffset={len * (1 - progress)}
          />
          {pts.map(([x, y], i) => {
            const frac = i / (pts.length - 1);
            const on = progress >= frac - 0.001;
            return (
              <circle
                key={i}
                cx={x}
                cy={y}
                r={5.5}
                fill={T.blue}
                stroke={T.surface}
                strokeWidth={2}
                opacity={on ? 1 : 0}
              />
            );
          })}
          {labels.map((l, i) => (
            <text
              key={l}
              x={pts[i][0]}
              y={top + ch + 26}
              textAnchor="middle"
              fill={T.muted}
              fontSize={17}
              fontFamily={T.font}
            >
              {l}
            </text>
          ))}
        </g>
      </svg>
      {/* étiquettes directes : premier et dernier point seulement */}
      <div
        style={{
          position: 'absolute',
          left: gutter + first[0] - 30,
          top: first[1] - 44,
          color: T.ink2,
          fontSize: 19,
          fontWeight: 700,
          whiteSpace: 'nowrap',
          opacity: progress > 0.05 ? 1 : 0,
        }}
      >
        {data[0]}
        {unit}
      </div>
      <div
        style={{
          position: 'absolute',
          left: gutter + last[0] - 76,
          top: last[1] - 52,
          padding: '5px 14px',
          borderRadius: 999,
          background: T.blue,
          color: '#fff',
          fontSize: 21,
          fontWeight: 800,
          whiteSpace: 'nowrap',
          opacity: progress > 0.97 ? 1 : 0,
        }}
      >
        {data[data.length - 1]}
        {unit}
      </div>
    </div>
  );
};

const polar = (cx: number, cy: number, r: number, deg: number): [number, number] => {
  const a = ((deg - 90) * Math.PI) / 180;
  return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
};

const arcPath = (
  cx: number,
  cy: number,
  r: number,
  a0: number,
  a1: number,
): string => {
  const [x0, y0] = polar(cx, cy, r, a0);
  const [x1, y1] = polar(cx, cy, r, a1);
  const large = a1 - a0 > 180 ? 1 : 0;
  return `M ${x0} ${y0} A ${r} ${r} 0 ${large} 1 ${x1} ${y1}`;
};

// Anneau : segments avec écart (padAngle), balayage séquentiel.
export const Donut: React.FC<{
  segments: {value: number; color: string}[];
  r: number;
  stroke: number;
  progress: number;
}> = ({segments, r, stroke, progress}) => {
  const total = segments.reduce((a, b) => a + b.value, 0);
  const pad = 4;
  const size = (r + stroke) * 2;
  const c = r + stroke;
  let acc = 0;
  const budget = 360 * progress;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      {segments.map((s, i) => {
        const start = acc;
        const span = (s.value / total) * 360;
        acc += span;
        const visible = Math.max(0, Math.min(span - pad, budget - start));
        if (visible < 1) {
          return null;
        }
        return (
          <path
            key={i}
            d={arcPath(c, c, r, start + pad / 2, start + pad / 2 + visible)}
            fill="none"
            stroke={s.color}
            strokeWidth={stroke}
          />
        );
      })}
    </svg>
  );
};

// Jauge semi-circulaire de risque.
export const Gauge: React.FC<{
  p: number;
  w?: number;
  color: string;
}> = ({p, w = 460, color}) => {
  const r = w * 0.38;
  const cx = w / 2;
  const cy = r + 24;
  const circ = Math.PI * r;
  return (
    <svg width={w} height={cy + 18} viewBox={`0 0 ${w} ${cy + 18}`}>
      <path
        d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
        fill="none"
        stroke={T.grid}
        strokeWidth={26}
        strokeLinecap="round"
      />
      <path
        d={`M ${cx - r} ${cy} A ${r} ${r} 0 0 1 ${cx + r} ${cy}`}
        fill="none"
        stroke={color}
        strokeWidth={26}
        strokeLinecap="round"
        strokeDasharray={circ}
        strokeDashoffset={circ * (1 - p)}
      />
    </svg>
  );
};
