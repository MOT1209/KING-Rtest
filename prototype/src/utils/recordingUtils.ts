/**
 * Pure utility functions for KING RECORDER
 * Decoupled from UI for deterministic logic and unit testing.
 */

export interface Dimensions {
  width: number;
  height: number;
}

export interface Position {
  x: number;
  y: number;
}

export type OverlayShape = 'rounded-rect' | 'circle';
export type CornerPosition = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | 'custom';

export interface OverlayConfig {
  shape: OverlayShape;
  sizePercent: number; // e.g. 15 to 40% of screen height/width
  corner: CornerPosition;
  customPos?: Position;
  margin: number;
  borderWidth: number;
  borderColor: string;
}

/**
 * Calculates accurate pixel bounds for webcam overlay compositing
 */
export function calculateOverlayGeometry(
  screenWidth: number,
  screenHeight: number,
  config: OverlayConfig
): { x: number; y: number; width: number; height: number; borderRadius: number } {
  // Base size on percentage of minimum screen dimension
  const baseSize = Math.round(Math.min(screenWidth, screenHeight) * (config.sizePercent / 100));
  
  let width = baseSize;
  let height = baseSize;

  if (config.shape === 'rounded-rect') {
    // 16:9 aspect ratio for rounded rect camera
    height = Math.round(width * (9 / 16));
  } else {
    // Circle is 1:1
    height = width;
  }

  let x = config.margin;
  let y = config.margin;

  if (config.corner === 'top-left') {
    x = config.margin;
    y = config.margin;
  } else if (config.corner === 'top-right') {
    x = screenWidth - width - config.margin;
    y = config.margin;
  } else if (config.corner === 'bottom-left') {
    x = config.margin;
    y = screenHeight - height - config.margin;
  } else if (config.corner === 'bottom-right') {
    x = screenWidth - width - config.margin;
    y = screenHeight - height - config.margin;
  } else if (config.corner === 'custom' && config.customPos) {
    // Clamp inside screen bounds
    x = Math.max(0, Math.min(screenWidth - width, config.customPos.x));
    y = Math.max(0, Math.min(screenHeight - height, config.customPos.y));
  }

  const borderRadius = config.shape === 'circle' ? Math.round(width / 2) : 16;

  return { x, y, width, height, borderRadius };
}

/**
 * Formats seconds into HH:MM:SS
 */
export function formatDuration(totalSeconds: number): string {
  const safeSeconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(safeSeconds / 3600);
  const minutes = Math.floor((safeSeconds % 3600) / 60);
  const seconds = safeSeconds % 60;

  const pad = (num: number) => num.toString().padStart(2, '0');
  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

/**
 * Formats byte size into human readable string
 */
export function formatFileSize(bytes: number): string {
  if (bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const val = (bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1);
  return `${val} ${units[i]}`;
}

/**
 * Generates unique recording filename according to KING RECORDER specification
 */
export function generateRecordingFilename(prefix: string = 'KING_REC', ext: string = 'mp4', date: Date = new Date()): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hours = pad(date.getHours());
  const mins = pad(date.getMinutes());
  const secs = pad(date.getSeconds());

  return `${prefix}_${year}${month}${day}_${hours}${mins}${secs}.${ext.replace(/^\./, '')}`;
}

/**
 * Resolves video dimensions from target setting
 */
export function resolveTargetResolution(setting: 'original' | '720p' | '1080p' | '4k', sourceWidth: number, sourceHeight: number): Dimensions {
  switch (setting) {
    case '720p':
      return { width: 1280, height: 720 };
    case '1080p':
      return { width: 1920, height: 1080 };
    case '4k':
      return { width: 3840, height: 2160 };
    case 'original':
    default:
      return { width: sourceWidth || 1920, height: sourceHeight || 1080 };
  }
}

/**
 * DPI Coordinate mapping for custom selection area
 */
export function mapDpiCoordinates(
  rawRect: { x: number; y: number; width: number; height: number },
  dpiScale: number = 1.0,
  maxBounds: Dimensions = { width: 1920, height: 1080 }
) {
  const scale = Math.max(0.25, dpiScale);
  const x = Math.max(0, Math.round(rawRect.x * scale));
  const y = Math.max(0, Math.round(rawRect.y * scale));
  const width = Math.min(maxBounds.width - x, Math.round(rawRect.width * scale));
  const height = Math.min(maxBounds.height - y, Math.round(rawRect.height * scale));

  return { x, y, width: Math.max(16, width), height: Math.max(16, height) };
}

/**
 * Prevents audio clipping when mixing microphone and system audio
 */
export function calculateMixedGain(micVolume: number, sysVolume: number): { micGain: number; sysGain: number; peakDamping: number } {
  // Normalize 0 - 100 to 0 - 1.0
  const m = Math.max(0, Math.min(100, micVolume)) / 100;
  const s = Math.max(0, Math.min(100, sysVolume)) / 100;

  // If combined sum exceeds 1.0, soft damp to prevent harsh digital clipping
  const sum = m + s;
  const peakDamping = sum > 1.0 ? 1.0 / Math.sqrt(sum) : 1.0;

  return {
    micGain: Number((m * peakDamping).toFixed(3)),
    sysGain: Number((s * peakDamping).toFixed(3)),
    peakDamping: Number(peakDamping.toFixed(3)),
  };
}
