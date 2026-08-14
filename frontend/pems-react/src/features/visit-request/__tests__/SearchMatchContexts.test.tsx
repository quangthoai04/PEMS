import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import SearchMatchContexts from '../components/SearchMatchContexts';
import type { SearchMatchContext } from '../../delegations/types/delegations.types';

// Test env renders EN labels (jsdom navigator.language = en-US per src/test/setup.ts).

describe('SearchMatchContexts', () => {
  it('renders nothing when there are no contexts', () => {
    const { container } = render(<SearchMatchContexts contexts={[]} />);
    expect(container).toBeEmptyDOMElement();
    const { container: c2 } = render(<SearchMatchContexts contexts={null} />);
    expect(c2).toBeEmptyDOMElement();
  });

  it('renders a campus context with the campus name and mapped field label', () => {
    const contexts: SearchMatchContext[] = [
      { scope: 'CAMPUS', visitInstanceId: 10, campusId: 2, campusName: 'FPTU HCM', matchedFields: ['DELEGATION_NAME'] },
    ];
    render(<SearchMatchContexts contexts={contexts} />);
    expect(screen.getByText('FPTU HCM')).toBeInTheDocument();
    expect(screen.getByText('Delegation name')).toBeInTheDocument();
    expect(screen.getByText(/Matched in/)).toBeInTheDocument();
  });

  it('renders a request-level context as general info', () => {
    const contexts: SearchMatchContext[] = [
      { scope: 'REQUEST', matchedFields: ['REQUEST_CODE'] },
    ];
    render(<SearchMatchContexts contexts={contexts} />);
    expect(screen.getByText('General info')).toBeInTheDocument();
    expect(screen.getByText('Request code')).toBeInTheDocument();
  });

  it('renders one entry per authorized campus context', () => {
    const contexts: SearchMatchContext[] = [
      { scope: 'CAMPUS', visitInstanceId: 10, campusId: 1, campusName: 'FPTU HN', matchedFields: ['DELEGATION_NAME'] },
      { scope: 'CAMPUS', visitInstanceId: 11, campusId: 2, campusName: 'FPTU HCM', matchedFields: ['DELEGATION_NAME'] },
    ];
    render(<SearchMatchContexts contexts={contexts} />);
    expect(screen.getByText('FPTU HN')).toBeInTheDocument();
    expect(screen.getByText('FPTU HCM')).toBeInTheDocument();
    expect(screen.getAllByText('Delegation name')).toHaveLength(2);
  });

  // NP-01. These three codes are what the search was matching on with nothing to show for it: the
  // predicate searched the registrant's name, nationality and job title, the match-context builder
  // did not know about them, and a row found by somebody's name came back with an empty
  // `matchedContexts` — so this component rendered no "Khớp tại" line for a hit it plainly had. A
  // missing LABEL would reproduce the same symptom one layer up (the code printed at the user), so
  // each one is pinned.
  it.each([
    ['REGISTRANT_FULL_NAME', 'Registrant name'],
    ['REGISTRANT_NATIONALITY', 'Registrant nationality'],
    ['REGISTRANT_JOB_TITLE', 'Registrant job title'],
  ])('labels the %s match', (code, label) => {
    render(<SearchMatchContexts contexts={[{ scope: 'REQUEST', matchedFields: [code] }]} />);
    expect(screen.getByText(label)).toBeInTheDocument();
    // The stable code is never what the user reads.
    expect(screen.queryByText(code)).not.toBeInTheDocument();
  });

  it('falls back to the raw code for an unknown field (no crash, no raw value)', () => {
    const contexts: SearchMatchContext[] = [
      { scope: 'CAMPUS', visitInstanceId: 10, campusId: 2, campusName: 'FPTU HCM', matchedFields: ['SOME_NEW_FIELD'] },
    ];
    render(<SearchMatchContexts contexts={contexts} />);
    expect(screen.getByText('SOME_NEW_FIELD')).toBeInTheDocument();
  });
});
