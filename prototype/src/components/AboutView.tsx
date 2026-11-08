import React from 'react';
import { Crown, CheckCircle2, Shield, Cpu, Monitor, Radio, Info } from 'lucide-react';

interface AboutViewProps {
  isRtl: boolean;
  t: Record<string, string>;
}

export const AboutView: React.FC<AboutViewProps> = ({ isRtl, t }) => {
  const specs = [
    { label: 'Operating System', value: 'Windows 10 (19041+) / Windows 11' },
    { label: 'UI Framework', value: 'WinUI 3 + Windows App SDK 1.5' },
    { label: 'Capture API', value: 'Windows.Graphics.Capture + Direct3D 11' },
    { label: 'Audio Engine', value: 'WASAPI System Loopback + Microphone (48kHz)' },
    { label: 'Encoding Engine', value: 'Windows Media Foundation (Hardware H.264 / AAC)' },
    { label: 'Clock Synchronization', value: 'High-Resolution Monotonic Clock (QueryPerformanceCounter)' },
  ];

  return (
    <div className="flex flex-col gap-6 w-full max-w-5xl mx-auto p-4 md:p-6" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Brand Hero */}
      <div className="relative bg-[#13151b] border border-[#242834] rounded-2xl p-8 shadow-2xl overflow-hidden flex flex-col items-center text-center">
        {/* Subtle radial glow */}
        <div className="absolute top-0 w-96 h-96 bg-gradient-to-b from-[#d4af37]/15 to-transparent rounded-full blur-3xl pointer-events-none -translate-y-1/2" />

        <div className="relative z-10 flex flex-col items-center">
          <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#d4af37] to-[#aa8210] p-0.5 shadow-xl shadow-amber-500/20 mb-4 flex items-center justify-center">
            <div className="w-full h-full bg-[#12141a] rounded-[14px] flex items-center justify-center text-[#d4af37]">
              <Crown className="w-9 h-9" />
            </div>
          </div>

          <h1 className="text-3xl font-extrabold text-white tracking-wider">
            {t.appName}
          </h1>
          <p className="text-[#d4af37] font-medium text-sm tracking-widest uppercase mt-1">
            {t.tagline}
          </p>
          <span className="inline-block mt-3 px-3 py-1 bg-[#1e1c14] border border-[#d4af37]/30 text-[#fce084] font-mono text-xs rounded-full">
            {t.version}
          </span>
        </div>
      </div>

      {/* Verification Matrix */}
      <div className="bg-[#13151b] border border-[#242834] rounded-2xl p-6 shadow-xl flex flex-col gap-5">
        <h3 className="text-sm font-bold text-white uppercase tracking-wider flex items-center gap-2">
          <Shield className="w-4 h-4 text-[#d4af37]" />
          <span>System Compatibility & Engine Verification</span>
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {specs.map((spec) => (
            <div
              key={spec.label}
              className="bg-[#0e1015] border border-[#202430] p-4 rounded-xl flex items-center justify-between"
            >
              <div>
                <div className="text-[11px] text-slate-400">{spec.label}</div>
                <div className="text-xs font-semibold text-slate-200 mt-0.5">{spec.value}</div>
              </div>
              <CheckCircle2 className="w-5 h-5 text-emerald-400 flex-shrink-0" />
            </div>
          ))}
        </div>
      </div>

      {/* Engineering Pillars */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
        <div className="bg-[#13151b] border border-[#242834] p-5 rounded-2xl shadow-xl flex flex-col gap-2">
          <Cpu className="w-6 h-6 text-[#d4af37]" />
          <h4 className="text-sm font-bold text-white">Direct3D 11 Zero-Copy</h4>
          <p className="text-xs text-slate-400 leading-relaxed">
            Screen frames are acquired straight to GPU textures via Direct3D11CaptureFramePool, eliminating costly CPU RAM copies.
          </p>
        </div>

        <div className="bg-[#13151b] border border-[#242834] p-5 rounded-2xl shadow-xl flex flex-col gap-2">
          <Radio className="w-6 h-6 text-[#d4af37]" />
          <h4 className="text-sm font-bold text-white">Unified Timeline Sync</h4>
          <p className="text-xs text-slate-400 leading-relaxed">
            Monotonic clock prevents audio desynchronization, webcam drift, or stuttering across prolonged recording sessions.
          </p>
        </div>

        <div className="bg-[#13151b] border border-[#242834] p-5 rounded-2xl shadow-xl flex flex-col gap-2">
          <Monitor className="w-6 h-6 text-[#d4af37]" />
          <h4 className="text-sm font-bold text-white">Composited Overlay</h4>
          <p className="text-xs text-slate-400 leading-relaxed">
            Webcam overlay is composited directly into the primary video stream with rounded-rectangle or circular masks.
          </p>
        </div>
      </div>
    </div>
  );
};
