import React, { useState } from 'react';
import {
  Settings,
  HardDrive,
  Video,
  Mic,
  Keyboard,
  Globe,
  Cpu,
  Check,
  FolderOpen,
  Volume2,
} from 'lucide-react';
import { VideoSettings, AudioSettings, Language } from '../types/recorder';

interface SettingsViewProps {
  videoSettings: VideoSettings;
  onUpdateVideoSettings: (settings: Partial<VideoSettings>) => void;
  audioSettings: AudioSettings;
  onUpdateAudioSettings: (settings: Partial<AudioSettings>) => void;
  language: Language;
  onLanguageChange: (lang: Language) => void;
  isRtl: boolean;
  t: Record<string, string>;
}

export const SettingsView: React.FC<SettingsViewProps> = ({
  videoSettings,
  onUpdateVideoSettings,
  audioSettings,
  onUpdateAudioSettings,
  language,
  onLanguageChange,
  isRtl,
  t,
}) => {
  const [activeTab, setActiveTab] = useState<'general' | 'video' | 'audio' | 'shortcuts'>('general');
  const [showSavedToast, setShowSavedToast] = useState(false);

  const triggerSaveToast = () => {
    setShowSavedToast(true);
    setTimeout(() => setShowSavedToast(false), 2500);
  };

  return (
    <div className="flex flex-col gap-6 w-full max-w-6xl mx-auto p-4 md:p-6" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Header */}
      <div className="bg-[#13151b] border border-[#242834] rounded-2xl p-5 shadow-xl flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#d4af37]/20 border border-[#d4af37]/40 flex items-center justify-center text-[#d4af37]">
            <Settings className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white tracking-wide">{t.settingsTitle}</h2>
            <p className="text-xs text-slate-400">Configure recording pipeline, encoder parameters, audio devices & hotkeys</p>
          </div>
        </div>

        {showSavedToast && (
          <div className="flex items-center gap-1.5 px-3 py-1.5 bg-green-500/20 text-green-300 border border-green-500/30 rounded-lg text-xs font-medium animate-fade-in">
            <Check className="w-4 h-4" />
            <span>Settings updated</span>
          </div>
        )}
      </div>

      {/* Tabs navigation */}
      <div className="flex items-center gap-2 border-b border-[#242834] pb-2 overflow-x-auto">
        <button
          type="button"
          onClick={() => setActiveTab('general')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all ${
            activeTab === 'general'
              ? 'bg-[#d4af37] text-black font-semibold shadow'
              : 'text-slate-400 hover:text-white hover:bg-[#1a1d26]'
          }`}
        >
          <HardDrive className="w-4 h-4" />
          <span>{t.generalSettings}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('video')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all ${
            activeTab === 'video'
              ? 'bg-[#d4af37] text-black font-semibold shadow'
              : 'text-slate-400 hover:text-white hover:bg-[#1a1d26]'
          }`}
        >
          <Video className="w-4 h-4" />
          <span>{t.videoEncodingSettings}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('audio')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all ${
            activeTab === 'audio'
              ? 'bg-[#d4af37] text-black font-semibold shadow'
              : 'text-slate-400 hover:text-white hover:bg-[#1a1d26]'
          }`}
        >
          <Mic className="w-4 h-4" />
          <span>{t.audioSettingsTab}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('shortcuts')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all ${
            activeTab === 'shortcuts'
              ? 'bg-[#d4af37] text-black font-semibold shadow'
              : 'text-slate-400 hover:text-white hover:bg-[#1a1d26]'
          }`}
        >
          <Keyboard className="w-4 h-4" />
          <span>{t.shortcutsTab}</span>
        </button>
      </div>

      {/* Tab Contents */}
      <div className="bg-[#13151b] border border-[#242834] rounded-2xl p-6 shadow-xl flex flex-col gap-6">
        {/* GENERAL TAB */}
        {activeTab === 'general' && (
          <div className="flex flex-col gap-6">
            <div>
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                {t.defaultSavePath}
              </label>
              <div className="flex items-center gap-3">
                <input
                  type="text"
                  value={videoSettings.storagePath}
                  onChange={(e) => {
                    onUpdateVideoSettings({ storagePath: e.target.value });
                    triggerSaveToast();
                  }}
                  className="flex-1 bg-[#0d0e12] border border-[#272b38] rounded-xl px-4 py-2.5 text-xs text-white font-mono focus:outline-none focus:border-[#d4af37]"
                />
                <button
                  type="button"
                  onClick={() => {
                    onUpdateVideoSettings({ storagePath: 'D:\\KING_Recordings\\' });
                    triggerSaveToast();
                  }}
                  className="flex items-center gap-2 px-4 py-2.5 bg-[#1b1e27] hover:bg-[#252a38] text-slate-200 border border-[#2d3344] rounded-xl text-xs font-medium transition-colors"
                >
                  <FolderOpen className="w-4 h-4 text-[#d4af37]" />
                  <span>{t.browsePath}</span>
                </button>
              </div>
              <p className="text-[11px] text-slate-500 mt-1.5">
                Default folder where finalized MP4 recordings and PNG screenshots are automatically saved.
              </p>
            </div>

            <div className="border-t border-[#1f232e] pt-5">
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-3">
                {t.languageSetting}
              </label>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 max-w-md">
                <button
                  type="button"
                  onClick={() => onLanguageChange('en')}
                  className={`flex items-center justify-between p-3.5 rounded-xl border transition-all text-xs ${
                    language === 'en'
                      ? 'bg-[#1e1b12] border-[#d4af37] text-white shadow'
                      : 'bg-[#0d0e12] border-[#222633] text-slate-400 hover:text-white'
                  }`}
                >
                  <span className="font-medium">English (United States)</span>
                  {language === 'en' && <Check className="w-4 h-4 text-[#d4af37]" />}
                </button>

                <button
                  type="button"
                  onClick={() => onLanguageChange('ar')}
                  className={`flex items-center justify-between p-3.5 rounded-xl border transition-all text-xs ${
                    language === 'ar'
                      ? 'bg-[#1e1b12] border-[#d4af37] text-white shadow'
                      : 'bg-[#0d0e12] border-[#222633] text-slate-400 hover:text-white'
                  }`}
                >
                  <span className="font-medium">العربية (RTL)</span>
                  {language === 'ar' && <Check className="w-4 h-4 text-[#d4af37]" />}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* VIDEO TAB */}
        {activeTab === 'video' && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Resolution */}
            <div>
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                {t.resolution}
              </label>
              <select
                value={videoSettings.resolution}
                onChange={(e) => {
                  onUpdateVideoSettings({ resolution: e.target.value as any });
                  triggerSaveToast();
                }}
                className="w-full bg-[#0d0e12] border border-[#272b38] rounded-xl px-4 py-2.5 text-xs text-white focus:outline-none focus:border-[#d4af37]"
              >
                <option value="original">Original Screen Resolution (Native)</option>
                <option value="1080p">1080p Full HD (1920 × 1080)</option>
                <option value="720p">720p Standard HD (1280 × 720)</option>
                <option value="4k">4K Ultra HD (3840 × 2160)</option>
              </select>
            </div>

            {/* Frame Rate */}
            <div>
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                {t.fps}
              </label>
              <div className="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => {
                    onUpdateVideoSettings({ frameRate: 60 });
                    triggerSaveToast();
                  }}
                  className={`py-2.5 rounded-xl text-xs font-semibold border transition-all ${
                    videoSettings.frameRate === 60
                      ? 'bg-[#d4af37] text-black border-[#d4af37]'
                      : 'bg-[#0d0e12] text-slate-300 border-[#272b38] hover:text-white'
                  }`}
                >
                  60 FPS (Ultra Smooth)
                </button>
                <button
                  type="button"
                  onClick={() => {
                    onUpdateVideoSettings({ frameRate: 30 });
                    triggerSaveToast();
                  }}
                  className={`py-2.5 rounded-xl text-xs font-semibold border transition-all ${
                    videoSettings.frameRate === 30
                      ? 'bg-[#d4af37] text-black border-[#d4af37]'
                      : 'bg-[#0d0e12] text-slate-300 border-[#272b38] hover:text-white'
                  }`}
                >
                  30 FPS (Standard)
                </button>
              </div>
            </div>

            {/* Quality Preset */}
            <div>
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                {t.quality}
              </label>
              <div className="grid grid-cols-3 gap-2">
                {(['high', 'medium', 'low'] as const).map((q) => (
                  <button
                    key={q}
                    type="button"
                    onClick={() => {
                      onUpdateVideoSettings({ quality: q });
                      triggerSaveToast();
                    }}
                    className={`py-2 rounded-xl text-xs font-medium border capitalize transition-all ${
                      videoSettings.quality === q
                        ? 'bg-[#d4af37] text-black font-semibold border-[#d4af37]'
                        : 'bg-[#0d0e12] text-slate-400 border-[#272b38] hover:text-white'
                    }`}
                  >
                    {q} ({q === 'high' ? '18 Mbps' : q === 'medium' ? '10 Mbps' : '5 Mbps'})
                  </button>
                ))}
              </div>
            </div>

            {/* Hardware Acceleration */}
            <div>
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                <span className="flex items-center gap-1.5">
                  <Cpu className="w-3.5 h-3.5 text-[#d4af37]" />
                  <span>{t.hardwareAcc}</span>
                </span>
              </label>
              <select
                value={videoSettings.hardwareAcceleration}
                onChange={(e) => {
                  onUpdateVideoSettings({ hardwareAcceleration: e.target.value as any });
                  triggerSaveToast();
                }}
                className="w-full bg-[#0d0e12] border border-[#272b38] rounded-xl px-4 py-2.5 text-xs text-white focus:outline-none focus:border-[#d4af37]"
              >
                <option value="auto">Auto Detect (Recommended)</option>
                <option value="nvenc">NVIDIA NVENC (GeForce / RTX)</option>
                <option value="quicksync">Intel QuickSync Video (QSV)</option>
                <option value="amf">AMD AMF (Radeon GPU)</option>
                <option value="software">Software CPU Fallback (Microsoft H.264 MFT)</option>
              </select>
            </div>
          </div>
        )}

        {/* AUDIO TAB */}
        {activeTab === 'audio' && (
          <div className="flex flex-col gap-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Sample rate */}
              <div>
                <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                  Sample Rate
                </label>
                <select
                  value={audioSettings.sampleRate}
                  onChange={(e) => {
                    onUpdateAudioSettings({ sampleRate: parseInt(e.target.value) as any });
                    triggerSaveToast();
                  }}
                  className="w-full bg-[#0d0e12] border border-[#272b38] rounded-xl px-4 py-2.5 text-xs text-white focus:outline-none focus:border-[#d4af37]"
                >
                  <option value={48000}>48,000 Hz (Professional Broadcast Standard)</option>
                  <option value={44100}>44,100 Hz (CD Audio Standard)</option>
                </select>
              </div>

              {/* Toggles */}
              <div className="flex flex-col justify-end gap-3">
                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={audioSettings.noiseSuppression}
                    onChange={(e) => {
                      onUpdateAudioSettings({ noiseSuppression: e.target.checked });
                      triggerSaveToast();
                    }}
                    className="w-4 h-4 accent-[#d4af37] rounded"
                  />
                  <span className="text-xs text-slate-300 font-medium">Active Microphone Noise Suppression</span>
                </label>

                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={audioSettings.echoCancellation}
                    onChange={(e) => {
                      onUpdateAudioSettings({ echoCancellation: e.target.checked });
                      triggerSaveToast();
                    }}
                    className="w-4 h-4 accent-[#d4af37] rounded"
                  />
                  <span className="text-xs text-slate-300 font-medium">Acoustic Echo Cancellation (AEC)</span>
                </label>
              </div>
            </div>

            <div className="p-4 bg-[#0d0e12] rounded-xl border border-[#222633] text-xs text-slate-400 flex items-center gap-3">
              <Volume2 className="w-5 h-5 text-[#d4af37] flex-shrink-0" />
              <span>
                Audio is captured simultaneously via WASAPI loopback and microphone input, mixed synchronously onto a 48kHz stereo stream to eliminate audio drift.
              </span>
            </div>
          </div>
        )}

        {/* SHORTCUTS TAB */}
        {activeTab === 'shortcuts' && (
          <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between p-3.5 bg-[#0d0e12] rounded-xl border border-[#222633]">
              <span className="text-xs font-medium text-slate-200">{t.hotkeyRecord}</span>
              <kbd className="px-3 py-1 bg-[#1c1f2b] text-[#d4af37] font-mono text-xs rounded border border-[#2f3549] shadow-sm">
                F9 / Ctrl + R
              </kbd>
            </div>

            <div className="flex items-center justify-between p-3.5 bg-[#0d0e12] rounded-xl border border-[#222633]">
              <span className="text-xs font-medium text-slate-200">{t.hotkeyPause}</span>
              <kbd className="px-3 py-1 bg-[#1c1f2b] text-[#d4af37] font-mono text-xs rounded border border-[#2f3549] shadow-sm">
                F10 / Ctrl + P
              </kbd>
            </div>

            <div className="flex items-center justify-between p-3.5 bg-[#0d0e12] rounded-xl border border-[#222633]">
              <span className="text-xs font-medium text-slate-200">{t.hotkeyScreenshot}</span>
              <kbd className="px-3 py-1 bg-[#1c1f2b] text-[#d4af37] font-mono text-xs rounded border border-[#2f3549] shadow-sm">
                F12 / Ctrl + Shift + S
              </kbd>
            </div>

            <div className="flex items-center justify-between p-3.5 bg-[#0d0e12] rounded-xl border border-[#222633]">
              <span className="text-xs font-medium text-slate-200">{t.hotkeyMute}</span>
              <kbd className="px-3 py-1 bg-[#1c1f2b] text-[#d4af37] font-mono text-xs rounded border border-[#2f3549] shadow-sm">
                Ctrl + M
              </kbd>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
