import React from 'react';
import {AbsoluteFill, interpolate, useCurrentFrame} from 'remotion';
import {T} from './theme';

// Enveloppe de scène : fond + fondu d'entrée/sortie aux bords de la Sequence.
export const Scene: React.FC<{
  durationInFrames: number;
  children: React.ReactNode;
}> = ({durationInFrames, children}) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(
    frame,
    [0, 12, durationInFrames - 12, durationInFrames],
    [0, 1, 1, 0],
    {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'},
  );
  return (
    <AbsoluteFill style={{backgroundColor: T.page, opacity, fontFamily: T.font}}>
      <BackgroundGrid />
      {children}
    </AbsoluteFill>
  );
};

const BackgroundGrid: React.FC = () => (
  <AbsoluteFill
    style={{
      backgroundImage: `linear-gradient(${T.grid}55 1px, transparent 1px),
        linear-gradient(90deg, ${T.grid}55 1px, transparent 1px)`,
      backgroundSize: '120px 120px',
      maskImage: 'radial-gradient(ellipse at center, black 30%, transparent 85%)',
      WebkitMaskImage:
        'radial-gradient(ellipse at center, black 30%, transparent 85%)',
    }}
  />
);

export const SceneTitle: React.FC<{kicker: string; title: string}> = ({
  kicker,
  title,
}) => {
  const frame = useCurrentFrame();
  const y = interpolate(frame, [0, 20], [24, 0], {
    extrapolateRight: 'clamp',
  });
  const opacity = interpolate(frame, [0, 20], [0, 1], {
    extrapolateRight: 'clamp',
  });
  return (
    <div style={{position: 'absolute', top: 80, left: 110, right: 110, opacity, transform: `translateY(${y}px)`}}>
      <div style={{color: T.blue, fontSize: 26, fontWeight: 700, letterSpacing: 4, textTransform: 'uppercase'}}>
        {kicker}
      </div>
      <div style={{color: T.ink, fontSize: 58, fontWeight: 800, marginTop: 10}}>{title}</div>
    </div>
  );
};
