import React from 'react';
import {spring, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Scene, SceneTitle} from '../Scene';

export const FEATURES_DURATION = 270;

const BellIcon: React.FC<{color: string}> = ({color}) => (
  <svg width={56} height={56} viewBox="0 0 24 24" fill="none">
    <path
      d="M6 9a6 6 0 1 1 12 0c0 4 1.5 5.5 2 6H4c.5-.5 2-2 2-6Z"
      stroke={color}
      strokeWidth={1.8}
      strokeLinejoin="round"
    />
    <path d="M10 18a2 2 0 0 0 4 0" stroke={color} strokeWidth={1.8} />
  </svg>
);

const FileIcon: React.FC<{color: string}> = ({color}) => (
  <svg width={56} height={56} viewBox="0 0 24 24" fill="none">
    <path
      d="M6 2h8l4 4v16H6Z"
      stroke={color}
      strokeWidth={1.8}
      strokeLinejoin="round"
    />
    <path d="M14 2v4h4" stroke={color} strokeWidth={1.8} strokeLinejoin="round" />
    <path d="M9 13h6M9 17h6" stroke={color} strokeWidth={1.8} strokeLinecap="round" />
  </svg>
);

const ChatIcon: React.FC<{color: string}> = ({color}) => (
  <svg width={56} height={56} viewBox="0 0 24 24" fill="none">
    <path
      d="M4 4h16v12H9l-5 5Z"
      stroke={color}
      strokeWidth={1.8}
      strokeLinejoin="round"
    />
    <circle cx={9} cy={10} r={1.1} fill={color} />
    <circle cx={12.5} cy={10} r={1.1} fill={color} />
    <circle cx={16} cy={10} r={1.1} fill={color} />
  </svg>
);

const CARDS = [
  {
    icon: BellIcon,
    color: T.yellow,
    title: 'Alertes en continu',
    lines: [
      'Détection des étudiants à risque',
      'Actualisation automatique toutes les 30 s',
      'Priorisation par niveau de gravité',
    ],
  },
  {
    icon: FileIcon,
    color: T.blue,
    title: 'Exports décisionnels',
    lines: [
      'Rapports PDF prêts à diffuser',
      'Classeurs Excel pour l’analyse',
      'CSV pour l’interopérabilité',
    ],
  },
  {
    icon: ChatIcon,
    color: T.aqua,
    title: 'Assistant IA Copilot',
    lines: [
      'Questions en langage naturel',
      'Requêtes paramétrées — jamais de SQL libre',
      'Accès en lecture seule au data warehouse',
    ],
  },
];

export const Features: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();

  return (
    <Scene durationInFrames={FEATURES_DURATION}>
      <SceneTitle kicker="Fonctionnalités" title="Du signal à la décision" />
      <div
        style={{
          position: 'absolute',
          top: 330,
          left: 110,
          right: 110,
          display: 'flex',
          gap: 40,
        }}
      >
        {CARDS.map((c, i) => {
          const pop = spring({
            frame: frame - (12 + i * 14),
            fps,
            config: {damping: 13},
          });
          const Icon = c.icon;
          return (
            <div
              key={c.title}
              style={{
                flex: 1,
                background: T.surface,
                border: `1px solid ${T.border}`,
                borderRadius: 20,
                padding: '44px 44px 40px',
                transform: `translateY(${(1 - pop) * 90}px)`,
                opacity: pop,
              }}
            >
              <Icon color={c.color} />
              <div style={{color: T.ink, fontSize: 36, fontWeight: 800, marginTop: 26}}>
                {c.title}
              </div>
              <div style={{marginTop: 22, display: 'flex', flexDirection: 'column', gap: 16}}>
                {c.lines.map((l) => (
                  <div key={l} style={{display: 'flex', gap: 14, alignItems: 'baseline'}}>
                    <div
                      style={{
                        width: 8,
                        height: 8,
                        borderRadius: 4,
                        background: c.color,
                        flexShrink: 0,
                        transform: 'translateY(-4px)',
                      }}
                    />
                    <div style={{color: T.ink2, fontSize: 24, lineHeight: 1.45}}>{l}</div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </Scene>
  );
};
