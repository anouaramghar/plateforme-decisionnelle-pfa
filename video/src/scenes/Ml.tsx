import React from 'react';
import {interpolate, useCurrentFrame, useVideoConfig} from 'remotion';
import {T} from '../theme';
import {Backdrop, Card, Heading, RiskChip, pop, fadeIn} from '../ui';
import {Gauge} from '../charts';

export const ML_DURATION = 300;

const ROWS: {name: string; init: string; avg: string; abs: string; risk: 'low' | 'mid' | 'high'}[] = [
  {name: 'A. Benali', init: 'AB', avg: '9,4', abs: '18 %', risk: 'high'},
  {name: 'K. Haddad', init: 'KH', avg: '10,1', abs: '12 %', risk: 'mid'},
  {name: 'S. Mansouri', init: 'SM', avg: '11,8', abs: '9 %', risk: 'mid'},
  {name: 'Y. El Amrani', init: 'YE', avg: '14,2', abs: '4 %', risk: 'low'},
];

const RISK_P = 0.72;

export const Ml: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();
  const cardIn = pop(frame, fps, 6, 15);
  const rightIn = pop(frame, fps, 96, 15);
  const gaugeP = interpolate(frame, [118, 185], [0, RISK_P], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const hl = fadeIn(frame, 104, 14); // surbrillance de la ligne analysée
  const color = gaugeP < 0.4 ? T.good : gaugeP < 0.6 ? T.warning : T.critical;
  const label = gaugeP < 0.4 ? 'Risque faible' : gaugeP < 0.6 ? 'Risque modéré' : 'Risque élevé';

  return (
    <Backdrop glow="violet">
      <Heading
        kicker="Machine learning"
        title="Repérer tôt, accompagner à temps"
        note="scores calculés par lot — POST /predict"
      />
      {/* Table des étudiants */}
      <Card
        style={{
          position: 'absolute',
          left: 110,
          top: 250,
          width: 960,
          padding: '30px 36px',
          opacity: cardIn,
          transform: `translateY(${(1 - cardIn) * 40}px)`,
        }}
      >
        <div style={{color: T.ink2, fontSize: 23, marginBottom: 20}}>
          Étudiants — semestre courant
        </div>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '1fr 170px 170px 200px',
            padding: '0 18px 12px',
            color: T.muted,
            fontSize: 18,
            borderBottom: `1px solid ${T.border}`,
          }}
        >
          <div>Étudiant</div>
          <div style={{textAlign: 'right'}}>Moyenne</div>
          <div style={{textAlign: 'right'}}>Absences</div>
          <div style={{textAlign: 'right'}}>Risque prédit</div>
        </div>
        {ROWS.map((r, i) => {
          const s = pop(frame, fps, 22 + i * 12, 15);
          const chipS = pop(frame, fps, 46 + i * 12, 11);
          const isHl = i === 0;
          return (
            <div
              key={r.name}
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 170px 170px 200px',
                alignItems: 'center',
                padding: '16px 18px',
                marginTop: 10,
                borderRadius: 14,
                border: `1.5px solid ${isHl ? `rgba(208,59,59,${hl * 0.75})` : 'transparent'}`,
                background: isHl ? `rgba(208,59,59,${hl * 0.07})` : 'transparent',
                opacity: s,
                transform: `translateX(${(1 - s) * -40}px)`,
              }}
            >
              <div style={{display: 'flex', alignItems: 'center', gap: 16}}>
                <div
                  style={{
                    width: 46,
                    height: 46,
                    borderRadius: 23,
                    background: T.surface2,
                    border: `1px solid ${T.border}`,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: T.ink2,
                    fontSize: 18,
                    fontWeight: 700,
                  }}
                >
                  {r.init}
                </div>
                <span style={{color: T.ink, fontSize: 24, fontWeight: 600}}>{r.name}</span>
              </div>
              <div style={{textAlign: 'right', color: T.ink, fontSize: 24, fontWeight: 700}}>
                {r.avg}
                <span style={{color: T.muted, fontSize: 18}}> / 20</span>
              </div>
              <div style={{textAlign: 'right', color: T.ink2, fontSize: 24}}>{r.abs}</div>
              <div style={{textAlign: 'right', opacity: chipS, transform: `scale(${chipS})`}}>
                <RiskChip level={r.risk} size={19} />
              </div>
            </div>
          );
        })}
        <div style={{color: T.muted, fontSize: 18, marginTop: 18}}>
          Signaux utilisés : moyenne courante · taux d’absence · charge de modules
        </div>
      </Card>

      {/* Analyse détaillée */}
      <Card
        style={{
          position: 'absolute',
          left: 1130,
          top: 250,
          width: 680,
          padding: '30px 40px',
          opacity: rightIn,
          transform: `translateY(${(1 - rightIn) * 40}px)`,
        }}
      >
        <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center'}}>
          <div style={{color: T.ink2, fontSize: 23}}>Analyse — A. Benali</div>
          <div style={{color: T.muted, fontSize: 17}}>XGBoost · FastAPI</div>
        </div>
        <div style={{display: 'flex', justifyContent: 'center', marginTop: 12}}>
          <Gauge p={gaugeP} color={color} w={480} />
        </div>
        <div style={{textAlign: 'center', marginTop: -148}}>
          <div style={{color: T.ink, fontSize: 76, fontWeight: 800}}>
            {Math.round(gaugeP * 100)}
            <span style={{fontSize: 40, color: T.ink2}}> %</span>
          </div>
          <div style={{color: T.muted, fontSize: 19, marginTop: 2}}>
            probabilité d’échec au semestre suivant
          </div>
          <div style={{marginTop: 14, opacity: gaugeP >= RISK_P - 0.001 ? 1 : 0}}>
            <RiskChip level="high" size={22} />
          </div>
        </div>
        <div
          style={{
            marginTop: 30,
            paddingTop: 22,
            borderTop: `1px solid ${T.border}`,
            color: T.ink2,
            fontSize: 21,
            lineHeight: 1.55,
            opacity: fadeIn(frame, 190, 20),
          }}
        >
          Un signal d’<span style={{color: T.ink, fontWeight: 700}}>alerte précoce</span>,
          pas un verdict : la plateforme oriente les équipes pédagogiques vers les
          étudiants à accompagner en priorité.
        </div>
      </Card>
      <div style={{position: 'absolute', bottom: 34, left: 110, color: T.muted, fontSize: 19, opacity: fadeIn(frame, 210, 18)}}>
        Statut : {label} · seuils — faible &lt; 40 % · modéré &lt; 60 % · élevé ≥ 60 %
      </div>
    </Backdrop>
  );
};
