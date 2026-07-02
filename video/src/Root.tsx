import React from 'react';
import {Composition} from 'remotion';
import {PlatformVideo, TOTAL_DURATION} from './PlatformVideo';

export const RemotionRoot: React.FC = () => (
  <Composition
    id="PlatformVideo"
    component={PlatformVideo}
    durationInFrames={TOTAL_DURATION}
    fps={30}
    width={1920}
    height={1080}
  />
);
