import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { useState } from 'react';
import type { CampusHostSelectionChoice, CreateHostCandidate } from '../api/visitRequestApi';

const getCreateHostCandidates = vi.fn();
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { getCreateHostCandidates: (...a: unknown[]) => getCreateHostCandidates(...a) },
}));

// i18n: render the key so assertions do not depend on copy.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

import { CampusHostSelectionV2Panel } from '../components/v2/CampusHostSelectionV2Panel';

const CANDIDATES: CreateHostCandidate[] = [
  { userId: 7, fullName: 'IC Staff A', hasScheduleConflict: false, conflictCount: 0 } as CreateHostCandidate,
  { userId: 8, fullName: 'IC Staff B', hasScheduleConflict: true, conflictCount: 2 } as CreateHostCandidate,
];

/** Renders the panel with real state so mode switches behave as they do in the form. */
function Harness(props: {
  campusCode: string;
  role: 'VISITOR' | 'STAFF' | 'STAFF_LEADER';
  ownCampusCode?: string;
  onValue?: (v: CampusHostSelectionChoice | undefined) => void;
}) {
  const [value, setValue] = useState<CampusHostSelectionChoice | undefined>(undefined);
  return (
    <CampusHostSelectionV2Panel
      campusCode={props.campusCode}
      role={props.role}
      ownCampusCode={props.ownCampusCode}
      value={value}
      onChange={next => { setValue(next); props.onValue?.(next); }}
    />
  );
}

describe('CampusHostSelectionV2Panel — per-campus processing affordances', () => {
  beforeEach(() => {
    getCreateHostCandidates.mockReset();
    getCreateHostCandidates.mockResolvedValue(CANDIDATES);
  });

  it('renders nothing for a public/visitor creator', () => {
    const { container } = render(<Harness campusCode="HN" role="VISITOR" ownCampusCode="HN" />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing until a campus has actually been picked', () => {
    const { container } = render(<Harness campusCode="" role="STAFF" ownCampusCode="HN" />);
    expect(container).toBeEmptyDOMElement();
  });

  it('offers a regular Staff self-host and ask-leader on their OWN campus, but never assign', () => {
    render(<Harness campusCode="HN" role="STAFF" ownCampusCode="HN" />);
    expect(screen.getByTestId('campus-host-selection-SELF-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN')).toBeTruthy();
    expect(screen.queryByTestId('campus-host-selection-SELECTED-HN')).toBeNull();
  });

  it('gives a Staff Leader all three options on their OWN campus', () => {
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);
    expect(screen.getByTestId('campus-host-selection-SELF-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-host-selection-SELECTED-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN')).toBeTruthy();
  });

  it.each(['STAFF', 'STAFF_LEADER'] as const)(
    'shows %s a read-only routed notice for a campus outside their scope',
    role => {
      render(<Harness campusCode="HCM" role={role} ownCampusCode="HN" />);
      expect(screen.getByTestId('campus-host-selection-readonly-HCM')).toBeTruthy();
      expect(screen.queryByTestId('campus-host-selection-SELF-HCM')).toBeNull();
      expect(screen.queryByTestId('campus-host-selection-SELECTED-HCM')).toBeNull();
      expect(screen.queryByTestId('campus-host-selection-host-HCM')).toBeNull();
      expect(getCreateHostCandidates).not.toHaveBeenCalled();
    },
  );

  it('loads candidates only once the Leader chooses assign', async () => {
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);
    expect(getCreateHostCandidates).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('campus-host-selection-SELECTED-HN'));
    await waitFor(() => expect(screen.getByTestId('campus-host-selection-host-HN')).toBeTruthy());
    expect(getCreateHostCandidates).toHaveBeenCalledTimes(1);
  });

  it('clears a stale host id when the Leader switches away from assign', async () => {
    const onValue = vi.fn();
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" onValue={onValue} />);

    fireEvent.click(screen.getByTestId('campus-host-selection-SELECTED-HN'));
    const picker = await screen.findByTestId('campus-host-selection-host-HN');
    fireEvent.change(picker, { target: { value: '7' } });
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'SELECTED', proposedHostUserId: 7 });

    fireEvent.click(screen.getByTestId('campus-host-selection-SELF-HN'));
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'SELF', proposedHostUserId: null });

    fireEvent.click(screen.getByTestId('campus-host-selection-WAIT_FOR_LATER-HN'));
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'WAIT_FOR_LATER', proposedHostUserId: null });
  });

  it('surfaces a candidate load failure with a retry instead of an empty picker', async () => {
    getCreateHostCandidates.mockRejectedValueOnce(new Error('boom'));
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);

    fireEvent.click(screen.getByTestId('campus-host-selection-SELECTED-HN'));
    await screen.findByText('visitRequest:campusProcessing.candidatesError');
    expect(screen.queryByTestId('campus-host-selection-host-HN')).toBeNull();

    fireEvent.click(screen.getByText('visitRequest:campusProcessing.retryCandidates'));
    await waitFor(() => expect(screen.getByTestId('campus-host-selection-host-HN')).toBeTruthy());
  });
});
