import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { delegationsApi } from '../../../delegations/api/delegationsApi';
import type { HostCandidate } from '../../../delegations/types/delegations.types';
import { getApiErrorMessage } from '../../../../shared/utils/toast';
import { HostSelectionModalView } from '../../../../components/modals/HostSelectionModalView';

interface Props {
  visitInstanceId: number;
  campusName: string;
  onCancel: () => void;
  /** Hands the choice back; the CALLER makes the write, so the approval can ride along with an edit. */
  onConfirm: (hostUserId: number, decisionNote: string | null) => void;
}

/**
 * Picks the Host for a campus that is about to be approved from "Lưu và duyệt".
 *
 * <p>Deliberately does not approve anything itself — it returns the choice. Approving a campus assigns
 * its Host in the same act, and the Staff Leader's "Lưu và duyệt" additionally has an edit to commit
 * beside it; a picker that fired its own approve call would split that into two writes, and the campus
 * could end up rewritten but still waiting. <c>AssignHostModal</c> elsewhere in the app is the
 * standalone approval, and keeps its own call.</p>
 *
 * <p>Rendered through the SAME shared presentation as `AssignHostModal` (`HostSelectionModalView`) so
 * the two Host-picking screens stop drifting apart visually — only the title/CTA wording and the
 * write orchestration differ, per that component's own header comment.</p>
 *
 * <p>A schedule conflict is shown, not blocked: hosting two delegations at once is a judgement the
 * leader makes, and the backend records it as a warning on the approval rather than refusing it.</p>
 */
export default function AssignHostPicker({ visitInstanceId, campusName, onCancel, onConfirm }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [candidates, setCandidates] = useState<HostCandidate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [note, setNote] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    delegationsApi
      .getHostCandidates(visitInstanceId)
      .then(rows => { if (!cancelled) { setCandidates(rows); setError(null); } })
      .catch(err => { if (!cancelled) setError(getApiErrorMessage(err, t('visitRequestV2:pendingCampusEdit.hostLoadFailed'))); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitInstanceId, t]);

  return (
    <HostSelectionModalView
      title={t('visitRequestV2:pendingCampusEdit.hostTitleSaveAndApprove')}
      subtitle={campusName}
      infoBanner={t('visitRequestV2:pendingCampusEdit.hostRequired')}
      candidates={candidates}
      isLoading={loading}
      loadError={error}
      selectedId={selectedId}
      onSelect={setSelectedId}
      decisionNote={note}
      onDecisionNoteChange={setNote}
      decisionNoteLabel={t('visitRequestV2:pendingCampusEdit.decisionNote')}
      isSubmitting={false}
      submitError={null}
      reloadLabel={t('visitRequestV2:edit.reload')}
      confirmLabel={t('visitRequestV2:pendingCampusEdit.saveAndApprove')}
      // No API call here — see the header comment. The parent's own `send()` performs the ONE combined
      // edit+approve write, and reports its own submit/error/conflict state on the page itself.
      onSubmit={(hostUserId) => onConfirm(hostUserId, note.trim() || null)}
      onClose={onCancel}
      searchPlaceholder={t('visitRequestV2:pendingCampusEdit.hostSearchPlaceholder')}
      emptyCandidatesText={t('visitRequestV2:pendingCampusEdit.hostNone')}
      noMatchText={t('visitRequestV2:pendingCampusEdit.hostNoMatch')}
      conflictLabel={(count) => t('visitRequestV2:pendingCampusEdit.hostConflict', { count })}
      conflictOverlayTitle={t('visitRequestV2:pendingCampusEdit.hostConflictOverlayTitle')}
      conflictOverlayBody={(fullName) => t('visitRequestV2:pendingCampusEdit.hostConflictOverlayBody', { name: fullName })}
      conflictOverlayCancel={t('visitRequestV2:pendingCampusEdit.hostConflictOverlayCancel')}
      conflictOverlayConfirm={t('visitRequestV2:pendingCampusEdit.saveAndApprove')}
      cancelLabel={t('visitRequestV2:common.cancel')}
      submittingLabel={t('visitRequestV2:pendingCampusEdit.saveAndApprove')}
      loadingCandidatesLabel={t('visitRequestV2:detail.loading')}
      hostCardTestId={(userId) => `pending-campus-host-${userId}`}
      confirmTestId="pending-campus-host-confirm"
      decisionNoteTestId="pending-campus-decision-note"
      labels={{
        selfHostBadge: t('visitRequestV2:pendingCampusEdit.hostSelfBadge'),
        leaderBadge: t('visitRequestV2:pendingCampusEdit.hostLeaderBadge'),
        currentHostBadge: t('visitRequestV2:pendingCampusEdit.hostCurrentBadge'),
        noConflict: t('visitRequestV2:pendingCampusEdit.hostNoConflict'),
        hasConflict: t('visitRequestV2:pendingCampusEdit.hostHasConflict'),
        conflictSourceCalendar: t('visitRequestV2:pendingCampusEdit.hostConflictSourceCalendar'),
        conflictSourceVisit: t('visitRequestV2:pendingCampusEdit.hostConflictSourceVisit'),
      }}
    />
  );
}
