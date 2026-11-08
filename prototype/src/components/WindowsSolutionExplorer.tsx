import React, { useState } from 'react';
import {
  Code,
  Folder,
  FileCode,
  FileText,
  Download,
  Copy,
  Check,
  ChevronRight,
  ChevronDown,
  Layers,
  Terminal,
  ShieldCheck,
  Cpu,
} from 'lucide-react';
import { csharpProjectFiles, CSharpFile } from '../services/csharpProjectData';

interface WindowsSolutionExplorerProps {
  isRtl: boolean;
  t: Record<string, string>;
}

export const WindowsSolutionExplorer: React.FC<WindowsSolutionExplorerProps> = ({
  isRtl,
  t,
}) => {
  const [selectedFile, setSelectedFile] = useState<CSharpFile>(csharpProjectFiles[0]);
  const [copied, setCopied] = useState(false);
  const [expandedFolders, setExpandedFolders] = useState<Record<string, boolean>>({
    'KING-RECORDER': true,
    'src': true,
    'KINGRecorder.Capture': true,
    'KINGRecorder.Recording': true,
    'KINGRecorder.App': true,
    'docs': true,
  });

  const handleCopyCode = () => {
    navigator.clipboard.writeText(selectedFile.content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownloadZip = () => {
    // Generate text bundle of entire C# solution
    let fullBundle = `// KING RECORDER — NATIVE WINDOWS SOLUTION ARCHITECTURE BUNDLE\n`;
    fullBundle += `// Version 1.0.0 MVP | WinUI 3 + Windows App SDK 1.5 + .NET 8 LTS\n`;
    fullBundle += `// Generated: ${new Date().toISOString()}\n\n`;

    csharpProjectFiles.forEach((file) => {
      fullBundle += `\n======================================================\n`;
      fullBundle += `// FILE: ${file.path}\n`;
      fullBundle += `======================================================\n\n`;
      fullBundle += file.content + '\n';
    });

    const blob = new Blob([fullBundle], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'KING-RECORDER-WinUI3-CSharp-Solution.txt';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const toggleFolder = (folderName: string) => {
    setExpandedFolders((prev) => ({ ...prev, [folderName]: !prev[folderName] }));
  };

  return (
    <div className="flex flex-col gap-6 w-full max-w-7xl mx-auto p-4 md:p-6" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Header Banner */}
      <div className="bg-[#13151b] border border-[#242834] rounded-2xl p-5 shadow-xl flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-[#d4af37]/25 to-[#f2c94c]/5 border border-[#d4af37]/40 flex items-center justify-center text-[#d4af37]">
            <Layers className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white tracking-wide">{t.solutionTitle}</h2>
            <p className="text-xs text-slate-400 mt-0.5">{t.solutionSubtitle}</p>
          </div>
        </div>

        <button
          type="button"
          onClick={handleDownloadZip}
          className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-[#d4af37] to-[#f2c94c] text-black text-xs font-bold rounded-xl shadow-lg hover:brightness-110 active:scale-95 transition-all"
        >
          <Download className="w-4 h-4" />
          <span>{t.downloadSolution}</span>
        </button>
      </div>

      {/* Highlights bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-[#101217] border border-[#202430] p-4 rounded-xl flex items-center gap-3">
          <Cpu className="w-5 h-5 text-[#d4af37]" />
          <div>
            <h4 className="text-xs font-semibold text-white">Direct3D 11 & Media Foundation</h4>
            <p className="text-[11px] text-slate-400">Zero-copy GPU frame pool & hardware H.264</p>
          </div>
        </div>
        <div className="bg-[#101217] border border-[#202430] p-4 rounded-xl flex items-center gap-3">
          <Terminal className="w-5 h-5 text-blue-400" />
          <div>
            <h4 className="text-xs font-semibold text-white">WASAPI Loopback 48kHz</h4>
            <p className="text-[11px] text-slate-400">Synchronized system audio and microphone mixing</p>
          </div>
        </div>
        <div className="bg-[#101217] border border-[#202430] p-4 rounded-xl flex items-center gap-3">
          <ShieldCheck className="w-5 h-5 text-emerald-400" />
          <div>
            <h4 className="text-xs font-semibold text-white">WinUI 3 + Windows App SDK</h4>
            <p className="text-[11px] text-slate-400">Modern Fluent design & monotonic clock sync</p>
          </div>
        </div>
      </div>

      {/* Explorer Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 min-h-[560px]">
        {/* Solution Tree (Left 4 cols) */}
        <div className="lg:col-span-4 bg-[#111319] border border-[#232733] rounded-2xl p-4 shadow-xl flex flex-col gap-3">
          <div className="flex items-center justify-between border-b border-[#202430] pb-3 px-1">
            <span className="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
              <Folder className="w-4 h-4 text-[#d4af37]" />
              <span>{t.fileTree}</span>
            </span>
            <span className="text-[11px] font-mono text-slate-500">.NET 8.0</span>
          </div>

          <div className="flex-1 overflow-y-auto pr-1 flex flex-col gap-1 text-xs">
            {csharpProjectFiles.map((file) => {
              const isSelected = selectedFile.path === file.path;
              return (
                <button
                  key={file.path}
                  type="button"
                  onClick={() => setSelectedFile(file)}
                  className={`w-full text-left px-3 py-2 rounded-xl flex items-center gap-2.5 transition-all ${
                    isSelected
                      ? 'bg-[#d4af37]/15 border border-[#d4af37]/40 text-[#fce084] font-medium'
                      : 'text-slate-300 hover:bg-[#191c25] hover:text-white'
                  }`}
                >
                  {file.path.endsWith('.cs') ? (
                    <FileCode className={`w-4 h-4 flex-shrink-0 ${isSelected ? 'text-[#d4af37]' : 'text-blue-400'}`} />
                  ) : (
                    <FileText className={`w-4 h-4 flex-shrink-0 ${isSelected ? 'text-[#d4af37]' : 'text-amber-300'}`} />
                  )}
                  <div className="truncate flex-1">
                    <div className="truncate">{file.name}</div>
                    <div className="text-[10px] text-slate-500 truncate">{file.path}</div>
                  </div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Code Viewer (Right 8 cols) */}
        <div className="lg:col-span-8 bg-[#0b0c10] border border-[#232733] rounded-2xl overflow-hidden shadow-2xl flex flex-col">
          {/* Code Header */}
          <div className="px-5 py-3.5 bg-[#14161d] border-b border-[#232733] flex items-center justify-between">
            <div className="flex items-center gap-2 truncate">
              <FileCode className="w-4 h-4 text-[#d4af37] flex-shrink-0" />
              <span className="text-xs font-mono text-white font-medium truncate">{selectedFile.path}</span>
            </div>

            <button
              type="button"
              onClick={handleCopyCode}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-[#1c1f2b] hover:bg-[#252a3a] text-slate-200 border border-[#2f3549] rounded-lg text-xs font-medium transition-colors"
            >
              {copied ? <Check className="w-3.5 h-3.5 text-green-400" /> : <Copy className="w-3.5 h-3.5" />}
              <span>{copied ? t.codeCopied : t.copyCode}</span>
            </button>
          </div>

          {/* Syntax Code Body */}
          <div className="p-5 flex-1 overflow-auto bg-[#0a0b0f] font-mono text-xs leading-relaxed text-slate-300" dir="ltr">
            <pre className="whitespace-pre text-left">
              <code>{selectedFile.content}</code>
            </pre>
          </div>
        </div>
      </div>
    </div>
  );
};
