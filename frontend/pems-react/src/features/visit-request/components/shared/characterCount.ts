/**
 * When a "typed / limit" counter is worth showing (plan §14).
 *
 * A counter under every bounded field meant a freshly opened form greeted the user with a column of
 * "0/2000", "0/4000", "0/200" — noise that says nothing, because nobody is near a limit they have
 * not started typing towards. The counter is only information once the user is either working in
 * the field or approaching the ceiling, so that is exactly when it appears.
 */
export interface CharacterCountState {
  value: string;
  maxLength?: number;
  /** The field currently has the caret. */
  focused: boolean;
  /** The field is showing a validation error (any kind). */
  hasError?: boolean;
  /** Fraction of the limit at which the counter appears even unfocused. Default 0.8. */
  showThreshold?: number;
}

export const CHARACTER_COUNT_THRESHOLD = 0.8;

/** True when the value has passed its limit — the counter then turns red and never hides. */
export function isOverCharacterLimit(value: string, maxLength?: number): boolean {
  return maxLength !== undefined && value.length > maxLength;
}

export function shouldShowCharacterCount({
  value, maxLength, focused, hasError, showThreshold = CHARACTER_COUNT_THRESHOLD,
}: CharacterCountState): boolean {
  if (maxLength === undefined) return false;
  if (focused) return true;
  if (value.length > maxLength) return true;
  if (value.length >= maxLength * showThreshold) return true;
  // A blurred, EMPTY field that is merely "required" has nothing to count — showing "0/2000" beside
  // "this field is required" adds a number to a message that is not about numbers.
  return !!hasError && value.length > 0;
}
