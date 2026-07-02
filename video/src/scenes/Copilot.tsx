import React from 'react';
import {useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Chip, Heading, Icon, RiskChip, pop, fadeIn} from '../ui';

export const COPILOT_DURATION = 240;

const ANSWER_ROWS: {name: string; detail: string; risk: 'high' | 'mid'}[] = [
  {name: 'A. Benali', detail: 'moyenne 9,4 · absences 18 %', risk: 'high'},
  {name: 'M. Cherkaoui', detail: 'moyenne 9,8 · absences 21 %', risk: 'high'},
  {name: 'K. Haddad', detail: 'moyenne 10,1 · absences 12 %', risk: 'mid'},
];

const TypingDots: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <div style={{display: 'flex', gap: 7, padding: '6px 2px'}}>
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          style={{
            width: 10,
            height: 10,
            borderRadius: 5,
            background: T.muted,
            opacity: 0.35 + 0.65 * Math.abs(Math.sin((frame - i * 5) / 9)),
          }}
        />
      ))}
    </div>
  );
};

export const Copilot: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const panelIn = pop(frame, fps, 4, 15);
  const userIn = pop(frame, fps, 22, 13);
  const typing = frame >= 46 && frame < 88;
  const answerIn = pop(frame, fps, 88, 15);

  return (
    <Backdrop glow="violet">
      <Heading
        kicker="Assistant IA"
        title="Interroger les données, sans SQL"
        note="CopilotKit · NVIDIA NIM"
      />
      <Card
        style={{
          position: 'absolute',
          left: 370,
          top: 246,
          width: 1180,
          padding: '28px 40px 32px',
          opacity: panelIn,
          transform: `translateY(${(1 - panelIn) * 44}px)`,
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            paddingBottom: 20,
            borderBottom: `1px solid ${T.border}`,
          }}
        >
          <div style={{display: 'flex', alignItems: 'center', gap: 14}}>
            <div
              style={{
                width: 46,
                height: 46,
                borderRadius: 13,
                background: `linear-gradient(135deg, ${T.violet}, ${T.blue})`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <Icon name="sparkle" color="#fff" size={26} />
            </div>
            <span style={{color: T.ink, fontSize: 26, fontWeight: 800}}>Copilot ENIAD</span>
          </div>
          <Chip color={T.aqua} style={{fontSize: 18}}>
            <Icon name="db" color={T.aqua} size={18} />
            data warehouse · lecture seule
          </Chip>
        </div>

        {/* question */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'flex-end',
            marginTop: 26,
            opacity: userIn,
            transform: `translateY(${(1 - userIn) * 24}px)`,
          }}
        >
          <div
            style={{
              background: T.blue,
              color: '#fff',
              fontSize: 24,
              padding: '16px 26px',
              borderRadius: '20px 20px 6px 20px',
              maxWidth: 640,
            }}
          >
            Quels étudiants sont les plus à risque ce semestre ?
          </div>
        </div>

        {/* réponse */}
        <div style={{display: 'flex', marginTop: 22}}>
          <div
            style={{
              background: `${T.surface2}dd`,
              border: `1px solid ${T.border}`,
              borderRadius: '20px 20px 20px 6px',
              padding: '18px 26px',
              maxWidth: 780,
              minHeight: 30,
            }}
          >
            {typing ? (
              <TypingDots />
            ) : frame >= 88 ? (
              <div style={{opacity: answerIn}}>
                <div style={{color: T.ink2, fontSize: 23, lineHeight: 1.5}}>
                  3 étudiants concentrent le risque le plus élevé :
                </div>
                {ANSWER_ROWS.map((r, i) => (
                  <div
                    key={r.name}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 24,
                      marginTop: 14,
                      padding: '13px 18px',
                      borderRadius: 12,
                      background: `${T.page}88`,
                      opacity: pop(frame, fps, 100 + i * 10, 14),
                    }}
                  >
                    <div>
                      <span style={{color: T.ink, fontSize: 22, fontWeight: 700}}>{r.name}</span>
                      <span style={{color: T.muted, fontSize: 19, marginLeft: 14}}>{r.detail}</span>
                    </div>
                    <RiskChip level={r.risk} size={17} />
                  </div>
                ))}
                <div style={{color: T.muted, fontSize: 18, marginTop: 16, opacity: fadeIn(frame, 140, 16)}}>
                  Source : PFA_DW · requête paramétrée « top risques »
                </div>
              </div>
            ) : null}
          </div>
        </div>
      </Card>

      <div
        style={{
          position: 'absolute',
          bottom: 66,
          left: 0,
          right: 0,
          display: 'flex',
          justifyContent: 'center',
          gap: 22,
          opacity: fadeIn(frame, 160, 20),
        }}
      >
        <Chip>requêtes paramétrées uniquement</Chip>
        <Chip>jamais de SQL généré librement</Chip>
        <Chip>service optionnel — jamais bloquant</Chip>
      </div>
    </Backdrop>
  );
};
