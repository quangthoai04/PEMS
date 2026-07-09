/**
 * Trang EditVisitRequest — Visitor sửa đơn đang chờ xử lý (mode "edit") hoặc
 * sửa & gửi lại đơn đã bị từ chối toàn bộ (mode "resubmit").
 *
 * - Load dữ liệu thật từ GET /visit-requests/{id}/edit-detail (owner-only).
 * - Tái sử dụng đúng các section của form đăng ký công khai (UC-17) nhưng KHÔNG có OTP.
 * - Mốc thời gian tối thiểu là 24h (VISIT_REQUEST_EDIT_MIN_ADVANCE_HOURS) thay vì 72h như đơn mới.
 * - edit  → PUT  /visit-requests/{id}/pending-edit   (trạng thái giữ nguyên Chờ xử lý)
 * - resubmit → POST /visit-requests/{id}/resubmit     (REJECTED → PENDING_APPROVAL,
 *   không được đổi danh sách cơ sở — backend sẽ chặn nếu đổi).
 */

import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useLocation } from 'react-router-dom';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, ArrowLeft, Loader2, PencilLine, RefreshCw, Send } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  buildVisitRequestSchema,
  VISIT_REQUEST_EDIT_MIN_ADVANCE_HOURS,
  type VisitRequestSchema,
} from '../../../features/visit-request/schema/visitRequest.schema';
import {
  visitRequestApi,
  type EditableVisitRequestDetail,
} from '../../../features/visit-request/api/visitRequestApi';
import { RegisterInfoSection } from '../../../features/visit-request/components/sections/RegisterInfoSection';
import { VisitInfoSection } from '../../../features/visit-request/components/sections/VisitInfoSection';
import { VisitorListSection } from '../../../features/visit-request/components/sections/VisitorListSection';
import { ContactSection } from '../../../features/visit-request/components/sections/ContactSection';
import { AdditionalSection } from '../../../features/visit-request/components/sections/AdditionalSection';
import { DEFAULT_VISIT_REQUEST_VALUES } from '../../../features/visit-request/hooks/useVisitRequestForm';
import { showSuccessToast, showErrorToast, getApiErrorMessage } from '../../../shared/utils/toast';

type FormMode = 'edit' | 'resubmit';

/** "2026-07-10T09:00:00" (wall-clock từ backend) → "2026-07-10T09:00" cho input datetime-local. */
const toLocalInputValue = (value: string | null | undefined): string =>
  value ? value.slice(0, 16) : '';

function mapDetailToFormValues(detail: EditableVisitRequestDetail): VisitRequestSchema {
  // Chỉ giữ link partner khi đối tác CÒN hợp lệ (ACTIVE + APPROVED). Nếu partner cũ đã bị
  // vô hiệu/từ chối/xóa → KHÔNG block form: hạ về tổ chức nhập tay, partnerId = null.
  const canUseExistingPartner =
    detail.partnerId != null &&
    detail.partnerIsActive === true &&
    detail.partnerProfileStatus === 'APPROVED';
  const organizationText =
    detail.partnerName || detail.registrantOrganization || '';

  return {
    ...DEFAULT_VISIT_REQUEST_VALUES,
    registerInfo: {
      fullName: detail.registrantFullName || '',
      organization: organizationText,
      jobTitle: detail.registrantJobTitle || '',
      phone: detail.registrantPhone || '',
      email: detail.registrantEmail || '',
      nationality: detail.registrantNationality || '',
    },
    delegationName: detail.delegationName || '',
    visitMode: detail.visitScope === 'MULTI_CAMPUS' ? 'multiple' : 'single',
    visitType: (detail.visitType || 'CAMPUS_TOUR') as VisitRequestSchema['visitType'],
    visitTypeOther: detail.visitTypeOther || '',
    visits: detail.campusVisits.map((c) => ({
      campus: c.campusCode,
      startDatetime: toLocalInputValue(c.plannedStartAt),
      endDatetime: toLocalInputValue(c.plannedEndAt),
    })),
    purpose: detail.purpose || '',
    workingContent: detail.workingContent || '',
    visitors: detail.visitors.length
      ? detail.visitors.map((v) => ({
          fullName: v.fullName || '',
          jobTitle: v.jobTitle || '',
          organization: v.organization || '',
          nationality: v.nationality || '',
        }))
      : DEFAULT_VISIT_REQUEST_VALUES.visitors,
    supportTeam: detail.supportMembers.map((s) => ({
      fullName: s.fullName || '',
      jobTitle: s.jobTitle || '',
      organization: s.organization || '',
      nationality: s.nationality || '',
    })),
    contactPoint: {
      fullName: detail.contactPersonFullName || '',
      organization: detail.contactPersonOrganization || '',
      phone: detail.contactPersonPhone || '',
      email: detail.contactPersonEmail || '',
    },
    workingLanguage: (detail.workingLanguage === 'VI' ? 'VI' : 'EN') as 'VI' | 'EN',
    transportationNote: detail.transportationNote || '',
    mediaConsentStatus: (detail.mediaConsentStatus === 'AGREED' ? 'AGREED' : 'DECLINED') as 'AGREED' | 'DECLINED',
    mediaConsentNote: detail.mediaConsentNote || '',
    partnerSelectionMode: canUseExistingPartner ? 'EXISTING_PARTNER' : 'NEW_ORGANIZATION',
    partnerId: canUseExistingPartner ? detail.partnerId : null,
    notes: detail.noteToFptu || '',
    timeOverlapConfirmed: false,
  };
}

