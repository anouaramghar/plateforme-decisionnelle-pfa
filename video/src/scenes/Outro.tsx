import React from 'react';
import {
  AbsoluteFill,
  interpolate,
  spring,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import {T} from '../theme';
import {Scene} from '../Scene';

export const OUTRO_DURATION = 180;

export const Outro: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const pop = spring({frame, fps, config: {damping: 14}});
  const team = interpolate(frame, [30, 55], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const sup = interpolate(frame, [55, 80], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Scene durationInFrames={OUTRO_DURATION}>
      <AbsoluteFill
        style={{justifyContent: 'center', alignItems: 'center', textAlign: 'center'}}
      >
        <div
          style={{
            color: T.ink,
            fontSize: 64,
            fontWeight: 800,
            transform: `scale(${pop})`,
          }}
        >
          Plateforme Décisionnelle <span style={{color: T.blue}}>ENIAD</span>
        </div>
        <div style={{color: T.muted, fontSize: 26, marginTop: 16, opacity: team}}>
          Projet de fin d'année — 2025 / 2026
        </div>
        <div
          style={{
            display: 'flex',
            gap: 26,
            marginTop: 56,
            opacity: team,
            alignItems: 'center',
          }}
        >
          {['AIT ALI', 'AMGHAR', 'ENGAR'].map((n) => (
            <div
              key={n}
              style={{
                background: T.surface,
                border: `1px solid ${T.border}`,
                borderRadius: 999,
                padding: '16px 40px',
                color: T.ink2,
                fontSize: 30,
                fontWeight: 700,
              }}
            >
              {n}
            </div>
          ))}
        </div>
        <div style={{color: T.ink2, fontSize: 28, marginTop: 44, opacity: sup}}>
          Encadré par <span style={{color: T.ink, fontWeight: 700}}>Prof. LHIADI</span>
        </div>
      </AbsoluteFill>
    </Scene>
  );
};
