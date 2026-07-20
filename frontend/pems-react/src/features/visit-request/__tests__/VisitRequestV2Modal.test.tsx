import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';

/**
 * The modal shell only: it must present the SHARED form (never a copy), keep the body as the sole
 * scroll region so the sticky header/footer stay put, and refuse to throw away typed data on a
 * stray Esc or backdrop click.
 */

let lastFormProps: Record<string, unknown> = {};
vi.mock('../components/v2/VisitRequestFormV2', () => ({
  VisitRequestFormV2: (props: Record<string, unknown>) => {
    lastFormProps = props;
    return <div data-testid="shared-v2-form" />;
  },
}));

vi.mock('../components/v2/VisitRequestV2SuccessPanel', () => ({
  VisitRequestV2SuccessPanel: () => <div data-testid="v2-success" />,
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key, i18n: { language: 'vi' } }),
}));

import { VisitRequestV2Modal } from '../components/v2/VisitRequestV2Modal';

const open = (onClose = vi.fn()) => {
  const utils = render(
    <VisitRequestV2Modal isOpen onClose={onClose} mode="authenticated" onSuccess={vi.fn()} />,
  );
  return { ...utils, onClose };
};

/** Simulates the user typing into the form (the shared form reports dirtiness upward). */
const markDirty = () => act(() => {
  (lastFormProps.onDirtyChange as (d: boolean) => void)(true);
});

describe('VisitRequestV2Modal', () => {
  beforeEach(() => { lastFormProps = {}; });

  it('renders nothing when closed', () => {
    const { container } = render(
      <VisitRequestV2Modal isOpen={false} onClose={vi.fn()} mode="public" onSuccess={vi.fn()} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('hosts the SHARED form component rather than a modal-only copy', () => {
    open();
    expect(screen.getByTestId('shared-v2-form')).toBeTruthy();
    expect(lastFormProps.mode).toBe('authenticated');
  });

  it('scrolls only the body, so the sticky header and footer stay in place', () => {
    open();
    const dialog = screen.getByTestId('v2-create-modal');
    // Three explicit rows: header / body / footer. Only the middle one scrolls.
    expect(dialog.className).toContain('grid-rows-[auto_1fr_auto]');
    expect(dialog.className).toContain('overflow-hidden');

    const scrollables = Array.from(dialog.children).filter(c => c.className.includes('overflow-y-auto'));
    expect(scrollables).toHaveLength(1);
  });

  it('gives the form a footer node so submit actions render in the sticky footer', () => {
    open();
    expect(lastFormProps.footerSlot).toBe(screen.getByTestId('v2-modal-footer'));
  });

  it('closes immediately when nothing has been typed', () => {
    const { onClose } = open();
    fireEvent.click(screen.getByTestId('v2-modal-close'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('asks before discarding typed data, and honours "keep editing"', () => {
    const { onClose } = open();
    markDirty();

    fireEvent.click(screen.getByTestId('v2-modal-close'));
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText('visitRequestV2:modal.keepEditing'));
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('v2-modal-close'));
    fireEvent.click(screen.getByTestId('v2-modal-discard'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('Esc closes a clean form outright', () => {
    const { onClose } = open();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('Esc on a dirty form prompts instead of discarding', () => {
    const { onClose } = open();
    markDirty();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByTestId('v2-modal-discard')).toBeTruthy();
  });

  it('stays open on success and shows the receipt instead of vanishing', () => {
    const onSuccess = vi.fn();
    render(<VisitRequestV2Modal isOpen onClose={vi.fn()} mode="public" onSuccess={onSuccess} />);

    act(() => {
      (lastFormProps.onSuccess as (r: unknown, v: unknown) => void)(
        { requestCode: 'VR-1', instances: [], hasMixedCampusDetails: false }, {},
      );
    });

    expect(screen.getByTestId('v2-success')).toBeTruthy();
    expect(screen.queryByTestId('shared-v2-form')).toBeNull();
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });
});
