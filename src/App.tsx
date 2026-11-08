/**
 * KING RECORDER — Professional Windows Screen Recording Studio
 * Main Application Entry Point
 */

import React, { useState, useEffect } from 'react';
import { NavigationHeader } from './components/NavigationHeader';
import { RecorderDashboard } from './components/RecorderDashboard';
import { RecordingsLibrary } from './components/RecordingsLibrary';
import { SettingsView } from './components/SettingsView';
import { WindowsSolutionExplorer } from './components/WindowsSolutionExplorer';
import { AboutView } from './components/AboutView';
import {
  RecordingItem,
  VideoSettings,
  AudioSettings,
  Language,
  RecordingState,
} from './types/recorder';
import { translations } from './services/localization';

export default function App() {
  const [activeTab, setActiveTab] = useState<'recorder' | 'library' | 'settings' | 'solution' | 'about'>('recorder');
  const [language, setLanguage] = useState<Language>('ar');

  // Video Settings
  const [videoSettings, setVideoSettings] = useState<VideoSettings>({
    resolution: '1080p',
    frameRate: 60,
    quality: 'high',
    codec: 'h264',
    hardwareAcceleration: 'auto',
    storagePath: 'C:\\Users\\User\\Videos\\KING Recorder\\',
  });

  // Audio Settings
  const [audioSettings, setAudioSettings] = useState<AudioSettings>({
    micEnabled: true,
    micDeviceId: 'default',
    micVolume: 85,
    systemAudioEnabled: true,
    systemAudioVolume: 90,
    sampleRate: 48000,
    echoCancellation: true,
    noiseSuppression: true,
  });

  // Recording State & Library
  const [recordings, setRecordings] = useState<RecordingItem[]>([
    {
      id: 'demo-1',
      title: 'KING_REC_20261108_073000',
      filename: 'KING_REC_20261108_073000.mp4',
      type: 'video',
      durationSeconds: 142, // 2m 22s
      fileSizeBytes: 34500000, // ~34.5 MB
      createdAt: '11/8/2026, 7:30 AM',
      thumbnailUrl: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=800&auto=format&fit=crop&q=80',
      resolution: '1920×1080',
      fps: 60,
      storagePath: 'C:\\Users\\User\\Videos\\KING Recorder\\',
    },
    {
      id: 'demo-2',
      title: 'KING_REC_20261107_182015',
      filename: 'KING_REC_20261107_182015.mp4',
      type: 'video',
      durationSeconds: 78,
      fileSizeBytes: 18200000,
      createdAt: '11/7/2026, 6:20 PM',
      thumbnailUrl: 'https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=800&auto=format&fit=crop&q=80',
      resolution: '1920×1080',
      fps: 60,
      storagePath: 'C:\\Users\\User\\Videos\\KING Recorder\\',
    },
    {
      id: 'demo-3',
      title: 'KING_SHOT_20261108_071530',
      filename: 'KING_SHOT_20261108_071530.png',
      type: 'screenshot',
      durationSeconds: 0,
      fileSizeBytes: 2450000,
      createdAt: '11/8/2026, 7:15 AM',
      thumbnailUrl: 'https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=800&auto=format&fit=crop&q=80',
      resolution: '1920×1080',
      storagePath: 'C:\\Users\\User\\Videos\\KING Recorder\\',
    },
  ]);

  const [currentRecordingState, setCurrentRecordingState] = useState<RecordingState>('idle');
  const [currentRecordingDuration, setCurrentRecordingDuration] = useState(0);

  // Set HTML dir attribute for RTL when Arabic is selected
  useEffect(() => {
    document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = language;
  }, [language]);

  const t = translations[language];
  const isRtl = language === 'ar';

  const handleRecordingSaved = (item: RecordingItem) => {
    setRecordings((prev) => [item, ...prev]);
  };

  const handleDeleteRecording = (id: string) => {
    setRecordings((prev) => prev.filter((r) => r.id !== id));
  };

  const handleRenameRecording = (id: string, newTitle: string) => {
    setRecordings((prev) =>
      prev.map((r) =>
        r.id === id
          ? {
              ...r,
              title: newTitle,
              filename: `${newTitle}.${r.type === 'video' ? 'mp4' : 'png'}`,
            }
          : r
      )
    );
  };

  return (
    <div className="min-h-screen bg-[#0a0b0e] text-slate-100 flex flex-col selection:bg-[#d4af37]/30 selection:text-white">
      {/* Navigation Header */}
      <NavigationHeader
        activeTab={activeTab}
        onTabChange={(tab) => setActiveTab(tab)}
        recordingCount={recordings.length}
        recordingState={currentRecordingState}
        recordingDuration={currentRecordingDuration}
        language={language}
        onLanguageChange={(lang) => setLanguage(lang)}
        isRtl={isRtl}
        t={t}
      />

      {/* Main Content Area */}
      <main className="flex-1 pb-12">
        {activeTab === 'recorder' && (
          <RecorderDashboard
            onRecordingSaved={handleRecordingSaved}
            videoSettings={videoSettings}
            audioSettings={audioSettings}
            onUpdateAudioSettings={(patch) =>
              setAudioSettings((prev) => ({ ...prev, ...patch }))
            }
            isRtl={isRtl}
            t={t}
          />
        )}

        {activeTab === 'library' && (
          <RecordingsLibrary
            recordings={recordings}
            onDeleteRecording={handleDeleteRecording}
            onRenameRecording={handleRenameRecording}
            storagePath={videoSettings.storagePath}
            isRtl={isRtl}
            t={t}
          />
        )}

        {activeTab === 'solution' && (
          <WindowsSolutionExplorer isRtl={isRtl} t={t} />
        )}

        {activeTab === 'settings' && (
          <SettingsView
            videoSettings={videoSettings}
            onUpdateVideoSettings={(patch) =>
              setVideoSettings((prev) => ({ ...prev, ...patch }))
            }
            audioSettings={audioSettings}
            onUpdateAudioSettings={(patch) =>
              setAudioSettings((prev) => ({ ...prev, ...patch }))
            }
            language={language}
            onLanguageChange={(lang) => setLanguage(lang)}
            isRtl={isRtl}
            t={t}
          />
        )}

        {activeTab === 'about' && (
          <AboutView isRtl={isRtl} t={t} />
        )}
      </main>

      {/* Persistent Windows Fluent Bottom Status Bar */}
      <footer className="border-t border-[#1a1d26] bg-[#0c0d12] px-6 py-2 flex flex-wrap items-center justify-between text-[11px] text-slate-500 font-mono">
        <div className="flex items-center gap-3">
          <span className="flex items-center gap-1.5 text-[#d4af37]">
            <span className="w-1.5 h-1.5 rounded-full bg-[#d4af37]" />
            <span>Direct3D 11 Ready</span>
          </span>
          <span>•</span>
          <span>WASAPI 48kHz</span>
          <span>•</span>
          <span>H.264 NVENC/QSV</span>
        </div>

        <div className="flex items-center gap-4">
          <span>{videoSettings.storagePath}</span>
          <span>•</span>
          <span className="text-slate-400">KING RECORDER v1.0.0 MVP</span>
        </div>
      </footer>
    </div>
  );
}
