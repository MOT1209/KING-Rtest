import React from 'react';
import { Crown, Film, Video, Settings, Layers, Info, Globe } from 'lucide-react';
import { Language, RecordingState } from '../types/recorder';
import { formatDuration } from '../utils/recordingUtils';

interface NavigationHeaderProps {
  activeTab: 'recorder' | 'library' | 'settings' | 'solution' | 'about';
  onTabChange: (tab: 'recorder' | 'library' | 'settings' | 'solution' | 'about') => void;
  recordingCount: number;
  recordingState: RecordingState;
  recordingDuration: number;
  language: Language;
  onLanguageChange: (lang: Language) => void;
  isRtl: boolean;
  t: Record<string, string>;
}

export const NavigationHeader: React.FC<NavigationHeaderProps> = ({
  activeTab,
  onTabChange,
  recordingCount,
  recordingState,
  recordingDuration,
  language,
  onLanguageChange,
  isRtl,
  t,
}) => {
  const isRecording = recordingState === 'recording';
  const isPaused = recordingState === 'paused';

  return (
    <header
      className="bg-[#0f1116] border-b border-[#222530] sticky top-0 z-40 backdrop-blur-md px-4 md:px-8 py-3 flex items-center justify-between"
      dir={isRtl ? 'rtl' : 'ltr'}
    >
      {/* Brand */}
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => onTabChange('recorder')}
          className="flex items-center gap-2.5 focus:outline-none group text-left"
        >
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-[#d4af37] to-[#aa8210] p-0.5 shadow-md shadow-amber-500/10 group-hover:scale-105 transition-transform flex items-center justify-center">
            <div className="w-full h-full bg-[#12141a] rounded-[10px] flex items-center justify-center text-[#d4af37]">
              <Crown className="w-6 h-6" />
            </div>
          </div>
          <div>
            <div className="text-base font-extrabold tracking-wider text-white flex items-center gap-2">
              <span>{t.appName}</span>
              <span className="text-[10px] font-mono px-1.5 py-0.5 bg-[#1f1d14] text-[#fce084] rounded border border-[#d4af37]/30">
                1.0 MVP
              </span>
            </div>
            <div className="text-[10px] text-slate-400 font-medium tracking-wide">
              {t.tagline}
            </div>
          </div>
        </button>
      </div>

      {/* Nav Tabs */}
      <nav className="hidden lg:flex items-center bg-[#151821] p-1 rounded-xl border border-[#272b38]">
        <button
          type="button"
          onClick={() => onTabChange('recorder')}
          className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'recorder'
              ? 'bg-[#d4af37] text-black shadow-md'
              : 'text-slate-300 hover:text-white hover:bg-white/5'
          }`}
        >
          <Video className="w-3.5 h-3.5" />
          <span>{t.navRecorder}</span>
        </button>

        <button
          type="button"
          onClick={() => onTabChange('library')}
          className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'library'
              ? 'bg-[#d4af37] text-black shadow-md'
              : 'text-slate-300 hover:text-white hover:bg-white/5'
          }`}
        >
          <Film className="w-3.5 h-3.5" />
          <span>{t.navRecordings}</span>
          {recordingCount > 0 && (
            <span
              className={`text-[10px] px-1.5 py-0.2 rounded-full ${
                activeTab === 'library' ? 'bg-black/30 text-black' : 'bg-[#d4af37]/20 text-[#fce084]'
              }`}
            >
              {recordingCount}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => onTabChange('solution')}
          className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'solution'
              ? 'bg-[#d4af37] text-black shadow-md'
              : 'text-slate-300 hover:text-white hover:bg-white/5'
          }`}
        >
          <Layers className="w-3.5 h-3.5" />
          <span>{t.navSolution}</span>
        </button>

        <button
          type="button"
          onClick={() => onTabChange('settings')}
          className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'settings'
              ? 'bg-[#d4af37] text-black shadow-md'
              : 'text-slate-300 hover:text-white hover:bg-white/5'
          }`}
        >
          <Settings className="w-3.5 h-3.5" />
          <span>{t.navSettings}</span>
        </button>

        <button
          type="button"
          onClick={() => onTabChange('about')}
          className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'about'
              ? 'bg-[#d4af37] text-black shadow-md'
              : 'text-slate-300 hover:text-white hover:bg-white/5'
          }`}
        >
          <Info className="w-3.5 h-3.5" />
          <span>{t.navAbout}</span>
        </button>
      </nav>

      {/* Right side: Recording chip & Language switch */}
      <div className="flex items-center gap-3">
        {/* Active recording indicator chip */}
        {(isRecording || isPaused) && (
          <div
            onClick={() => onTabChange('recorder')}
            className={`cursor-pointer flex items-center gap-2 px-3 py-1.5 rounded-xl border text-xs font-mono font-bold transition-all ${
              isRecording
                ? 'bg-red-500/20 border-red-500/40 text-red-400'
                : 'bg-amber-500/20 border-amber-500/40 text-amber-300'
            }`}
          >
            <span
              className={`w-2 h-2 rounded-full ${
                isRecording ? 'bg-red-500 animate-rec-pulse' : 'bg-amber-400'
              }`}
            />
            <span>{isRecording ? 'REC' : 'PAUSED'}</span>
            <span className="text-white">{formatDuration(recordingDuration)}</span>
          </div>
        )}

        {/* Language switch */}
        <div className="flex items-center bg-[#151821] p-1 rounded-xl border border-[#272b38]">
          <button
            type="button"
            onClick={() => onLanguageChange(language === 'en' ? 'ar' : 'en')}
            className="flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium text-slate-300 hover:text-[#d4af37] transition-colors"
          >
            <Globe className="w-3.5 h-3.5 text-[#d4af37]" />
            <span>{language === 'en' ? 'العربية' : 'English'}</span>
          </button>
        </div>
      </div>
    </header>
  );
};
