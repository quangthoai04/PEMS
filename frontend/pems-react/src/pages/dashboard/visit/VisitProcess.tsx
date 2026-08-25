/**
 * Trang VisitProcess
 * Chu kỳ giám sát chung toàn trình quá trình đón tiếp theo giai đoạn (Trước/Trong/Sau).
 */

import React, { useState, useEffect, useRef } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ChevronDown,
  ChevronUp,
  Clock,
  CheckCircle2,
  Mail,
  Bell,
  Plus,
  Edit3,
  X,
  AlertCircle,
  Lock,
  Loader2,
  ArrowRightCircle,
  Wand2,
  Download,
  FileText,
  Calendar,
  Info
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { VisitDuringTab } from './VisitDuringTab';
import { VisitAfterTab } from './VisitAfterTab';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitProcessPermission, VisitProcessDetail, SetupProgressEmailMessage } from '../../../features/delegations/types/delegations.types';
import { EmailComposeModal } from '../../../features/emails/components/EmailComposeModal';
import { AgendaSetupPanel } from '../../../features/agenda-templates/components/AgendaSetupPanel';
import { ParticipantInvitationSection } from '../../../features/delegations/components/ParticipantInvitationSection';
import { LogisticsRequestSection } from '../../../features/delegations/components/LogisticsRequestSection';
import { RegistrantInfoReadOnly, DelegationInfoReadOnly } from '../../../features/delegations/components/RequestInfoReadOnly';
import { VisitorVisitDetailPage } from './VisitorVisitDetailPage';
import { formatVietnamTime, formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage } from '../../../shared/utils/toast';
import { StaleDataBanner } from '../../../shared/components/state';
import { canSubmitReminders } from './visitProcessGuards';

// Lightweight in-page toast (top-right) — cùng pattern với CampusManagement/VisitRequestManagement.
type ProcessToast = { id: number; type: 'success' | 'error' | 'warning' | 'info'; msg: string };

// ── "Cảnh báo & Thông báo" (Part C): 4 independent configs = channel × target group. ──
const REMINDER_CONFIGS = [
  { key: 'sysHost', channel: 'IN_APP', targetGroup: 'HOST', title: 'Thông báo hệ thống cho người phụ trách', desc: 'Thông báo trên hệ thống tới người phụ trách tiếp đón' },
  { key: 'emailHost', channel: 'EMAIL', targetGroup: 'HOST', title: 'Email nhắc người phụ trách', desc: 'Gửi email nhắc nhở người phụ trách tiếp đón' },
  { key: 'sysParticipants', channel: 'IN_APP', targetGroup: 'PARTICIPANTS', title: 'Thông báo hệ thống cho thành phần tham gia', desc: 'Thông báo trên hệ thống tới thành phần tham gia' },
  { key: 'emailParticipants', channel: 'EMAIL', targetGroup: 'PARTICIPANTS', title: 'Email nhắc thành phần tham gia', desc: 'Gửi email nhắc nhở thành phần tham gia' },
] as const;
type ReminderKey = typeof REMINDER_CONFIGS[number]['key'];
type ReminderRow = { enabled: boolean; days: number; time: string };

/**
 * What each stage transition asks, and — the part that matters — what it COSTS (NP-05).
 *
 * A stage transition is one-way and it closes an editing window. The "before" one is the sharpest:
 * the moment it lands, the whole preparation tab becomes read-only, and until now the Host found
 * that out afterwards. So each confirmation names the consequence in the same words the tab uses
 * ("lịch trình, thành phần tham gia, hậu cần, cảnh báo, ghi chú chuẩn bị") rather than asking a bare
 * "bạn có chắc không?".
 *
 * `notice` is the inline sentence on the confirm bar; `consequence` is the fuller version in the
 * dialog. They agree by construction because they live in one table.
 */
const STAGE_CONFIRM: Record<'before' | 'during' | 'after', {
  title: string;
  notice: string;
  consequence: string;
  confirmLabel: string;
}> = {
  before: {
    title: 'Xác nhận chuyển sang “Trong tiếp khách”?',
    notice: 'Sau khi xác nhận, thông tin ở tab này sẽ khóa và chuyển sang bước Trong tiếp khách.',
    consequence:
      'Sau khi chuyển giai đoạn, toàn bộ thông tin trong tab “Trước tiếp khách” sẽ chỉ còn chế độ xem. '
      + 'Bạn sẽ không thể tạo, sửa hoặc xóa lịch trình, thành phần tham gia, hậu cần, cảnh báo và ghi chú chuẩn bị.',
    confirmLabel: 'Xác nhận chuyển',
  },
  during: {
    title: 'Xác nhận kết thúc tiếp khách?',
    notice: 'Sau khi xác nhận, quy trình sẽ chuyển sang giai đoạn Sau tiếp khách.',
    consequence:
      'Quy trình sẽ chuyển sang giai đoạn “Sau tiếp khách”. Các thao tác của giai đoạn đang tiếp khách '
      + 'sẽ dừng lại, và hệ thống mời các bên đánh giá chuyến thăm.',
    confirmLabel: 'Xác nhận kết thúc',
  },
  after: {
    title: 'Xác nhận đóng đoàn?',
    notice: 'Sau khi đóng đoàn, toàn bộ quy trình sẽ chuyển sang chế độ chỉ xem.',
    consequence:
      'Đóng đoàn là bước cuối cùng: toàn bộ quy trình tiếp khách sẽ chuyển sang chế độ chỉ xem và '
      + 'hồ sơ được lưu trữ. Bạn sẽ không thể chỉnh sửa bất kỳ nội dung nào sau bước này.',
    confirmLabel: 'Xác nhận đóng đoàn',
  },
};

