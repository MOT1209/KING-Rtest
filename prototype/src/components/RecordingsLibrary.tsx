import React, { useState } from 'react';
import {
  Film,
  Camera,
  Search,
  Play,
  Download,
  Trash2,
  Edit2,
  FolderOpen,
  Calendar,
  Clock,
  HardDrive,
  X,
  Maximize,
  Volume2,
  VolumeX,
  RotateCcw,
} from 'lucide-react';
import { RecordingItem } from '../types/recorder';
import { formatDuration, formatFileSize } from '../utils/recordingUtils';

interface RecordingsLibraryProps {
  recordings: RecordingItem[];
  onDeleteRecording: (id: string) => void;
  onRenameRecording: (id: string, newTitle: string) => void;
  storagePath: string;
  isRtl: boolean;
  t: Record<string, string>;
}

export const RecordingsLibrary: React.FC<RecordingsLibraryProps> = ({
  recordings,
  onDeleteRecording,
  onRenameRecording,
  storagePath,
  isRtl,
  t,
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<'all' | 'video' | 'screenshot'>('all');
  
  // Modals state
  const [activeItem, setActiveItem] = useState<RecordingItem | null>(null);
  const [isPlayerOpen, setIsPlayerOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<RecordingItem | null>(null);
  const [renameTarget, setRenameTarget] = useState<RecordingItem | null>(null);
  const [renameValue, setRenameValue] = useState('');
  const [folderNotice, setFolderNotice] = useState<string | null>(null);

  // Player controls state
  const [isPlaying, setIsPlaying] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [progress, setProgress] = useState(0);

  const filteredItems = recordings.filter((item) => {
    const matchesSearch =
      item.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.filename.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesFilter = filterType === 'all' || item.type === filterType;
    return matchesSearch && matchesFilter;
  });

  const totalBytes = recordings.reduce((acc, curr) => acc + curr.fileSizeBytes, 0);

  const handleOpenPlayer = (item: RecordingItem) => {
    setActiveItem(item);
    setIsPlayerOpen(true);
    setIsPlaying(true);
    setProgress(0);
  };

  const handleDownload = (item: RecordingItem) => {
    if (item.blobUrl) {
      const a = document.createElement('a');
      a.href = item.blobUrl;
      a.download = item.filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
    } else {
      // Fallback: create mock blob
      const content = item.type === 'video' ? 'KING RECORDER MP4 DATA' : 'KING RECORDER PNG DATA';
      const blob = new Blob([content], { type: item.type === 'video' ? 'video/mp4' : 'image/png' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = item.filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }
  };

  const handleConfirmRename = () => {
    if (renameTarget && renameValue.trim()) {
      onRenameRecording(renameTarget.id, renameValue.trim());
      setRenameTarget(null);
    }
  };

  const handleConfirmDelete = () => {
    if (deleteTarget) {
      onDeleteRecording(deleteTarget.id);
      setDeleteTarget(null);
    }
  };

  const handleOpenFolder = () => {
    setFolderNotice(`${t.folderOpenedNotice} "${storagePath}"`);
    setTimeout(() => setFolderNotice(null), 4000);
  };

  return (
    <div className="flex flex-col gap-6 w-full max-w-7xl mx-auto p-4 md:p-6" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Storage and Stat Header */}
      <div className="bg-[#13151b] border border-[#242834] rounded-2xl p-5 shadow-xl flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-[#d4af37]/20 to-[#f2c94c]/5 border border-[#d4af37]/30 flex items-center justify-center text-[#d4af37]">
            <Film className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white tracking-wide">{t.libraryTitle}</h2>
            <div className="flex flex-wrap items-center gap-3 text-xs text-slate-400 mt-1">
              <span className="flex items-center gap-1">
                <HardDrive className="w-3.5 h-3.5 text-[#d4af37]" />
                {recordings.length} {t.navRecordings.toLowerCase()} ({formatFileSize(totalBytes)})
              </span>
              <span>•</span>
              <span className="font-mono text-slate-300 truncate max-w-xs md:max-w-md">
                {storagePath}
              </span>
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={handleOpenFolder}
          className="flex items-center gap-2 px-4 py-2 bg-[#1b1e27] hover:bg-[#252936] text-slate-200 border border-[#2f3546] rounded-xl text-xs font-medium transition-all shadow-sm active:scale-95"
        >
          <FolderOpen className="w-4 h-4 text-[#d4af37]" />
          <span>{t.openFolder}</span>
        </button>
      </div>

      {folderNotice && (
        <div className="bg-[#1b261b] border border-green-500/30 text-green-300 text-xs px-4 py-2.5 rounded-xl shadow-lg flex items-center justify-between animate-fade-in">
          <span>{folderNotice}</span>
          <button onClick={() => setFolderNotice(null)} className="text-green-400 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        {/* Type pills */}
        <div className="flex items-center bg-[#13151b] p-1 rounded-xl border border-[#242834]">
          <button
            type="button"
            onClick={() => setFilterType('all')}
            className={`px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              filterType === 'all'
                ? 'bg-[#d4af37] text-black font-semibold shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            {t.filterAll} ({recordings.length})
          </button>
          <button
            type="button"
            onClick={() => setFilterType('video')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              filterType === 'video'
                ? 'bg-[#d4af37] text-black font-semibold shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Film className="w-3.5 h-3.5" />
            <span>{t.filterVideos} ({recordings.filter((r) => r.type === 'video').length})</span>
          </button>
          <button
            type="button"
            onClick={() => setFilterType('screenshot')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              filterType === 'screenshot'
                ? 'bg-[#d4af37] text-black font-semibold shadow-md'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Camera className="w-3.5 h-3.5" />
            <span>{t.filterScreenshots} ({recordings.filter((r) => r.type === 'screenshot').length})</span>
          </button>
        </div>

        {/* Search input */}
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={t.searchPlaceholder}
            className="w-full bg-[#13151b] border border-[#242834] rounded-xl pl-10 pr-4 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-[#d4af37] transition-colors"
          />
        </div>
      </div>

      {/* Grid of Recordings */}
      {filteredItems.length === 0 ? (
        <div className="bg-[#12141a]/60 border border-dashed border-[#242834] rounded-2xl p-12 text-center flex flex-col items-center justify-center gap-3">
          <div className="w-16 h-16 rounded-full bg-[#1a1d26] flex items-center justify-center text-slate-500">
            <Film className="w-8 h-8" />
          </div>
          <p className="text-slate-300 font-medium text-sm">{t.noRecordingsYet}</p>
          <p className="text-slate-500 text-xs max-w-md">
            Click &quot;Recorder&quot; in the header to record your screen and camera, or press [F12] to take a quick screenshot.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {filteredItems.map((item) => (
            <div
              key={item.id}
              className="group bg-[#13151b] border border-[#242834] hover:border-[#d4af37]/40 rounded-2xl overflow-hidden shadow-lg transition-all duration-300 flex flex-col"
            >
              {/* Thumbnail / Video Preview Area */}
              <div
                className="relative aspect-video bg-[#0a0b0e] overflow-hidden cursor-pointer"
                onClick={() => handleOpenPlayer(item)}
              >
                <img
                  src={item.thumbnailUrl}
                  alt={item.title}
                  className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                />

                {/* Gradient overlay */}
                <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-black/30" />

                {/* Badge top-left */}
                <div className="absolute top-3 left-3 flex items-center gap-1.5 bg-black/70 backdrop-blur-md px-2.5 py-1 rounded-md text-[11px] font-semibold text-white border border-white/10">
                  {item.type === 'video' ? (
                    <>
                      <Film className="w-3.5 h-3.5 text-[#d4af37]" />
                      <span>MP4</span>
                    </>
                  ) : (
                    <>
                      <Camera className="w-3.5 h-3.5 text-blue-400" />
                      <span>PNG</span>
                    </>
                  )}
                  <span className="text-slate-400">• {item.resolution}</span>
                </div>

                {/* Duration bottom-right (for videos) */}
                {item.type === 'video' && (
                  <div className="absolute bottom-3 right-3 bg-black/80 backdrop-blur-md px-2 py-0.5 rounded text-[11px] font-mono text-white flex items-center gap-1 border border-white/10">
                    <Clock className="w-3 h-3 text-[#d4af37]" />
                    <span>{formatDuration(item.durationSeconds)}</span>
                  </div>
                )}

                {/* Play hover button */}
                <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                  <div className="w-12 h-12 rounded-full bg-[#d4af37] text-black flex items-center justify-center shadow-xl shadow-amber-500/30 transform scale-90 group-hover:scale-100 transition-transform">
                    <Play className="w-5 h-5 ml-0.5 fill-current" />
                  </div>
                </div>
              </div>

              {/* Card Meta & Actions */}
              <div className="p-4 flex flex-col justify-between flex-1 gap-3">
                <div>
                  <h4 className="text-sm font-semibold text-white truncate group-hover:text-[#d4af37] transition-colors">
                    {item.title}
                  </h4>
                  <div className="flex items-center justify-between text-[11px] text-slate-400 mt-1">
                    <span className="flex items-center gap-1">
                      <Calendar className="w-3 h-3 text-slate-500" />
                      {item.createdAt}
                    </span>
                    <span>{formatFileSize(item.fileSizeBytes)}</span>
                  </div>
                </div>

                <div className="flex items-center justify-between pt-2 border-t border-[#1f222b] text-xs">
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => handleOpenPlayer(item)}
                      className="p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors"
                      title={t.openFile}
                    >
                      <Play className="w-4 h-4 text-[#d4af37]" />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDownload(item)}
                      className="p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors"
                      title={t.downloadFile}
                    >
                      <Download className="w-4 h-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        setRenameTarget(item);
                        setRenameValue(item.title);
                      }}
                      className="p-1.5 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors"
                      title={t.renameFile}
                    >
                      <Edit2 className="w-4 h-4" />
                    </button>
                  </div>

                  <button
                    type="button"
                    onClick={() => setDeleteTarget(item)}
                    className="p-1.5 rounded-lg text-slate-400 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                    title={t.deleteFile}
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Built-in Video Player Modal */}
      {isPlayerOpen && activeItem && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/85 backdrop-blur-md p-4">
          <div className="bg-[#12141a] border border-[#272b38] rounded-2xl max-w-4xl w-full overflow-hidden shadow-2xl flex flex-col">
            {/* Header */}
            <div className="px-5 py-3.5 border-b border-[#242834] bg-[#161820] flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Film className="w-5 h-5 text-[#d4af37]" />
                <h3 className="text-white text-sm font-semibold truncate max-w-md">
                  {activeItem.title} ({activeItem.filename})
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setIsPlayerOpen(false)}
                className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/10 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Video Viewport */}
            <div className="relative aspect-video bg-black flex items-center justify-center overflow-hidden">
              {activeItem.type === 'video' ? (
                activeItem.blobUrl ? (
                  <video
                    src={activeItem.blobUrl}
                    controls
                    autoPlay
                    className="w-full h-full object-contain"
                  />
                ) : (
                  <div className="relative w-full h-full flex items-center justify-center">
                    <img
                      src={activeItem.thumbnailUrl}
                      alt={activeItem.title}
                      className="w-full h-full object-cover opacity-80"
                    />
                    <div className="absolute inset-0 bg-black/40 flex flex-col items-center justify-center gap-3">
                      <div className="w-16 h-16 rounded-full bg-[#d4af37] text-black flex items-center justify-center shadow-lg">
                        <Play className="w-7 h-7 ml-1 fill-current" />
                      </div>
                      <span className="text-white font-mono text-sm bg-black/70 px-3 py-1 rounded-full border border-white/10">
                        {formatDuration(activeItem.durationSeconds)} • 1080p 60 FPS
                      </span>
                    </div>
                  </div>
                )
              ) : (
                <img
                  src={activeItem.thumbnailUrl}
                  alt={activeItem.title}
                  className="w-full h-full object-contain"
                />
              )}
            </div>

            {/* Custom Control Bar & Download */}
            <div className="p-4 bg-[#15171e] border-t border-[#242834] flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3 text-xs text-slate-300">
                <span className="font-mono bg-[#1d202b] px-2.5 py-1 rounded text-amber-400 border border-[#2b3142]">
                  {formatDuration(activeItem.durationSeconds)}
                </span>
                <span>{formatFileSize(activeItem.fileSizeBytes)}</span>
                <span>•</span>
                <span className="text-slate-400">{activeItem.createdAt}</span>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => handleDownload(activeItem)}
                  className="flex items-center gap-1.5 px-4 py-2 bg-gradient-to-r from-[#d4af37] to-[#f2c94c] text-black text-xs font-semibold rounded-lg shadow-md hover:brightness-110 active:scale-95 transition-all"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>{t.downloadFile}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Rename Modal */}
      {renameTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div className="bg-[#14161d] border border-[#272b38] rounded-xl max-w-md w-full p-5 shadow-2xl flex flex-col gap-4">
            <h3 className="text-white font-semibold text-base">{t.renameFile}</h3>
            <input
              type="text"
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              className="bg-[#0e1015] border border-[#2a2f3f] rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-[#d4af37]"
              autoFocus
            />
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setRenameTarget(null)}
                className="px-3.5 py-1.5 text-xs text-slate-300 hover:text-white"
              >
                {t.cancel}
              </button>
              <button
                type="button"
                onClick={handleConfirmRename}
                className="px-4 py-1.5 text-xs font-semibold bg-[#d4af37] text-black rounded-lg hover:brightness-110"
              >
                {t.save}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div className="bg-[#14161d] border border-red-500/20 rounded-xl max-w-md w-full p-5 shadow-2xl flex flex-col gap-4">
            <h3 className="text-white font-semibold text-base flex items-center gap-2">
              <Trash2 className="w-5 h-5 text-red-400" />
              <span>{t.confirmDeleteTitle}</span>
            </h3>
            <p className="text-xs text-slate-300 leading-relaxed">
              {t.confirmDeleteDesc}
            </p>
            <div className="p-2.5 rounded bg-black/40 text-[11px] font-mono text-slate-400 truncate">
              {deleteTarget.filename}
            </div>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setDeleteTarget(null)}
                className="px-3.5 py-1.5 text-xs text-slate-300 hover:text-white"
              >
                {t.cancel}
              </button>
              <button
                type="button"
                onClick={handleConfirmDelete}
                className="px-4 py-1.5 text-xs font-semibold bg-red-600 hover:bg-red-500 text-white rounded-lg"
              >
                {t.delete}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
