import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { inputCls } from './FormField';

interface Props {
  /**
   * Everything that binds the input: the output of `register(...)`, or `value`/`onChange` for a
   * controlled field. Spreading keeps this a drop-in for the plain `<input>` it replaces, so no
   * caller has to switch between controlled and uncontrolled to gain the hint.
   */
  field: React.ComponentPropsWithRef<'input'>;
  hasError?: boolean;
  /** The validation message currently shown for this field, if any. */
  error?: string;
  testId?: string;
  disabled?: boolean;
  className?: string;
}

/**
 * A phone input that says what a phone number looks like here (plan §17/§18).
 *
 * The rule is national Vietnamese ("0912345678") or full international ("+84912345678"), no
 * extensions — the same rule `shared/utils/phoneNumber` and the backend's `PhoneNumber.IsValid`
 * apply. What the user never saw was the rule itself: the field said "không hợp lệ" and left them
 * trying spellings. The example appears while they are typing; once there IS an error the example
 * gives way to the full message, which already carries both accepted shapes — showing a long hint
 * and a long error at once just makes two paragraphs under one small box.
 */
export const PhoneField: React.FC<Props> = ({
  field, hasError, error, testId, disabled, className,
}) => {
  const { t } = useTranslation(['validation']);
  const [focused, setFocused] = useState(false);
  const { onFocus, onBlur, ...rest } = field;

  return (
    <>
      <input
        type="tel"
        inputMode="tel"
        autoComplete="tel"
        data-testid={testId}
        disabled={disabled}
        placeholder="0912345678"
        {...rest}
        onFocus={e => { setFocused(true); onFocus?.(e); }}
        onBlur={e => { setFocused(false); onBlur?.(e); }}
        className={className ?? inputCls(hasError, false, false)}
      />
      {focused && !error && (
        <p
          data-testid={testId ? `${testId}-hint` : undefined}
          className="mt-1 text-xs font-medium text-slate-500"
        >
          {t('validation:phoneHint')}
        </p>
      )}
    </>
  );
};