export function EditVisitRequest() {
  const navigate = useNavigate();
  const location = useLocation();
  const { visitRequestId } = useParams<{ visitRequestId: string }>();
  const mode: FormMode = location.pathname.includes('/resubmit/') ? 'resubmit' : 'edit';

  const [detail, setDetail] = useState<EditableVisitRequestDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [showErrors, setShowErrors] = useState(false);

  const { t: tv, i18n } = useTranslation(['validation']);

  // Rebuilt on language change — Zod bakes messages in at construction time.
  const editSchema = useMemo(
    () => buildVisitRequestSchema(VISIT_REQUEST_EDIT_MIN_ADVANCE_HOURS, (key, options) =>
      tv(key, { ns: 'validation', ...options }),
    ),
    [tv, i18n.language],
  );

  const form = useForm<VisitRequestSchema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(editSchema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: DEFAULT_VISIT_REQUEST_VALUES,
  });

  const visitFields = useFieldArray({ control: form.control, name: 'visits' });
  const visitorFields = useFieldArray({ control: form.control, name: 'visitors' });
  const supportTeamFields = useFieldArray({ control: form.control, name: 'supportTeam' });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!visitRequestId) return;
      setIsLoading(true);
      setLoadError(null);
      try {
        const data = await visitRequestApi.getEditableDetail(visitRequestId);
        if (cancelled) return;
        // Route và trạng thái thật phải khớp nhau (edit ↔ EDIT, resubmit ↔ RESUBMIT).
        if (mode === 'edit' && !data.isEditablePending) {
          setLoadError('Đơn này không còn ở trạng thái có thể sửa. Vui lòng quay lại danh sách.');
        } else if (mode === 'resubmit' && !data.isResubmittable) {
          setLoadError('Đơn này không ở trạng thái có thể gửi lại. Vui lòng quay lại danh sách.');
        } else {
          setDetail(data);
          const values = mapDetailToFormValues(data);
          form.reset(values);
          visitFields.replace(values.visits);
          visitorFields.replace(values.visitors);
          supportTeamFields.replace(values.supportTeam);
        }
      } catch (err) {
        if (!cancelled) setLoadError(getApiErrorMessage(err, 'Không tải được dữ liệu đơn. Vui lòng thử lại.'));
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visitRequestId, mode]);

  // ── Helpers dùng lại cho ContactSection (đồng bộ đầu mối/đội hỗ trợ từ người đăng ký) ──
  const syncSupportFromRegister = () => {
    const reg = form.getValues('registerInfo');
    const registrantAsSupport = {
      fullName: reg.fullName,
      jobTitle: reg.jobTitle,
      organization: reg.organization,
      nationality: reg.nationality,
      isAutoFilledFromRegistrant: true,
    };
    const currentTeam = form.getValues('supportTeam') || [];
    const existingIndex = currentTeam.findIndex((m) => m.isAutoFilledFromRegistrant);
    if (existingIndex >= 0) {
      supportTeamFields.update(existingIndex, registrantAsSupport);
    } else {
      supportTeamFields.append(registrantAsSupport);
    }
    form.trigger('supportTeam');
  };

  const clearSupportFirstRow = () => {
    const currentTeam = form.getValues('supportTeam') || [];
    const filtered = currentTeam.filter((m) => !m.isAutoFilledFromRegistrant);
    supportTeamFields.replace(filtered);
  };

  const syncContactFromRegister = () => {
    const reg = form.getValues('registerInfo');
    form.setValue('contactPoint', {
      fullName: reg.fullName,
      organization: reg.organization,
      phone: reg.phone,
      email: reg.email,
    }, { shouldValidate: true });
  };

  const clearContactPoint = () => {
    form.setValue('contactPoint', { fullName: '', organization: '', phone: '', email: '' });
  };

  const onSubmit = form.handleSubmit(
    async (data) => {
      if (!visitRequestId) return;
      setIsSubmitting(true);
      setSubmitError(null);
      try {
        const res = mode === 'edit'
          ? await visitRequestApi.updatePending(visitRequestId, data)
          : await visitRequestApi.resubmitRejected(visitRequestId, data);
        showSuccessToast(res.message || (mode === 'edit' ? 'Đã cập nhật đơn.' : 'Đã gửi lại đơn.'));
        navigate('/dashboard/visit');
      } catch (err) {
        const message = getApiErrorMessage(
          err,
          mode === 'edit' ? 'Không thể cập nhật đơn. Vui lòng thử lại.' : 'Không thể gửi lại đơn. Vui lòng thử lại.'
        );
        setSubmitError(message);
        showErrorToast(err, message);
      } finally {
        setIsSubmitting(false);
      }
    },
    () => {
      setShowErrors(true);
      setSubmitError('Vui lòng kiểm tra lại các thông tin còn thiếu hoặc chưa hợp lệ.');
      setTimeout(() => {
        document.querySelector('.error-scroll-target')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }, 100);
    }
  );

  const pageTitle = mode === 'edit' ? 'Sửa đơn đăng ký tham quan' : 'Sửa & gửi lại đơn đăng ký';
  const previousDecisions = useMemo(
    () => (detail?.previousDecisions || []).filter((d) => d.decisionNote || d.decidedByName),
    [detail]
  );

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="cursor-pointer hover:text-[#004c91] transition-colors" onClick={() => navigate('/dashboard/visit')}>
          Quản lý tiếp khách
        </span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">{pageTitle}</span>
      </div>

      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/dashboard/visit')}
            className="p-2 rounded-xl border border-gray-200 bg-white text-gray-500 hover:text-[#004c91] hover:border-[#004c91]/40 transition-colors"
            title="Quay lại danh sách"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-[#004c91] flex items-center gap-2">
              {mode === 'edit' ? <PencilLine className="w-7 h-7" /> : <RefreshCw className="w-7 h-7" />}
              {pageTitle}
            </h1>
            {detail && (
              <p className="text-gray-500 mt-1 font-medium text-sm">
                Mã đơn: <span className="font-bold text-[#004c91]">{detail.requestCode}</span>
                {detail.resubmissionCount > 0 && (
                  <span className="ml-3 text-orange-600 font-semibold">Đã gửi lại {detail.resubmissionCount} lần</span>
                )}
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Banner theo mode */}
      {!isLoading && !loadError && (
        mode === 'edit' ? (
          <div className="mb-6 flex items-start gap-3 rounded-2xl border border-blue-200 bg-blue-50 px-5 py-4 text-sm text-blue-800 font-medium">
            <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
            <span>
              Bạn đang chỉnh sửa đơn đang chờ xử lý. Sau khi lưu, Staff Leader các cơ sở sẽ xem thông tin mới nhất.
              Chỉ có thể sửa khi chưa cơ sở nào ra quyết định và lịch còn cách hiện tại tối thiểu 24 giờ.
            </span>
          </div>
        ) : (
          <div className="mb-6 rounded-2xl border border-orange-200 bg-orange-50 px-5 py-4 text-sm text-orange-800 font-medium">
            <div className="flex items-start gap-3">
              <AlertCircle className="w-5 h-5 shrink-0 mt-0.5" />
              <span>
                Đơn này đã bị từ chối. Bạn có thể chỉnh sửa thông tin và gửi lại để các cơ sở xem xét lại.
                Lý do từ chối cũ sẽ được lưu trong lịch sử hệ thống. Không thể đổi danh sách cơ sở khi gửi lại —
                nếu muốn thăm cơ sở khác, vui lòng tạo đơn mới.
              </span>
            </div>
            {previousDecisions.length > 0 && (
              <div className="mt-3 ml-8 space-y-1.5">
                {previousDecisions.map((d) => (
                  <div key={d.visitInstanceId} className="text-xs text-orange-900 bg-white/70 border border-orange-100 rounded-lg px-3 py-2">
                    <span className="font-bold">{d.campusName}:</span>{' '}
                    {d.decisionNote || 'Không có ghi chú'}
                    {d.decidedByName && <span className="text-orange-700"> — {d.decidedByName}</span>}
                  </div>
                ))}
              </div>
            )}
          </div>
        )
      )}

      {isLoading ? (
        <div className="flex items-center justify-center py-24 text-gray-400 gap-3">
          <Loader2 className="w-6 h-6 animate-spin" />
          <span className="font-medium">Đang tải dữ liệu đơn...</span>
        </div>
      ) : loadError ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 px-6 py-8 text-center">
          <AlertCircle className="w-10 h-10 text-red-400 mx-auto mb-3" />
          <p className="text-red-700 font-semibold mb-4">{loadError}</p>
          <button
            type="button"
            onClick={() => navigate('/dashboard/visit')}
            className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#013565] transition-colors"
          >
            Về danh sách tiếp khách
          </button>
        </div>
      ) : (
        <form onSubmit={onSubmit} noValidate>
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5 sm:p-8 space-y-12">
            <RegisterInfoSection form={form} showErrors={showErrors} />
            <VisitInfoSection form={form} visitFields={visitFields} showErrors={showErrors} />
            <VisitorListSection form={form} visitorFields={visitorFields} showErrors={showErrors} />
            <ContactSection
              form={form}
              supportTeamFields={supportTeamFields}
              onSyncSupportFromRegister={syncSupportFromRegister}
              onClearSupportFirstRow={clearSupportFirstRow}
              onSyncContactFromRegister={syncContactFromRegister}
              onClearContactPoint={clearContactPoint}
              showErrors={showErrors}
            />
            <AdditionalSection form={form} />
          </div>

          {/* Action bar */}
          <div className="flex items-center justify-end gap-3 mt-8">
            {submitError && (
              <div className="flex items-center gap-2 text-red-600 text-xs font-medium bg-red-50 px-3 py-2 rounded-lg border border-red-200">
                <AlertCircle className="w-4 h-4 shrink-0" />
                {submitError}
              </div>
            )}
            <button
              type="button"
              onClick={() => navigate('/dashboard/visit')}
              disabled={isSubmitting}
              className="px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors disabled:opacity-50"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className={`inline-flex items-center gap-2 px-8 py-3 rounded-xl font-black tracking-wide text-white shadow-lg transition-all transform hover:-translate-y-0.5 disabled:opacity-60 disabled:transform-none ${
                mode === 'edit'
                  ? 'bg-gradient-to-r from-[#004c91] to-[#013565] hover:from-[#013565] hover:to-[#012a52] shadow-blue-900/30'
                  : 'bg-gradient-to-r from-[#f37021] to-[#e06111] hover:from-[#e06111] hover:to-[#c4530c] shadow-orange-500/30'
              }`}
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Đang gửi...
                </>
              ) : mode === 'edit' ? (
                <>
                  <Send className="w-4 h-4" />
                  Lưu thay đổi
                </>
              ) : (
                <>
                  <RefreshCw className="w-4 h-4" />
                  Gửi lại đơn
                </>
              )}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
