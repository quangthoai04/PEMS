import type { FieldPath, FieldValues, UseFormReturn } from 'react-hook-form';

/**
 * Commit a CUSTOM control's value and clear its inline error the instant the value becomes valid
 * (NP-02).
 *
 * <p>Why this exists. React Hook Form only re-runs the resolver for a field as it changes once the
 * form has been submitted (`reValidateMode: 'onChange'`). Anything that writes an error BEFORE that
 * first submit — a profile autofill, a server-side error mapped onto a field, a manual
 * `setError` — therefore leaves an error nothing is armed to take back: the user picks a perfectly
 * valid country and the red message just sits there until they press Submit.</p>
 *
 * <p>Native inputs registered with `register()` are less exposed to this because their `onChange`
 * goes through RHF's own field handler. A control wrapped in `Controller` that only calls
 * `field.onChange(value)` gets no such treatment, which is why every non-native required control
 * (country, organization combobox, date/time range, plain selects) routes through here.</p>
 *
 * <p>Deliberately conditional: it triggers ONLY when the field is currently showing an error, so a
 * form nobody has submitted still does not start validating fields as they are first filled in.
 * Switching the whole form to `mode: 'onChange'` would do that, and on a form this long it means
 * shouting at the user about field 12 while they are typing field 2.</p>
 */
export function commitFieldValue<TFieldValues extends FieldValues>(
  form: UseFormReturn<TFieldValues>,
  name: FieldPath<TFieldValues>,
  value: unknown,
  onChange: (value: unknown) => void,
): void {
  onChange(value);
  if (form.getFieldState(name).error) void form.trigger(name);
}

/**
 * `commitFieldValue` bound to one field — handy inside a `Controller` render prop, where the
 * handler is passed straight to the control:
 *
 * ```tsx
 * <CountrySelect onChange={fieldChangeHandler(form, 'registerInfo.nationality', field.onChange)} />
 * ```
 */
export function fieldChangeHandler<TFieldValues extends FieldValues>(
  form: UseFormReturn<TFieldValues>,
  name: FieldPath<TFieldValues>,
  onChange: (value: unknown) => void,
): (value: unknown) => void {
  return value => commitFieldValue(form, name, value, onChange);
}
