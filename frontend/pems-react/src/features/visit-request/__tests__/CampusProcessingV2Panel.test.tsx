import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { useState } from 'react';
import type { CampusProcessingChoice, CreateHostCandidate } from '../api/visitRequestApi';

const getCreateHostCandidates = vi.fn();
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { getCreateHostCandidates: (...a: unknown[]) => getCreateHostCandidates(...a) },
}));

// i18n: render the key so assertions do not depend on copy.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

import { CampusProcessingV2Panel } from '../components/v2/CampusProcessingV2Panel';

const CANDIDATES: CreateHostCandidate[] = [
  { userId: 7, fullName: 'IC Staff A', hasScheduleConflict: false, conflictCount: 0 } as CreateHostCandidate,
  { userId: 8, fullName: 'IC Staff B', hasScheduleConflict: true, conflictCount: 2 } as CreateHostCandidate,
];

/** Renders the panel with real state so mode switches behave as they do in the form. */
function Harness(props: {
  campusCode: string;
  role: 'VISITOR' | 'STAFF' | 'STAFF_LEADER';
  ownCampusCode?: string;
  onValue?: (v: CampusProcessingChoice | undefined) => void;
}) {
  const [value, setValue] = useState<CampusProcessingChoice | undefined>(undefined);
  return (
    <CampusProcessingV2Panel
      campusCode={props.campusCode}
      role={props.role}
      ownCampusCode={props.ownCampusCode}
      value={value}
      onChange={next => { setValue(next); props.onValue?.(next); }}
    />
  );
}

describe('CampusProcessingV2Panel — per-campus processing affordances', () => {
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
    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-SEND_FOR_REVIEW-HN')).toBeTruthy();
    expect(screen.queryByTestId('campus-processing-ASSIGN_HOST-HN')).toBeNull();
  });

  it('gives a Staff Leader all three options on their OWN campus', () => {
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);
    expect(screen.getByTestId('campus-processing-SELF_HOST-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-ASSIGN_HOST-HN')).toBeTruthy();
    expect(screen.getByTestId('campus-processing-SEND_FOR_REVIEW-HN')).toBeTruthy();
  });

  it.each(['STAFF', 'STAFF_LEADER'] as const)(
    'shows %s a read-only routed notice for a campus outside their scope',
    role => {
      render(<Harness campusCode="HCM" role={role} ownCampusCode="HN" />);
      expect(screen.getByTestId('campus-processing-readonly-HCM')).toBeTruthy();
      expect(screen.queryByTestId('campus-processing-SELF_HOST-HCM')).toBeNull();
      expect(screen.queryByTestId('campus-processing-ASSIGN_HOST-HCM')).toBeNull();
      expect(screen.queryByTestId('campus-processing-host-HCM')).toBeNull();
      expect(getCreateHostCandidates).not.toHaveBeenCalled();
    },
  );

  it('loads candidates only once the Leader chooses assign', async () => {
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);
    expect(getCreateHostCandidates).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('campus-processing-ASSIGN_HOST-HN'));
    await waitFor(() => expect(screen.getByTestId('campus-processing-host-HN')).toBeTruthy());
    expect(getCreateHostCandidates).toHaveBeenCalledTimes(1);
  });

  it('clears a stale host id when the Leader switches away from assign', async () => {
    const onValue = vi.fn();
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" onValue={onValue} />);

    fireEvent.click(screen.getByTestId('campus-processing-ASSIGN_HOST-HN'));
    const picker = await screen.findByTestId('campus-processing-host-HN');
    fireEvent.change(picker, { target: { value: '7' } });
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'ASSIGN_HOST', hostUserId: 7 });

    fireEvent.click(screen.getByTestId('campus-processing-SELF_HOST-HN'));
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'SELF_HOST', hostUserId: null });

    fireEvent.click(screen.getByTestId('campus-processing-SEND_FOR_REVIEW-HN'));
    expect(onValue).toHaveBeenLastCalledWith({ campusId: 'HN', mode: 'SEND_FOR_REVIEW', hostUserId: null });
  });

  it('surfaces a candidate load failure with a retry instead of an empty picker', async () => {
    getCreateHostCandidates.mockRejectedValueOnce(new Error('boom'));
    render(<Harness campusCode="HN" role="STAFF_LEADER" ownCampusCode="HN" />);

    fireEvent.click(screen.getByTestId('campus-processing-ASSIGN_HOST-HN'));
    await screen.findByText('visitRequest:campusProcessing.candidatesError');
    expect(screen.queryByTestId('campus-processing-host-HN')).toBeNull();

    fireEvent.click(screen.getByText('visitRequest:campusProcessing.retryCandidates'));
    await waitFor(() => expect(screen.getByTestId('campus-processing-host-HN')).toBeTruthy());
  });
});
