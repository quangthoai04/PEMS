import React from 'react';
import { HelpTooltip } from './HelpTooltip';

/**
 * Flat section wrapper for the public visit-request form (UC17 single-form spec):
 * one heading per content group, spacing + bottom divider only — no nested cards,
 * no decorative left borders, no section shadows.
 */
interface FormSectionProps {
  id: string;
  title: string;
  description?: string;
  /** Renders the required asterisk next to the title. */
  required?: boolean;
  /** Optional control rendered on the right of the heading (e.g. a checkbox). */
  headerRight?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}

export const FormSection: React.FC<FormSectionProps> = ({
  id,
  title,
  description,
  required,
  headerRight,
  children,
  className = '',
}) => (
  <section
    id={id}
    aria-labelledby={`${id}-title`}
    className={`scroll-mt-24 rounded-2xl bg-white p-5 shadow-sm border border-slate-100 sm:p-6 ${className}`}
  >
    <div className="mb-5 flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
      <div className="min-w-0 flex items-center">
        <h2
          id={`${id}-title`}
          className="text-lg font-extrabold text-[#004c91] sm:text-xl flex items-center flex-wrap"
        >
          {title} {required && <span className="text-red-500 ml-1">*</span>}
          {description && <HelpTooltip content={description} className="ml-2" />}
        </h2>
      </div>
      {headerRight}
    </div>
    {children}
  </section>
);
