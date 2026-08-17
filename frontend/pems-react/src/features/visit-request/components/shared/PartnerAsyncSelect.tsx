import React, { useCallback, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import AsyncSelect from 'react-select/async';
import type { StylesConfig, SingleValue } from 'react-select';
import { visitRequestApi } from '../../api/visitRequestApi';

interface PartnerOption {
  value: number | null;
  label: string;
}

const buildStyles = (hasError?: boolean): StylesConfig<PartnerOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: '0.75rem',
    borderColor: hasError
      ? '#f87171'
      : state.isFocused
        ? '#004c91'
        : '#d1d5db',
    boxShadow: state.isFocused ? `0 0 0 1px ${hasError ? '#f87171' : '#004c91'}` : 'none',
    '&:hover': { borderColor: hasError ? '#f87171' : '#004c91' },
    minHeight: '48px',
    fontSize: '0.875rem',
    fontWeight: '400',
    backgroundColor: 'white',
  }),
  option: (base, state) => ({
    ...base,
    backgroundColor: state.isSelected ? '#004c91' : state.isFocused ? '#f0f7ff' : 'white',
    color: state.isSelected ? 'white' : '#111827',
    fontSize: '0.875rem',
    cursor: 'pointer',
  }),
  menu: (base) => ({
    ...base,
    borderRadius: '0.75rem',
    overflow: 'hidden',
    zIndex: 9999,
    boxShadow: '0 4px 24px rgba(0,0,0,0.12)',
  }),
  placeholder: (base) => ({ ...base, color: '#9ca3af', fontWeight: '400' }),
  singleValue: (base) => ({ ...base, color: '#111827', fontWeight: '400' }),
  indicatorSeparator: () => ({ display: 'none' }),
});


interface PartnerAsyncSelectProps {
  value: number | null | undefined;
  partnerName?: string;
  onChange: (value: number | null, name: string) => void;
  onBlur?: () => void;
  hasError?: boolean;
}

export const PartnerAsyncSelect: React.FC<PartnerAsyncSelectProps> = ({
  value,
  partnerName,
  onChange,
  onBlur,
  hasError,
}) => {
  const { t } = useTranslation(['visitRequest']);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Rebuilt on language change so the sentinel option follows the active locale.
  const newOrganizationOption = useMemo<PartnerOption>(
    () => ({ value: null, label: t('visitRequest:select.partnerNewOrg') }),
    [t],
  );

  const loadOptions = useCallback(
    (inputValue: string): Promise<PartnerOption[]> =>
      new Promise((resolve) => {
        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        debounceTimer.current = setTimeout(async () => {
          try {
            const results = await visitRequestApi.searchOrganizations(inputValue);
            const apiOptions = results.map((r) => ({
              value: r.partnerId,
              label: r.displayName,
            }));
            resolve([newOrganizationOption, ...apiOptions]);
          } catch {
            resolve([newOrganizationOption]);
          }
        }, 300);
      }),
    [newOrganizationOption]
  );

  return (
    <AsyncSelect<PartnerOption>
      cacheOptions
      defaultOptions
      loadOptions={loadOptions}
      value={
        value === undefined
          ? null
          : value === null
          ? newOrganizationOption
          : { value, label: partnerName || t('visitRequest:select.partnerLoading') } // Fallback while the option list is still loading
      }
      onChange={(opt: SingleValue<PartnerOption>) => {
        onChange(opt?.value ?? null, opt?.label ?? '');
      }}
      onBlur={onBlur}
      placeholder={t('visitRequest:select.partnerPlaceholder')}
      styles={buildStyles(hasError)}
      isClearable={false}
      noOptionsMessage={() => t('visitRequest:select.noResults')}
      loadingMessage={() => t('visitRequest:select.searching')}
    />
  );
};
