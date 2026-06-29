import React, { useCallback, useRef } from 'react';
import AsyncCreatableSelect from 'react-select/async-creatable';
import type { StylesConfig, SingleValue } from 'react-select';
import { visitRequestApi } from '../../api/visitRequestApi';

interface OrgOption {
  value: string;
  label: string;
}

const buildStyles = (hasError?: boolean): StylesConfig<OrgOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: '0.375rem',
    borderColor: hasError ? '#f87171' : state.isFocused ? '#f37021' : 'transparent',
    boxShadow: state.isFocused ? `0 0 0 1px ${hasError ? '#f87171' : '#f37021'}` : 'none',
    '&:hover': { borderColor: hasError ? '#f87171' : '#f37021' },
    minHeight: '36px',
    height: '36px',
    fontSize: '0.875rem',
    fontWeight: '400',
    backgroundColor: 'transparent',
    padding: 0,
  }),
  valueContainer: (base) => ({
    ...base,
    padding: '0 8px',
  }),
  input: (base) => ({
    ...base,
    margin: 0,
    padding: 0,
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
    borderRadius: '0.5rem',
    overflow: 'hidden',
    boxShadow: '0 4px 20px rgba(0,0,0,0.1)',
  }),
  menuPortal: (base) => ({
    ...base,
    zIndex: 9999,
  }),
  placeholder: (base) => ({ ...base, color: '#9ca3af', fontWeight: '400' }),
  singleValue: (base) => ({ ...base, color: '#111827', fontWeight: '400' }),
  indicatorSeparator: () => ({ display: 'none' }),
  dropdownIndicator: (base) => ({ ...base, padding: '4px' }),
  clearIndicator: (base) => ({ ...base, padding: '4px' }),
});

interface Props {
  value: string;
  onChange: (val: string) => void;
  onBlur?: () => void;
  hasError?: boolean;
  placeholder?: string;
}

export const OrganizationCombobox: React.FC<Props> = ({
  value,
  onChange,
  onBlur,
  hasError,
  placeholder = 'Nhập hoặc chọn...'
}) => {
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [inputValue, setInputValue] = React.useState('');

  const loadOptions = useCallback(
    (inputValue: string): Promise<OrgOption[]> =>
      new Promise((resolve) => {
        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        debounceTimer.current = setTimeout(async () => {
          try {
            const results = await visitRequestApi.searchOrganizations(inputValue);
            const apiOptions = results.map((r) => ({
              value: r.displayName,
              label: r.displayName,
            }));
            resolve(apiOptions);
          } catch {
            resolve([]);
          }
        }, 300);
      }),
    []
  );

  const selectedOption = value ? { value, label: value } : null;

  return (
    <AsyncCreatableSelect<OrgOption>
      cacheOptions
      defaultOptions={true}
      loadOptions={loadOptions}
      value={selectedOption}
      inputValue={inputValue}
      onInputChange={(val, { action }) => {
        if (action === 'input-change') {
          setInputValue(val);
          onChange(val);
        } else if (action === 'set-value' || action === 'input-blur') {
          setInputValue('');
        }
      }}
      onChange={(opt: SingleValue<OrgOption>, meta) => {
        if (meta.action === 'clear') {
          setInputValue('');
          onChange('');
        } else {
          setInputValue('');
          onChange(opt?.value ?? '');
        }
      }}
      onBlur={() => {
        if (inputValue.trim()) {
          onChange(inputValue.trim());
        }
        setInputValue('');
        if (onBlur) onBlur();
      }}
      placeholder={placeholder}
      styles={buildStyles(hasError)}
      isClearable={true}
      noOptionsMessage={() => 'Nhập để tìm hoặc tạo mới...'}
      loadingMessage={() => 'Đang tìm kiếm...'}
      formatCreateLabel={(inputValue) => `Sử dụng "${inputValue}"`}
      menuPortalTarget={document.body}
      menuPosition="fixed"
      closeMenuOnScroll={true}
      maxMenuHeight={240}
    />
  );
};
