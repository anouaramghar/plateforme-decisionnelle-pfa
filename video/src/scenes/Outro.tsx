import React from 'react';
import {
  AbsoluteFill,
  interpolate,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import {T} from '../theme';
import {Backdrop, Chip, GradientLine, pop, fadeIn} from '../ui';

export const OUTRO_DURATION = 170;

const TEAM = ['AIT ALI', 'AMGHAR', 'ENGAR'];
const TECH = ['React', 'ASP.NET Core', 'FastAPI', 'SQL Server', 'Docker'];

export const Outro: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const lineW = interpolate(frame, [34, 64], [0, 520], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Backdrop glow="blue">
      <AbsoluteFill style={{justifyContent: 'center', alignItems: 'center', textAlign: 'center'}}>
        <GradientLine text="Décider avec les données." delay={8} fontSize={92} id="og" />
        <div
          style={{
            height: 5,
            width: lineW,
            borderRadius: 3,
            marginTop: 38,
            background: `linear-gradient(90deg, ${T.blue}, ${T.aqua})`,
          }}
        />
        <div style={{color: T.ink2, fontSize: 30, marginTop: 34, opacity: fadeIn(frame, 44, 20)}}>
          Plateforme Décisionnelle ENIAD — 2025 / 2026
        </div>
        <div style={{display: 'flex', gap: 24, marginTop: 52}}>
          {TEAM.map((n, i) => {
            const s = pop(frame, fps, 58 + i * 8, 13);
            return (
              <div
                key={n}
                style={{
                  background: T.surface,
                  border: `1px solid ${T.border}`,
                  borderRadius: 999,
                  padding: '15px 38px',
                  color: T.ink,
                  fontSize: 28,
                  fontWeight: 700,
                  opacity: s,
                  transform: `translateY(${(1 - s) * 26}px)`,
                }}
              >
                {n}
              </div>
            );
          })}
        </div>
        <div style={{color: T.ink2, fontSize: 26, marginTop: 40, opacity: fadeIn(frame, 88, 18)}}>
          Encadré par <span style={{color: T.ink, fontWeight: 800}}>Prof. LHIADI</span>
        </div>
        <div
          style={{
            position: 'absolute',
            bottom: 54,
            display: 'flex',
            gap: 16,
            opacity: fadeIn(frame, 104, 18),
          }}
        >
          {TECH.map((t) => (
            <Chip key={t} style={{fontSize: 18, padding: '6px 16px'}}>
              {t}
            </Chip>
          ))}
        </div>
      </AbsoluteFill>
    </Backdrop>
  );
};
