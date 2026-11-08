import test from 'node:test';
import assert from 'node:assert/strict';
import {
  calculateOverlayGeometry,
  formatDuration,
  formatFileSize,
  generateRecordingFilename,
  resolveTargetResolution,
  mapDpiCoordinates,
  calculateMixedGain,
} from './recordingUtils';

test('formatDuration formats seconds accurately', () => {
  assert.equal(formatDuration(0), '00:00:00');
  assert.equal(formatDuration(59), '00:00:59');
  assert.equal(formatDuration(65), '00:01:05');
  assert.equal(formatDuration(3665), '01:01:05');
  assert.equal(formatDuration(86400), '24:00:00');
});

test('formatFileSize formats byte quantities appropriately', () => {
  assert.equal(formatFileSize(0), '0 B');
  assert.equal(formatFileSize(1024), '1.0 KB');
  assert.equal(formatFileSize(1048576 * 15.5), '15.5 MB');
  assert.equal(formatFileSize(1073741824 * 2.3), '2.3 GB');
});

test('generateRecordingFilename matches naming convention', () => {
  const d = new Date(2026, 10, 8, 14, 30, 15);
  const name = generateRecordingFilename('KING_REC', 'mp4', d);
  assert.equal(name, 'KING_REC_20261108_143015.mp4');
});

test('calculateOverlayGeometry calculates correct bounds for rounded rectangle', () => {
  const geom = calculateOverlayGeometry(1920, 1080, {
    shape: 'rounded-rect',
    sizePercent: 20, // 20% of 1080 = 216 width
    corner: 'bottom-right',
    margin: 24,
    borderWidth: 2,
    borderColor: '#D4AF37',
  });

  assert.equal(geom.width, 216);
  assert.equal(geom.height, Math.round(216 * 9 / 16));
  assert.equal(geom.x, 1920 - 216 - 24);
  assert.equal(geom.y, 1080 - geom.height - 24);
  assert.equal(geom.borderRadius, 16);
});

test('calculateOverlayGeometry calculates correct bounds for circle', () => {
  const geom = calculateOverlayGeometry(1920, 1080, {
    shape: 'circle',
    sizePercent: 25, // 25% of 1080 = 270 width & height
    corner: 'top-left',
    margin: 20,
    borderWidth: 3,
    borderColor: '#D4AF37',
  });

  assert.equal(geom.width, 270);
  assert.equal(geom.height, 270);
  assert.equal(geom.x, 20);
  assert.equal(geom.y, 20);
  assert.equal(geom.borderRadius, 135);
});

test('resolveTargetResolution maps standard resolutions', () => {
  assert.deepEqual(resolveTargetResolution('720p', 1920, 1080), { width: 1280, height: 720 });
  assert.deepEqual(resolveTargetResolution('1080p', 1280, 720), { width: 1920, height: 1080 });
  assert.deepEqual(resolveTargetResolution('4k', 1920, 1080), { width: 3840, height: 2160 });
  assert.deepEqual(resolveTargetResolution('original', 2560, 1440), { width: 2560, height: 1440 });
});

test('mapDpiCoordinates scales and bounds rect correctly', () => {
  const mapped = mapDpiCoordinates({ x: 100, y: 100, width: 500, height: 400 }, 1.25, { width: 1920, height: 1080 });
  assert.equal(mapped.x, 125);
  assert.equal(mapped.y, 125);
  assert.equal(mapped.width, 625);
  assert.equal(mapped.height, 500);
});

test('calculateMixedGain prevents digital audio clipping', () => {
  const lowMix = calculateMixedGain(50, 40); // 0.5 + 0.4 = 0.9 <= 1.0
  assert.equal(lowMix.peakDamping, 1);
  assert.equal(lowMix.micGain, 0.5);
  assert.equal(lowMix.sysGain, 0.4);

  const highMix = calculateMixedGain(100, 100); // 1.0 + 1.0 = 2.0 > 1.0
  assert.ok(highMix.peakDamping < 1.0);
  assert.ok(highMix.micGain < 1.0);
  assert.ok(highMix.sysGain < 1.0);
});
