import React, { useMemo } from 'react';
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

const buildStyles = (hasError?: boolean, hasValue?: boolean): StylesConfig<CountryOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: '0.75rem',
    borderColor: hasError
      ? '#f87171'
      : state.isFocused
        ? '#f37021'
        : hasValue
          ? '#4ade80'
          : '#d1d5db',
    boxShadow: state.isFocused
      ? hasError
        ? '0 0 0 1px #f87171'
        : '0 0 0 1px #f37021'
      : 'none',
    '&:hover': { borderColor: hasError ? '#f87171' : '#f37021' },
    minHeight: '42px',
    fontSize: '0.875rem',
    fontWeight: '500',
    backgroundColor: 'white',
    cursor: 'text',
  }),
  option: (base, state) => ({
    ...base,
    backgroundColor: state.isSelected
      ? '#f37021'
      : state.isFocused
        ? '#fff3ea'
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
    '&:hover': { color: '#f37021' },
  }),
  dropdownIndicator: (base) => ({
    ...base,
    color: '#9ca3af',
    '&:hover': { color: '#f37021' },
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
  placeholder = 'Tìm kiếm quốc gia...',
  hasError,
}) => {
  const selectedOption = useMemo(
    () => COUNTRY_OPTIONS.find((o) => o.value === value) ?? null,
    [value]
  );

  const styles = useMemo(
    () => buildStyles(hasError, !!value),
    [hasError, value]
  );

  return (
    <CreatableSelect<CountryOption>
      options={COUNTRY_OPTIONS}
      value={selectedOption}
      onChange={(opt: SingleValue<CountryOption>) => onChange(opt?.value ?? '')}
      onCreateOption={(inputValue) => onChange(inputValue)}
      onBlur={onBlur}
      placeholder={placeholder}
      styles={styles}
      isClearable
      isSearchable
      menuPortalTarget={document.body}
      menuPosition="fixed"
      formatCreateLabel={(input) => `Sử dụng "${input}"`}
      noOptionsMessage={() => 'Không tìm thấy quốc gia'}
      filterOption={(option, inputValue) =>
        option.label.toLowerCase().includes(inputValue.toLowerCase())
      }
    />
  );
};
