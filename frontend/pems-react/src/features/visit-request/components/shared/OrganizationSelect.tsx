import React, { useCallback, useRef } from 'react';
import AsyncCreatableSelect from 'react-select/async-creatable';
import type { StylesConfig, SingleValue } from 'react-select';
import { visitRequestApi } from '../../api/visitRequestApi';

interface OrgOption {
  value: string;
  label: string;
}

const buildStyles = (hasError?: boolean, hasValue?: boolean): StylesConfig<OrgOption> => ({
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
    boxShadow: state.isFocused ? `0 0 0 1px ${hasError ? '#f87171' : '#f37021'}` : 'none',
    '&:hover': { borderColor: hasError ? '#f87171' : '#f37021' },
    minHeight: '42px',
    fontSize: '0.875rem',
    fontWeight: '500',
    backgroundColor: 'white',
  }),
  option: (base, state) => ({
    ...base,
    backgroundColor: state.isSelected ? '#f37021' : state.isFocused ? '#fff3ea' : 'white',
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
  singleValue: (base) => ({ ...base, color: '#111827', fontWeight: '500' }),
  indicatorSeparator: () => ({ display: 'none' }),
});

interface OrganizationSelectProps {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  hasError?: boolean;
  placeholder?: string;
}

export const OrganizationSelect: React.FC<OrganizationSelectProps> = ({
  value,
  onChange,
  onBlur,
  hasError,
  placeholder = 'Tìm kiếm hoặc nhập tên đơn vị...',
}) => {
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const loadOptions = useCallback(
    (inputValue: string): Promise<OrgOption[]> =>
      new Promise((resolve) => {
        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        if (!inputValue || inputValue.length < 2) {
          resolve([]);
          return;
        }

        debounceTimer.current = setTimeout(async () => {
          try {
            const results = await visitRequestApi.searchOrganizations(inputValue);
            resolve(results.map((r) => ({ value: r.name, label: r.name })));
          } catch {
            resolve([]);
          }
        }, 300);
      }),
    []
  );

  const selectedOption: OrgOption | null = value ? { value, label: value } : null;

  return (
    <AsyncCreatableSelect<OrgOption>
      cacheOptions
      defaultOptions={false}
      loadOptions={loadOptions}
      value={selectedOption}
      onChange={(opt: SingleValue<OrgOption>) => onChange(opt?.value ?? '')}
      onCreateOption={(inputValue) => onChange(inputValue)}
      onBlur={onBlur}
      placeholder={placeholder}
      styles={buildStyles(hasError, !!value)}
      isClearable
      formatCreateLabel={(input) => `Sử dụng "${input}"`}
      noOptionsMessage={({ inputValue }) =>
        inputValue.length < 2 ? 'Nhập ít nhất 2 ký tự để tìm kiếm' : 'Không tìm thấy kết quả'
      }
      loadingMessage={() => 'Đang tìm kiếm...'}
    />
  );
};
