import React from 'react';
import { CheckCircle2 } from 'lucide-react';

interface FormFieldProps {
  label: string;
  required?: boolean;
  error?: string;
  isValid?: boolean;
  subtitle?: string;
  /**
   * Whether to render the green "valid" check inside the field.
   * Pass false for Select/Combobox/DateTime wrappers that already render their
   * own chevron/clear indicators — otherwise the icons overlap on the right edge.
   */
  showValidIcon?: boolean;
  children: React.ReactNode;
  className?: string;
}

export const FormField: React.FC<FormFieldProps> = ({
  label,
  required,
  error,
  isValid,
  subtitle,
  showValidIcon = true,
  children,
  className = '',
}) => (
  <div className={`space-y-2 ${className}`}>
    <div>
      <label className="block text-sm font-bold text-slate-900">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {subtitle && <p className="text-xs font-medium text-slate-500 mt-1">{subtitle}</p>}
    </div>
    <div className="relative">
      {children}
      {showValidIcon && isValid && !error && (
        <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none z-10">
          <CheckCircle2 className="w-5 h-5 text-green-500" />
        </div>
      )}
    </div>
    {error && (
      <p className="text-xs font-semibold text-red-600">
        {error}
      </p>
    )}
  </div>
);

export const inputCls = (hasError?: boolean, hasValue?: boolean, hasIcon: boolean = true) =>
  [
    `flex h-12 w-full min-w-0 items-center rounded-xl border bg-white pl-4 ${hasIcon ? 'pr-10' : 'pr-4'} text-sm font-semibold text-slate-800 outline-none transition-all shadow-sm`,
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
      : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10',
  ].join(' ');
