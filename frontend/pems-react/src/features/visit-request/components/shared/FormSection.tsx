import React from 'react';

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
    className={`scroll-mt-24 border-b border-slate-200 py-7 first:pt-0 last:border-b-0 last:pb-0 ${className}`}
  >
    <div className="mb-5 flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
      <div className="min-w-0">
        <h2
          id={`${id}-title`}
          className="text-lg font-extrabold text-[#004c91] sm:text-xl"
        >
          {title} {required && <span className="text-red-500">*</span>}
        </h2>
        {description && (
          <p className="mt-1 text-sm leading-6 text-slate-500">{description}</p>
        )}
      </div>
      {headerRight}
    </div>
    {children}
  </section>
);
