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

export const INTRO_DURATION = 150;

export const Intro: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();

  const scale = spring({frame, fps, config: {damping: 14, mass: 0.8}});
  const sub = interpolate(frame, [25, 50], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const barW = interpolate(frame, [15, 55], [0, 560], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const year = interpolate(frame, [45, 70], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <Scene durationInFrames={INTRO_DURATION}>
      <AbsoluteFill
        style={{justifyContent: 'center', alignItems: 'center', textAlign: 'center'}}
      >
        <div
          style={{
            color: T.ink,
            fontSize: 92,
            fontWeight: 800,
            transform: `scale(${scale})`,
            lineHeight: 1.1,
          }}
        >
          Plateforme Décisionnelle
          <br />
          <span style={{color: T.blue}}>ENIAD</span>
        </div>
        <div
          style={{
            height: 6,
            width: barW,
            borderRadius: 3,
            marginTop: 36,
            background: `linear-gradient(90deg, ${T.blue}, ${T.aqua})`,
          }}
        />
        <div style={{color: T.ink2, fontSize: 36, marginTop: 36, opacity: sub}}>
          Business Intelligence &amp; Analytique Prédictive
        </div>
        <div style={{color: T.muted, fontSize: 28, marginTop: 14, opacity: year}}>
          Performance étudiante — 2025 / 2026
        </div>
      </AbsoluteFill>
    </Scene>
  );
};
