import React from 'react';
import { CheckCircle2 } from 'lucide-react';

interface FormFieldProps {
  label: React.ReactNode;
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
  <div className={`flex flex-col gap-2 ${className}`} data-field-error={error ? 'true' : undefined}>
    <div>
      <label className="flex flex-wrap items-baseline justify-between gap-2 text-sm font-bold text-slate-900">
        <span>{label} {required && <span className="text-red-500">*</span>}</span>
        {subtitle && <span className="text-xs font-medium text-slate-500">{subtitle}</span>}
      </label>
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
    `flex h-11 w-full min-w-0 items-center rounded-xl border bg-white pl-4 ${hasIcon ? 'pr-10' : 'pr-4'} text-sm font-semibold text-slate-800 outline-none transition-colors`,
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
      : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10',
  ].join(' ');

/** Textarea variant of the standard control: same border/focus, no shadow. */
export const textareaCls = (hasError?: boolean) =>
  [
    'w-full min-w-0 rounded-xl border bg-white px-4 py-3 text-sm font-medium text-slate-800 outline-none transition-colors resize-none',
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
      : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10',
  ].join(' ');

/** Select variant: chevron space on the right, no shadow. */
export const selectCls = (hasError?: boolean) =>
  [
    'h-11 w-full min-w-0 appearance-none rounded-xl border bg-white pl-4 pr-9 text-sm font-medium text-slate-800 outline-none transition-colors',
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-2 focus:ring-red-500/10'
      : 'border-slate-300 focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10',
  ].join(' ');
