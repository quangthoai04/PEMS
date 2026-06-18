import React from 'react';
import { CheckCircle2 } from 'lucide-react';

interface FormFieldProps {
  label: string;
  required?: boolean;
  error?: string;
  isValid?: boolean;
  subtitle?: string;
  children: React.ReactNode;
  className?: string;
}

export const FormField: React.FC<FormFieldProps> = ({
  label,
  required,
  error,
  isValid,
  subtitle,
  children,
  className = '',
}) => (
  <div className={className}>
    <div className="mb-2">
      <label className="block text-base font-bold text-gray-900">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {subtitle && <p className="text-xs text-gray-500 mt-0.5">{subtitle}</p>}
    </div>
    <div className="relative">
      {children}
      {isValid && !error && (
        <div className="absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none z-10">
          <CheckCircle2 className="w-5 h-5 text-green-500" />
        </div>
      )}
    </div>
    {error && (
      <p className="mt-1.5 text-xs text-red-600 font-medium flex items-center gap-1">
        <span className="shrink-0">⚠</span>
        {error}
      </p>
    )}
  </div>
);

export const inputCls = (hasError?: boolean, hasValue?: boolean) =>
  [
    'w-full px-4 py-2.5 rounded-xl border outline-none transition-all bg-white text-sm font-medium text-gray-900 shadow-sm',
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-1 focus:ring-red-400'
      : hasValue
        ? 'border-green-400 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]'
        : 'border-gray-300 focus:border-[#f37021] focus:ring-1 focus:ring-[#f37021]',
  ].join(' ');
