import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import CreatableSelect from 'react-select/creatable';
import Select from 'react-select';
import type { StylesConfig, SingleValue } from 'react-select';
import {
  getViCountryNames,
  getEnCountryNames,
  countryNameToAlpha2,
} from '../../../../shared/utils/countryNames';

export interface CountryOption {
  value: string;
  label: string;
  /** Mã ISO alpha-2 — có khi option đến từ danh sách chuẩn, không có khi user nhập tự do. */
  code?: string;
}

export type CountryLang = 'en' | 'vi';

// label hiển thị theo ngôn ngữ UI đang chọn; value LƯU theo quy ước của từng form
// (partner lưu tên tiếng Việt, visit request lưu tiếng Anh) — nhờ vậy tạo bằng UI
// tiếng Việt hay tiếng Anh thì DB vẫn chỉ có MỘT tên cho một quốc gia.
const optionCache = new Map<string, CountryOption[]>();
function getCountryOptions(displayLang: CountryLang, storeLang: CountryLang): CountryOption[] {
  const key = `${displayLang}|${storeLang}`;
  let opts = optionCache.get(key);
  if (!opts) {
    const displayNames = displayLang === 'vi' ? getViCountryNames() : getEnCountryNames();
    const storeNames = storeLang === 'vi' ? getViCountryNames() : getEnCountryNames();
    opts = Object.keys(displayNames)
      .map((code) => ({
        code,
        label: displayNames[code],
        value: storeNames[code] ?? displayNames[code],
      }))
      .sort((a, b) => a.label.localeCompare(b.label, displayLang));
    optionCache.set(key, opts);
  }
  return opts;
}

// Bỏ dấu để gõ "phap" vẫn khớp "Pháp" khi hiển thị tiếng Việt.
const stripDiacritics = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();

export const buildSelectStyles = (hasError?: boolean, isCell?: boolean): StylesConfig<CountryOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: isCell ? '0' : '0.75rem',
    borderColor: isCell ? (hasError ? '#fca5a5' : 'transparent') : (hasError
      ? '#f87171'
      : state.isFocused
        ? '#004c91'
        : '#d1d5db'),
    boxShadow: isCell ? 'none' : (state.isFocused
      ? hasError
        ? '0 0 0 1px #f87171'
        : '0 0 0 1px #004c91'
      : 'none'),
    '&:hover': { borderColor: isCell ? (hasError ? '#f87171' : '#bfdbfe') : (hasError ? '#f87171' : '#004c91') },
    minHeight: '44px',
    fontSize: '0.875rem',
    fontWeight: '400',
    backgroundColor: isCell ? (hasError ? 'rgba(254, 226, 226, 0.2)' : 'transparent') : 'white',
    padding: isCell ? '0 4px' : '0',
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
    fontWeight: '400',
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
  isCell?: boolean;
  disabled?: boolean;
  /** Quy ước NGÔN NGỮ LƯU của form (mặc định 'en' — visit request lưu tên tiếng Anh;
   *  form đối tác truyền 'vi'). Label hiển thị luôn theo ngôn ngữ UI hiện tại. */
  storeLang?: CountryLang;
  /** Accessible name for the search input — react-select does not accept a `<label htmlFor>`. */
  ariaLabel?: string;
  /**
   * Patch 4 (nationality contract): when true, the field only accepts a pick from the ISO country
   * list — no free-text "create" option. Every visit-request nationality surface (registrant, guest,
   * support member) passes this; the backend is the actual authority (it resolves/rejects on write
   * regardless), but a picker that still advertised free text here would just teach users to type
   * something the next save refuses. Partner/profile forms leave this false — their contract was not
   * touched by Patch 4.
   */
  strict?: boolean;
}

export const CountrySelect: React.FC<CountrySelectProps> = ({
  value,
  onChange,
  onBlur,
  placeholder,
  hasError,
  isCell,
  disabled,
  storeLang = 'en',
  ariaLabel,
  strict,
}) => {
  const { t, i18n } = useTranslation(['visitRequest']);
  const displayLang: CountryLang = i18n.language?.toLowerCase().startsWith('vi') ? 'vi' : 'en';
  const options = useMemo(
    () => getCountryOptions(displayLang, storeLang),
    [displayLang, storeLang]
  );
  const selectedOption = useMemo(() => {
    const normalizedValue = value?.trim();
    if (!normalizedValue) {
      return null;
    }

    const code = countryNameToAlpha2(normalizedValue);
    const matchedOption = options.find(
      (option) =>
        (code && option.code === code) ||
        option.value.trim().toLowerCase() === normalizedValue.toLowerCase()
    );

    return matchedOption ?? {
      value: normalizedValue,
      label: normalizedValue,
    };
  }, [value, options]);

  const styles = useMemo(
    () => buildSelectStyles(hasError, isCell),
    [hasError, isCell]
  );

  const commonProps = {
    options,
    value: selectedOption,
    onChange: (opt: SingleValue<CountryOption>) => onChange(opt?.value ?? ''),
    onBlur,
    'aria-label': ariaLabel,
    placeholder: placeholder ?? t('visitRequest:select.countryPlaceholder'),
    styles,
    isClearable: true,
    isDisabled: disabled,
    isSearchable: true,
    menuPortalTarget: document.body,
    menuPosition: 'fixed' as const,
    noOptionsMessage: () => t('visitRequest:select.countryNoOptions'),
    filterOption: (option: { label: string }, inputValue: string) =>
      stripDiacritics(option.label).includes(stripDiacritics(inputValue)),
  };

  if (strict) {
    // No onCreateOption / formatCreateLabel: the field only accepts a pick from `options`. A value
    // that arrived from legacy data and matches nothing still displays (selectedOption's fallback
    // above), it just cannot be RE-SUBMITTED without being changed to a real option.
    return <Select<CountryOption> {...commonProps} />;
  }

  return (
    <CreatableSelect<CountryOption>
      {...commonProps}
      onCreateOption={(inputValue) => onChange(inputValue)}
      formatCreateLabel={(input) => t('visitRequest:select.useInput', { input })}
    />
  );
};
