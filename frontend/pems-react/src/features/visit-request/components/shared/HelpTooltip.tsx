import React from 'react';
import { HelpCircle } from 'lucide-react';

interface HelpTooltipProps {
  content: React.ReactNode;
  className?: string;
}

export const HelpTooltip: React.FC<HelpTooltipProps> = ({ content, className = '' }) => {
  return (
    <div className={`group relative inline-flex items-center align-middle ml-1.5 ${className}`}>
      <HelpCircle className="w-4 h-4 text-slate-400 hover:text-[#004c91] cursor-help transition-colors" />
      <div className="pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 mb-2 w-max max-w-xs opacity-0 transition-opacity group-hover:opacity-100 z-50">
        <div className="bg-slate-800 text-white text-xs leading-5 rounded-lg py-2 px-3 shadow-xl relative text-center">
          {content}
          <div className="absolute top-full left-1/2 -translate-x-1/2 -mt-px border-4 border-transparent border-t-slate-800" />
        </div>
      </div>
    </div>
  );
};
