import React from 'react';
import {AbsoluteFill, Sequence} from 'remotion';
import {T} from './theme';
import {Intro, INTRO_DURATION} from './scenes/Intro';
import {Kpis, KPIS_DURATION} from './scenes/Kpis';
import {Architecture, ARCHI_DURATION} from './scenes/Architecture';
import {Prediction, PREDICTION_DURATION} from './scenes/Prediction';
import {Features, FEATURES_DURATION} from './scenes/Features';
import {Outro, OUTRO_DURATION} from './scenes/Outro';

export const TOTAL_DURATION =
  INTRO_DURATION +
  KPIS_DURATION +
  ARCHI_DURATION +
  PREDICTION_DURATION +
  FEATURES_DURATION +
  OUTRO_DURATION;

export const PlatformVideo: React.FC = () => {
  let at = 0;
  const seq = (d: number) => {
    const from = at;
    at += d;
    return {from, durationInFrames: d};
  };
  return (
    <AbsoluteFill style={{backgroundColor: T.page}}>
      <Sequence {...seq(INTRO_DURATION)} name="Intro">
        <Intro />
      </Sequence>
      <Sequence {...seq(KPIS_DURATION)} name="KPIs">
        <Kpis />
      </Sequence>
      <Sequence {...seq(ARCHI_DURATION)} name="Architecture">
        <Architecture />
      </Sequence>
      <Sequence {...seq(PREDICTION_DURATION)} name="Prédiction">
        <Prediction />
      </Sequence>
      <Sequence {...seq(FEATURES_DURATION)} name="Fonctionnalités">
        <Features />
      </Sequence>
      <Sequence {...seq(OUTRO_DURATION)} name="Générique">
        <Outro />
      </Sequence>
    </AbsoluteFill>
  );
};