export function VisitProcess() {
  const navigate = useNavigate();
  const location = useLocation();
  const returnUrl = location.state?.returnTo ?? '/dashboard/visit';
  const { id } = useParams();

  const { user } = useAuthContext();
  const roleCode = (user?.roleCode || '').toUpperCase();
  
  const isHO = roleCode === 'HO';
  const isDept = roleCode === 'DEPARTMENT' || roleCode === 'DEPT' || roleCode === 'STUDENT' || roleCode === 'VISITOR';
  const isStudent = roleCode === 'STUDENT';
  const isVisitor = roleCode === 'VISITOR';

  // The rest of this page is a Staff/Host operational tool (Vietnamese-only by design); only the
  // two isVisitor-gated sections below (Album/News) and the generic access-denied shell are
  // genuinely VISITOR-reachable, so only those go through `tt`.
  const { t, i18n } = useTranslation('visitTasks');
  const tt = (key: string) => t(key, { lng: isVisitor ? i18n.language : 'vi' });

  const [currentStatus, setCurrentStatus] = useState(() => {
    if (location.state?.status) return location.state.status;
    if (location.state?.isPrep) return 'Đang chuẩn bị';
    return (id === '1' ? 'Đang chuẩn bị' : 
            id === '4' ? 'Trong tiếp khách' : 
            (id === '2' || id === '5') ? 'Chờ đóng đoàn' : 'Trong tiếp khách');
  });

  const isReceptionDetail = window.location.pathname.includes('/reception-detail');
  const isReadOnlyRoute = location.state?.isReadOnly || isReceptionDetail || false;
  // Campus đã hủy mở ở chế độ xem-lại read-only (nội bộ): khóa toàn bộ thao tác + hiện banner riêng.
  const isCancelledView = location.state?.cancelled === true || location.state?.status === 'Đã hủy';
  const isClosed = currentStatus === 'Đã đóng đoàn' || currentStatus === 'Đã kết thúc' || isReadOnlyRoute || isCancelledView;

  const renderEmptyState = () => (
    <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px] animate-in fade-in duration-300">
      <div className="w-20 h-20 bg-slate-100 rounded-full flex items-center justify-center mb-6">
        <Clock className="w-10 h-10 text-slate-400 stroke-[1.5]" />
      </div>
      <h2 className="text-xl font-bold text-slate-800 mb-2 font-sans tracking-tight">Chưa đến giai đoạn này</h2>
      <p className="text-gray-500 font-normal max-w-sm mx-auto leading-relaxed text-sm">
        Giai đoạn này sẽ được mở khóa sau khi hoàn tất các bước trước đó trong quy trình tiếp khách.
      </p>
    </div>
  );

  const [activeTab, setActiveTab] = useState(isReceptionDetail ? 'before' : (location.state?.defaultTab || 'before'));
  const isPrep = (currentStatus === 'Đang chuẩn bị' || currentStatus === 'Trước tiếp khách') && !isClosed;
  // Vào tab "Trước tiếp khách" mở sẵn cả "1. Thông tin chung" (kèm ba mục con) lẫn
  // "2. Chuẩn bị chi tiết": Host cần nhìn thấy toàn bộ phần chuẩn bị ngay khi vào trang thay vì
  // phải bung từng khối. Thu gọn thủ công vẫn giữ nguyên — đây chỉ là trạng thái mặc định.
  // Effect bên dưới đặt lại đúng năm trạng thái này mỗi lần quay lại tab.
  const [isInfoExpanded, setIsInfoExpanded] = useState(true);
  const [isSetupExpanded, setIsSetupExpanded] = useState(true);
  const [isAlbumExpanded, setIsAlbumExpanded] = useState(false);
  const [isNewsExpanded, setIsNewsExpanded] = useState(false);
  
  const [isSection1Expanded, setIsSection1Expanded] = useState(true);
  const [isSection2Expanded, setIsSection2Expanded] = useState(true);
  const [isSection3Expanded, setIsSection3Expanded] = useState(true);
  const [isSection4Expanded, setIsSection4Expanded] = useState(true);

  const [isInfoSection1Expanded, setIsInfoSection1Expanded] = useState(true);
  const [isInfoSection2Expanded, setIsInfoSection2Expanded] = useState(true);
  const [isInfoSection3Expanded, setIsInfoSection3Expanded] = useState(true);

  const [isInfoEditableState, setIsInfoEditable] = useState(false);

  // Mỗi khi vào (hoặc quay lại) tab "Trước tiếp khách": mở lại cả năm khối — "1. Thông tin chung"
  // với ba mục con (Thông tin người tạo / Thông tin đoàn khách / Thiết lập & Điều phối sự kiện) và
  // "2. Chuẩn bị chi tiết". Rời tab rồi quay lại là một lần vào mới, nên trạng thái thu gọn thủ
  // công của lần trước không được giữ lại.
  useEffect(() => {
    if (activeTab === 'before') {
      setIsInfoExpanded(true);
      setIsSetupExpanded(true);
      setIsInfoSection1Expanded(true);
      setIsInfoSection2Expanded(true);
      setIsInfoSection3Expanded(true);
    }
  }, [activeTab]);

  // Phase 2: backend permission flags are the source of truth for tab view/edit. Fetched by
  // visitInstanceId; if unavailable (e.g. prototype/mock id), we fall back to the legacy
  // client-side role computation so the page still renders.
  const [perm, setPerm] = useState<VisitProcessPermission | null>(null);
  const [permLoadFailed, setPermLoadFailed] = useState(false);
  const [isLoadingPerm, setIsLoadingPerm] = useState(true);
  const numericId = Number(id);
  const hasNumericId = Number.isFinite(numericId) && numericId > 0;

  // Backend permission flags are the source of truth for tab view/edit + stage transitions.
  // Reusable so we can refetch after a stage transition to unlock the next tab.
  const loadPermissions = React.useCallback(async () => {
    if (!hasNumericId) { setPerm(null); setIsLoadingPerm(false); return; }
    try {
      const p = await delegationsApi.getVisitProcessPermissions(numericId);
      setPerm(p);
      setPermLoadFailed(false);
    } catch {
      setPerm(null);
      setPermLoadFailed(true);
    } finally {
      setIsLoadingPerm(false);
    }
  }, [numericId, hasNumericId]);

  useEffect(() => { void loadPermissions(); }, [loadPermissions]);

  // ── Toasts (top-right) ──
  const [toasts, setToasts] = useState<ProcessToast[]>([]);
  const pushToast = (type: ProcessToast['type'], msg: string) => {
    const tid = Date.now() + Math.floor(Math.random() * 1000);
    setToasts((prev) => [...prev, { id: tid, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== tid)), 4500);
  };
  /**
   * Delegates to the shared extractor rather than re-reading `response.data` here.
   *
   * The local copy this replaces skipped `errorCode` entirely and took `data.message` first, so a
   * backend that had classified a failure precisely — GOOGLE_DRIVE_TOKEN_EXPIRED, say — was rendered
   * as whatever prose happened to be attached to it, in whatever language the server wrote it. The
   * shared helper resolves the code against `errors:api.*` first, which is where the Vietnamese and
   * English wordings for these codes live, and masks credentials on the way out.
   */
  const apiErrorMessage = (e: any, fallback: string): string => getApiErrorMessage(e, fallback);

  // Before-tab setup/agenda/logistics/participants are NOT yet backed by a persistence API
  // (PrepareVisitLogistics / UpdateVisitLogistics and the agenda/participant saves are still
  // server stubs). They are therefore shown READ-ONLY — never fake-editable — so a Host can't
  // type into a form that silently drops the data. Flip this to the backend flag once the real
  // save endpoints exist (see report for the exact APIs required).
  const SETUP_SAVE_AVAILABLE = false;
  // `canEditBefore` / `isInfoEditable` are defined further down, next to `canMutateBeforeVisit`:
  // they are the same gate, and one of them reading a different status than the others is exactly
  // the class of bug NP-04 is about.
  // Tab visibility (backend says every in-scope role may at least view all tabs read-only).
  const canViewBefore = !!perm?.canViewBeforeVisit;
  const canViewDuring = !!perm?.canViewDuringVisit;
  const canViewAfter = !!perm?.canViewAfterVisit;
  // During/After read-only unless backend grants edit (fallback: legacy isClosed gate).
  const duringReadOnly = perm ? !perm.canEditDuringVisit : isClosed;
  const afterReadOnly = perm ? !perm.canEditAfterVisit : isClosed;

  // ── Status-driven tab lock/unlock (source of truth = instance status from backend). ──
  // A tab unlocks only once the instance has actually advanced (status updated by the API),
  // never by frontend state alone. When perm is unavailable (legacy/mock id) we fall back to the
  // old "all tabs viewable" behavior so the prototype ids still render.
  const stageRank = (s?: string | null): number => {
    switch (s) {
      case 'ASSIGNED':
      case 'BEFORE_VISIT': return 1;
      case 'DURING_VISIT': return 2;
      case 'AFTER_VISIT': return 3;
      case 'CLOSED': return 4;
      default: return 0; // WAITING_*/CANCELLED
    }
  };
  const instRank = stageRank(perm?.instanceStatus);
  const duringUnlocked = perm ? instRank >= 2 : true;
  const afterUnlocked = perm ? instRank >= 3 : true;

  // §10: Host xác nhận chuyến này không cần bài tin tức — gửi kèm khi đóng đoàn (tab Sau). Backend
  // chỉ chấp nhận đóng đoàn khi có bài tin tức đã duyệt HOẶC cờ này = true.
  const [confirmNoNews, setConfirmNoNews] = useState(false);

  // Stage transition (Host only). Only unlocks the next tab AFTER the API confirms the new status.
  const [stageSubmitting, setStageSubmitting] = useState(false);
  // ASSIGNED → BEFORE_VISIT. Only ever from this click handler: the campus must not start preparing
  // because the Host opened the page, switched tabs or triggered a refetch.
  const [startingPreparation, setStartingPreparation] = useState(false);
  // Click-toggled (not hover-only), so the "what does this open" explanation reaches touch and
  // keyboard users exactly like mouse users — see the button markup below.
  const [showStartPrepInfo, setShowStartPrepInfo] = useState(false);
  const startPreparation = async () => {
    if (!perm || startingPreparation) return; // second guard = the double-click guard
    setStartingPreparation(true);
    try {
      await delegationsApi.startPreparation(perm.visitRequestId, perm.visitInstanceId);
      // Refetch rather than patching local state: the new status is what unlocks every setup
      // control, and the backend is the only thing entitled to say what it is. All three sources
      // together — permissions AND detail AND reminders — so no capability is left reading ASSIGNED.
      await refreshProcessState();
      pushToast('success', 'Đã bắt đầu giai đoạn chuẩn bị.');
    } catch (e: any) {
      pushToast('error', apiErrorMessage(e, 'Không thể bắt đầu chuẩn bị. Vui lòng thử lại.'));
      if (e?.response?.status === 409) { await refreshProcessState(); }
    } finally {
      setStartingPreparation(false);
    }
  };

  /**
   * Which stage the Host is being asked to confirm, or null when no dialog is open (NP-05).
   *
   * Every stage transition goes through a confirmation now, and the "before" one exists mainly to
   * say the thing the old flow never did: the preparation tab becomes READ-ONLY. That is not a
   * detail — it ends the window in which the agenda, the invitations, the logistics and the notes
   * can be changed at all — and it used to be discovered afterwards.
   */
  const [pendingStage, setPendingStage] = useState<'before' | 'during' | 'after' | null>(null);

  const advanceStage = async (stage: 'before' | 'during' | 'after') => {
    if (!perm || stageSubmitting) return;
    setStageSubmitting(true);
    try {
      if (stage === 'before') {
        await delegationsApi.completeBeforeVisit(perm.visitRequestId, perm.visitInstanceId);
      } else if (stage === 'during') {
        await delegationsApi.completeDuringVisit(perm.visitRequestId, perm.visitInstanceId);
      } else {
        await delegationsApi.completeAfterVisit(perm.visitRequestId, perm.visitInstanceId, confirmNoNews);
      }
      // Close the dialog only once the API has said yes: a failure must leave the user where they
      // were, looking at the error, rather than on a screen that pretends the stage moved.
      setPendingStage(null);
      // Refetch permissions AND detail AND reminders → every capability advances together, so the
      // preparation tab is read-only in the same render the toast appears in (NP-04).
      await refreshProcessState();
      // After moving to the next stage, jump the user to the TOP of the new tab so they never stay
      // stranded at the bottom of the page where the confirm button was (đặc tả mục 8.5).
      const scrollTabToTop = () => window.scrollTo({ top: 0, behavior: 'smooth' });
      if (stage === 'before') { pushToast('success', 'Đã xác nhận hoàn thành chuẩn bị.'); setActiveTab('during'); setCurrentStatus('Trong tiếp khách'); scrollTabToTop(); }
      else if (stage === 'during') { pushToast('success', 'Đã xác nhận kết thúc tiếp khách.'); setActiveTab('after'); setCurrentStatus('Chờ đóng đoàn'); scrollTabToTop(); }
      else { pushToast('success', 'Đã đóng đoàn thành công.'); setCurrentStatus('Đã đóng đoàn'); scrollTabToTop(); }
    } catch (e: any) {
      pushToast('error', apiErrorMessage(e, 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.'));
      // On a 409 the status changed under us — refetch so the UI reflects the real state.
      if (e?.response?.status === 409) { setPendingStage(null); await refreshProcessState(); }
    } finally {
      setStageSubmitting(false);
    }
  };

  // "Báo cáo Lịch trình" PDF — backend generates from real data (agenda, participants, form).
  // Fetched once into a blob, previewed in-modal (object URL in an iframe), and only written to
  // disk when the user explicitly clicks "Tải xuống" inside the modal — pure export, does not
  // touch the stage/lock state above. Ticking "Tiếng Anh" re-fetches with languageCode=en — the
  // backend auto-translates the whole report before rendering — and the download button then
  // writes that English PDF; leaving it unticked downloads the original Vietnamese PDF as before.
  const [scheduleReportModal, setScheduleReportModal] = useState<{
    isOpen: boolean;
    loading: boolean;
    error: string | null;
    blob: Blob | null;
    fileName: string;
    previewUrl: string | null;
    english: boolean;
  }>({ isOpen: false, loading: false, error: null, blob: null, fileName: '', previewUrl: null, english: false });

  const fetchScheduleReport = async (english: boolean) => {
    if (!perm) return;
    setScheduleReportModal(prev => ({ ...prev, isOpen: true, loading: true, error: null, english }));
    try {
      const { blob, fileName } = await delegationsApi.exportScheduleReportPdf(
        perm.visitRequestId, perm.visitInstanceId, english ? 'en' : 'vi');
      const previewUrl = URL.createObjectURL(blob);
      setScheduleReportModal(prev => {
        if (prev.previewUrl) URL.revokeObjectURL(prev.previewUrl);
        return { isOpen: true, loading: false, error: null, blob, fileName, previewUrl, english };
      });
    } catch (e: any) {
      setScheduleReportModal(prev => ({
        ...prev, loading: false, blob: null, previewUrl: null,
        error: apiErrorMessage(e, 'Không thể tải báo cáo lịch trình. Vui lòng thử lại sau.'),
      }));
    }
  };

  const openScheduleReportPreview = () => {
    if (scheduleReportModal.loading) return;
    fetchScheduleReport(false);
  };

  const toggleScheduleReportEnglish = (english: boolean) => {
    if (scheduleReportModal.loading) return;
    fetchScheduleReport(english);
  };

  const closeScheduleReportModal = () => {
    if (scheduleReportModal.previewUrl) URL.revokeObjectURL(scheduleReportModal.previewUrl);
    setScheduleReportModal({ isOpen: false, loading: false, error: null, blob: null, fileName: '', previewUrl: null, english: false });
  };

  const downloadScheduleReport = () => {
    if (!scheduleReportModal.blob || !scheduleReportModal.previewUrl) return;
    const link = document.createElement('a');
    link.href = scheduleReportModal.previewUrl;
    link.download = scheduleReportModal.fileName || 'BaoCaoLichTrinh.pdf';
    document.body.appendChild(link);
    link.click();
    link.remove();
  };

  // ── "Gửi cập nhật chuẩn bị" (Host only) ────────────────────────────────────
  // The backend renders the whole message: subject, body, default recipients and — when file storage
  // allows — the Schedule Report. This screen only asks for a language, opens the composer on what it
  // gets back, and lets the Host edit everything before previewing and sending. The button is rendered
  // from the backend flag alone — never from roleCode, because Staff Leader and HO read this same page
  // and can pull the same report without being allowed to write to the guest as its host.
  //
  // The report is attached by DEFAULT and the Host may remove it, so `reportFileId` can be null and a
  // composer with no attachment is a normal, sendable message.
  //
  // Nothing is persisted between the two steps: the message is held by the composer and posted whole.
  const [setupEmail, setSetupEmail] = useState<{
    picking: boolean;
    preparing: boolean;
    error: string | null;
    message: SetupProgressEmailMessage | null;
    /**
     * One number per opening of the composer, and what its React key is built from.
     *
     * It used to be keyed on the report's file id, which meant a sync — whose whole job is to produce a
     * NEW file id — remounted the composer: the freshly synced body and attachment were thrown away and
     * the modal came back holding the message the prepare had returned, along with anything the Host had
     * typed. Keying on the session makes a remount mean what it should: a different message.
     */
    session: number;
  }>({ picking: false, preparing: false, error: null, message: null, session: 0 });

  const prepareSetupProgressEmail = async (language: 'vi' | 'en') => {
    if (!perm || setupEmail.preparing) return;
    setSetupEmail(prev => ({ ...prev, preparing: true, error: null }));
    try {
      const message = await delegationsApi.prepareSetupProgressEmail(
        perm.visitRequestId, perm.visitInstanceId, language);
      setSetupEmail(prev => ({
        picking: false, preparing: false, error: null, message, session: prev.session + 1,
      }));
    } catch (e: any) {
      setSetupEmail(prev => ({
        ...prev,
        preparing: false,
        // Back to the language step, which is where the error is readable and where the retry lives.
        // Without this a failed render left picking=false and message=null, so the reason was written
        // into state that nothing on screen was rendering — the modal simply closed.
        picking: true,
        message: null,
        error: apiErrorMessage(e, 'Không chuẩn bị được email cập nhật. Vui lòng thử lại sau.'),
      }));
    }
  };

  const closeSetupEmail = () =>
    setSetupEmail(prev => ({
      picking: false, preparing: false, error: null, message: null, session: prev.session,
    }));

  // ── Real before-visit setup data (agenda). Loaded from the process-detail API; the Host edits
  // and saves it independently of the still-prototype sections (this is a genuine real slice). ──
  // Each row keeps the FULL local wall-clock datetime (YYYY-MM-DDTHH:mm), not just HH:mm — agenda
  // items can span multiple days, and storing only the time would force every item onto a single
  // date on save. See the time helpers below for why we never touch Date()/toISOString() here.
  type AgendaRow = {
    agendaId: number | null;
    title: string;
    startLocal: string;
    endLocal: string;
    location: string;
    // Free-typed name of the responsible person (plain text, not tied to a real user account).
    // Distinct from the template's suggested role label below, which is display-only.
    responsibleName: string;
    templateResponsibleRoleLabel: string | null;
  };
  const [detail, setDetail] = useState<VisitProcessDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(true);
  const [detailLoadError, setDetailLoadError] = useState(false);
  const [agendaItems, setAgendaItems] = useState<AgendaRow[]>([]);
  const [isSavingAgenda, setIsSavingAgenda] = useState(false);
  // View mode by default once an agenda exists (edit fields hidden, "Chỉnh sửa" shown instead of
  // "Lưu lịch trình") — flips to edit mode on "Chỉnh sửa" click, and back to view mode right after
  // a successful save. Initialised once per instance below (agendaPanelInitRef), same as
  // isAgendaTemplatePanelOpen, so a background refetch never yanks the host out of edit mode.
  const [isEditingAgenda, setIsEditingAgenda] = useState(false);
  const [remindersLoadFailed, setRemindersLoadFailed] = useState(false);
  // ── Planned visit window (visit_request_campuses.planned_start_at/end_at), editable from this tab.
  // Draft only takes effect once "Lưu lịch trình" is pressed — same as every agenda row edit. ──
  const [plannedStartDraft, setPlannedStartDraft] = useState('');
  const [plannedEndDraft, setPlannedEndDraft] = useState('');
  const [isEditingPlannedTime, setIsEditingPlannedTime] = useState(false);

  // ── Datetime serialization (PEMS rule: MySQL DATETIME is LOCAL wall-clock, never UTC). ──
  // The API returns "YYYY-MM-DDTHH:mm:ss" (or "YYYY-MM-DD HH:mm:ss") with no timezone. We slice it
  // straight into the <input type="datetime-local"> value WITHOUT new Date(): parsing to a Date and
  // back (toISOString) would shift the value by the browser's UTC offset on every save — the root
  // cause of the "time keeps drifting on repeated saves" bug.
  const toDatetimeLocalInputValue = (value?: string | null): string => {
    if (!value) return '';
    const normalized = value.replace(' ', 'T');
    return normalized.slice(0, 16); // -> "YYYY-MM-DDTHH:mm"
  };
  // Send the local wall-clock back verbatim (only padding seconds). No toISOString(), no UTC — so a
  // save with no edits round-trips to the exact same DATETIME (idempotent).
  const fromDatetimeLocalInputValueToApi = (value: string): string | null => {
    if (!value) return null;
    return value.length === 16 ? `${value}:00` : value; // "YYYY-MM-DDTHH:mm" -> "YYYY-MM-DDTHH:mm:ss"
  };

  const getDatePart = (localStr?: string | null): string => {
    if (!localStr) return '';
    return localStr.slice(0, 10);
  };

  const getTimePart = (localStr?: string | null): string => {
    if (!localStr || localStr.length < 16) return '';
    return localStr.slice(11, 16);
  };

  // Display-only "HH:mm dd/MM/yyyy → HH:mm dd/MM/yyyy" for the planned-window pill. Built from the
  // date/time parts above (no Date()/toISOString()) — same no-timezone-shift rule as the rest of the tab.
  const formatPlannedWindow = (startLocal: string, endLocal: string): string => {
    const fmt = (v: string) => {
      const d = getDatePart(v);
      const t = getTimePart(v);
      if (!d || !t) return '—';
      const [y, m, day] = d.split('-');
      return `${t} ${day}/${m}/${y}`;
    };
    return `${fmt(startLocal)} → ${fmt(endLocal)}`;
  };

  const isSingleDayVisit = React.useMemo(() => {
    if (detail?.plannedStartAt && detail?.plannedEndAt) {
      const sDate = detail.plannedStartAt.replace(' ', 'T').slice(0, 10);
      const eDate = detail.plannedEndAt.replace(' ', 'T').slice(0, 10);
      if (sDate && eDate) {
        return sDate === eDate;
      }
    }
    if (agendaItems.length > 0) {
      const dates = new Set(
        agendaItems
          .map((it) => (it.startLocal ? it.startLocal.slice(0, 10) : ''))
          .filter(Boolean)
      );
      if (dates.size === 1) return true;
      if (dates.size > 1) return false;
    }
    return true;
  }, [detail?.plannedStartAt, detail?.plannedEndAt, agendaItems]);

  const loadDetail = React.useCallback(async () => {
    if (!perm) { setDetail(null); setAgendaItems([]); setDetailLoading(false); return; }
    try {
      setDetailLoading(true);
      const d = await delegationsApi.getVisitProcessDetail(perm.visitRequestId, perm.visitInstanceId);
      setDetail(d);
      setDetailLoadError(false);
      setPreparationNote(d.preparationNote ?? '');
      setPreparationNoteSaved(d.preparationNote ?? '');
      setPlannedStartDraft(toDatetimeLocalInputValue(d.plannedStartAt));
      setPlannedEndDraft(toDatetimeLocalInputValue(d.plannedEndAt));
      setIsEditingPlannedTime(false);
      setAgendaItems((d.agenda || []).map((a) => ({
        agendaId: a.agendaId,
        title: a.title,
        startLocal: toDatetimeLocalInputValue(a.startTime),
        endLocal: toDatetimeLocalInputValue(a.endTime),
        location: a.location ?? '',
        responsibleName: a.responsibleName ?? '',
        templateResponsibleRoleLabel: a.templateResponsibleRoleLabel ?? null,
      })));
    } catch {
      setDetail(null);
      setAgendaItems([]);
      setDetailLoadError(true);
    } finally {
      setDetailLoading(false);
    }
  }, [perm?.visitRequestId, perm?.visitInstanceId]);
  useEffect(() => { void loadDetail(); }, [loadDetail]);

  // ── "Cảnh báo & Thông báo": load saved reminder schedule and map onto the two UI rows ──
  const loadReminders = React.useCallback(async () => {
    if (!perm) return;
    try {
      const res = await delegationsApi.getReminderSettings(perm.visitInstanceId);
      const rows = res.items || [];
      setReminders((prev) => {
        const next = { ...prev };
        for (const cfg of REMINDER_CONFIGS) {
          const row = rows.find((i) => i.channel === cfg.channel && i.targetGroup === cfg.targetGroup && i.status !== 'CANCELLED');
          next[cfg.key] = row
            ? { enabled: true, days: row.daysBefore, time: row.reminderTime }
            : { ...prev[cfg.key], enabled: false };
        }
        return next;
      });
      setRemindersLoadFailed(false);
    } catch {
      // The saved schedule is unknown — flag the failure so Save is blocked (it would otherwise
      // overwrite the real schedule with the UI's blank defaults) and the user is offered a retry.
      setRemindersLoadFailed(true);
    }
  }, [perm?.visitInstanceId]);
  useEffect(() => { void loadReminders(); }, [loadReminders]);

  /**
   * Re-read EVERYTHING that describes where this campus is, in one call (NP-04).
   *
   * A stage transition changes neither `visitRequestId` nor `visitInstanceId`, so `loadDetail` and
   * `loadReminders` — both keyed on exactly those two ids — never re-ran after one. Only the
   * permissions were refetched, and the page ended up holding two contradictory answers at once:
   *
   *     permissions.instanceStatus = DURING_VISIT     (fresh)
   *     detail.instanceStatus      = BEFORE_VISIT     (stale)
   *
   * Every capability still computed from `detail` therefore kept the preparation tab looking
   * editable, and pressing one of those buttons got a toast from the backend about the wrong stage.
   * Refetching all three together is what makes the screen lock the moment the API confirms, with no
   * F5 — and closing the edit modes matters too, because an open editor is a form the user could go
   * on typing into.
   */
  const refreshProcessState = React.useCallback(async () => {
    await Promise.all([loadPermissions(), loadDetail(), loadReminders()]);
    setIsEditingAgenda(false);
    setIsEditingPlannedTime(false);
    setIsInfoEditable(false);
  }, [loadPermissions, loadDetail, loadReminders]);

  // Save the host's preparation note (null clears it). Toast on success/failure.
  const handleSavePreparationNote = async () => {
    if (!perm) return;
    setSavingNote(true);
    try {
      const trimmed = preparationNote.trim();
      const res = await delegationsApi.updatePreparationNote(perm.visitInstanceId, trimmed.length ? preparationNote : null);
      setPreparationNote(res.preparationNote ?? '');
      setPreparationNoteSaved(res.preparationNote ?? '');
      pushToast('success', res.message || 'Đã lưu ghi chú chung.');
    } catch (e) {
      pushToast('error', apiErrorMessage(e, 'Không thể lưu ghi chú chung. Vui lòng thử lại.'));
    } finally {
      setSavingNote(false);
    }
  };

  // Save the scheduled reminder configuration (nothing is sent now — a background job dispatches later).
  const handleSaveReminders = async () => {
    if (!perm) return;
    // Refuse to save when the saved schedule failed to load — the request would clobber it with defaults.
    if (remindersLoadFailed) { pushToast('error', 'Chưa tải được lịch cảnh báo đã lưu. Vui lòng thử lại trước khi lưu.'); return; }
    setSavingReminders(true);
    try {
      const items = REMINDER_CONFIGS.map((cfg) => ({
        channel: cfg.channel,
        targetGroup: cfg.targetGroup,
        daysBefore: reminders[cfg.key].days,
        reminderTime: reminders[cfg.key].time,
        enabled: reminders[cfg.key].enabled,
      }));
      const res = await delegationsApi.saveReminderSettings(perm.visitInstanceId, items);
      pushToast('success', res.message || 'Đã lưu cấu hình cảnh báo.');
      await loadReminders();
    } catch (e) {
      pushToast('error', apiErrorMessage(e, 'Bạn không có quyền cập nhật cảnh báo.'));
    } finally {
      setSavingReminders(false);
    }
  };

  // Turn off every still-pending reminder.
  const handleCancelReminders = async () => {
    if (!perm) return;
    setSavingReminders(true);
    try {
      const res = await delegationsApi.cancelReminderSettings(perm.visitInstanceId);
      pushToast('success', res.message || 'Đã hủy lịch gửi cảnh báo.');
      setReminders((prev) => {
        const next = { ...prev };
        for (const cfg of REMINDER_CONFIGS) next[cfg.key] = { ...prev[cfg.key], enabled: false };
        return next;
      });
      await loadReminders();
    } catch (e) {
      pushToast('error', apiErrorMessage(e, 'Không thể hủy cảnh báo. Vui lòng thử lại.'));
    } finally {
      setSavingReminders(false);
    }
  };

  const hasCurrentAgenda = agendaItems.length > 0;

  /**
   * THE status of this campus instance — one value, read by everything (NP-04).
   *
   * `permissions` is preferred because it is the payload a transition refetches first, so it is
   * never the stale one; `detail` is the fallback for the moment before permissions have loaded (and
   * for the legacy/mock ids where there are no permissions at all). Reading either one ad hoc, which
   * is what the page used to do, is how the same screen came to hold DURING_VISIT in one variable
   * and BEFORE_VISIT in another.
   */
  const instanceStatus = perm?.instanceStatus ?? detail?.instanceStatus ?? null;

  /**
   * The single gate for EVERY mutation of the "Trước tiếp khách" tab — agenda, participants,
   * invitations, logistics, reminders, preparation note, and every save/send/update/delete button.
   *
   * <p>`BEFORE_VISIT` and nothing else, deliberately. `ASSIGNED` used to be accepted here as well,
   * which was simply wrong: `VisitPreparationGate` on the backend refuses every one of these
   * commands outside `BEFORE_VISIT`, so on an ASSIGNED campus the page offered controls that could
   * only ever come back as "Host chưa bắt đầu giai đoạn chuẩn bị". The recovery for that state is
   * the "Bắt đầu chuẩn bị" button (`canStartPreparation`), not a form.</p>
   *
   * <p>`isClosed` also covers the read-only ROUTE (a Staff Leader or HO opening this page to watch),
   * which no status alone would exclude.</p>
   */
  const canMutateBeforeVisit = !isClosed
    && (perm ? perm.relation === 'HOST' : detail?.relation === 'HOST')
    && instanceStatus === 'BEFORE_VISIT';

  // Agenda: same gate. It used to read `detail.canEditBefore`, computed by the detail API — the one
  // payload a stage transition did NOT refetch, so it stayed true after the visit had started.
  const canEditAgenda = canMutateBeforeVisit;
  // Agenda item fields/buttons are only truly editable in view+permission BOTH being satisfied.
  const editableAgenda = canEditAgenda && isEditingAgenda;
  // Reminders + preparation note: the same gate, and independent of the (always-false)
  // SETUP_SAVE_AVAILABLE flag — the backend re-checks exactly this rule.
  const canConfigurePrep = canMutateBeforeVisit;
  // "1. Thông tin chung": still behind SETUP_SAVE_AVAILABLE (there is no persistence API for it yet),
  // but the status half of the gate is now the shared one rather than a second opinion.
  const canEditBefore = SETUP_SAVE_AVAILABLE && canMutateBeforeVisit;
  const isInfoEditable = isInfoEditableState && canEditBefore;

  // ── "Áp dụng mẫu Agenda" panel visibility ──
  // The apply-template panel is a heavy form (dropdown + preview table); leaving it always-open made
  // the page very long. It now collapses by default once an agenda exists — the host only expands it
  // to apply/replace a template. We init the open/closed state ONCE per visit instance (keyed ref)
  // so a user who manually closes the panel is never re-opened by this effect on the next render.
  const [isAgendaTemplatePanelOpen, setIsAgendaTemplatePanelOpen] = useState(false);
  const agendaPanelInitRef = useRef<string | null>(null);
  useEffect(() => {
    if (!perm || detail === null) return; // wait until the agenda has actually loaded
    const key = String(perm.visitInstanceId);
    if (agendaPanelInitRef.current === key) return; // already initialised for this instance
    agendaPanelInitRef.current = key;
    setIsAgendaTemplatePanelOpen(!hasCurrentAgenda); // open by default only when there's no agenda yet
    setIsEditingAgenda(!hasCurrentAgenda); // edit mode by default only when there's no agenda yet
  }, [perm, detail, hasCurrentAgenda]);

  const saveAgenda = async () => {
    if (!perm || !detail) return;
    // Double-submit guard: spamming the button must never fire a 2nd request while the 1st is in
    // flight (a stale response could otherwise overwrite a newer one).
    if (isSavingAgenda) return;

    // ── Validation (front-end). On failure we NEVER hit the API. ──
    for (const it of agendaItems) {
      if (!it.title.trim()) {
        pushToast('error', 'Vui lòng nhập tiêu đề / nội dung mục lịch trình.');
        return;
      }
      if (!it.startLocal) {
        pushToast('error', 'Vui lòng nhập thời gian bắt đầu.');
        return;
      }
      if (!it.endLocal) {
        pushToast('error', 'Vui lòng nhập thời gian kết thúc.');
        return;
      }
      // "YYYY-MM-DDTHH:mm" sorts lexically == chronologically, so a plain string compare is safe
      // and (unlike new Date()) introduces no timezone shift.
      if (it.endLocal <= it.startLocal) {
        pushToast('error', 'Thời gian kết thúc phải sau thời gian bắt đầu.');
        return;
      }
    }

    // Planned visit window (mirrors the DB CHECK constraints on visit_request_campuses: end > start
    // and duration >= 30 minutes) so a bad edit never reaches the backend as a raw failure.
    if (!plannedStartDraft || !plannedEndDraft) {
      pushToast('error', 'Vui lòng nhập thời gian dự kiến của chuyến.');
      return;
    }
    if (plannedEndDraft <= plannedStartDraft) {
      pushToast('error', 'Thời gian dự kiến kết thúc phải sau thời gian bắt đầu.');
      return;
    }
    const plannedDurationMinutes =
      (new Date(plannedEndDraft).getTime() - new Date(plannedStartDraft).getTime()) / 60000;
    if (plannedDurationMinutes < 30) {
      pushToast('error', 'Thời gian dự kiến phải kéo dài ít nhất 30 phút.');
      return;
    }

    setIsSavingAgenda(true);
    try {
      // Send local wall-clock verbatim — NO plannedStartAt re-basing, NO toISOString(). Each item
      // keeps its own date, so multi-day agendas survive and repeated saves are idempotent.
      const items = agendaItems.map((it) => ({
        agendaId: it.agendaId ?? undefined,
        title: it.title.trim(),
        startTime: fromDatetimeLocalInputValueToApi(it.startLocal)!,
        endTime: fromDatetimeLocalInputValueToApi(it.endLocal),
        location: it.location?.trim() || null,
        responsibleName: it.responsibleName.trim() || null,
      }));
      await delegationsApi.saveVisitAgenda(
        perm.visitRequestId,
        perm.visitInstanceId,
        items,
        fromDatetimeLocalInputValueToApi(plannedStartDraft)!,
        fromDatetimeLocalInputValueToApi(plannedEndDraft)!
      );
      pushToast('success', 'Lưu lịch trình thành công. Lịch trình và người phụ trách đã được cập nhật.');
      await loadDetail();
      setIsEditingAgenda(false);
    } catch (e: any) {
      const status = e?.response?.status;
      if (status === 403) {
        pushToast('error', 'Bạn không có quyền cập nhật lịch trình của chuyến này.');
      } else if (!status || status >= 500) {
        pushToast('error', 'Đã xảy ra lỗi hệ thống. Không thể lưu lịch trình lúc này. Vui lòng thử lại sau.');
      } else {
        pushToast('error', apiErrorMessage(e, 'Không thể lưu lịch trình. Vui lòng kiểm tra lại dữ liệu và thử lại.'));
      }
    } finally {
      setIsSavingAgenda(false);
    }
  };

  // End-of-tab confirm bar: shows the primary confirm action, a "completed" badge once the stage
  // has passed, or nothing when the caller has no update right. Only rendered for a real instance.
  const renderStageBar = (opts: {
    stage: 'before' | 'during' | 'after';
    canDo: boolean;
    done: boolean;
    label: string;
    doneLabel: string;
  }) => {
    if (!perm) return null;
    if (opts.done) {
      return (
        <div className="mt-4 flex items-center justify-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-3 text-sm font-bold text-emerald-700">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span>{opts.doneLabel}</span>
        </div>
      );
    }
    if (!opts.canDo) return null;
    // Tab-specific guidance (đặc tả mục 8.6): the "before" notice must warn that the prep tab will
    // be locked; "during" announces the move to the after stage; "after" warns the whole flow goes
    // read-only once closed.
    const stageNotice = opts.stage === 'before'
      ? STAGE_CONFIRM[opts.stage].notice
      : opts.stage === 'during'
        ? 'Sau khi xác nhận, quy trình sẽ chuyển sang giai đoạn Sau tiếp khách.'
        : 'Sau khi đóng đoàn, toàn bộ quy trình sẽ chuyển sang chế độ chỉ xem.';
    return (
      <div className="mt-6 rounded-2xl border border-gray-200 bg-white p-6 shadow-sm flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="text-sm font-normal text-slate-500 flex items-start gap-2">
          <AlertCircle className="w-4 h-4 text-[#f37021] shrink-0 mt-0.5" />
          <span>{stageNotice}</span>
        </div>
        <button
          type="button"
          data-testid={`stage-advance-${opts.stage}`}
          disabled={stageSubmitting}
          // Opens the confirmation; the API call happens only if the Host confirms there (NP-05).
          onClick={() => setPendingStage(opts.stage)}
          className="inline-flex items-center justify-center gap-2 rounded-xl bg-[#004c91] px-6 py-2.5 text-sm font-bold text-white shadow-sm outline-none transition-colors hover:bg-[#003b70] disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
        >
          {stageSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <ArrowRightCircle className="w-4 h-4" />}
          {stageSubmitting ? 'Đang xử lý...' : opts.label}
        </button>
      </div>
    );
  };

  const [participants, setParticipants] = useState({
    isMeHost: true,
    host: user || null,
    supporters: [] as any[],
    otherDepts: [] as any[],
    students: [] as any[]
  });

  const [selectedHostOption, setSelectedHostOption] = useState('');
  const [addedHost, setAddedHost] = useState<string | null>('Trần Thị IC');
  const [showNoAccountError, setShowNoAccountError] = useState(false);

  const [selectedSupporterOption, setSelectedSupporterOption] = useState('');
  const [addedSupporters, setAddedSupporters] = useState<string[]>(['Thêm người A']);
  const [showSupporterNoAccountError, setShowSupporterNoAccountError] = useState(false);

  const [selectedOtherDeptOption, setSelectedOtherDeptOption] = useState('');
  const [participantOtherDept, setParticipantOtherDept] = useState('');
  const [addedOtherDepts, setAddedOtherDepts] = useState<string[]>(['Nguyễn Có TK']);
  const [showOtherDeptNoAccountError, setShowOtherDeptNoAccountError] = useState(false);

  const [studentSearchText, setStudentSearchText] = useState('');
  const [addedStudents, setAddedStudents] = useState<string[]>(['Sinh viên 123 - Trịnh Thăng Bình']);
  const [showStudentNoAccountError, setShowStudentNoAccountError] = useState(false);

  // 4 reminder rows keyed by config; loaded from reminder-settings (enabled=false → no/CANCELLED row).
  const [reminders, setReminders] = useState<Record<ReminderKey, ReminderRow>>({
    sysHost: { enabled: false, days: 1, time: '09:00' },
    emailHost: { enabled: false, days: 2, time: '08:00' },
    sysParticipants: { enabled: false, days: 1, time: '09:00' },
    emailParticipants: { enabled: false, days: 2, time: '08:00' },
  });
  const setReminder = (key: ReminderKey, patch: Partial<ReminderRow>) =>
    setReminders((prev) => ({ ...prev, [key]: { ...prev[key], ...patch } }));
  const [savingReminders, setSavingReminders] = useState(false);
  // Host's "Ghi chú chung" (visit_request_campuses.preparation_note): editable draft + saved baseline (Part G).
  const [preparationNote, setPreparationNote] = useState('');
  const [preparationNoteSaved, setPreparationNoteSaved] = useState('');
  const [savingNote, setSavingNote] = useState(false);
  const noteTextareaRef = useRef<HTMLTextAreaElement | null>(null);

  useEffect(() => {
    if (noteTextareaRef.current) {
      noteTextareaRef.current.style.height = 'auto';
      noteTextareaRef.current.style.height = `${Math.max(48, noteTextareaRef.current.scrollHeight)}px`;
      noteTextareaRef.current.scrollTop = 0;
    }
  }, [preparationNote]);

  const campusOptions = ['Hà Nội', 'Đà Nẵng', 'Cần Thơ', 'Hồ Chí Minh', 'Quy Nhơn'];
  const [visitMode, setVisitMode] = useState<'single' | 'multiple'>('single');
  const [visits, setVisits] = useState([
    { id: '1', campus: isHO ? 'Hà Nội' : 'Hà Nội', date: '2023-10-20', startTime: '08:00', endTime: '16:30' }
  ]);

  useEffect(() => {
    if (visitMode === 'single' && visits.length > 1) {
      setVisits([visits[0]]);
    }
  }, [visitMode]);

  const [rejectReasonModal, setRejectReasonModal] = useState<{ isOpen: boolean, targetId: string | null, targetName: string | null, reasonText: string }>({ isOpen: false, targetId: null, targetName: null, reasonText: '' });
  const [viewReasonModal, setViewReasonModal] = useState<{isOpen: boolean, targetName: string | null, reasonText: string}>({isOpen: false, targetName: null, reasonText: ''});

  const [confirmations, setConfirmations] = useState<Record<string, { time: string, name: string, status: 'accepted' | 'rejected', reason?: string }>>({});
  
  const setConfirmStatus = (id: string, name: string, status: 'accepted' | 'rejected') => {
    if (status === 'rejected') {
      if (confirmations[id]?.status === 'rejected') {
        setConfirmations(prev => {
          const next = { ...prev };
          delete next[id];
          return next;
        });
        return;
      }
      setRejectReasonModal({ isOpen: true, targetId: id, targetName: name, reasonText: '' });
      return;
    }
    setConfirmations(prev => {
      if (prev[id] && prev[id].status === status) {
        const next = { ...prev };
        delete next[id];
        return next;
      }
      return {
        ...prev,
        [id]: { time: formatVietnamTime(new Date()), name, status }
      };
    });
  };

  const handleConfirmReject = () => {
    if (rejectReasonModal.targetId && rejectReasonModal.targetName) {
      setConfirmations(prev => ({
        ...prev,
        [rejectReasonModal.targetId!]: { 
          time: formatVietnamTime(new Date()),
          name: rejectReasonModal.targetName!, 
          status: 'rejected',
          reason: rejectReasonModal.reasonText
        }
      }));
    }
    setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' });
  };

  if (isLoadingPerm || detailLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-[#004c91] mb-4" />
        <p className="text-sm font-normal text-slate-500">
          {isLoadingPerm ? 'Đang tải phân quyền...' : 'Đang tải thông tin...'}
        </p>
      </div>
    );
  }

  // "OPERATIONAL_CONTACT" replaced the old request-wide "VISITOR_OWNER" relation (see
  // VisitInstanceAccess.cs) — the confirmed contact of THIS campus, resolved per-instance.
  const isVisitorOwner = perm?.relation === 'OPERATIONAL_CONTACT' || detail?.relation === 'OPERATIONAL_CONTACT';

  if (!hasNumericId || permLoadFailed || detailLoadError) {
    return (
      <div className="w-full py-4 pb-24">
        <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-20 h-20 bg-rose-50 rounded-full flex items-center justify-center mb-6">
            <AlertCircle className="w-10 h-10 text-rose-400 stroke-[1.5]" />
          </div>
          <h2 className="text-xl font-bold text-slate-800 mb-2">
            {isReceptionDetail ? tt('process.notFoundTitle') : tt('process.noAccessTitle')}
          </h2>
          <p className="text-gray-500 font-normal max-w-sm mx-auto leading-relaxed text-sm mb-6">
            {isReceptionDetail ? tt('process.notFoundDesc') : tt('process.noAccessDesc')}
          </p>
          <button onClick={() => navigate(returnUrl)} className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors outline-none">
            {tt('process.backToList')}
          </button>
        </div>
      </div>
    );
  }
  
  if (isReceptionDetail) {
    if (isVisitorOwner && perm != null && detail != null) {
      return <VisitorVisitDetailPage perm={perm} detail={detail} />;
    }
    // Lỡ lot xuống đây mà không đủ quyền
    return (
      <div className="w-full py-4 pb-24">
        <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-20 h-20 bg-rose-50 rounded-full flex items-center justify-center mb-6">
            <AlertCircle className="w-10 h-10 text-rose-400 stroke-[1.5]" />
          </div>
          <h2 className="text-xl font-bold text-slate-800 mb-2">Không tìm thấy thông tin chuyến thăm</h2>
          <p className="text-gray-500 font-normal max-w-sm mx-auto leading-relaxed text-sm mb-6">
            Bạn không có quyền xem chuyến thăm này.
          </p>
          <button onClick={() => navigate(returnUrl)} className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors outline-none">
            Quay lại danh sách tiếp khách
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full flex flex-col pb-24 animate-in fade-in duration-300">
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate(returnUrl)}>Quản lý tiếp khách</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">
          {isReceptionDetail ? 'Chi tiết đón tiếp' : 'Quy trình tiếp khách'}
        </span>
      </div>

      <div className="mb-4">
        <h1 className="text-3xl font-bold text-[#004c91]">
          {isReceptionDetail ? 'Chi tiết đón tiếp' : 'Quy trình tiếp khách'}
        </h1>
      </div>

      {(isCancelledView || perm?.instanceStatus === 'CANCELLED') && !isReceptionDetail ? (
        <div className="mb-8 bg-rose-50 border-l-4 border-rose-500 p-5 rounded-2xl flex items-center gap-3 text-left shadow-sm">
          <AlertCircle className="w-5 h-5 text-rose-600 shrink-0" />
          <p className="text-sm font-normal text-rose-700">
            Campus này đã bị hủy. Các thông tin chuẩn bị trước đó chỉ được hiển thị để tham khảo/lưu vết và không thể chỉnh sửa.
          </p>
        </div>
      ) : isClosed && !isReceptionDetail && (
        <div className="mb-8 bg-slate-100 border-l-4 border-slate-500 p-5 rounded-2xl flex items-center gap-3 text-left shadow-sm">
          <AlertCircle className="w-5 h-5 text-slate-600 shrink-0" />
          <p className="text-sm font-normal text-slate-700">
            {(currentStatus === 'Đã đóng đoàn' || currentStatus === 'Đã kết thúc')
              ? 'Hồ sơ lưu trữ: Đoàn khách này đã hoàn thành quy trình tiếp đón và đóng hồ sơ lịch sử. Dữ liệu đang hiển thị ở chế độ xem (Chỉ đọc).'
              : 'Chỉ người phụ trách tiếp đón mới có thể chỉnh sửa thông tin'
            }
          </p>
        </div>
      )}

      {/* Tabs + (when the campus is ASSIGNED and this Host has not opened preparation yet) the
          "Bắt đầu chuẩn bị" CTA, sharing one row instead of a separate orange card below: the CTA
          text itself already says the campus is ready to start, so a second block repeating "Đã
          phân công Host" said nothing the button did not. Driven by the backend flag alone — never
          by relation/role — so a Staff Leader, HO, the operational contact and the registrant simply
          never see the CTA. */}
      {!isReceptionDetail && (
        <div className="mb-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-1 bg-white rounded-2xl p-1.5 shadow-sm border border-gray-200 max-w-2xl">
            {canViewBefore && (
              <button
                onClick={() => setActiveTab('before')}
                className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none ${activeTab === 'before' ? 'bg-[#004c91] text-white shadow-md' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-700'}`}
              >
                1. Trước tiếp khách
              </button>
            )}
            {canViewDuring && (
              <button
                onClick={() => duringUnlocked && setActiveTab('during')}
                disabled={!duringUnlocked}
                title={duringUnlocked ? undefined : 'Hoàn thành giai đoạn "Trước tiếp khách" để mở khóa.'}
                aria-disabled={!duringUnlocked}
                className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none inline-flex items-center justify-center gap-1.5 ${activeTab === 'during' ? 'bg-[#f37021] text-white shadow-md' : duringUnlocked ? 'text-gray-500 hover:bg-gray-50 hover:text-gray-700' : 'text-slate-300 cursor-not-allowed'}`}
              >
                {!duringUnlocked && <Lock className="w-3.5 h-3.5" />}
                2. Đang tiếp khách
              </button>
            )}
            {canViewAfter && (
              <button
                onClick={() => afterUnlocked && setActiveTab('after')}
                disabled={!afterUnlocked}
                title={afterUnlocked ? undefined : 'Hoàn thành giai đoạn "Đang tiếp khách" để mở khóa.'}
                aria-disabled={!afterUnlocked}
                className={`flex-1 py-3 text-sm font-bold rounded-xl transition-all outline-none inline-flex items-center justify-center gap-1.5 ${activeTab === 'after' ? 'bg-[#00a651] text-white shadow-md' : afterUnlocked ? 'text-gray-500 hover:bg-gray-50 hover:text-gray-700' : 'text-slate-300 cursor-not-allowed'}`}
              >
                {!afterUnlocked && <Lock className="w-3.5 h-3.5" />}
                3. Sau tiếp khách
              </button>
            )}
          </div>

          {perm?.canStartPreparation && (
            <div className="flex shrink-0 items-center gap-2 self-start sm:self-auto">
              <div className="relative">
                <button
                  type="button"
                  data-testid="start-preparation-button"
                  disabled={startingPreparation}
                  onClick={startPreparation}
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-[#f37021] px-6 py-2.5 text-sm font-bold text-white shadow-sm outline-none transition-colors hover:bg-[#d95f16] disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                >
                  {startingPreparation ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
                  {startingPreparation ? 'Đang bắt đầu...' : 'Bắt đầu chuẩn bị'}
                </button>
                {showStartPrepInfo && (
                  <div
                    role="tooltip"
                    data-testid="start-preparation-info"
                    className="absolute right-0 top-full z-20 mt-1.5 w-64 rounded-xl border border-gray-200 bg-white p-3 text-xs font-normal leading-snug text-gray-600 shadow-lg"
                  >
                    Bắt đầu giai đoạn chuẩn bị để mở lịch trình, thành phần tham gia, hậu cần, nhắc
                    lịch và ghi chú chuẩn bị.
                  </div>
                )}
              </div>
              <button
                type="button"
                aria-label="Bắt đầu chuẩn bị mở gì?"
                aria-expanded={showStartPrepInfo}
                onClick={() => setShowStartPrepInfo((v) => !v)}
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-gray-400 outline-none transition-colors hover:bg-gray-100 hover:text-gray-600"
              >
                <Info className="h-4 w-4" />
              </button>
            </div>
          )}
        </div>
      )}

      {activeTab === 'before' && canViewBefore && (
        <div className="space-y-6">
          {/* Honest notice: the "Thông tin chung" registrant/delegation block is read-only
              reference data (what the guest registered). Lịch trình, thành phần tham gia and
              hậu cần (Chuẩn bị chi tiết) are all wired to real save APIs. */}

          {/* Preparation is over: say so, once, at the top (NP-04).
              Everything below is still shown — the agenda, who was invited, what was requested and
              what was noted are exactly what a Host refers back to while the visit runs — but none
              of it can be changed any more. Without this line the tab just quietly loses its
              buttons, which reads as a page that failed to load rather than a stage that closed. */}
          {instanceStatus && ['DURING_VISIT', 'AFTER_VISIT', 'CLOSED'].includes(instanceStatus) && (
            <div
              data-testid="before-readonly-banner"
              role="status"
              className="rounded-[2rem] border border-slate-200 bg-slate-50 p-5 flex items-start gap-3"
            >
              <Lock className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" />
              <p className="text-sm font-normal text-slate-600">
                Giai đoạn chuẩn bị đã kết thúc — tab “Trước tiếp khách” hiện chỉ còn chế độ xem.
                Bạn vẫn xem được lịch trình, thành phần tham gia, hậu cần, cảnh báo và ghi chú đã lưu.
              </p>
            </div>
          )}

          {/* Phần 1: Thông tin chung */}
          <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300">
            <div 
              className="px-6 py-4 flex items-center justify-between cursor-pointer transition-colors bg-[#f37021]"
              onClick={() => setIsInfoExpanded(!isInfoExpanded)}
            >
              <div>
                <h2 className="text-lg font-bold text-white border-l-4 border-white pl-3">1. Thông tin chung</h2>
              </div>
              <div className="flex items-center gap-3">
                {canEditBefore && !isInfoEditable && !isClosed && !isDept && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setIsInfoEditable(true);
                      setIsInfoExpanded(true);
                    }}
                    className="w-10 h-10 rounded-full hover:bg-white/20 flex items-center justify-center text-white transition-colors"
                  >
                    <Edit3 className="w-5 h-5" />
                  </button>
                )}
                <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                  {isInfoExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                </div>
              </div>
            </div>

            <AnimatePresence>
              {isInfoExpanded && (
                <motion.div
                  data-testid="before-info-body"
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  className="border-t border-gray-100"
                >
                  <div className={`p-6 space-y-4 ${!isInfoEditable ? 'bg-slate-50/50 opacity-90' : 'bg-white'}`}>
                    {/* Section 1: Thông tin người đăng ký */}
                    <div className="bg-white border border-[#004c91]/20 rounded-xl overflow-hidden shadow-sm">
                      <div 
                        className="bg-[#004c91] px-4 py-2.5 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection1Expanded(!isInfoSection1Expanded)}
                      >
                        <h2 className="text-sm font-bold text-white flex items-center gap-2 uppercase tracking-tight">
                          <span className="flex items-center justify-center w-5 h-5 rounded-full bg-[#f37021] text-white font-bold text-xs">1</span>
                          Thông tin người tạo
                        </h2>
                        <div className="flex items-center gap-2">
                          <span className="inline-flex items-center gap-1 rounded-md bg-white/15 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
                            <Lock className="w-3 h-3" /> Chỉ đọc
                          </span>
                          <div className="w-6 h-6 flex items-center justify-center text-white/80 hover:text-white transition-colors">
                            {isInfoSection1Expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                          </div>
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection1Expanded && (
                          <motion.div
                            data-testid="before-registrant-body"
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            {/* Thông tin người đăng ký do KHÁCH nhập — luôn read-only với Host (không
                                phải người tạo) và không có endpoint cập nhật. */}
                            <RegistrantInfoReadOnly summary={detail?.requestSummary} />
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {/* Section 2: Thông tin đoàn khách */}
                    <div className="bg-white border border-[#004c91]/20 rounded-xl overflow-hidden shadow-sm">
                      <div 
                        className="bg-[#004c91] px-4 py-2.5 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection2Expanded(!isInfoSection2Expanded)}
                      >
                        <h2 className="text-sm font-bold text-white flex items-center gap-2 uppercase tracking-tight">
                          <span className="flex items-center justify-center w-5 h-5 rounded-full bg-[#f37021] text-white font-bold text-xs">2</span>
                          Thông tin đoàn khách
                        </h2>
                        <div className="flex items-center gap-2">
                          <span className="inline-flex items-center gap-1 rounded-md bg-white/15 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
                            <Lock className="w-3 h-3" /> Chỉ đọc
                          </span>
                          <div className="w-6 h-6 flex items-center justify-center text-white/80 hover:text-white transition-colors">
                            {isInfoSection2Expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                          </div>
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection2Expanded && (
                          <motion.div
                            data-testid="before-delegation-body"
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            <DelegationInfoReadOnly summary={detail?.requestSummary} />
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {/* Section 3: Setup */}
                    <div className="bg-white border border-[#004c91]/20 rounded-xl overflow-hidden shadow-sm mb-4">
                      <div 
                        className="bg-[#004c91] px-4 py-2.5 flex items-center justify-between cursor-pointer"
                        onClick={() => setIsInfoSection3Expanded(!isInfoSection3Expanded)}
                      >
                        <h2 className="text-sm font-bold text-white flex items-center gap-2 uppercase tracking-tight">
                          <span className="flex items-center justify-center w-5 h-5 rounded-full bg-[#f37021] text-white font-bold text-xs">3</span>
                          Thiết lập & Điều phối sự kiện (Set up)
                        </h2>
                        <div className="w-6 h-6 flex items-center justify-center text-white/80 hover:text-white transition-colors">
                          {isInfoSection3Expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                        </div>
                      </div>
                      <AnimatePresence>
                        {isInfoSection3Expanded && (
                          <motion.div
                            data-testid="before-setup-body"
                            initial={{ opacity: 0 }}
                            animate={{ opacity: 1 }}
                            exit={{ opacity: 0 }}
                          >
                            <div className="p-0 bg-white border-t border-gray-100">

                        {/* 3.2 Agenda */}
                        <div className="p-6 border-b border-gray-100 bg-slate-50/50">
                          {/* Compact header: title + a single toggle for the (heavy) apply-template panel,
                              so "Lịch trình hiện tại" is the focus once an agenda exists. */}
                          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                            <div className="flex flex-wrap items-center gap-2">
                              <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2">
                                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                                1. Agenda
                              </h3>
                              {/* Planned visit window (visit_request_campuses.planned_start_at/end_at).
                                  Editable here; takes effect together with the agenda on "Lưu lịch trình". */}
                              {canEditAgenda && (
                                isEditingPlannedTime ? (
                                  <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2 py-1">
                                    <input
                                      type="datetime-local"
                                      value={plannedStartDraft}
                                      onChange={(e) => setPlannedStartDraft(e.target.value)}
                                      className="h-7 rounded border border-slate-200 px-1 text-xs font-normal text-slate-700 outline-none focus:border-[#004c91]"
                                    />
                                    <span className="text-xs font-bold text-slate-400">→</span>
                                    <input
                                      type="datetime-local"
                                      value={plannedEndDraft}
                                      onChange={(e) => setPlannedEndDraft(e.target.value)}
                                      className="h-7 rounded border border-slate-200 px-1 text-xs font-normal text-slate-700 outline-none focus:border-[#004c91]"
                                    />
                                    <button type="button" onClick={() => setIsEditingPlannedTime(false)}
                                      className="px-1 text-xs font-bold text-emerald-600 hover:underline">Xong</button>
                                    <button type="button"
                                      onClick={() => {
                                        setPlannedStartDraft(toDatetimeLocalInputValue(detail?.plannedStartAt));
                                        setPlannedEndDraft(toDatetimeLocalInputValue(detail?.plannedEndAt));
                                        setIsEditingPlannedTime(false);
                                      }}
                                      className="px-1 text-xs font-bold text-slate-400 hover:underline">Hủy</button>
                                  </div>
                                ) : (
                                  <button type="button" onClick={() => setIsEditingPlannedTime(true)}
                                    className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-slate-600 outline-none transition hover:border-[#004c91] hover:text-[#004c91]">
                                    <Calendar className="h-3.5 w-3.5" />
                                    Dự kiến: {formatPlannedWindow(plannedStartDraft, plannedEndDraft)}
                                    <Edit3 className="h-3 w-3 text-slate-400" />
                                  </button>
                                )
                              )}
                            </div>
                            {/* Applying a template WRITES visit_agendas, so it belongs behind the same
                                gate as every other agenda edit — it used to be offered on `perm`
                                alone, which meant a live button after the visit had started (NP-04). */}
                            {perm && canEditAgenda && (
                              <button
                                type="button"
                                data-testid="agenda-template-toggle"
                                onClick={() => setIsAgendaTemplatePanelOpen((prev) => !prev)}
                                aria-expanded={isAgendaTemplatePanelOpen}
                                className="inline-flex h-10 items-center justify-center gap-1.5 rounded-xl border border-slate-300 bg-white px-4 text-sm font-bold text-[#004c91] outline-none transition-colors hover:bg-blue-50"
                              >
                                {isAgendaTemplatePanelOpen
                                  ? <><ChevronUp className="h-4 w-4" /> Ẩn mẫu Agenda</>
                                  : <><Wand2 className="h-4 w-4" /> {hasCurrentAgenda ? 'Đổi / áp dụng mẫu Agenda' : 'Áp dụng mẫu Agenda'}</>}
                              </button>
                            )}
                          </div>
                          {/* Apply an agenda template (auto-default by campus/visit_type → GLOBAL fallback).
                              Backend computes absolute times from planned_start_at + offsets and writes
                              visit_agendas; on success we reload the agenda editor below and auto-collapse. */}
                          {perm && canEditAgenda && isAgendaTemplatePanelOpen && (
                            <AgendaSetupPanel
                              visitInstanceId={Number(perm.visitInstanceId)}
                              onApplied={async () => { await loadDetail(); setIsAgendaTemplatePanelOpen(false); }}
                              notify={pushToast}
                            />
                          )}
                          {/* Real agenda editor (visit_agendas). Host edits while preparing; saved
                              independently via "Lưu lịch trình" (does NOT change stage). View mode
                              (read-only + "Chỉnh sửa") is the default once an agenda exists, and is
                              restored automatically right after a successful save. */}
                          <div className="mb-2 flex flex-wrap items-start justify-between gap-3">
                            <div>
                              <h4 className="text-sm font-bold text-slate-800">Lịch trình hiện tại</h4>
                              {agendaItems.length > 0 && (
                                <p className="text-xs text-slate-400">{agendaItems.length} hoạt động</p>
                              )}
                            </div>
                            {canEditAgenda && !isEditingAgenda && (
                              <button type="button" onClick={() => setIsEditingAgenda(true)}
                                className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-bold text-slate-600 outline-none transition hover:border-[#004c91] hover:text-[#004c91]">
                                <Edit3 className="h-3.5 w-3.5" /> Chỉnh sửa
                              </button>
                            )}
                          </div>
                          <div className="space-y-3">
                            {agendaItems.length === 0 && (
                              <p className="text-sm text-slate-500 italic">
                                Chưa có mục lịch trình nào.{editableAgenda ? ' Bấm “Thêm mục” để tạo.' : ''}
                              </p>
                            )}
                            {agendaItems.length > 0 && (
                              <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <div className="hidden md:grid grid-cols-[44px_235px_minmax(200px,1fr)_150px_220px_36px] gap-3 bg-slate-50 px-4 py-2 text-[11px] font-bold uppercase tracking-wide text-slate-500 border-b border-slate-200">
                                  <span className="text-center">#</span>
                                  <span>Thời gian</span>
                                  <span>Nội dung</span>
                                  <span>Địa điểm</span>
                                  <span>Người phụ trách</span>
                                  <span />
                                </div>
                                <div className="divide-y divide-slate-100">
                                  {agendaItems.map((it, idx) => {
                                    const ghostInputClass = "h-9 w-full min-w-0 rounded-lg border border-transparent bg-transparent px-2 text-sm font-medium text-slate-800 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    const ghostTimeClass = "h-8 w-[130px] shrink-0 rounded-md border border-transparent bg-transparent px-1.5 text-sm font-semibold text-slate-800 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    // Auto-grows vertically with content (min one line, same visual style as ghostInputClass)
                                    // so long agenda text wraps instead of being clipped inside a fixed-height box.
                                    const ghostTextareaClass = "min-h-9 w-full min-w-0 resize-none overflow-hidden rounded-lg border border-transparent bg-transparent px-2 py-1.5 text-sm font-medium text-slate-800 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    return (
                                      <div key={idx} className="grid grid-cols-1 gap-2 px-4 py-3 transition-colors hover:bg-slate-50 md:grid-cols-[44px_235px_minmax(200px,1fr)_150px_220px_36px] md:items-start md:gap-3">
                                        {/* Order number — leads the row so the eye scans it first. */}
                                        <div className="flex items-center md:justify-center">
                                          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-orange-50 text-xs font-bold text-[#F37021]">
                                            {idx + 1}
                                          </span>
                                        </div>

                                        {/* Time range — conditionally rendered based on single-day vs multi-day visit */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="block text-[10px] font-bold uppercase text-slate-400 md:hidden">Thời gian</label>
                                          {isSingleDayVisit ? (
                                            /* 1-Day Visit: Only show time inputs (HH:mm) with Clock icon on the left */
                                            <div className="flex items-center gap-1.5 pt-0.5">
                                              <div
                                                onClick={(e) => {
                                                  if (!editableAgenda) return;
                                                  const input = e.currentTarget.querySelector('input');
                                                  if (input) { try { input.showPicker?.(); } catch {} }
                                                }}
                                                className={`flex items-center gap-1 rounded-lg border border-slate-200/80 bg-white px-1.5 py-0.5 shadow-xs hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-blue-100 transition-all ${editableAgenda ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'}`}
                                              >
                                                <Clock className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                                                <input
                                                  type="time"
                                                  value={getTimePart(it.startLocal)}
                                                  disabled={!editableAgenda}
                                                  onChange={(e) => {
                                                    const timeVal = e.target.value;
                                                    const dateVal = getDatePart(it.startLocal) || getDatePart(detail?.plannedStartAt) || '2026-01-01';
                                                    const newStart = timeVal ? `${dateVal}T${timeVal}` : it.startLocal;
                                                    setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, startLocal: newStart } : p));
                                                  }}
                                                  className="w-[74px] border-none bg-transparent p-0 text-sm font-normal text-slate-800 outline-none cursor-pointer disabled:cursor-not-allowed [&::-webkit-calendar-picker-indicator]:hidden"
                                                />
                                              </div>
                                              <span className="text-slate-400 font-bold text-xs shrink-0">→</span>
                                              <div
                                                onClick={(e) => {
                                                  if (!editableAgenda) return;
                                                  const input = e.currentTarget.querySelector('input');
                                                  if (input) { try { input.showPicker?.(); } catch {} }
                                                }}
                                                className={`flex items-center gap-1 rounded-lg border border-slate-200/80 bg-white px-1.5 py-0.5 shadow-xs hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-blue-100 transition-all ${editableAgenda ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'}`}
                                              >
                                                <Clock className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                                                <input
                                                  type="time"
                                                  value={getTimePart(it.endLocal)}
                                                  disabled={!editableAgenda}
                                                  onChange={(e) => {
                                                    const timeVal = e.target.value;
                                                    const dateVal = getDatePart(it.endLocal) || getDatePart(it.startLocal) || getDatePart(detail?.plannedStartAt) || '2026-01-01';
                                                    const newEnd = timeVal ? `${dateVal}T${timeVal}` : it.endLocal;
                                                    setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, endLocal: newEnd } : p));
                                                  }}
                                                  className="w-[74px] border-none bg-transparent p-0 text-sm font-normal text-slate-800 outline-none cursor-pointer disabled:cursor-not-allowed [&::-webkit-calendar-picker-indicator]:hidden"
                                                />
                                              </div>
                                            </div>
                                          ) : (
                                            /* Multi-Day Visit (2+ days) */
                                            (() => {
                                              const startDate = getDatePart(it.startLocal);
                                              const endDate = getDatePart(it.endLocal);
                                              const isSameDate = startDate && endDate && startDate === endDate;

                                              if (isSameDate) {
                                                /* Same start & end date: Show date ONCE on top, then time range below */
                                                return (
                                                  <div className="flex flex-col gap-1.5">
                                                    <div
                                                      onClick={(e) => {
                                                        if (!editableAgenda) return;
                                                        const input = e.currentTarget.querySelector('input');
                                                        if (input) { try { input.showPicker?.(); } catch {} }
                                                      }}
                                                      className={`flex items-center gap-1 rounded-lg border border-slate-200/60 bg-slate-50/70 px-2 py-0.5 w-max ${editableAgenda ? 'cursor-pointer' : 'cursor-not-allowed opacity-80'}`}
                                                    >
                                                      <Calendar className="h-3.5 w-3.5 text-slate-500 shrink-0" />
                                                      <input
                                                        type="date"
                                                        value={startDate}
                                                        disabled={!editableAgenda}
                                                        onChange={(e) => {
                                                          const newDate = e.target.value;
                                                          if (!newDate) return;
                                                          const sTime = getTimePart(it.startLocal) || '00:00';
                                                          const eTime = getTimePart(it.endLocal) || '00:00';
                                                          setAgendaItems((prev) =>
                                                            prev.map((p, i) =>
                                                              i === idx ? { ...p, startLocal: `${newDate}T${sTime}`, endLocal: `${newDate}T${eTime}` } : p
                                                            )
                                                          );
                                                        }}
                                                        className="h-6 min-w-0 border-none bg-transparent p-0 text-xs font-normal text-slate-700 outline-none cursor-pointer disabled:cursor-not-allowed"
                                                      />
                                                    </div>
                                                    <div className="flex items-center gap-1.5">
                                                      <div
                                                        onClick={(e) => {
                                                          if (!editableAgenda) return;
                                                          const input = e.currentTarget.querySelector('input');
                                                          if (input) { try { input.showPicker?.(); } catch {} }
                                                        }}
                                                        className={`flex items-center gap-1 rounded-lg border border-slate-200/80 bg-white px-1.5 py-0.5 shadow-xs hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-blue-100 transition-all ${editableAgenda ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'}`}
                                                      >
                                                        <Clock className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                                                        <input
                                                          type="time"
                                                          value={getTimePart(it.startLocal)}
                                                          disabled={!editableAgenda}
                                                          onChange={(e) => {
                                                            const timeVal = e.target.value;
                                                            const newStart = timeVal ? `${startDate}T${timeVal}` : it.startLocal;
                                                            setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, startLocal: newStart } : p));
                                                          }}
                                                          className="w-[74px] border-none bg-transparent p-0 text-sm font-normal text-slate-800 outline-none cursor-pointer disabled:cursor-not-allowed [&::-webkit-calendar-picker-indicator]:hidden"
                                                        />
                                                      </div>
                                                      <span className="text-slate-400 font-bold text-xs shrink-0">→</span>
                                                      <div
                                                        onClick={(e) => {
                                                          if (!editableAgenda) return;
                                                          const input = e.currentTarget.querySelector('input');
                                                          if (input) { try { input.showPicker?.(); } catch {} }
                                                        }}
                                                        className={`flex items-center gap-1 rounded-lg border border-slate-200/80 bg-white px-1.5 py-0.5 shadow-xs hover:border-[#004c91] focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-blue-100 transition-all ${editableAgenda ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'}`}
                                                      >
                                                        <Clock className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                                                        <input
                                                          type="time"
                                                          value={getTimePart(it.endLocal)}
                                                          disabled={!editableAgenda}
                                                          onChange={(e) => {
                                                            const timeVal = e.target.value;
                                                            const newEnd = timeVal ? `${endDate}T${timeVal}` : it.endLocal;
                                                            setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, endLocal: newEnd } : p));
                                                          }}
                                                          className="w-[74px] border-none bg-transparent p-0 text-sm font-normal text-slate-800 outline-none cursor-pointer disabled:cursor-not-allowed [&::-webkit-calendar-picker-indicator]:hidden"
                                                        />
                                                      </div>
                                                    </div>
                                                  </div>
                                                );
                                              } else {
                                                /* Spans across midnight (different start & end dates) */
                                                return (
                                                  <div className="grid grid-cols-[14px_minmax(0,1fr)] items-center gap-x-2 gap-y-1">
                                                    <span />
                                                    <input
                                                      type="datetime-local"
                                                      value={it.startLocal}
                                                      disabled={!editableAgenda}
                                                      onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, startLocal: e.target.value } : p))}
                                                      className={ghostTimeClass}
                                                    />
                                                    <span className="text-center text-slate-400 font-bold text-xs">→</span>
                                                    <input
                                                      type="datetime-local"
                                                      value={it.endLocal}
                                                      disabled={!editableAgenda}
                                                      onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, endLocal: e.target.value } : p))}
                                                      className={ghostTimeClass}
                                                    />
                                                  </div>
                                                );
                                              }
                                            })()
                                          )}
                                        </div>

                                        {/* Content */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Nội dung</label>
                                          <textarea rows={1} value={it.title} disabled={!editableAgenda} placeholder="Nội dung mục lịch trình"
                                            ref={(el) => { if (el) { el.style.height = 'auto'; el.style.height = `${el.scrollHeight}px`; } }}
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, title: e.target.value } : p))}
                                            className={ghostTextareaClass} />
                                        </div>

                                        {/* Location */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Địa điểm</label>
                                          <input type="text" value={it.location} disabled={!editableAgenda} placeholder="(tuỳ chọn)"
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, location: e.target.value } : p))}
                                            className={ghostInputClass} />
                                        </div>

                                        {/* Responsible person (free text) */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Người phụ trách</label>
                                          <input type="text" value={it.responsibleName} disabled={!editableAgenda} placeholder="Tên người phụ trách"
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, responsibleName: e.target.value } : p))}
                                            className={ghostInputClass} />
                                        </div>

                                        {/* Delete */}
                                        <div className="flex justify-end pl-9 md:justify-center md:pl-0">
                                          {editableAgenda && (
                                            <button type="button" title="Xoá mục"
                                              onClick={() => { setAgendaItems((prev) => prev.filter((_, i) => i !== idx)); pushToast('info', 'Đã xóa mục khỏi lịch trình. Bấm “Lưu lịch trình” để lưu thay đổi.'); }}
                                              className="mt-1 inline-flex h-8 w-8 items-center justify-center rounded-lg text-red-500 transition hover:bg-red-50 hover:text-red-600 outline-none">
                                              <X className="h-4 w-4" />
                                            </button>
                                          )}
                                        </div>
                                      </div>
                                    );
                                  })}
                                </div>
                              </div>
                            )}
                            {editableAgenda && (
                              <div className="flex flex-wrap items-center gap-3 pt-2">
                                <button type="button"
                                  onClick={() => setAgendaItems((prev) => [...prev, { agendaId: null, title: '', startLocal: toDatetimeLocalInputValue(detail?.plannedStartAt), endLocal: toDatetimeLocalInputValue(detail?.plannedEndAt), location: '', responsibleName: detail?.hostName ?? '', templateResponsibleRoleLabel: null }])}
                                  className="inline-flex items-center gap-1.5 rounded-xl border-2 border-dashed border-[#f37021]/40 px-4 py-2 text-sm font-bold text-[#f37021] hover:bg-orange-50 outline-none">
                                  <Plus className="w-4 h-4" /> Thêm mục
                                </button>
                                <button type="button" disabled={isSavingAgenda} onClick={saveAgenda}
                                  className="inline-flex items-center gap-2 rounded-xl bg-[#10b981] px-5 py-2 text-sm font-bold text-white hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed outline-none">
                                  {isSavingAgenda ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                                  {isSavingAgenda ? 'Đang lưu...' : 'Lưu lịch trình'}
                                </button>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* 2. Thành phần tham gia — mời thật + trạng thái lấy từ DB (không fake phản hồi) */}
                        <div className="p-6 border-b border-gray-100">
                          <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-6">
                            <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                            2. Thành phần tham gia
                          </h3>
                          {perm && detail ? (
                            <ParticipantInvitationSection
                              visitInstanceId={Number(perm.visitInstanceId)}
                              relation={detail.relation}
                              // The SHARED status, not `detail`'s own copy: after a transition the
                              // detail payload is momentarily the stale one, and this section gates
                              // every invite/remove button on what it is handed (NP-04).
                              instanceStatus={instanceStatus ?? detail.instanceStatus}
                              currentUserId={user?.userId ? Number(user.userId) : null}
                              host={detail.host ?? null}
                              participants={detail.participants ?? []}
                              onChanged={loadDetail}
                              pushToast={pushToast}
                              delegationName={detail.delegationName}
                              campusName={detail.campusName}
                              plannedStartAt={detail.plannedStartAt}
                              plannedEndAt={detail.plannedEndAt}
                            />
                          ) : (
                            <p className="text-sm italic text-slate-400">Đang tải thành phần tham gia...</p>
                          )}
                        </div>

            {/* 3. Cảnh báo nhắc nhở trước thời gian tiếp khách */}
            <div className="p-6 border-b border-gray-100 bg-slate-50/50">
              <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-4">
                <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                3. Cảnh báo nhắc nhở trước thời gian tiếp khách
              </h3>

              {canConfigurePrep && remindersLoadFailed && (
                <StaleDataBanner onRetry={() => { void loadReminders(); }} className="mb-4" />
              )}

              <div className="grid grid-cols-1 gap-3 xl:grid-cols-4">
                {REMINDER_CONFIGS.map((cfg) => {
                  const row = reminders[cfg.key];
                  const isEmail = cfg.channel === 'EMAIL';
                  return (
                    <div key={cfg.key} className={`bg-white border rounded-xl p-3.5 shadow-sm flex flex-col justify-between ${isEmail ? 'border-orange-100' : 'border-blue-100'}`}>
                      <div>
                        <h4 className="text-sm font-bold text-gray-800 flex items-center gap-2 mb-2.5 leading-snug">
                          <div className={`w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${isEmail ? 'bg-orange-100 text-[#f37021]' : 'bg-blue-100 text-[#004c91]'}`}>
                            {isEmail ? <Mail className="w-3.5 h-3.5" /> : <Bell className="w-3.5 h-3.5" />}
                          </div>
                          <span>{cfg.title}</span>
                        </h4>
                        <label className="flex items-center gap-2 mb-3 pl-1.5 text-xs font-semibold text-gray-700 select-none cursor-pointer">
                          <input disabled={!canConfigurePrep} type="checkbox" checked={row.enabled}
                            onChange={(e) => setReminder(cfg.key, { enabled: e.target.checked })} />
                          Bật cảnh báo này
                        </label>
                      </div>
                      <div className="flex flex-wrap items-center gap-1.5 text-xs text-gray-600 font-normal pt-2 border-t border-gray-100">
                        <input disabled={!canConfigurePrep || !row.enabled} type="number" min="0" max="31"
                          className="w-12 px-1.5 py-1.5 text-center text-xs font-normal rounded-lg border border-gray-200 outline-none bg-gray-50 disabled:opacity-50"
                          value={row.days}
                          onChange={(e) => setReminder(cfg.key, { days: Math.max(0, Math.min(31, parseInt(e.target.value) || 0)) })} />
                        <span>ngày trước, vào lúc</span>
                        <input disabled={!canConfigurePrep || !row.enabled} type="time"
                          className="px-1.5 py-1.5 border border-gray-200 rounded-lg text-xs outline-none bg-white disabled:opacity-50 font-normal text-gray-800"
                          value={row.time}
                          onChange={(e) => setReminder(cfg.key, { time: e.target.value })} />
                      </div>
                    </div>
                  );
                })}
              </div>

              {canConfigurePrep && (() => {
                const remindersActionEnabled = canSubmitReminders({ canConfigurePrep, remindersLoadFailed, busy: savingReminders });
                return (
                <div className="flex flex-wrap justify-end gap-3 mt-5">
                  <button type="button" onClick={handleCancelReminders} disabled={!remindersActionEnabled}
                    className="px-5 py-2.5 rounded-xl font-bold text-sm text-red-600 bg-white border border-red-200 hover:bg-red-50 transition-colors shadow-sm outline-none disabled:opacity-60">
                    Tắt tất cả cảnh báo
                  </button>
                  <button type="button" onClick={handleSaveReminders} disabled={!remindersActionEnabled}
                    className="px-5 py-2.5 rounded-xl font-bold text-sm text-white bg-[#004c91] hover:bg-[#013565] transition-colors shadow-sm outline-none disabled:opacity-60 flex items-center gap-2">
                    <Bell className="w-4 h-4" />
                    {savingReminders ? 'Đang lưu...' : 'Lưu cảnh báo'}
                  </button>
                </div>
                );
              })()}
            </div>

            {/* 3.5 Ghi chú */}
                        <div className="p-6">
                          <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2 mb-3">
                            <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                            4. Ghi chú chung
                          </h3>
                          <textarea
                            ref={(el) => {
                              // Any refetch of `detail` (invite participant, save reminders/agenda...)
                              // flips `detailLoading` true→false, which early-returns a bare spinner
                              // and unmounts this whole tree — so this textarea gets a BRAND NEW DOM
                              // node on remount. The [preparationNote]-keyed effect below won't re-run
                              // for it (the note text itself didn't change), leaving the fresh node at
                              // its default ~48px height and clipping long notes. Resizing here, right
                              // when the node is (re)attached, covers that case too.
                              noteTextareaRef.current = el;
                              if (el) {
                                el.style.height = 'auto';
                                el.style.height = `${Math.max(48, el.scrollHeight)}px`;
                                el.scrollTop = 0;
                              }
                            }}
                            readOnly={!canConfigurePrep}
                            rows={1}
                            maxLength={5000}
                            placeholder="Ghi chú chuẩn bị nội bộ cho chuyến tiếp khách..."
                            className="w-full px-4 py-3 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-normal text-sm leading-relaxed min-h-[48px] resize-none overflow-hidden focus:bg-white focus:border-[#004c91] outline-none transition-all"
                            value={preparationNote}
                            onChange={(e) => {
                              setPreparationNote(e.target.value);
                              e.target.style.height = 'auto';
                              e.target.style.height = `${Math.max(48, e.target.scrollHeight)}px`;
                              e.target.scrollTop = 0;
                            }}
                          ></textarea>
                          <div className="flex items-center justify-between mt-2">
                            <span className="text-[11px] text-gray-400">{preparationNote.length}/5000</span>
                            {canConfigurePrep && (
                              <button
                                type="button"
                                onClick={handleSavePreparationNote}
                                disabled={savingNote || preparationNote === preparationNoteSaved}
                                title={preparationNote === preparationNoteSaved ? 'Chưa có thay đổi để lưu' : undefined}
                                className="px-5 py-2.5 rounded-xl font-bold text-sm text-white bg-[#004c91] hover:bg-[#013565] transition-colors shadow-sm outline-none disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
                              >
                                <CheckCircle2 className="w-4 h-4" />
                                {savingNote ? 'Đang lưu...' : 'Lưu ghi chú'}
                              </button>
                            )}
                          </div>
                        </div>

                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </div>

                    {isInfoEditable && (
                      <div className="flex flex-wrap justify-end gap-3 px-8 pb-8 pt-4 border-t border-gray-100">
                        <button 
                          onClick={() => setIsInfoEditable(false)}
                          className="px-8 py-3 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors shadow-sm outline-none"
                        >
                          Hủy
                        </button>
                        <button 
                          onClick={() => setIsInfoEditable(false)}
                          className="px-8 py-3 rounded-xl font-bold text-white bg-[#10b981] hover:bg-emerald-600 transition-all shadow-md hover:shadow-lg active:scale-[0.98] flex items-center gap-2 outline-none uppercase tracking-wider"
                        >
                          <CheckCircle2 className="w-5 h-5"/>
                          Hoàn thành
                        </button>
                      </div>
                    )}

                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>

          {/* Phần 2: Chuẩn bị chi tiết */}
          <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300">
            <div 
              className="px-6 py-4 flex items-center justify-between cursor-pointer transition-colors bg-[#004c91]"
              onClick={() => setIsSetupExpanded(!isSetupExpanded)}
            >
              <div>
                <h2 className="text-lg font-bold text-white border-l-4 border-[#f37021] pl-3">2. Chuẩn bị chi tiết</h2>
              </div>
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-white/10 flex items-center justify-center text-white">
                  {isSetupExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                </div>
              </div>
            </div>

            <AnimatePresence>
              {isSetupExpanded && (
                <motion.div
                  data-testid="before-prep-body"
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  className="border-t border-gray-100 overflow-hidden"
                >
                  <div className="p-8 space-y-12 bg-white">
                    {/* "Chuẩn bị chi tiết": the ORIGINAL styled card UI (Welcome LED / Campus Tour /
                        Họp / Khác) restored from commit da14fba, but fully wired to the REAL logistics
                        backend — real departments, prepareVisitLogistics, status enum, email
                        preview/edit. No mock data, no hard-coded leader names. See LogisticsRequestSection. */}
                    {perm && detail ? (
                      <LogisticsRequestSection
                        visitInstanceId={Number(perm.visitInstanceId)}
                        relation={detail.relation}
                        // Shared status — same reason as ParticipantInvitationSection above (NP-04).
                        instanceStatus={instanceStatus ?? detail.instanceStatus}
                        delegationName={detail.delegationName}
                        campusName={detail.campusName ?? 'FPT University'}
                        hostName={detail.hostName ?? 'Host'}
                        plannedStartAt={detail.plannedStartAt}
                        plannedEndAt={detail.plannedEndAt}
                        pushToast={pushToast}
                      />
                    ) : (
                      <p className="text-sm italic text-slate-400">Đang tải thông tin chuẩn bị chi tiết...</p>
                    )}
             </div>
           </motion.div>
         )}
       </AnimatePresence>
      </div>

      {/* Phần 3: Album ảnh (chỉ hiển thị cho VISITOR) */}
      {isVisitor && (
        <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300 mt-6">
          <div 
            className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#00a651]"
            onClick={() => setIsAlbumExpanded(!isAlbumExpanded)}
          >
            <div>
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">{tt('process.album.title')}</h2>
              <p className="text-sm font-normal text-green-100 mt-1 pl-4">{tt('process.album.subtitle')}</p>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                {isAlbumExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
              </div>
            </div>
          </div>

          <AnimatePresence>
            {isAlbumExpanded && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-gray-100 overflow-hidden"
              >
                <div className="p-4 sm:p-6 md:p-8 bg-white">
                  <div className="p-6 border-2 border-dashed border-gray-200 rounded-2xl flex flex-col items-center justify-center min-h-[160px] max-w-xl mx-auto bg-gray-50/50">
                    <p className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                      {tt('process.album.linkText')}
                    </p>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {/* Phần 4: Bài tin tức (chỉ hiển thị cho VISITOR) */}
      {isVisitor && (
        <div className="bg-white rounded-[2rem] border border-gray-200 shadow-sm overflow-hidden transition-all duration-300 mt-6">
          <div 
            className="px-8 py-6 flex items-center justify-between cursor-pointer transition-colors bg-[#4F46E5]"
            onClick={() => setIsNewsExpanded(!isNewsExpanded)}
          >
            <div>
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">{tt('process.news.title')}</h2>
              <p className="text-sm font-normal text-indigo-100 mt-1 pl-4">{tt('process.news.subtitle')}</p>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-white/20 flex items-center justify-center text-white">
                {isNewsExpanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
              </div>
            </div>
          </div>

          <AnimatePresence>
            {isNewsExpanded && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="border-t border-gray-100 overflow-hidden"
              >
                <div className="p-4 sm:p-6 md:p-8 bg-white">
                  <div className="p-6 border-2 border-dashed border-gray-200 rounded-2xl flex flex-col items-center justify-center min-h-[160px] max-w-xl mx-auto bg-gray-50/50">
                    <p className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">
                      {tt('process.news.placeholderHeadline')}
                    </p>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {/* Action row — deliberately OUTSIDE the collapsible panels above: a send button that
          disappears when the Host folds a section is a button they cannot find. */}
      {perm && (
        <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
          <button
            type="button"
            disabled={scheduleReportModal.loading}
            onClick={openScheduleReportPreview}
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-[#004c91] bg-white px-6 py-2.5 text-sm font-bold text-[#004c91] shadow-sm outline-none transition-colors hover:bg-[#004c91]/5 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
          >
            {scheduleReportModal.loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            {scheduleReportModal.loading ? 'Đang tạo báo cáo...' : 'Báo cáo Lịch trình'}
          </button>

          {perm.canSendSetupProgressEmail && (
            <button
              type="button"
              data-testid="send-setup-progress-email"
              disabled={setupEmail.preparing}
              onClick={() => setSetupEmail(prev => ({ ...prev, picking: true, error: null }))}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-[#004c91] px-6 py-2.5 text-sm font-bold text-white shadow-sm outline-none transition-colors hover:bg-[#013565] disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
            >
              {setupEmail.preparing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Mail className="w-4 h-4" />}
              {setupEmail.preparing ? 'Đang chuẩn bị email...' : 'Gửi cập nhật chuẩn bị'}
            </button>
          )}
        </div>
      )}

     {renderStageBar({
       stage: 'before',
       canDo: !!perm?.canStartVisit,
       done: instRank >= 2,
       label: 'Xác nhận hoàn thành chuẩn bị',
       doneLabel: 'Đã hoàn thành chuẩn bị',
     })}
     </div>
      )}

      {activeTab === 'during' && canViewDuring && (
        (perm ? !duringUnlocked : isPrep) ? (
          renderEmptyState()
        ) : (
          <>
            <VisitDuringTab isReadOnly={duringReadOnly} isDept={isDept} visitInstanceId={perm?.visitInstanceId} guestMembers={detail?.requestSummary?.guestMembers || []} supportMembers={detail?.requestSummary?.externalSupportMembers || []} />
            {renderStageBar({
              stage: 'during',
              canDo: !!perm?.canCompleteVisit,
              done: instRank >= 3,
              label: 'Xác nhận kết thúc tiếp khách',
              doneLabel: 'Đã kết thúc tiếp khách',
            })}
          </>
        )
      )}

      {activeTab === 'after' && canViewAfter && (
        (perm ? !afterUnlocked : (isPrep || currentStatus === 'Trong tiếp khách')) ? (
          renderEmptyState()
        ) : (
          <>
            <VisitAfterTab onTourCloseSuccess={() => navigate('/dashboard/visit')} isReadOnly={afterReadOnly} isDept={isDept && !isStudent} visitInstanceId={perm?.visitInstanceId} instanceStatus={perm?.instanceStatus} />
            {/* §10 điều kiện đóng đoàn: nếu chuyến không có bài tin tức được duyệt, Host phải tích xác
                nhận "không cần tin tức" thì mới đóng được (backend re-validate). */}
            {perm?.canCloseVisit && (
              <label className="mt-6 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 px-5 py-4 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={confirmNoNews}
                  onChange={(e) => setConfirmNoNews(e.target.checked)}
                  className="mt-0.5 w-4 h-4 shrink-0"
                />
                <span className="text-sm font-semibold text-amber-800">
                  Chuyến này không cần tạo/duyệt bài tin tức.{' '}
                  <span className="font-normal text-amber-700">
                    Tích chọn nếu đoàn không có bài tin tức. Nếu đã có bài tin tức được duyệt thì không cần tích.
                  </span>
                </span>
              </label>
            )}
            {renderStageBar({
              stage: 'after',
              canDo: !!perm?.canCloseVisit,
              done: instRank >= 4,
              label: 'Hoàn tất & đóng đoàn',
              doneLabel: 'Đã đóng đoàn',
            })}
          </>
        )
      )}

      {/* Rejection Reason Modal */}
      <AnimatePresence>
        {rejectReasonModal.isOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })} />
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl p-6 w-full max-w-md relative z-10 shadow-2xl border border-gray-100"
            >
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <X className="w-5 h-5 text-red-500" />
                  Lý do từ chối
                </h3>
                <button 
                  onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })}
                  className="p-1.5 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
              <p className="text-sm text-gray-600 mb-4">
                Bạn đang từ chối sự tham gia của <span className="font-bold text-[#004c91]">{String(rejectReasonModal.targetName)}</span>. Vui lòng cung cấp lý do (bắt buộc):
              </p>
              <textarea
                value={rejectReasonModal.reasonText}
                onChange={(e) => setRejectReasonModal(prev => ({ ...prev, reasonText: e.target.value }))}
                className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-red-500 focus:ring-1 focus:ring-red-500 outline-none transition-colors mb-6 text-sm resize-none"
                rows={3}
                placeholder="Nhập lý do từ chối..."
              />
              <div className="flex flex-wrap justify-end gap-3">
                <button
                  onClick={() => setRejectReasonModal({ isOpen: false, targetId: null, targetName: null, reasonText: '' })}
                  className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors"
                >
                  Huỷ
                </button>
                <button
                  onClick={handleConfirmReject}
                  disabled={!rejectReasonModal.reasonText.trim()}
                  className="px-5 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  Xác nhận từ chối
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* View Reason Modal */}
      <AnimatePresence>
        {viewReasonModal.isOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })} />
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl p-6 w-full max-w-sm relative z-10 shadow-2xl border border-gray-100"
            >
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <X className="w-5 h-5 text-red-500" />
                  Từ chối tham gia
                </h3>
                <button 
                  onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })}
                  className="p-1.5 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
              <p className="text-sm text-gray-600 mb-3">Lý do từ chối của <span className="font-bold text-[#004c91]">{viewReasonModal.targetName}</span>:</p>
              <div className="p-4 bg-red-50 text-red-800 rounded-xl border border-red-100 text-sm italic mb-6">
                "{viewReasonModal.reasonText}"
              </div>
              <div className="flex justify-end">
                <button 
                  onClick={() => setViewReasonModal({ isOpen: false, targetName: null, reasonText: '' })}
                  className="px-5 py-2 rounded-lg font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 transition-colors"
                >
                  Đóng
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Schedule Report Preview Modal */}
      <AnimatePresence>
        {scheduleReportModal.isOpen && (
          <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={closeScheduleReportModal} />
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl w-full max-w-5xl h-[92dvh] relative z-10 shadow-2xl border border-gray-100 flex flex-col overflow-hidden"
            >
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
                <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <FileText className="w-5 h-5 text-[#004c91]" />
                  Xem trước Báo cáo Lịch trình
                </h3>
                <button
                  onClick={closeScheduleReportModal}
                  className="p-1.5 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="px-6 py-3 border-b border-gray-100 flex-shrink-0 bg-white">
                <label className="inline-flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={scheduleReportModal.english}
                    disabled={scheduleReportModal.loading}
                    onChange={(e) => toggleScheduleReportEnglish(e.target.checked)}
                    className="w-4 h-4 rounded border-gray-300 text-[#004c91] focus:ring-[#004c91]/30 disabled:opacity-50"
                  />
                  <span className="text-sm font-bold text-gray-700">Tiếng Anh</span>
                  <span className="text-xs text-gray-400 font-normal">— hệ thống tự động dịch toàn bộ báo cáo</span>
                </label>
              </div>

              <div className="flex-1 min-h-0 bg-gray-100">
                {scheduleReportModal.loading && (
                  <div className="h-full flex flex-col items-center justify-center gap-3 text-gray-500">
                    <Loader2 className="w-6 h-6 animate-spin" />
                    <p className="text-sm font-normal">
                      {scheduleReportModal.english ? 'Đang dịch và tạo báo cáo...' : 'Đang tạo báo cáo...'}
                    </p>
                  </div>
                )}
                {!scheduleReportModal.loading && scheduleReportModal.error && (
                  <div className="h-full flex flex-col items-center justify-center gap-3 text-center px-6">
                    <AlertCircle className="w-6 h-6 text-red-500" />
                    <p className="text-sm font-normal text-red-600">{scheduleReportModal.error}</p>
                    <button
                      onClick={() => fetchScheduleReport(scheduleReportModal.english)}
                      className="px-4 py-2 rounded-lg font-bold text-sm text-[#004c91] bg-white border border-[#004c91] hover:bg-[#004c91]/5 transition-colors"
                    >
                      Thử lại
                    </button>
                  </div>
                )}
                {!scheduleReportModal.loading && !scheduleReportModal.error && scheduleReportModal.previewUrl && (
                  <iframe
                    src={scheduleReportModal.previewUrl}
                    title="Xem trước báo cáo lịch trình"
                    className="w-full h-full border-0"
                  />
                )}
              </div>

              <div className="px-6 py-4 bg-gray-50 flex flex-wrap items-center justify-end gap-3 border-t border-gray-100 flex-shrink-0">
                <button
                  onClick={closeScheduleReportModal}
                  className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors"
                >
                  Đóng
                </button>
                <button
                  onClick={downloadScheduleReport}
                  disabled={!scheduleReportModal.blob}
                  className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  <Download className="w-4 h-4" />
                  Tải xuống
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* "Gửi cập nhật chuẩn bị" — step 1: the language, asked BEFORE the draft is built.
          The template and the attached report must be produced in the same language, and both are
          rendered server-side at prepare time, so this cannot be a toggle inside the composer
          without re-rendering over whatever the Host has already written. */}
      {setupEmail.picking && (
        <div className="fixed inset-0 z-[130] flex items-center justify-center p-4" data-testid="setup-email-language">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={closeSetupEmail} />
          <div className="relative w-full max-w-md rounded-2xl bg-white shadow-2xl">
            <div className="border-b border-gray-100 px-6 py-4">
              <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
                <Mail className="w-5 h-5" /> Gửi cập nhật chuẩn bị
              </h3>
            </div>
            <div className="px-6 py-5 space-y-4">
              <p className="text-sm text-gray-600">
                Chọn ngôn ngữ cho nội dung email và Báo cáo Lịch trình đính kèm. Sau bước này anh/chị
                vẫn sửa được người nhận, tiêu đề và nội dung trước khi gửi.
              </p>
              {setupEmail.error && (
                <div role="alert" data-testid="setup-email-error"
                  className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                  <p>{setupEmail.error}</p>
                  {/* The language buttons below ARE the retry, so this says so rather than adding a
                      third button that would have to ask which language all over again. */}
                  <p className="mt-1 text-xs text-red-600/80">
                    Chưa có email nào được gửi. Chọn lại ngôn ngữ bên dưới để thử lại, hoặc bấm Huỷ.
                  </p>
                </div>
              )}
              <div className="flex gap-3">
                <button
                  type="button"
                  disabled={setupEmail.preparing}
                  onClick={() => { void prepareSetupProgressEmail('vi'); }}
                  className="flex-1 rounded-xl bg-[#004c91] px-4 py-2.5 text-sm font-bold text-white hover:bg-[#013565] disabled:opacity-60"
                >
                  Tiếng Việt
                </button>
                <button
                  type="button"
                  disabled={setupEmail.preparing}
                  onClick={() => { void prepareSetupProgressEmail('en'); }}
                  className="flex-1 rounded-xl border border-[#004c91] bg-white px-4 py-2.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5 disabled:opacity-60"
                >
                  English
                </button>
              </div>
            </div>
            <div className="flex items-center justify-end border-t border-gray-100 px-6 py-4">
              <button type="button" onClick={closeSetupEmail}
                className="rounded-lg px-4 py-2 text-sm font-semibold text-gray-500 hover:bg-gray-100">
                Huỷ
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Step 2: the shared composer, opened on the message the backend just rendered. Keyed by the
          composer SESSION so a re-prepare (a language change, a retry) mounts a fresh composer rather
          than leaving the previous message's form state behind — and so a sync, which changes the
          report's file id, does not. */}
      {perm && setupEmail.message && (
        <EmailComposeModal
          key={`setup-progress-${setupEmail.session}`}
          open
          onClose={closeSetupEmail}
          onSent={closeSetupEmail}
          pushToast={pushToast}
          contextTitle="Gửi cập nhật chuẩn bị"
          initialSubject={setupEmail.message.subject}
          // The body as generated. The composer compares the editor against this to know whether the
          // Host has edited it, and so whether a sync must warn before overwriting.
          initialBodyHtml={setupEmail.message.bodyHtml}
          // Grouped, so a CC the backend chose arrives as a CC. A flat string could not have said so.
          initialEnvelope={setupEmail.message.recipients.map((r, i) => ({
            email: r.email,
            name: r.name ?? null,
            recipientType: r.recipientType,
            displayOrder: i,
          }))}
          relatedType="VISIT_INSTANCE"
          relatedId={Number(perm.visitInstanceId)}
          lockedTemplate
          // The Schedule Report is attached by DEFAULT, not locked: it is in the list from the moment the
          // composer opens, and the Host may take it off. Passing only `lockedAttachmentFileIds` — which
          // names an id without attaching anything — is what produced a composer that claimed to carry a
          // report it never held, and a send the backend then refused for not carrying it.
          //
          // Absent when the backend could not produce one; the reason is in `warnings` below, and the
          // message is sendable without it.
          initialAttachments={setupEmail.message.reportFileId != null
            ? [{
                fileId: setupEmail.message.reportFileId,
                name: setupEmail.message.reportFileName ?? 'PEMS_Schedule_Report.pdf',
                mimeType: 'application/pdf',
                size: null,
              }]
            : undefined}
          notices={setupEmail.message.warnings}
          onRefreshRequiredAttachment={async () => {
            const language = setupEmail.message?.languageCode === 'en' ? 'en' : 'vi';
            const fresh = await delegationsApi.refreshSetupProgressEmail(
              perm.visitRequestId, perm.visitInstanceId, language);
            // Nothing is written back into this screen's state: from the moment it opens, the composer
            // owns the message. Copying the new file id up here used to change the composer's key and
            // remount it, throwing away the very sync that produced it.
            //
            // The body comes back too: it and the PDF are one snapshot, so the composer replaces both.
            // A null file id means the body was rebuilt but the PDF could not be stored — the composer
            // then drops the report it was holding rather than passing an older one off as the latest.
            return {
              fileId: fresh.reportFileId ?? null,
              name: fresh.reportFileName ?? null,
              generatedAt: fresh.reportGeneratedAt,
              bodyHtml: fresh.bodyHtml,
              warnings: fresh.warnings ?? [],
            };
          }}
          onSend={(payload, idempotencyKey) =>
            delegationsApi.sendSetupProgressEmail(
              perm.visitRequestId, perm.visitInstanceId,
              {
                subject: payload.subject,
                bodyHtml: payload.bodyHtml,
                languageCode: setupEmail.message?.languageCode ?? 'vi',
                recipients: payload.recipients,
                attachments: payload.attachments,
              },
              idempotencyKey)}
        />
      )}

      {/* ── Xác nhận chuyển giai đoạn (NP-05) ─────────────────────────────────────────────────
          Bắt buộc cho MỌI lần chuyển giai đoạn, và nói rõ HỆ QUẢ chứ không chỉ hỏi "chắc chưa?":
          chuyển khỏi "Trước tiếp khách" là đóng lại cửa sổ duy nhất còn sửa được lịch trình,
          thành phần tham gia, hậu cần, cảnh báo và ghi chú. Trước đây Host chỉ biết điều đó sau
          khi đã bấm. Bấm "Hủy" không gọi API; bấm xác nhận chỉ gửi đúng một request (nút khóa
          theo stageSubmitting) và hộp thoại chỉ đóng khi API trả thành công. */}
      {pendingStage && (
        <div
          className="fixed inset-0 z-[9998] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
          role="presentation"
          onClick={() => { if (!stageSubmitting) setPendingStage(null); }}
        >
          <div
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="stage-confirm-title"
            data-testid="stage-confirm-modal"
            className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 id="stage-confirm-title" className="text-lg font-bold text-gray-900">
              {STAGE_CONFIRM[pendingStage].title}
            </h3>
            <p className="mt-3 whitespace-pre-line text-sm leading-relaxed text-gray-600">
              {STAGE_CONFIRM[pendingStage].consequence}
            </p>
            {pendingStage === 'before' && perm?.isBeforeRecommendedStartWindow && perm?.recommendedStartVisitAt && (
              <p
                data-testid="stage-confirm-early"
                className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm font-normal text-amber-800"
              >
                Bạn đang chuyển sớm hơn mốc thường lệ
                {` (${formatVietnamDateTime(perm.recommendedStartVisitAt)}, 6 giờ trước giờ bắt đầu dự kiến).`}
                {' '}Việc này được phép — chỉ cần chắc chắn đoàn đã tới hoặc sắp tới.
              </p>
            )}
            <div className="mt-6 flex flex-wrap justify-end gap-3">
              <button
                type="button"
                data-testid="stage-confirm-cancel"
                disabled={stageSubmitting}
                onClick={() => setPendingStage(null)}
                className="rounded-xl border border-gray-200 bg-white px-5 py-2.5 text-sm font-bold text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
              >
                Hủy
              </button>
              <button
                type="button"
                data-testid="stage-confirm-submit"
                disabled={stageSubmitting}
                onClick={() => void advanceStage(pendingStage)}
                className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-50"
              >
                {stageSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <ArrowRightCircle className="h-4 w-4" />}
                {stageSubmitting ? 'Đang xử lý...' : STAGE_CONFIRM[pendingStage].confirmLabel}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toasts (top-right) */}
      {toasts.length > 0 && (
        <div className="fixed top-6 right-6 z-[9999] flex flex-col gap-3 w-[360px] max-w-[calc(100vw-32px)]">
          {toasts.map((t) => (
            <motion.div
              key={t.id}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              role="status"
              className={`flex items-start gap-2 rounded-xl border px-4 py-3 text-sm font-semibold shadow-lg ${
                t.type === 'success'
                  ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                  : t.type === 'warning'
                  ? 'bg-amber-50 border-amber-200 text-amber-800'
                  : t.type === 'info'
                  ? 'bg-blue-50 border-blue-200 text-blue-800'
                  : 'bg-red-50 border-red-200 text-red-700'
              }`}
            >
              {t.type === 'success' ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />}
              <span className="flex-1">{t.msg}</span>
              <button type="button" aria-label={tt('close')} onClick={() => setToasts((prev) => prev.filter((x) => x.id !== t.id))} className="text-current/70 hover:text-current">
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  );
}
