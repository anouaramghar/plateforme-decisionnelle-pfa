import React from 'react';
import {
  AbsoluteFill,
  interpolate,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Chip, GradientLine, WordReveal, pop, fadeIn} from '../ui';
import {Sparkline, Donut} from '../charts';

export const INTRO_DURATION = 170;

// Éléments décoratifs flottants (cartes BI miniatures).
const Floater: React.FC<{
  x: number;
  y: number;
  delay: number;
  phase: number;
  children: React.ReactNode;
}> = ({x, y, delay, phase, children}) => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const s = pop(frame, fps, delay, 16);
  const bob = Math.sin((frame + phase) / 42) * 9;
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        opacity: s * 0.62,
        transform: `translateY(${(1 - s) * 40 + bob}px) scale(${0.6 + s * 0.4})`,
      }}
    >
      {children}
    </div>
  );
};

export const Intro: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const chipIn = pop(frame, fps, 6, 14);
  const lineW = interpolate(frame, [46, 78], [0, 640], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const subIn = fadeIn(frame, 62, 22);

  return (
    <Backdrop glow="blue">
      <Floater x={140} y={150} delay={26} phase={0}>
        <Card style={{padding: '18px 22px'}}>
          <div style={{color: T.muted, fontSize: 16, marginBottom: 8}}>Réussite</div>
          <Sparkline data={[64, 68, 66, 72, 75, 81]} progress={1} w={140} h={40} />
        </Card>
      </Floater>
      <Floater x={1590} y={190} delay={34} phase={40}>
        <Card style={{padding: 20}}>
          <Donut
            segments={[
              {value: 40, color: T.blue},
              {value: 32, color: T.aqua},
              {value: 28, color: T.yellow},
            ]}
            r={44}
            stroke={14}
            progress={1}
          />
        </Card>
      </Floater>
      <Floater x={1500} y={740} delay={42} phase={80}>
        <Card style={{padding: '18px 24px', display: 'flex', alignItems: 'center', gap: 14}}>
          <div style={{width: 12, height: 12, borderRadius: 6, background: T.critical}} />
          <div style={{color: T.ink2, fontSize: 19}}>37 alertes actives</div>
        </Card>
      </Floater>
      <Floater x={200} y={760} delay={50} phase={120}>
        <Card style={{padding: '18px 24px'}}>
          <div style={{color: T.ink, fontSize: 34, fontWeight: 800}}>78 %</div>
          <div style={{color: T.muted, fontSize: 16}}>taux de réussite</div>
        </Card>
      </Floater>

      <AbsoluteFill style={{justifyContent: 'center', alignItems: 'center'}}>
        <div style={{opacity: chipIn, transform: `translateY(${(1 - chipIn) * 20}px)`, marginBottom: 42}}>
          <Chip color={T.blue}>
            <span style={{width: 9, height: 9, borderRadius: 5, background: T.blue, display: 'inline-block'}} />
            ENIAD · Projet de fin d’année 2025 / 2026
          </Chip>
        </div>
        <WordReveal
          text="La donnée au service de"
          delay={14}
          style={{
            color: T.ink,
            fontSize: 86,
            fontWeight: 800,
            textAlign: 'center',
            lineHeight: 1.16,
          }}
        />
        <GradientLine text="la réussite étudiante" delay={34} fontSize={86} id="ig" />
        <div
          style={{
            height: 6,
            width: lineW,
            borderRadius: 3,
            marginTop: 44,
            background: `linear-gradient(90deg, ${T.blue}, ${T.aqua})`,
          }}
        />
        <div style={{color: T.ink2, fontSize: 32, marginTop: 36, opacity: subIn}}>
          Plateforme décisionnelle — BI &amp; analytique prédictive
        </div>
      </AbsoluteFill>
    </Backdrop>
  );
};
