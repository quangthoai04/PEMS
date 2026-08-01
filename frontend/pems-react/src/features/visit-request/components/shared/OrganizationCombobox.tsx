import React, { useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import AsyncCreatableSelect from 'react-select/async-creatable';
import type { StylesConfig, SingleValue } from 'react-select';
import { visitRequestApi } from '../../api/visitRequestApi';

interface OrgOption {
  value: string;
  label: string;
}

const buildStyles = (hasError?: boolean, isCell?: boolean): StylesConfig<OrgOption> => ({
  control: (base, state) => ({
    ...base,
    borderRadius: isCell ? '0' : '0.75rem',
    borderColor: isCell ? (hasError ? '#fca5a5' : 'transparent') : (hasError ? '#f87171' : state.isFocused ? '#004c91' : '#cbd5e1'),
    boxShadow: isCell ? 'none' : (state.isFocused ? `0 0 0 1px ${hasError ? '#f87171' : '#004c91'}` : 'none'),
    '&:hover': { borderColor: isCell ? (hasError ? '#f87171' : '#bfdbfe') : (hasError ? '#f87171' : state.isFocused ? '#004c91' : '#94a3b8') },
    minHeight: isCell ? '44px' : '44px',
    // No fixed height: a long organization name must WRAP and grow the row rather than be
    // clipped to an ellipsis the user cannot read back (plan §21.3).
    height: 'auto',
    fontSize: '0.875rem',
    fontWeight: isCell ? '500' : '400',
    backgroundColor: isCell ? (hasError ? 'rgba(254, 226, 226, 0.2)' : 'transparent') : (state.isDisabled ? '#f1f5f9' : 'white'),
    padding: isCell ? '0 4px' : '0',
    cursor: state.isDisabled ? 'not-allowed' : 'text',
  }),
  valueContainer: (base) => ({
    ...base,
    padding: '2px 8px',
    flexWrap: 'wrap',
  }),
  singleValue: (base) => ({
    ...base,
    color: '#111827',
    fontWeight: '400',
    whiteSpace: 'normal',
    overflow: 'visible',
    textOverflow: 'clip',
    wordBreak: 'break-word',
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
  isCell?: boolean;
  /** Wrapper test id — react-select does not forward arbitrary props to its input. */
  testId?: string;
  /** Accessible name for the search input (the visible label lives in the FormField). */
  ariaLabel?: string;
  /** `id` on the search input, so a `<label htmlFor>` can point at it. */
  inputId?: string;
}

export const OrganizationCombobox: React.FC<Props> = ({
  value,
  onChange,
  onBlur,
  hasError,
  placeholder,
  isCell,
  testId,
  ariaLabel,
  inputId,
}) => {
  const { t } = useTranslation(['visitRequest']);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [inputValue, setInputValue] = React.useState('');
  /**
   * True only while the CURRENT value is one the user picked from the results. It drives a light
   * "already in the system" note — deliberately NOT the big green "Đã chọn đối tác có sẵn" badge,
   * which belongs to the request-level partner field alone: these organizations are snapshots and
   * may well belong to a different body than the request's partner. Repeating the badge everywhere
   * would suggest they are all the same partner record.
   */
  const [pickedFromList, setPickedFromList] = React.useState(false);

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
    <div data-testid={testId}>
      <AsyncCreatableSelect<OrgOption>
        cacheOptions
        defaultOptions={true}
        loadOptions={loadOptions}
        value={selectedOption}
        inputValue={inputValue}
        inputId={inputId}
        aria-label={ariaLabel}
        onInputChange={(val, { action }) => {
          if (action === 'input-change') {
            setInputValue(val);
            setPickedFromList(false);
            onChange(val);
          } else if (action === 'set-value' || action === 'input-blur') {
            setInputValue('');
          }
        }}
        onChange={(opt: SingleValue<OrgOption>, meta) => {
          setInputValue('');
          if (meta.action === 'clear') {
            setPickedFromList(false);
            onChange('');
            return;
          }
          // `create-option` is free text the user invented; only `select-option` is a real match.
          setPickedFromList(meta.action === 'select-option');
          onChange(opt?.value ?? '');
        }}
        onBlur={() => {
          if (inputValue.trim()) {
            onChange(inputValue.trim());
          }
          setInputValue('');
          if (onBlur) onBlur();
        }}
        placeholder={placeholder ?? t('visitRequest:select.orgComboPlaceholder')}
        styles={buildStyles(hasError, isCell)}
        isClearable={true}
        noOptionsMessage={() => t('visitRequest:select.orgComboNoOptions')}
        loadingMessage={() => t('visitRequest:select.searching')}
        formatCreateLabel={(inputValue) => t('visitRequest:select.useInput', { input: inputValue })}
        menuPortalTarget={document.body}
        menuPosition="fixed"
        closeMenuOnScroll={true}
        maxMenuHeight={240}
      />
      {pickedFromList && !isCell && (
        <p
          data-testid={testId ? `${testId}-known` : undefined}
          className="mt-1 text-xs font-medium text-green-700"
        >
          {t('visitRequest:select.orgKnown')}
        </p>
      )}
    </div>
  );
};
