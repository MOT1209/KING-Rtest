import React, { useState, useRef, useEffect } from 'react';
import { X, Check, Monitor, Move, Maximize2 } from 'lucide-react';
import { CustomArea } from '../types/recorder';

interface AreaSelectorModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (area: CustomArea) => void;
  initialArea: CustomArea;
  isRtl: boolean;
  t: Record<string, string>;
}

export const AreaSelectorModal: React.FC<AreaSelectorModalProps> = ({
  isOpen,
  onClose,
  onConfirm,
  initialArea,
  isRtl,
  t,
}) => {
  const [rect, setRect] = useState<{ x: number; y: number; width: number; height: number }>({
    x: initialArea.x,
    y: initialArea.y,
    width: initialArea.width,
    height: initialArea.height,
  });

  const [dpiScale, setDpiScale] = useState<number>(initialArea.dpiScale || 1.0);
  const [isDrawing, setIsDrawing] = useState(false);
  const [startPos, setStartPos] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen) {
      setRect({
        x: initialArea.x,
        y: initialArea.y,
        width: initialArea.width,
        height: initialArea.height,
      });
      setDpiScale(initialArea.dpiScale || 1.0);
    }
  }, [isOpen, initialArea]);

  if (!isOpen) return null;

  const handleMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current) return;
    const bounds = containerRef.current.getBoundingClientRect();
    const x = Math.round(e.clientX - bounds.left);
    const y = Math.round(e.clientY - bounds.top);
    setStartPos({ x, y });
    setRect({ x, y, width: 20, height: 20 });
    setIsDrawing(true);
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!isDrawing || !containerRef.current) return;
    const bounds = containerRef.current.getBoundingClientRect();
    const currentX = Math.round(e.clientX - bounds.left);
    const currentY = Math.round(e.clientY - bounds.top);

    const x = Math.min(startPos.x, currentX);
    const y = Math.min(startPos.y, currentY);
    const width = Math.max(30, Math.abs(currentX - startPos.x));
    const height = Math.max(30, Math.abs(currentY - startPos.y));

    setRect({ x, y, width, height });
  };

  const handleMouseUp = () => {
    setIsDrawing(false);
  };

  const applyPreset = (w: number, h: number) => {
    setRect({
      x: 40,
      y: 40,
      width: Math.min(w / 2, 600),
      height: Math.min(h / 2, 340),
    });
  };

  const handleSave = () => {
    onConfirm({
      x: rect.x,
      y: rect.y,
      width: rect.width,
      height: rect.height,
      dpiScale,
    });
    onClose();
  };

  // Scaled coordinates
  const physicalWidth = Math.round(rect.width * dpiScale * 2);
  const physicalHeight = Math.round(rect.height * dpiScale * 2);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
      <div
        className="bg-[#12141a] border border-[#262a36] rounded-xl shadow-2xl max-w-4xl w-full flex flex-col overflow-hidden text-slate-100"
        dir={isRtl ? 'rtl' : 'ltr'}
      >
        {/* Header */}
        <div className="px-5 py-4 border-b border-[#262a36] flex items-center justify-between bg-[#161820]">
          <div className="flex items-center gap-2">
            <Monitor className="w-5 h-5 text-[#d4af37]" />
            <h3 className="font-semibold text-lg text-white">
              {t.customArea} — {t.configureArea}
            </h3>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/10 transition-colors"
            aria-label="Close"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-5 flex flex-col gap-4">
          <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-slate-300 bg-[#0d0e12] p-3 rounded-lg border border-[#1f222b]">
            <div className="flex items-center gap-2">
              <Move className="w-4 h-4 text-[#d4af37]" />
              <span>Click and drag on the preview canvas below to select the recording rectangle.</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-slate-400">DPI Scale:</span>
              <select
                value={dpiScale}
                onChange={(e) => setDpiScale(parseFloat(e.target.value))}
                className="bg-[#191c24] border border-[#2c3140] text-slate-200 rounded px-2 py-1 text-xs outline-none"
              >
                <option value={1.0}>100% (1.0x)</option>
                <option value={1.25}>125% (1.25x)</option>
                <option value={1.5}>150% (1.5x)</option>
                <option value={2.0}>200% (2.0x)</option>
              </select>
            </div>
          </div>

          {/* Canvas area */}
          <div
            ref={containerRef}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            className="relative w-full h-80 bg-[#090a0d] border border-dashed border-slate-700 rounded-lg overflow-hidden cursor-crosshair select-none"
          >
            {/* Grid pattern background */}
            <div
              className="absolute inset-0 opacity-20"
              style={{
                backgroundImage:
                  'radial-gradient(circle, #384050 1px, transparent 1px)',
                backgroundSize: '24px 24px',
              }}
            />

            {/* Selection rectangle */}
            <div
              className="absolute border-2 border-[#d4af37] bg-[#d4af37]/15 shadow-lg transition-all pointer-events-none"
              style={{
                left: `${rect.x}px`,
                top: `${rect.y}px`,
                width: `${rect.width}px`,
                height: `${rect.height}px`,
              }}
            >
              {/* Corner indicators */}
              <div className="absolute -top-1.5 -left-1.5 w-3 h-3 bg-[#d4af37] rounded-sm" />
              <div className="absolute -top-1.5 -right-1.5 w-3 h-3 bg-[#d4af37] rounded-sm" />
              <div className="absolute -bottom-1.5 -left-1.5 w-3 h-3 bg-[#d4af37] rounded-sm" />
              <div className="absolute -bottom-1.5 -right-1.5 w-3 h-3 bg-[#d4af37] rounded-sm" />

              {/* Dimension label */}
              <div className="absolute top-2 left-2 bg-black/80 text-[#d4af37] text-[11px] font-mono px-2 py-0.5 rounded border border-[#d4af37]/40">
                {rect.width} × {rect.height} px (Scaled: {physicalWidth} × {physicalHeight})
              </div>
            </div>
          </div>

          {/* Presets and readouts */}
          <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-400">Presets:</span>
              <button
                type="button"
                onClick={() => applyPreset(1920, 1080)}
                className="px-2.5 py-1 text-xs bg-[#1a1d26] hover:bg-[#252a38] text-slate-200 border border-[#2a2f3f] rounded transition-colors"
              >
                1080p (16:9)
              </button>
              <button
                type="button"
                onClick={() => applyPreset(1280, 720)}
                className="px-2.5 py-1 text-xs bg-[#1a1d26] hover:bg-[#252a38] text-slate-200 border border-[#2a2f3f] rounded transition-colors"
              >
                720p
              </button>
              <button
                type="button"
                onClick={() => applyPreset(600, 600)}
                className="px-2.5 py-1 text-xs bg-[#1a1d26] hover:bg-[#252a38] text-slate-200 border border-[#2a2f3f] rounded transition-colors"
              >
                Square (1:1)
              </button>
            </div>

            <div className="text-xs font-mono text-slate-300 bg-[#161820] px-3 py-1.5 rounded border border-[#262a36]">
              X: <span className="text-amber-400">{rect.x}</span> | Y:{' '}
              <span className="text-amber-400">{rect.y}</span> | W:{' '}
              <span className="text-amber-400">{rect.width}</span> | H:{' '}
              <span className="text-amber-400">{rect.height}</span>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="px-5 py-3 border-t border-[#262a36] bg-[#161820] flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm text-slate-300 hover:text-white bg-transparent hover:bg-white/5 rounded-lg transition-colors"
          >
            {t.cancel}
          </button>
          <button
            type="button"
            onClick={handleSave}
            className="flex items-center gap-1.5 px-5 py-2 text-sm font-semibold bg-gradient-to-r from-[#d4af37] to-[#f2c94c] text-black rounded-lg shadow-md hover:brightness-110 active:scale-95 transition-all"
          >
            <Check className="w-4 h-4" />
            <span>{t.save}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
