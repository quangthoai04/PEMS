import { describe, expect, it } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { HelpTooltip } from '../components/shared/HelpTooltip';

/**
 * UX-01. The guidance for "Đầu mối là ai trong đoàn?" moved out of a standing paragraph and into
 * this `?`. That is only an improvement if the `?` can actually be reached: the previous version was
 * a hover-only `<div>` — no keyboard could open it, a tap did nothing, and the text it hid was
 * announced to nobody. Guidance that exists for a mouse only is guidance half the users cannot have.
 */
describe('HelpTooltip', () => {
  const content = 'Chọn một người trong danh sách khách hoặc nhân sự hỗ trợ.';

  const setup = () => {
    render(<HelpTooltip testId="tip" label="Đầu mối là ai trong đoàn?" content={content} />);
    return screen.getByTestId('tip');
  };

  it('starts closed', () => {
    setup();
    expect(screen.getByRole('tooltip', { hidden: true })).not.toBeVisible();
  });

  it('opens on hover', () => {
    const trigger = setup();
    fireEvent.mouseEnter(trigger);
    expect(screen.getByRole('tooltip')).toHaveTextContent(content);
    fireEvent.mouseLeave(trigger);
    expect(screen.getByRole('tooltip', { hidden: true })).not.toBeVisible();
  });

  it('opens on keyboard focus — there is no hover on a keyboard', () => {
    const trigger = setup();
    fireEvent.focus(trigger);
    expect(screen.getByRole('tooltip')).toBeVisible();
    fireEvent.blur(trigger);
    expect(screen.getByRole('tooltip', { hidden: true })).not.toBeVisible();
  });

  it('toggles on click, which is the only way in on a touch screen', () => {
    const trigger = setup();
    fireEvent.click(trigger);
    expect(screen.getByRole('tooltip')).toBeVisible();
    fireEvent.click(trigger);
    expect(screen.getByRole('tooltip', { hidden: true })).not.toBeVisible();
  });

  it('is reachable by keyboard at all — a div was not', () => {
    expect(setup().tagName).toBe('BUTTON');
  });

  it('describes the trigger only while the text is on screen', () => {
    const trigger = setup();
    // Pointing at a hidden element would have a screen reader read out a description the sighted
    // user is not being shown.
    expect(trigger).not.toHaveAttribute('aria-describedby');
    fireEvent.focus(trigger);
    const tooltip = screen.getByRole('tooltip');
    expect(trigger).toHaveAttribute('aria-describedby', tooltip.id);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
  });

  it('names what it is about, not "help"', () => {
    expect(setup()).toHaveAccessibleName('Đầu mối là ai trong đoàn?');
  });

  it('closes on Escape', () => {
    const trigger = setup();
    fireEvent.focus(trigger);
    fireEvent.keyDown(trigger, { key: 'Escape' });
    expect(screen.getByRole('tooltip', { hidden: true })).not.toBeVisible();
  });
});
