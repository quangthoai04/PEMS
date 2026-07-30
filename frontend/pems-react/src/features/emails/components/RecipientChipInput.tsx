/**
 * One recipient field — used for TO, CC and BCC alike.
 *
 * The three groups get the same component on purpose. When each field carried its own parsing, CC and
 * BCC ended up with weaker rules than TO (or none), so an address the TO field would have refused went
 * out as a copy. Everything here defers to `../types/recipients`, which mirrors the backend validator.
 *
 * Errors are reported per address rather than as one message for the field: with a list of chips,
 * "email không hợp lệ" tells the user nothing about which one.
 */
import { useId, useMemo, useRef, useState, type KeyboardEvent, type ClipboardEvent } from 'react';
import {
  RECIPIENT_GROUP_LABELS,
  isWellFormedEmail,
  normalizeEmail,
  splitPastedRecipients,
  type EmailRecipientInput,
  type RecipientGroup,
} from '../types/recipients';

export interface RecipientChipInputProps {
  group: RecipientGroup;
  value: EmailRecipientInput[];
  onChange: (next: EmailRecipientInput[]) => void;
  /** Addresses already used in the other two groups — used to warn about cross-group duplicates. */
  takenElsewhere?: Set<string>;
  disabled?: boolean;
  /** Rendered under the field, e.g. a server-side rejection mapped back to this group. */
  externalError?: string | null;
  autoFocus?: boolean;
}

export function RecipientChipInput({
  group,
  value,
  onChange,
  takenElsewhere,
  disabled = false,
  externalError = null,
  autoFocus = false,
}: RecipientChipInputProps) {
  const [draft, setDraft] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const fieldId = useId();
  const errorId = `${fieldId}-error`;

  const label = RECIPIENT_GROUP_LABELS[group];
  const existing = useMemo(() => new Set(value.map(r => normalizeEmail(r.email))), [value]);

  /** Commits whatever is in the text box. Returns false when nothing was added. */
  const commit = (raw: string): boolean => {
    const candidates = splitPastedRecipients(raw);
    if (candidates.length === 0) return false;

    const accepted: EmailRecipientInput[] = [];
    const seen = new Set(existing);
    let rejection: string | null = null;

    for (const candidate of candidates) {
      if (!isWellFormedEmail(candidate)) {
        rejection ??= `Địa chỉ email không hợp lệ ở mục ${label}: '${candidate}'.`;
        continue;
      }
      const key = normalizeEmail(candidate);
      if (seen.has(key)) {
        rejection ??= `Địa chỉ '${candidate}' bị lặp trong cùng mục ${label}.`;
        continue;
      }
      if (takenElsewhere?.has(key)) {
        rejection ??=
          `Địa chỉ '${candidate}' đã có ở mục khác. Mỗi người nhận chỉ được thuộc một mục.`;
        continue;
      }
      seen.add(key);
      accepted.push({ email: candidate });
    }

    if (accepted.length > 0) onChange([...value, ...accepted]);

    // A rejected address stays in the box so it can be corrected instead of vanishing.
    setLocalError(rejection);
    return rejection === null;
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter' || event.key === ',' || event.key === ';' || event.key === 'Tab') {
      if (draft.trim().length === 0) return; // Tab with an empty box must still move focus.
      event.preventDefault();
      if (commit(draft)) setDraft('');
      return;
    }

    // Backspace on an empty box takes the last chip back into the text box, so a typo can be fixed
    // rather than retyped.
    if (event.key === 'Backspace' && draft.length === 0 && value.length > 0) {
      event.preventDefault();
      const last = value[value.length - 1];
      onChange(value.slice(0, -1));
      setDraft(last.email);
      setLocalError(null);
    }
  };

  const handlePaste = (event: ClipboardEvent<HTMLInputElement>) => {
    const text = event.clipboardData.getData('text');
    if (!/[,;\s]/.test(text)) return; // A single address pastes normally and is committed on Enter.
    event.preventDefault();
    if (commit(text)) setDraft('');
  };

  const remove = (index: number) => {
    onChange(value.filter((_, i) => i !== index));
    setLocalError(null);
    inputRef.current?.focus();
  };

  const error = externalError ?? localError;

  return (
    <div className="space-y-1">
      <label htmlFor={fieldId} className="block text-sm font-medium text-gray-700">
        {label}
      </label>

      <div
        className={[
          'flex flex-wrap items-center gap-1.5 rounded-lg border px-2 py-1.5 bg-white',
          error ? 'border-red-500' : 'border-gray-300',
          disabled ? 'opacity-60' : 'focus-within:ring-2 focus-within:ring-blue-500 focus-within:border-blue-500',
        ].join(' ')}
        onClick={() => inputRef.current?.focus()}
      >
        {value.map((recipient, index) => (
          <span
            key={`${normalizeEmail(recipient.email)}-${index}`}
            className="inline-flex items-center gap-1 rounded bg-gray-100 px-2 py-0.5 text-sm text-gray-800"
            data-testid={`chip-${group}`}
          >
            <span>{recipient.name ? `${recipient.name} <${recipient.email}>` : recipient.email}</span>
            {!disabled && (
              <button
                type="button"
                onClick={event => { event.stopPropagation(); remove(index); }}
                aria-label={`Xóa người nhận ${recipient.email} khỏi mục ${label}`}
                className="text-gray-500 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-blue-500 rounded"
              >
                ×
              </button>
            )}
          </span>
        ))}

        <input
          ref={inputRef}
          id={fieldId}
          type="text"
          value={draft}
          disabled={disabled}
          autoFocus={autoFocus}
          onChange={event => { setDraft(event.target.value); if (localError) setLocalError(null); }}
          onKeyDown={handleKeyDown}
          onPaste={handlePaste}
          onBlur={() => { if (draft.trim().length > 0 && commit(draft)) setDraft(''); }}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className="flex-1 min-w-[8rem] border-0 p-0 text-sm focus:outline-none focus:ring-0"
        />
      </div>

      {error && (
        // role="alert" so the message is announced, and the ✕ glyph means the error does not rely on
        // colour alone.
        <p id={errorId} role="alert" className="text-sm text-red-600">
          <span aria-hidden="true">✕ </span>{error}
        </p>
      )}
    </div>
  );
}

export default RecipientChipInput;
