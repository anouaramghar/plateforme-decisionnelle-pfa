import React from 'react';
import {useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Heading, Icon, StatusIcon, pop, fadeIn} from '../ui';

export const ALERTS_DURATION = 270;

const ALERTS = [
  {
    sev: 'Critique',
    color: T.critical,
    icon: 'tri' as const,
    text: 'A. Benali — 3 modules sous 8 / 20',
    time: 'il y a 2 min',
  },
  {
    sev: 'Élevé',
    color: T.serious,
    icon: 'tri' as const,
    text: 'K. Haddad — absences au-delà de 25 %',
    time: 'il y a 14 min',
  },
  {
    sev: 'Modéré',
    color: T.warning,
    icon: 'minus' as const,
    text: 'S. Mansouri — moyenne en baisse de 2 pts',
    time: 'il y a 41 min',
  },
];

const FILES = [
  {name: 'rapport-s2.pdf', desc: 'Rapport de synthèse', color: T.red},
  {name: 'notes-modules.xlsx', desc: 'Classeur d’analyse', color: T.aqua},
  {name: 'etudiants.csv', desc: 'Données brutes', color: T.muted},
];

// Anneau de progression : cycle d'actualisation (30 s).
const PollRing: React.FC = () => {
  const frame = useCurrentFrame();
  const p = (frame % 90) / 90;
  const r = 21;
  const circ = 2 * Math.PI * r;
  return (
    <svg width={52} height={52} viewBox="0 0 52 52">
      <circle cx={26} cy={26} r={r} fill="none" stroke={T.grid} strokeWidth={5} />
      <circle
        cx={26}
        cy={26}
        r={r}
        fill="none"
        stroke={T.aqua}
        strokeWidth={5}
        strokeLinecap="round"
        strokeDasharray={circ}
        strokeDashoffset={circ * (1 - p)}
        transform="rotate(-90 26 26)"
      />
    </svg>
  );
};

export const AlertsExports: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const leftIn = pop(frame, fps, 6, 15);
  const rightIn = pop(frame, fps, 20, 15);

  return (
    <Backdrop glow="yellow">
      <Heading kicker="Alertes & exports" title="Du signal à l’action" />
      {/* Alertes */}
      <Card
        style={{
          position: 'absolute',
          left: 110,
          top: 250,
          width: 850,
          padding: '30px 36px',
          opacity: leftIn,
          transform: `translateY(${(1 - leftIn) * 40}px)`,
        }}
      >
        <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center'}}>
          <div style={{display: 'flex', alignItems: 'center', gap: 14}}>
            <Icon name="bell" color={T.yellow} size={30} />
            <span style={{color: T.ink, fontSize: 27, fontWeight: 800}}>Alertes</span>
            <span
              style={{
                background: T.critical,
                color: '#fff',
                borderRadius: 999,
                fontSize: 17,
                fontWeight: 800,
                padding: '3px 12px',
              }}
            >
              37
            </span>
          </div>
          <div style={{display: 'flex', alignItems: 'center', gap: 12}}>
            <PollRing />
            <div style={{color: T.muted, fontSize: 17, lineHeight: 1.3}}>
              actualisation
              <br />
              auto · 30 s
            </div>
          </div>
        </div>
        {ALERTS.map((a, i) => {
          const s = pop(frame, fps, 34 + i * 16, 13);
          return (
            <div
              key={a.text}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 18,
                marginTop: 18,
                padding: '20px 24px',
                borderRadius: 16,
                background: `${T.surface2}88`,
                border: `1px solid ${T.border}`,
                borderLeft: `4px solid ${a.color}`,
                opacity: s,
                transform: `translateX(${(1 - s) * 60}px)`,
              }}
            >
              <StatusIcon kind={a.icon} color={a.color} size={26} />
              <div style={{flex: 1}}>
                <div style={{color: T.ink, fontSize: 23, fontWeight: 600}}>{a.text}</div>
                <div style={{color: T.muted, fontSize: 17, marginTop: 3}}>{a.time}</div>
              </div>
              <span
                style={{
                  color: a.color,
                  border: `1px solid ${a.color}66`,
                  background: `${a.color}1a`,
                  borderRadius: 999,
                  padding: '5px 14px',
                  fontSize: 17,
                  fontWeight: 800,
                }}
              >
                {a.sev}
              </span>
            </div>
          );
        })}
        <div style={{color: T.muted, fontSize: 18, marginTop: 20, opacity: fadeIn(frame, 110, 16)}}>
          Détection automatique après chaque import de notes ou prédiction ML
        </div>
      </Card>

      {/* Exports */}
      <Card
        style={{
          position: 'absolute',
          left: 1020,
          top: 250,
          width: 790,
          padding: '30px 36px',
          opacity: rightIn,
          transform: `translateY(${(1 - rightIn) * 40}px)`,
        }}
      >
        <div style={{display: 'flex', alignItems: 'center', gap: 14}}>
          <Icon name="file" color={T.blue} size={30} />
          <span style={{color: T.ink, fontSize: 27, fontWeight: 800}}>Exports en un clic</span>
        </div>
        {FILES.map((f, i) => {
          const s = pop(frame, fps, 50 + i * 16, 13);
          return (
            <div
              key={f.name}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 20,
                marginTop: 18,
                padding: '20px 24px',
                borderRadius: 16,
                background: `${T.surface2}88`,
                border: `1px solid ${T.border}`,
                opacity: s,
                transform: `translateY(${(1 - s) * 34}px)`,
              }}
            >
              <div
                style={{
                  width: 54,
                  height: 54,
                  borderRadius: 13,
                  background: `${f.color}1c`,
                  border: `1px solid ${f.color}55`,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Icon name="file" color={f.color} size={28} />
              </div>
              <div style={{flex: 1}}>
                <div style={{color: T.ink, fontSize: 23, fontWeight: 700, fontFamily: 'ui-monospace, monospace'}}>
                  {f.name}
                </div>
                <div style={{color: T.muted, fontSize: 17, marginTop: 3}}>{f.desc}</div>
              </div>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  color: T.good,
                  fontSize: 18,
                  fontWeight: 700,
                  opacity: pop(frame, fps, 68 + i * 16, 11),
                }}
              >
                <StatusIcon kind="check" color={T.good} size={20} />
                prêt
              </div>
            </div>
          );
        })}
        <div style={{color: T.muted, fontSize: 18, marginTop: 20, opacity: fadeIn(frame, 120, 16)}}>
          jsPDF · SheetJS · CSV natif — générés côté client, sans aller-retour serveur
        </div>
      </Card>
    </Backdrop>
  );
};
