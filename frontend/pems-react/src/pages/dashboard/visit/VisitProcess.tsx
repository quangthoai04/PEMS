/**
 * Trang VisitProcess
 * Chu kỳ giám sát chung toàn trình quá trình đón tiếp theo giai đoạn (Trước/Trong/Sau).
 */

import React, { useState, useEffect, useRef } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
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
  FileText
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { VisitDuringTab } from './VisitDuringTab';
import { VisitAfterTab } from './VisitAfterTab';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitProcessPermission, VisitProcessDetail, AgendaResponsibleCandidate } from '../../../features/delegations/types/delegations.types';
import { AgendaSetupPanel } from '../../../features/agenda-templates/components/AgendaSetupPanel';
import { ParticipantInvitationSection } from '../../../features/delegations/components/ParticipantInvitationSection';
import { LogisticsRequestSection } from '../../../features/delegations/components/LogisticsRequestSection';
import { RegistrantInfoReadOnly, DelegationInfoReadOnly } from '../../../features/delegations/components/RequestInfoReadOnly';
import { VisitorVisitDetailPage } from './VisitorVisitDetailPage';
import { formatVietnamTime } from '../../../shared/utils/vietnamTime';
import { StaleDataBanner } from '../../../shared/components/state';
import { canSubmitReminders, canAssignResponsible, candidatesAreGenuinelyEmpty } from './visitProcessGuards';

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
      <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm">
        Giai đoạn này sẽ được mở khóa sau khi hoàn tất các bước trước đó trong quy trình tiếp khách.
      </p>
    </div>
  );

  const [activeTab, setActiveTab] = useState(isReceptionDetail ? 'before' : (location.state?.defaultTab || 'before'));
  const isPrep = (currentStatus === 'Đang chuẩn bị' || currentStatus === 'Trước tiếp khách') && !isClosed;
  // Vào tab "Trước tiếp khách" ưu tiên mở "1. Thông tin chung" (bản đăng ký gốc của khách,
  // chỉ đọc); "2. Chuẩn bị chi tiết" không mở mặc định. Effect bên dưới đồng bộ lại khi đổi tab.
  const [isInfoExpanded, setIsInfoExpanded] = useState(true);
  const [isSetupExpanded, setIsSetupExpanded] = useState(false);
  const [isAlbumExpanded, setIsAlbumExpanded] = useState(false);
  const [isNewsExpanded, setIsNewsExpanded] = useState(false);
  
  const [isSection1Expanded, setIsSection1Expanded] = useState(true);
  const [isSection2Expanded, setIsSection2Expanded] = useState(true);
  const [isSection3Expanded, setIsSection3Expanded] = useState(true);
  const [isSection4Expanded, setIsSection4Expanded] = useState(true);

  const [isInfoSection1Expanded, setIsInfoSection1Expanded] = useState(false);
  const [isInfoSection2Expanded, setIsInfoSection2Expanded] = useState(false);
  const [isInfoSection3Expanded, setIsInfoSection3Expanded] = useState(true);

  const [isInfoEditableState, setIsInfoEditable] = useState(false);

  // Mỗi khi vào (hoặc quay lại) tab "Trước tiếp khách": mặc định đóng mục 1 (Thông tin người tạo)
  // và mục 2 (Thông tin đoàn khách), đồng thời mở mặc định mục 3 (Thiết lập & Điều phối sự kiện)
  // để người dùng dễ dàng thao tác chuẩn bị sự kiện.
  useEffect(() => {
    if (activeTab === 'before') {
      setIsInfoExpanded(true);
      setIsSetupExpanded(false);
      setIsInfoSection1Expanded(false);
      setIsInfoSection2Expanded(false);
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
  const apiErrorMessage = (e: any, fallback: string): string => {
    const data = e?.response?.data;
    if (!data) return fallback;
    if (typeof data === 'string' && data.trim()) return data;
    if (data.message) return data.message;
    if (data.error) return data.error;
    if (data.errors) {
      const flat = Array.isArray(data.errors) ? data.errors : Object.values(data.errors).flat();
      const first = (flat as any[]).find((x) => typeof x === 'string' && x.trim());
      if (first) return first;
    }
    if (data.title) return data.title;
    return fallback;
  };

  // Before-tab setup/agenda/logistics/participants are NOT yet backed by a persistence API
  // (PrepareVisitLogistics / UpdateVisitLogistics and the agenda/participant saves are still
  // server stubs). They are therefore shown READ-ONLY — never fake-editable — so a Host can't
  // type into a form that silently drops the data. Flip this to the backend flag once the real
  // save endpoints exist (see report for the exact APIs required).
  const SETUP_SAVE_AVAILABLE = false;
  const canEditBefore = SETUP_SAVE_AVAILABLE && (perm ? perm.canEditBeforeVisit : !isDept);
  const isInfoEditable = isInfoEditableState && !isClosed && canEditBefore;
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
      // Refetch permissions → instanceStatus advances → next tab unlocks.
      await loadPermissions();
      // After moving to the next stage, jump the user to the TOP of the new tab so they never stay
      // stranded at the bottom of the page where the confirm button was (đặc tả mục 8.5).
      const scrollTabToTop = () => window.scrollTo({ top: 0, behavior: 'smooth' });
      if (stage === 'before') { pushToast('success', 'Đã xác nhận hoàn thành chuẩn bị.'); setActiveTab('during'); setCurrentStatus('Trong tiếp khách'); scrollTabToTop(); }
      else if (stage === 'during') { pushToast('success', 'Đã xác nhận kết thúc tiếp khách.'); setActiveTab('after'); setCurrentStatus('Chờ đóng đoàn'); scrollTabToTop(); }
      else { pushToast('success', 'Đã đóng đoàn thành công.'); setCurrentStatus('Đã đóng đoàn'); scrollTabToTop(); }
    } catch (e: any) {
      pushToast('error', apiErrorMessage(e, 'Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.'));
      // On a 409 the status changed under us — refetch so the UI reflects the real state.
      if (e?.response?.status === 409) { await loadPermissions(); }
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
    // Concrete assigned person (real user). Null = unassigned. Distinct from the template's
    // suggested role label below, which is display-only.
    responsibleUserId: number | null;
    responsibleUserName: string | null;
    templateResponsibleRoleLabel: string | null;
  };
  const [detail, setDetail] = useState<VisitProcessDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(true);
  const [detailLoadError, setDetailLoadError] = useState(false);
  const [agendaItems, setAgendaItems] = useState<AgendaRow[]>([]);
  const [isSavingAgenda, setIsSavingAgenda] = useState(false);
  const [agendaResponsibleCandidates, setAgendaResponsibleCandidates] = useState<AgendaResponsibleCandidate[]>([]);
  // A failed dependency load must NOT masquerade as "empty" — it blocks the action that consumes it.
  const [candidatesLoadFailed, setCandidatesLoadFailed] = useState(false);
  const [remindersLoadFailed, setRemindersLoadFailed] = useState(false);

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

  const loadDetail = React.useCallback(async () => {
    if (!perm) { setDetail(null); setAgendaItems([]); setDetailLoading(false); return; }
    try {
      setDetailLoading(true);
      const d = await delegationsApi.getVisitProcessDetail(perm.visitRequestId, perm.visitInstanceId);
      setDetail(d);
      setDetailLoadError(false);
      setPreparationNote(d.preparationNote ?? '');
      setPreparationNoteSaved(d.preparationNote ?? '');
      setAgendaItems((d.agenda || []).map((a) => ({
        agendaId: a.agendaId,
        title: a.title,
        startLocal: toDatetimeLocalInputValue(a.startTime),
        endLocal: toDatetimeLocalInputValue(a.endTime),
        location: a.location ?? '',
        responsibleUserId: a.responsibleUserId ?? null,
        responsibleUserName: a.responsibleUserName ?? null,
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

  // Responsible-person candidates (active host + ACCEPTED supporting participants of THIS instance).
  // Loaded once per instance; on failure we keep an empty list (dropdown just shows "Chưa chọn").
  const loadAgendaResponsibleCandidates = React.useCallback(async () => {
    if (!perm) { setAgendaResponsibleCandidates([]); setCandidatesLoadFailed(false); return; }
    try {
      const list = await delegationsApi.getAgendaResponsibleCandidates(perm.visitInstanceId);
      setAgendaResponsibleCandidates(Array.isArray(list) ? list : []);
      setCandidatesLoadFailed(false);
    } catch {
      // Do NOT let a failed load read as "no candidates" — clear the list AND flag the failure so the
      // assign dropdown is disabled and a retry is offered instead of a misleading empty picker.
      setAgendaResponsibleCandidates([]);
      setCandidatesLoadFailed(true);
    }
  }, [perm?.visitInstanceId]);
  useEffect(() => { void loadAgendaResponsibleCandidates(); }, [loadAgendaResponsibleCandidates]);

  const canEditAgenda = !!detail?.canEditBefore;
  const hasCurrentAgenda = agendaItems.length > 0;
  // Reminders + preparation note are editable by the instance Host during the prep window. This is
  // independent of the (always-false) SETUP_SAVE_AVAILABLE gate — the backend re-checks the same rule.
  const canConfigurePrep = !isClosed && detail?.relation === 'HOST'
    && (detail?.instanceStatus === 'ASSIGNED' || detail?.instanceStatus === 'BEFORE_VISIT');
  // Dependency gates: block the action whose data failed to load (never mutate on blank defaults).
  // `remindersActionEnabled` also folds in the in-flight `savingReminders` flag at the button site.
  const responsibleAssignEnabled = canAssignResponsible({ canEditAgenda, candidatesLoadFailed });
  const supportingCandidateCount = agendaResponsibleCandidates.filter((c) => !c.isMainHost).length;
  const showNoSupportingCandidatesHint = candidatesAreGenuinelyEmpty({ candidatesLoadFailed, supportingCandidateCount });

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
  }, [perm, detail, hasCurrentAgenda]);

  const saveAgenda = async () => {
    if (!perm || !detail) return;
    // Double-submit guard: spamming the button must never fire a 2nd request while the 1st is in
    // flight (a stale response could otherwise overwrite a newer one).
    if (isSavingAgenda) return;
    // The responsible-person validation below checks each assignee against the candidate list. If that
    // list failed to load it is empty, which would falsely reject every VALID existing assignee. Refuse
    // the save and prompt a reload instead of corrupting the agenda on a dependency failure.
    if (candidatesLoadFailed) {
      pushToast('error', 'Chưa tải được danh sách người phụ trách. Vui lòng thử lại trước khi lưu lịch trình.');
      return;
    }

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
      // Người phụ trách is optional, but if chosen it MUST be one of the valid candidates (the
      // backend re-validates this too — we just fail fast and avoid a doomed API call).
      if (it.responsibleUserId != null
          && !agendaResponsibleCandidates.some((c) => c.userId === it.responsibleUserId)) {
        pushToast('error', 'Người phụ trách đã chọn không hợp lệ. Vui lòng chọn lại.');
        return;
      }
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
        responsibleUserId: it.responsibleUserId ?? null,
      }));
      await delegationsApi.saveVisitAgenda(perm.visitRequestId, perm.visitInstanceId, items);
      pushToast('success', 'Lưu lịch trình thành công. Lịch trình và người phụ trách đã được cập nhật.');
      await loadDetail();
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
      ? 'Sau khi xác nhận, thông tin ở tab Trước tiếp khách sẽ được khóa và quy trình chuyển sang giai đoạn Trong tiếp khách.'
      : opts.stage === 'during'
        ? 'Sau khi xác nhận, quy trình sẽ chuyển sang giai đoạn Sau tiếp khách.'
        : 'Sau khi đóng đoàn, toàn bộ quy trình sẽ chuyển sang chế độ chỉ xem.';
    return (
      <div className="mt-6 rounded-2xl border border-gray-200 bg-white p-6 shadow-sm flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm font-medium text-slate-500 flex items-center gap-2">
          <AlertCircle className="w-4 h-4 text-[#f37021] shrink-0" />
          {stageNotice}
        </p>
        <button
          type="button"
          disabled={stageSubmitting}
          onClick={() => advanceStage(opts.stage)}
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
      noteTextareaRef.current.style.height = `${Math.max(44, noteTextareaRef.current.scrollHeight)}px`;
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
        <p className="text-sm font-bold text-slate-500">
          {isLoadingPerm ? 'Đang tải phân quyền...' : 'Đang tải thông tin...'}
        </p>
      </div>
    );
  }

  const isVisitorOwner = perm?.relation === 'VISITOR_OWNER' || detail?.relation === 'VISITOR_OWNER';

  if (!hasNumericId || permLoadFailed || detailLoadError) {
    return (
      <div className="w-full py-4 pb-24">
        <div className="bg-white rounded-[2rem] border border-gray-200 p-16 text-center shadow-sm flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-20 h-20 bg-rose-50 rounded-full flex items-center justify-center mb-6">
            <AlertCircle className="w-10 h-10 text-rose-400 stroke-[1.5]" />
          </div>
          <h2 className="text-xl font-bold text-slate-800 mb-2">
            {isReceptionDetail ? 'Không tìm thấy thông tin chuyến thăm' : 'Không có quyền truy cập'}
          </h2>
          <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm mb-6">
            {isReceptionDetail
              ? 'Bạn không có quyền xem chuyến thăm này hoặc đường dẫn không hợp lệ.'
              : 'Bạn không có quyền thao tác trang vận hành tiếp đón của đoàn này.'}
          </p>
          <button onClick={() => navigate(returnUrl)} className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003b70] transition-colors outline-none">
            Quay lại danh sách tiếp khách
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
          <p className="text-gray-500 font-medium max-w-sm mx-auto leading-relaxed text-sm mb-6">
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
          <p className="text-sm font-bold text-rose-700">
            Campus này đã bị hủy. Các thông tin chuẩn bị trước đó chỉ được hiển thị để tham khảo/lưu vết và không thể chỉnh sửa.
          </p>
        </div>
      ) : isClosed && !isReceptionDetail && (
        <div className="mb-8 bg-slate-100 border-l-4 border-slate-500 p-5 rounded-2xl flex items-center gap-3 text-left shadow-sm">
          <AlertCircle className="w-5 h-5 text-slate-600 shrink-0" />
          <p className="text-sm font-bold text-slate-700">
            {(currentStatus === 'Đã đóng đoàn' || currentStatus === 'Đã kết thúc')
              ? 'Hồ sơ lưu trữ: Đoàn khách này đã hoàn thành quy trình tiếp đón và đóng hồ sơ lịch sử. Dữ liệu đang hiển thị ở chế độ xem (Chỉ đọc).'
              : 'Chỉ người phụ trách tiếp đón mới có thể chỉnh sửa thông tin'
            }
          </p>
        </div>
      )}

      {/* Tabs */}
      {!isReceptionDetail && (
        <div className="flex bg-white rounded-2xl p-1.5 shadow-sm border border-gray-200 mb-8 max-w-2xl">
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
      )}

      {activeTab === 'before' && canViewBefore && (
        <div className="space-y-6">
          {/* Honest notice: the "Thông tin chung" registrant/delegation block is read-only
              reference data (what the guest registered). Lịch trình, thành phần tham gia and
              hậu cần (Chuẩn bị chi tiết) are all wired to real save APIs. */}

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
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: 'auto', opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  className="border-t border-gray-100 overflow-hidden"
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
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                          >
                            <div className="p-0 bg-white border-t border-gray-100">

                        {/* 3.2 Agenda */}
                        <div className="p-6 border-b border-gray-100 bg-slate-50/50">
                          {/* Compact header: title + a single toggle for the (heavy) apply-template panel,
                              so "Lịch trình hiện tại" is the focus once an agenda exists. */}
                          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                            <h3 className="text-base font-bold text-orange-900 bg-orange-50 w-max px-3 py-1.5 rounded-lg border border-orange-100 flex items-center gap-2">
                              <span className="w-1.5 h-4 bg-[#f37021] rounded-full"></span>
                              1. Agenda
                            </h3>
                            {perm && (
                              <button
                                type="button"
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
                          {perm && isAgendaTemplatePanelOpen && (
                            <AgendaSetupPanel
                              visitInstanceId={Number(perm.visitInstanceId)}
                              onApplied={async () => { await loadDetail(); setIsAgendaTemplatePanelOpen(false); }}
                              notify={pushToast}
                            />
                          )}
                          {/* Real agenda editor (visit_agendas). Host edits while preparing; saved
                              independently via "Lưu lịch trình" (does NOT change stage). */}
                          <div className="mb-2">
                            <h4 className="text-sm font-bold text-slate-800">Lịch trình hiện tại</h4>
                            <p className="text-xs text-slate-500">
                              Sắp xếp các hoạt động theo thứ tự thời gian. Mỗi dòng gồm thời gian, nội dung, địa điểm và người phụ trách.
                              {agendaItems.length > 0 && <span className="text-slate-400"> · {agendaItems.length} hoạt động</span>}
                            </p>
                          </div>
                          <div className="space-y-3">
                            {agendaItems.length === 0 && (
                              <p className="text-sm text-slate-500 italic">
                                Chưa có mục lịch trình nào.{canEditAgenda ? ' Bấm “Thêm mục” để tạo.' : ''}
                              </p>
                            )}
                            {agendaItems.length > 0 && (
                              <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <div className="hidden md:grid grid-cols-[44px_220px_minmax(240px,1fr)_160px_260px_36px] gap-3 bg-slate-50 px-4 py-2 text-[11px] font-bold uppercase tracking-wide text-slate-500 border-b border-slate-200">
                                  <span className="text-center">#</span>
                                  <span>Thời gian</span>
                                  <span>Nội dung</span>
                                  <span>Địa điểm</span>
                                  <span>Người phụ trách</span>
                                  <span />
                                </div>
                                <div className="divide-y divide-slate-100">
                                  {agendaItems.map((it, idx) => {
                                    // A previously-assigned responsible who is no longer a valid candidate
                                    // (e.g. later declined/removed) — keep them visible so the row doesn't
                                    // look unassigned; the host must re-pick before saving (validation guards it).
                                    const responsibleStale = it.responsibleUserId != null
                                      && !agendaResponsibleCandidates.some((c) => c.userId === it.responsibleUserId);
                                    const ghostInputClass = "h-9 w-full min-w-0 rounded-lg border border-transparent bg-transparent px-2 text-sm font-medium text-slate-800 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    const ghostSelectClass = "h-9 w-full rounded-lg border border-transparent bg-transparent px-2 text-sm font-medium text-slate-700 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    const ghostTimeClass = "h-8 w-full min-w-0 rounded-md border border-transparent bg-transparent px-2 text-sm font-semibold text-slate-800 outline-none transition hover:border-slate-200 hover:bg-white focus:border-[#004c91] focus:bg-white focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-transparent disabled:hover:bg-transparent";
                                    return (
                                      <div key={idx} className="grid grid-cols-1 gap-2 px-4 py-3 transition-colors hover:bg-slate-50 md:grid-cols-[44px_220px_minmax(240px,1fr)_160px_260px_36px] md:items-start md:gap-3">
                                        {/* Order number — leads the row so the eye scans it first. */}
                                        <div className="flex items-center md:justify-center">
                                          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-orange-50 text-xs font-bold text-[#F37021]">
                                            {idx + 1}
                                          </span>
                                        </div>

                                        {/* Time range — datetime-local keeps date + time together so multi-day agendas stay
                                            correct; stacked (not side-by-side) so the full date/time never gets clipped. */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="block text-[10px] font-bold uppercase text-slate-400 md:hidden">Thời gian</label>
                                          <div className="grid grid-cols-[14px_1fr] items-center gap-x-2 gap-y-1">
                                            <span />
                                            <input type="datetime-local" value={it.startLocal} disabled={!canEditAgenda}
                                              onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, startLocal: e.target.value } : p))}
                                              className={ghostTimeClass} />
                                            <span className="text-center text-slate-400">→</span>
                                            <input type="datetime-local" value={it.endLocal} disabled={!canEditAgenda}
                                              onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, endLocal: e.target.value } : p))}
                                              className={ghostTimeClass} />
                                          </div>
                                        </div>

                                        {/* Content */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Nội dung</label>
                                          <input type="text" value={it.title} disabled={!canEditAgenda} placeholder="Nội dung mục lịch trình"
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, title: e.target.value } : p))}
                                            className={ghostInputClass} />
                                        </div>

                                        {/* Location */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Địa điểm</label>
                                          <input type="text" value={it.location} disabled={!canEditAgenda} placeholder="(tuỳ chọn)"
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, location: e.target.value } : p))}
                                            className={ghostInputClass} />
                                        </div>

                                        {/* Responsible person (real user) + the template's suggested role hint */}
                                        <div className="pl-9 md:pl-0">
                                          <label className="mb-1 block text-[10px] font-bold uppercase text-slate-400 md:hidden">Người phụ trách</label>
                                          <select
                                            value={it.responsibleUserId ?? ''}
                                            disabled={!responsibleAssignEnabled}
                                            onChange={(e) => setAgendaItems((prev) => prev.map((p, i) => i === idx ? { ...p, responsibleUserId: e.target.value ? Number(e.target.value) : null } : p))}
                                            className={ghostSelectClass}
                                          >
                                            <option value="">Chưa chọn người phụ trách</option>
                                            {responsibleStale && (
                                              <option value={it.responsibleUserId as number}>
                                                {(it.responsibleUserName ?? `Người dùng #${it.responsibleUserId}`)} (không còn khả dụng)
                                              </option>
                                            )}
                                            {agendaResponsibleCandidates.map((candidate) => (
                                              <option key={candidate.userId} value={candidate.userId}>
                                                {candidate.fullName} — {candidate.displayRole}
                                              </option>
                                            ))}
                                          </select>
                                          {it.templateResponsibleRoleLabel && (
                                            <p className="mt-1 text-[11px] font-medium text-slate-500">
                                              Gợi ý: <span className="font-bold text-[#004c91]">{it.templateResponsibleRoleLabel}</span>
                                            </p>
                                          )}
                                        </div>

                                        {/* Delete */}
                                        <div className="flex justify-end pl-9 md:justify-center md:pl-0">
                                          {canEditAgenda && (
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
                            {canEditAgenda && candidatesLoadFailed && (
                              <div className="flex flex-wrap items-center justify-between gap-2 text-xs font-medium text-red-700 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
                                <span>Không tải được danh sách người phụ trách. Không thể chọn người phụ trách cho tới khi tải lại.</span>
                                <button type="button" onClick={() => { void loadAgendaResponsibleCandidates(); }}
                                  className="inline-flex items-center gap-1 underline hover:no-underline">Thử lại</button>
                              </div>
                            )}
                            {canEditAgenda && showNoSupportingCandidatesHint && (
                              <p className="text-xs font-medium text-amber-700 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2">
                                Chưa có người tham gia nào đã chấp nhận lời mời. Hiện tại chỉ người phụ trách tiếp đón có thể được chọn.
                              </p>
                            )}
                            {canEditAgenda && (
                              <div className="flex flex-wrap items-center gap-3 pt-2">
                                <button type="button"
                                  onClick={() => setAgendaItems((prev) => [...prev, { agendaId: null, title: '', startLocal: toDatetimeLocalInputValue(detail?.plannedStartAt), endLocal: toDatetimeLocalInputValue(detail?.plannedEndAt), location: '', responsibleUserId: null, responsibleUserName: null, templateResponsibleRoleLabel: null }])}
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
                              instanceStatus={detail.instanceStatus}
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
                      <div className="flex flex-wrap items-center gap-1.5 text-xs text-gray-600 font-medium pt-2 border-t border-gray-100">
                        <input disabled={!canConfigurePrep || !row.enabled} type="number" min="0" max="31"
                          className="w-12 px-1.5 py-1.5 text-center text-xs font-bold rounded-lg border border-gray-200 outline-none bg-gray-50 disabled:opacity-50"
                          value={row.days}
                          onChange={(e) => setReminder(cfg.key, { days: Math.max(0, Math.min(31, parseInt(e.target.value) || 0)) })} />
                        <span>ngày trước, vào lúc</span>
                        <input disabled={!canConfigurePrep || !row.enabled} type="time"
                          className="px-1.5 py-1.5 border border-gray-200 rounded-lg text-xs outline-none bg-white disabled:opacity-50 font-semibold text-gray-800"
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
                            ref={noteTextareaRef}
                            readOnly={!canConfigurePrep}
                            rows={1}
                            maxLength={5000}
                            placeholder="Ghi chú chuẩn bị nội bộ cho chuyến tiếp khách..."
                            className="w-full px-4 py-2.5 rounded-xl border border-gray-200 bg-gray-50/50 text-gray-800 font-medium text-sm min-h-[44px] resize-none overflow-hidden focus:bg-white focus:border-[#004c91] outline-none transition-all"
                            value={preparationNote}
                            onChange={(e) => {
                              setPreparationNote(e.target.value);
                              e.target.style.height = 'auto';
                              e.target.style.height = `${Math.max(44, e.target.scrollHeight)}px`;
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
                      <div className="flex justify-end gap-3 px-8 pb-8 pt-4 border-t border-gray-100">
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
                        instanceStatus={detail.instanceStatus}
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
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">3. Album ảnh</h2>
              <p className="text-sm font-medium text-green-100 mt-1 pl-4">Thư viện hình ảnh của chuyến tham quan</p>
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
                      Xem toàn bộ Album ảnh trên thư mục Drive
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
              <h2 className="text-xl font-bold text-white border-l-4 border-white pl-3">4. Bài tin tức</h2>
              <p className="text-sm font-medium text-indigo-100 mt-1 pl-4">Các bài đăng và tin tức sau chuyến tham quan</p>
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
                      Trải nghiệm khó quên của học sinh tại FPTU
                    </p>
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      )}

      {perm && (
        <div className="mt-6 flex justify-end">
          <button
            type="button"
            disabled={scheduleReportModal.loading}
            onClick={openScheduleReportPreview}
            className="inline-flex items-center justify-center gap-2 rounded-xl border border-[#004c91] bg-white px-6 py-2.5 text-sm font-bold text-[#004c91] shadow-sm outline-none transition-colors hover:bg-[#004c91]/5 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
          >
            {scheduleReportModal.loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            {scheduleReportModal.loading ? 'Đang tạo báo cáo...' : 'Báo cáo Lịch trình'}
          </button>
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
            <VisitAfterTab onTourCloseSuccess={() => navigate('/dashboard/visit')} isReadOnly={afterReadOnly} isDept={isDept && !isStudent} visitInstanceId={perm?.visitInstanceId} />
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
              <div className="flex justify-end gap-3">
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
              className="bg-white rounded-2xl w-full max-w-3xl h-[85vh] relative z-10 shadow-2xl border border-gray-100 flex flex-col overflow-hidden"
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
                  <span className="text-xs text-gray-400 font-medium">— hệ thống tự động dịch toàn bộ báo cáo</span>
                </label>
              </div>

              <div className="flex-1 min-h-0 bg-gray-100">
                {scheduleReportModal.loading && (
                  <div className="h-full flex flex-col items-center justify-center gap-3 text-gray-500">
                    <Loader2 className="w-6 h-6 animate-spin" />
                    <p className="text-sm font-semibold">
                      {scheduleReportModal.english ? 'Đang dịch và tạo báo cáo...' : 'Đang tạo báo cáo...'}
                    </p>
                  </div>
                )}
                {!scheduleReportModal.loading && scheduleReportModal.error && (
                  <div className="h-full flex flex-col items-center justify-center gap-3 text-center px-6">
                    <AlertCircle className="w-6 h-6 text-red-500" />
                    <p className="text-sm font-semibold text-red-600">{scheduleReportModal.error}</p>
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

              <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100 flex-shrink-0">
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
              <button type="button" aria-label="Đóng" onClick={() => setToasts((prev) => prev.filter((x) => x.id !== t.id))} className="text-current/70 hover:text-current">
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  );
}
