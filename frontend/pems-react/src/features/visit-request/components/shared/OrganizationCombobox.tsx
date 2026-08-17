import React, { useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import AsyncCreatableSelect from 'react-select/async-creatable';
import { components as SelectComponents, type StylesConfig, type SingleValue, type InputProps } from 'react-select';
import { visitRequestApi } from '../../api/visitRequestApi';

/**
 * WHICH question the dropdown is answering, which is what decides the option set (PART-09).
 *
 * <p>It used to be decided by whether the user was signed in, so a Staff Leader filling in a
 * registration form was offered "Hồ sơ chờ duyệt · FPT University Hà Nội" — a profile nobody has
 * approved — as a guest's employer. Being logged in is not a reason to be shown an unapproved
 * partner; the form asks the same question of everyone and must get the same answers.</p>
 *
 * <ul>
 *   <li><c>REQUEST_FORM</c> — registration/edit/resubmit. ACTIVE + APPROVED + PUBLIC only.</li>
 *   <li><c>PARTNER_MANAGEMENT</c> — the partner module, where pending/internal profiles are the
 *   point. Scoped by the caller's own rights, server-side.</li>
 * </ul>
 *
 * <p>The duplicate matcher (<c>PARTNER_MATCHING</c>) deliberately has no entry here: it must keep
 * surfacing pending and rejected candidates so a second profile is not created for an organisation
 * that already has one, and it runs through its own endpoint rather than this control.</p>
 */
export type OrganizationSearchMode = 'REQUEST_FORM' | 'PARTNER_MANAGEMENT';

/**
 * One dropdown entry.
 *
 * <p>`partnerId` is the whole point of this type. The options used to be flattened to
 * `{ value: displayName, label: displayName }`, which threw the id away at the moment the user
 * picked a real organization — so the form submitted a NAME, the backend had nothing but a string to
 * go on, and the minutes screen later asked the user to "create or link" an organization the system
 * had just handed them from its own list (PART-01).</p>
 *
 * <p>`partnerId: null` marks free text the user invented — a real, ordinary answer, not a failure.</p>
 */
interface OrgOption {
  value: string;
  label: string;
  partnerId: number | null;
  profileStatus?: 'APPROVED' | 'PENDING_APPROVAL';
  ownerCampusName?: string | null;
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
    fontWeight: '400',
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
  /**
   * Receives the display text AND the partner id the text came from (`null` for free text).
   * Callers that do not track an identity may keep a one-argument handler — the extra argument is
   * simply ignored.
   */
  onChange: (val: string, partnerId: number | null) => void;
  /**
   * The id currently stored for this field, so re-opening a saved form restores the CHOICE and not
   * just the text it printed. Without it, every edit silently downgraded a picked organization back
   * to free text.
   */
  partnerId?: number | null;
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
  /**
   * What this dropdown is FOR. Defaults to the registration form, which is the narrow set: a caller
   * that forgets to pass it cannot accidentally widen what a request may cite (PART-09).
   */
  searchMode?: OrganizationSearchMode;
}

export const OrganizationCombobox: React.FC<Props> = ({
  value,
  onChange,
  partnerId = null,
  onBlur,
  hasError,
  placeholder,
  isCell,
  testId,
  ariaLabel,
  inputId,
  searchMode = 'REQUEST_FORM',
}) => {
  const { t } = useTranslation(['visitRequest']);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [inputValue, setInputValue] = React.useState('');

  /**
   * Which endpoint answers this mode. The public one returns ACTIVE + APPROVED + PUBLIC and nothing
   * else, which is exactly the registration form's rule — so the form asks it whether or not anybody
   * is signed in, and this control no longer reads the session at all. The backend applies the same
   * rule to the SUBMIT, so a stale draft or a hand-made request cannot get round the dropdown.
   */
  const useInternalOptions = searchMode === 'PARTNER_MANAGEMENT';

  /**
   * True while the CURRENT value came from the list. It drives a light "already in the system" note
   * — deliberately NOT the big green "Đã chọn đối tác có sẵn" badge, which belongs to the
   * request-level partner field alone: these organizations are snapshots and may well belong to a
   * different body than the request's partner. Repeating the badge everywhere would suggest they are
   * all the same partner record.
   *
   * <p>Two sources, because not every caller has somewhere to keep an id. A stored `partnerId` is the
   * durable answer and survives a remount or an edit reload; `justPicked` covers the fields that
   * carry no id of their own (the operational contact), where the click is all there is to remember.</p>
   */
  const [justPicked, setJustPicked] = React.useState(false);
  const pickedFromList = justPicked || (partnerId !== null && partnerId !== undefined);

  const loadOptions = useCallback(
    (inputValue: string): Promise<OrgOption[]> =>
      new Promise((resolve) => {
        if (debounceTimer.current) clearTimeout(debounceTimer.current);

        debounceTimer.current = setTimeout(async () => {
          try {
            const results = await visitRequestApi.searchOrganizations(inputValue, useInternalOptions);
            resolve(results.map((r) => ({
              value: r.displayName,
              label: r.displayName,
              partnerId: r.partnerId,
              profileStatus: r.profileStatus,
              ownerCampusName: r.ownerCampusName,
            })));
          } catch {
            resolve([]);
          }
        }, 300);
      }),
    [useInternalOptions]
  );

  const selectedOption: OrgOption | null = value
    ? { value, label: value, partnerId: partnerId ?? null }
    : null;

  // react-select's own Input never sets `aria-invalid` — a required field left empty gave a screen
  // reader no signal at all, unlike every plain `<input>` field beside it. This overrides ONLY that one
  // attribute; autosize, keyboard handling and ref plumbing stay the library's own default Input.
  const InputWithAria = useCallback(
    (props: InputProps<OrgOption>) => (
      <SelectComponents.Input {...props} aria-invalid={hasError ? true : undefined} />
    ),
    [hasError],
  );

  return (
    <div data-testid={testId}>
      <AsyncCreatableSelect<OrgOption>
        // Keyed by MODE, not just `true`. react-select drops its cache whenever this value changes,
        // so a result set fetched for the partner module can never be replayed into a registration
        // form — which would put back exactly the pending profiles this change removes (PART-09).
        cacheOptions={searchMode}
        defaultOptions={true}
        loadOptions={loadOptions}
        value={selectedOption}
        inputValue={inputValue}
        inputId={inputId}
        aria-label={ariaLabel}
        components={{ Input: InputWithAria }}
        onInputChange={(val, { action }) => {
          if (action === 'input-change') {
            setInputValue(val);
            setJustPicked(false);
            // Typing over a picked organization makes it free text again. The id MUST go with it:
            // keeping it would leave the form claiming "ABC University" while pointing at a partner
            // the user has just edited away from.
            onChange(val, null);
          } else if (action === 'set-value' || action === 'input-blur') {
            setInputValue('');
          }
        }}
        onChange={(opt: SingleValue<OrgOption>, meta) => {
          setInputValue('');
          if (meta.action === 'clear') {
            setJustPicked(false);
            onChange('', null);
            return;
          }
          // `create-option` is free text the user invented; only `select-option` carries an identity.
          const fromList = meta.action === 'select-option';
          setJustPicked(fromList);
          onChange(opt?.value ?? '', fromList ? (opt?.partnerId ?? null) : null);
        }}
        onBlur={() => {
          if (inputValue.trim()) {
            onChange(inputValue.trim(), null);
          }
          setInputValue('');
          if (onBlur) onBlur();
        }}
        formatOptionLabel={(opt, meta) => {
          // Only in the menu: a pending profile has to say so where the user is choosing, not
          // afterwards. In the closed control it is just the organization name.
          if (meta.context !== 'menu' || opt.profileStatus !== 'PENDING_APPROVAL') return opt.label;
          return (
            <span className="flex flex-col">
              <span>{opt.label}</span>
              <span className="text-[11px] text-amber-700">
                {t('visitRequest:select.orgPendingApproval')}
                {opt.ownerCampusName ? ` · ${opt.ownerCampusName}` : ''}
              </span>
            </span>
          );
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
