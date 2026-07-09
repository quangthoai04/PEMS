import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import CreatableSelect from 'react-select/creatable';
import type { StylesConfig, SingleValue } from 'react-select';
import countries from 'i18n-iso-countries';
import enLocale from 'i18n-iso-countries/langs/en.json';

countries.registerLocale(enLocale);

interface CountryOption {
  value: string;
  label: string;
}

const COUNTRY_OPTIONS: CountryOption[] = Object.entries(
  countries.getNames('en', { select: 'official' })
)
  .map(([, name]) => ({ value: name, label: name }))
  .sort((a, b) => a.label.localeCompare(b.label));

const buildStyles = (hasError?: boolean): StylesConfig<CountryOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: '0.75rem',
    borderColor: hasError
      ? '#f87171'
      : state.isFocused
        ? '#004c91'
        : '#d1d5db',
    boxShadow: state.isFocused
      ? hasError
        ? '0 0 0 1px #f87171'
        : '0 0 0 1px #004c91'
      : 'none',
    '&:hover': { borderColor: hasError ? '#f87171' : '#004c91' },
    minHeight: '48px',
    fontSize: '0.875rem',
    fontWeight: '500',
    backgroundColor: 'white',
    cursor: 'text',
  }),
  option: (base, state) => ({
    ...base,
    backgroundColor: state.isSelected
      ? '#004c91'
      : state.isFocused
        ? '#f0f7ff'
        : 'white',
    color: state.isSelected ? 'white' : '#111827',
    fontSize: '0.875rem',
    cursor: 'pointer',
    padding: '8px 12px',
  }),
  menu: (base) => ({
    ...base,
    borderRadius: '0.75rem',
    overflow: 'hidden',
    boxShadow: '0 4px 24px rgba(0,0,0,0.12)',
  }),
  menuPortal: (base) => ({
    ...base,
    zIndex: 10000,
  }),
  menuList: (base) => ({
    ...base,
    maxHeight: '220px',
  }),
  placeholder: (base) => ({
    ...base,
    color: '#9ca3af',
    fontWeight: '400',
  }),
  singleValue: (base) => ({
    ...base,
    color: '#111827',
    fontWeight: '500',
  }),
  clearIndicator: (base) => ({
    ...base,
    cursor: 'pointer',
    '&:hover': { color: '#004c91' },
  }),
  dropdownIndicator: (base) => ({
    ...base,
    color: '#9ca3af',
    '&:hover': { color: '#004c91' },
  }),
  indicatorSeparator: () => ({ display: 'none' }),
});

interface CountrySelectProps {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  hasError?: boolean;
}

export const CountrySelect: React.FC<CountrySelectProps> = ({
  value,
  onChange,
  onBlur,
  placeholder,
  hasError,
}) => {
  const { t } = useTranslation(['visitRequest']);
  const selectedOption = useMemo(
    () => COUNTRY_OPTIONS.find((o) => o.value === value) ?? null,
    [value]
  );

  const styles = useMemo(
    () => buildStyles(hasError),
    [hasError]
  );

  return (
    <CreatableSelect<CountryOption>
      options={COUNTRY_OPTIONS}
      value={selectedOption}
      onChange={(opt: SingleValue<CountryOption>) => onChange(opt?.value ?? '')}
      onCreateOption={(inputValue) => onChange(inputValue)}
      onBlur={onBlur}
      placeholder={placeholder ?? t('visitRequest:select.countryPlaceholder')}
      styles={styles}
      isClearable
      isSearchable
      menuPortalTarget={document.body}
      menuPosition="fixed"
      formatCreateLabel={(input) => t('visitRequest:select.useInput', { input })}
      noOptionsMessage={() => t('visitRequest:select.countryNoOptions')}
      filterOption={(option, inputValue) =>
        option.label.toLowerCase().includes(inputValue.toLowerCase())
      }
    />
  );
};
