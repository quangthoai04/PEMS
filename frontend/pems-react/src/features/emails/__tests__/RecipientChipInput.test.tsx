/**
 * Behaviour of the shared recipient field. These assert what the user can do and what leaves the
 * component — not how it renders — because the point of the component is that TO, CC and BCC behave
 * identically, and a snapshot would not notice if one of them stopped.
 */
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { useState } from 'react';
import { RecipientChipInput } from '../components/RecipientChipInput';
import type { EmailRecipientInput, RecipientGroup } from '../types/recipients';

/** Wrapper that holds state, so the component is exercised the way the modal uses it. */
function Harness({
  group = 'TO' as RecipientGroup,
  initial = [] as EmailRecipientInput[],
  takenElsewhere,
  onChangeSpy,
}: {
  group?: RecipientGroup;
  initial?: EmailRecipientInput[];
  takenElsewhere?: Set<string>;
  onChangeSpy?: (v: EmailRecipientInput[]) => void;
}) {
  const [value, setValue] = useState<EmailRecipientInput[]>(initial);
  return (
    <RecipientChipInput
      group={group}
      value={value}
      takenElsewhere={takenElsewhere}
      onChange={next => { setValue(next); onChangeSpy?.(next); }}
    />
  );
}

const typeAndCommit = (text: string, key = 'Enter') => {
  const input = screen.getByRole('textbox');
  fireEvent.change(input, { target: { value: text } });
  fireEvent.keyDown(input, { key });
  return input;
};

describe('RecipientChipInput', () => {
  it('turns a valid address into a chip and clears the box', () => {
    const spy = vi.fn();
    render(<Harness onChangeSpy={spy} />);
    const input = typeAndCommit('ha@fpt.edu.vn');

    expect(screen.getByTestId('chip-TO')).toHaveTextContent('ha@fpt.edu.vn');
    expect(spy).toHaveBeenCalledWith([{ email: 'ha@fpt.edu.vn' }]);
    expect(input).toHaveValue('');
  });

  it.each(['Enter', ',', ';'])('commits on %s', key => {
    render(<Harness />);
    typeAndCommit('a@fpt.vn', key);
    expect(screen.getByTestId('chip-TO')).toBeInTheDocument();
  });

  it('removes a chip through its accessible button', () => {
    render(<Harness initial={[{ email: 'a@fpt.vn' }, { email: 'b@fpt.vn' }]} />);
    fireEvent.click(screen.getByRole('button', { name: /Xóa người nhận a@fpt\.vn khỏi mục Đến/ }));

    expect(screen.queryByText('a@fpt.vn')).not.toBeInTheDocument();
    expect(screen.getByText('b@fpt.vn')).toBeInTheDocument();
  });

  it('reports an invalid address at the field and keeps the text so it can be fixed', () => {
    render(<Harness />);
    const input = typeAndCommit('not-an-email');

    expect(screen.getByRole('alert')).toHaveTextContent('không hợp lệ');
    expect(input).toHaveValue('not-an-email');           // not silently dropped
    expect(screen.queryByTestId('chip-TO')).not.toBeInTheDocument();
  });

  it('refuses a duplicate in the same group, ignoring case', () => {
    render(<Harness initial={[{ email: 'ha@fpt.vn' }]} />);
    typeAndCommit('HA@FPT.VN');

    expect(screen.getByRole('alert')).toHaveTextContent('bị lặp');
    expect(screen.getAllByTestId('chip-TO')).toHaveLength(1);
  });

  it('refuses an address already used in another group', () => {
    render(<Harness group="CC" takenElsewhere={new Set(['ha@fpt.vn'])} />);
    typeAndCommit('Ha@Fpt.vn');

    expect(screen.getByRole('alert')).toHaveTextContent('chỉ được thuộc một mục');
    expect(screen.queryByTestId('chip-CC')).not.toBeInTheDocument();
  });

  it('accepts the valid addresses from a paste and reports only the bad one', () => {
    render(<Harness />);
    const input = screen.getByRole('textbox');
    fireEvent.paste(input, { clipboardData: { getData: () => 'a@fpt.vn, oops, b@fpt.vn' } });

    expect(screen.getAllByTestId('chip-TO')).toHaveLength(2);
    expect(screen.getByRole('alert')).toHaveTextContent('oops');
  });

  it('takes the last chip back into the box on Backspace, so a typo can be corrected', () => {
    render(<Harness initial={[{ email: 'a@fpt.vn' }, { email: 'typo@fpt.vn' }]} />);
    const input = screen.getByRole('textbox');
    fireEvent.keyDown(input, { key: 'Backspace' });

    expect(input).toHaveValue('typo@fpt.vn');
    expect(screen.getAllByTestId('chip-TO')).toHaveLength(1);
  });

  it('commits a pending address on blur rather than losing it', () => {
    render(<Harness />);
    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: 'a@fpt.vn' } });
    fireEvent.blur(input);

    expect(screen.getByTestId('chip-TO')).toBeInTheDocument();
  });

  it('lets Tab move focus when the box is empty instead of swallowing it', () => {
    render(<Harness />);
    const input = screen.getByRole('textbox');
    const event = fireEvent.keyDown(input, { key: 'Tab' });
    expect(event).toBe(true);   // not preventDefault-ed
  });

  describe('accessibility', () => {
    it('labels the field and links the error to the input', () => {
      render(<Harness />);
      const input = screen.getByRole('textbox');
      expect(screen.getByText('Đến')).toBeInTheDocument();

      typeAndCommit('bad');
      expect(input).toHaveAttribute('aria-invalid', 'true');
      expect(input.getAttribute('aria-describedby')).toBe(screen.getByRole('alert').id);
    });

    it('does not signal the error with colour alone', () => {
      render(<Harness />);
      typeAndCommit('bad');
      expect(screen.getByRole('alert').textContent).toContain('✕');
    });

    it('gives every remove button a distinct accessible name', () => {
      render(<Harness initial={[{ email: 'a@fpt.vn' }, { email: 'b@fpt.vn' }]} />);
      expect(screen.getByRole('button', { name: /a@fpt\.vn/ })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /b@fpt\.vn/ })).toBeInTheDocument();
    });
  });

  it('uses the group label in its messages so CC and BCC do not say "Đến"', () => {
    render(<Harness group="BCC" />);
    typeAndCommit('bad');
    expect(screen.getByRole('alert')).toHaveTextContent('BCC');
  });
});
