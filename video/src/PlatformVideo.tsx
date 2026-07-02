import React from 'react';
import {AbsoluteFill} from 'remotion';
import {TransitionSeries, linearTiming} from '@remotion/transitions';
import {fade} from '@remotion/transitions/fade';
import {slide} from '@remotion/transitions/slide';
import {T} from './theme';
import {Intro, INTRO_DURATION} from './scenes/Intro';
import {Dashboard, DASHBOARD_DURATION} from './scenes/Dashboard';
import {Architecture, ARCHI_DURATION} from './scenes/Architecture';
import {Ml, ML_DURATION} from './scenes/Ml';
import {AlertsExports, ALERTS_DURATION} from './scenes/AlertsExports';
import {Copilot, COPILOT_DURATION} from './scenes/Copilot';
import {Outro, OUTRO_DURATION} from './scenes/Outro';

const SLIDE = 15;
const FADE = 12;

export const TOTAL_DURATION =
  INTRO_DURATION +
  DASHBOARD_DURATION +
  ARCHI_DURATION +
  ML_DURATION +
  ALERTS_DURATION +
  COPILOT_DURATION +
  OUTRO_DURATION -
  (SLIDE + FADE * 5);

export const PlatformVideo: React.FC = () => (
  <AbsoluteFill style={{backgroundColor: T.page}}>
    <TransitionSeries>
      <TransitionSeries.Sequence durationInFrames={INTRO_DURATION}>
        <Intro />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={slide({direction: 'from-bottom'})}
        timing={linearTiming({durationInFrames: SLIDE})}
      />
      <TransitionSeries.Sequence durationInFrames={DASHBOARD_DURATION}>
        <Dashboard />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={fade()}
        timing={linearTiming({durationInFrames: FADE})}
      />
      <TransitionSeries.Sequence durationInFrames={ARCHI_DURATION}>
        <Architecture />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={fade()}
        timing={linearTiming({durationInFrames: FADE})}
      />
      <TransitionSeries.Sequence durationInFrames={ML_DURATION}>
        <Ml />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={fade()}
        timing={linearTiming({durationInFrames: FADE})}
      />
      <TransitionSeries.Sequence durationInFrames={ALERTS_DURATION}>
        <AlertsExports />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={fade()}
        timing={linearTiming({durationInFrames: FADE})}
      />
      <TransitionSeries.Sequence durationInFrames={COPILOT_DURATION}>
        <Copilot />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition
        presentation={fade()}
        timing={linearTiming({durationInFrames: FADE})}
      />
      <TransitionSeries.Sequence durationInFrames={OUTRO_DURATION}>
        <Outro />
      </TransitionSeries.Sequence>
    </TransitionSeries>
  </AbsoluteFill>
);
