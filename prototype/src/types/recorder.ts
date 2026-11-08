import { OverlayShape, CornerPosition, Position } from '../utils/recordingUtils';

export type RecordingMode = 'fullscreen' | 'window' | 'custom';

export type RecordingState =
  | 'idle'
  | 'initializing'
  | 'recording'
  | 'paused'
  | 'stopping'
  | 'finalizing'
  | 'completed'
  | 'error';

export interface DisplaySource {
  id: string;
  name: string;
  resolution: string;
  isPrimary: boolean;
  refreshRate: number;
  scale: number;
}

export interface WindowSource {
  id: string;
  title: string;
  processName: string;
  icon: string;
  thumbnailUrl?: string;
  bounds: { width: number; height: number };
}

export interface MediaDeviceItem {
  deviceId: string;
  label: string;
  kind: 'audioinput' | 'videoinput' | 'audiooutput';
}

export interface CustomArea {
  x: number;
  y: number;
  width: number;
  height: number;
  dpiScale: number;
}

export interface WebcamOverlaySettings {
  enabled: boolean;
  shape: OverlayShape;
  sizePercent: number; // 15 - 45%
  corner: CornerPosition;
  customPos?: Position;
  margin: number;
  borderWidth: number;
  borderColor: string;
  opacity: number;
}

export interface AudioSettings {
  micEnabled: boolean;
  micDeviceId: string;
  micVolume: number; // 0 - 100
  systemAudioEnabled: boolean;
  systemAudioVolume: number; // 0 - 100
  sampleRate: 44100 | 48000;
  echoCancellation: boolean;
  noiseSuppression: boolean;
}

export interface VideoSettings {
  resolution: 'original' | '720p' | '1080p' | '4k';
  frameRate: 30 | 60;
  quality: 'low' | 'medium' | 'high';
  codec: 'h264' | 'hevc' | 'vp9';
  hardwareAcceleration: 'auto' | 'nvenc' | 'quicksync' | 'amf' | 'software';
  storagePath: string;
}

export interface RecordingItem {
  id: string;
  title: string;
  filename: string;
  type: 'video' | 'screenshot';
  durationSeconds: number;
  fileSizeBytes: number;
  createdAt: string;
  thumbnailUrl: string;
  blobUrl?: string;
  blob?: Blob;
  resolution: string;
  fps?: number;
  storagePath: string;
}

export type Language = 'en' | 'ar';
