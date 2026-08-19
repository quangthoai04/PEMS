/**
 * Trang AuditLogManagement (ADMIN) — list/detail từ audit_logs + audit_log_changes.
 * Giá trị nhạy cảm (password/token/credential/secret/cookie/refresh...) đã được
 * backend mask trước khi trả về.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ScrollText, Search, ChevronDown, ChevronLeft, ChevronRight,
  Loader2, RefreshCw, Eye, X, ShieldAlert,
} from 'lucide-react';
import { adminApi } from '../../../features/admin/api/adminApi';
import type {
  AdminAuditLogDetail, AdminAuditLogItem, PaginatedResult,
} from '../../../features/admin/types/admin.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage } from '../../../shared/utils/toast';

const dateCls =
  'px-4 py-3 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all bg-white/10 text-white shadow-inner [color-scheme:dark]';

// Nhãn tiếng Việt cho action/entityType — đây là mã ghi thẳng từ tên lệnh (command) ở backend nên
// không phải enum hữu hạn nhỏ; bảng dưới phủ các action/entity thực tế đang tồn tại trong hệ thống.
// Action/entity mới phát sinh sau này (chưa kịp thêm vào bảng) vẫn đọc được nhờ humanizeCode.
const ACTION_LABEL: Record<string, string> = {
  // Tài khoản
  CREATE_ACCOUNT: 'Tạo tài khoản',
  RECREATE_PENDING_ACCOUNT: 'Tạo lại tài khoản chờ xác nhận',
  CANCEL_PENDING_ACCOUNT: 'Hủy tài khoản chờ xác nhận',
  AUTO_CANCEL_PENDING_ACCOUNT: 'Tự động hủy tài khoản chờ xác nhận',
  CONFIRM_ACCOUNT_EMAIL: 'Xác nhận email tài khoản',
  EDIT_PENDING_ACCOUNT_EMAIL: 'Sửa email tài khoản chờ xác nhận',
  MANAGE_ACCOUNT_STATUS: 'Cập nhật trạng thái tài khoản',
  REPLACE_STAFF_LEADER: 'Thay thế Trưởng phòng IC',
  UPDATE_ACCOUNT_ROLE: 'Cập nhật vai trò tài khoản',
  UPDATE_ACCOUNT_BASIC_INFO: 'Cập nhật thông tin cơ bản tài khoản',
  // Phiên đăng nhập
  ADMIN_REVOKE_SESSION: 'Thu hồi phiên đăng nhập',
  ADMIN_REVOKE_USER_SESSIONS: 'Thu hồi toàn bộ phiên của người dùng',
  // Phòng ban
  ADD_DEPARTMENT_PERSONNEL: 'Thêm nhân sự phòng ban',
  CREATE_DEPARTMENT: 'Tạo phòng ban',
  MANAGE_DEPARTMENT_STATUS: 'Cập nhật trạng thái phòng ban',
  UPDATE_DEPARTMENT_NAME: 'Đổi tên phòng ban',
  UPDATE_DEPARTMENT_PERSONNEL: 'Cập nhật nhân sự phòng ban',
  UPDATE_DEPARTMENT_PERSONNEL_IDENTITY: 'Cập nhật thông tin định danh nhân sự',
  CORRECT_PENDING_DEPARTMENT_PERSONNEL_EMAIL: 'Sửa email nhân sự chờ xác nhận',
  DISABLE_DEPARTMENT_PERSONNEL: 'Vô hiệu hóa nhân sự phòng ban',
  ENABLE_DEPARTMENT_PERSONNEL: 'Kích hoạt nhân sự phòng ban',
  RESEND_DEPARTMENT_PERSONNEL_CONFIRMATION: 'Gửi lại email xác nhận nhân sự',
  TRANSFER_DEPARTMENT_LEADERSHIP: 'Chuyển giao vai trò trưởng phòng ban',
  // Cơ sở
  CREATE_CAMPUS: 'Tạo cơ sở',
  UPDATE_CAMPUS: 'Cập nhật cơ sở',
  ENABLE_CAMPUS: 'Kích hoạt cơ sở',
  DISABLE_CAMPUS: 'Vô hiệu hóa cơ sở',
  // Đơn tham quan / chuyến thăm
  VISIT_REQUEST_CREATED_V2: 'Tạo đơn tham quan',
  UPDATE_PENDING_VISIT_REQUEST_V2: 'Cập nhật đơn tham quan chờ duyệt',
  RESUBMIT_REJECTED_VISIT_REQUEST_V2: 'Gửi lại đơn tham quan bị từ chối',
  VISIT_SAFE_FIELDS_UPDATED: 'Cập nhật thông tin đơn tham quan',
  CANCEL_VISIT_REQUEST: 'Hủy đơn tham quan',
  UPDATE_REGISTRANT_INFO: 'Cập nhật thông tin người đăng ký',
  REJECT_CAMPUS_INSTANCE: 'Từ chối đơn tại cơ sở',
  APPROVE_CAMPUS_INSTANCE: 'Duyệt đơn tại cơ sở',
  COMPLETE_VISIT_STAGE: 'Hoàn tất giai đoạn tham quan',
  TRANSFER_VISIT_HOST: 'Chuyển giao vai trò host',
  HOST_TRANSFERRED: 'Chuyển giao vai trò host',
  SAVE_VISIT_AGENDA: 'Lưu lịch trình tham quan',
  APPLY_AGENDA_TEMPLATE: 'Áp dụng mẫu lịch trình',
  CANCEL_VISIT_LOGISTICS_ITEM: 'Hủy hạng mục hậu cần',
  CREATE_VISIT_LOGISTICS_OFFLINE: 'Tạo hậu cần ngoại tuyến',
  CREATE_VISIT_LOGISTICS_REQUEST: 'Tạo yêu cầu hậu cần',
  SAVE_LOGISTICS_HANDOVER_DOCUMENT: 'Lưu chứng từ bàn giao hậu cần',
  LOGISTICS_HANDOVER_BORROW_BORROWER: 'Ký nhận mượn hậu cần',
  LOGISTICS_HANDOVER_RETURN_BORROWER: 'Ký nhận trả hậu cần',
  INVITE_VISIT_PARTICIPANT: 'Mời người tham gia',
  REMOVE_VISIT_PARTICIPANT: 'Xóa người tham gia',
  ACCEPT_VISIT_INVITATION: 'Chấp nhận lời mời tham gia',
  DECLINE_VISIT_INVITATION: 'Từ chối lời mời tham gia',
  CANCEL_VISIT_REMINDER_SETTINGS: 'Hủy cài đặt nhắc nhở',
  SAVE_VISIT_REMINDER_SETTINGS: 'Lưu cài đặt nhắc nhở',
  UPDATE_VISIT_PREPARATION_NOTE: 'Cập nhật ghi chú chuẩn bị',
  UPLOAD_VISIT_DOCUMENT: 'Tải lên tài liệu tham quan',
  UPLOAD_VISIT_PHOTOS: 'Tải lên ảnh tham quan',
  REMOVE_VISIT_PHOTO: 'Xóa ảnh tham quan',
  VISIT_PHOTO_FACE_SCAN_STARTED: 'Bắt đầu quét khuôn mặt',
  VISIT_PHOTO_FACE_SCAN_SUCCEEDED: 'Quét khuôn mặt thành công',
  VISIT_PHOTO_FACE_SCAN_FAILED: 'Quét khuôn mặt thất bại',
  VISIT_PHOTO_FACE_TAGS_CONFIRMED: 'Xác nhận gắn thẻ khuôn mặt',
  PRIMARY_CONTACT_CLAIM_APPLIED: 'Nhận làm đầu mối liên hệ chính',
  PRIMARY_CONTACT_INVITATION_DECLINED: 'Từ chối lời mời làm đầu mối liên hệ',
  PRIMARY_CONTACT_REPLACED: 'Thay thế đầu mối liên hệ chính',
  PRIMARY_CONTACT_INVITATION_RESENT: 'Gửi lại lời mời làm đầu mối liên hệ',
  PRIMARY_CONTACT_TRANSFER_APPLIED: 'Chuyển giao đầu mối liên hệ',
  PRIMARY_CONTACT_TRANSFER_DECLINED: 'Từ chối chuyển giao đầu mối liên hệ',
  PRIMARY_CONTACT_TRANSFER_REQUESTED: 'Yêu cầu chuyển giao đầu mối liên hệ',
  PRIMARY_CONTACT_TRANSFER_RESENT: 'Gửi lại yêu cầu chuyển giao đầu mối liên hệ',
  PRIMARY_CONTACT_TRANSFER_CANCELLED: 'Hủy chuyển giao đầu mối liên hệ',
  IDENTITY_CHANGE_REDACTED: 'Ẩn thông tin định danh đã thay đổi',
  VISIT_AMENDMENT_SUBMITTED: 'Gửi yêu cầu chỉnh sửa đơn',
  VISIT_AMENDMENT_APPROVED: 'Duyệt yêu cầu chỉnh sửa đơn',
  VISIT_INSTANCE_FORM_REVISION_APPLIED: 'Áp dụng bản chỉnh sửa form',
  PARTICIPANT_RESPONSE_ACCEPT: 'Đồng ý tham gia (qua email)',
  PARTICIPANT_RESPONSE_DECLINE: 'Từ chối tham gia (qua email)',
  LOGISTICS_REQUEST_ACCEPT: 'Đồng ý yêu cầu hậu cần (qua email)',
  LOGISTICS_REQUEST_REJECT: 'Từ chối yêu cầu hậu cần (qua email)',
  LOGISTICS_ASSIGNEE_ACCEPT: 'Đồng ý nhận phụ trách hậu cần (qua email)',
  LOGISTICS_ASSIGNEE_DECLINE: 'Từ chối nhận phụ trách hậu cần (qua email)',
  LOGISTICS_PROPOSAL_APPROVE: 'Duyệt đề xuất hậu cần (qua email)',
  LOGISTICS_PROPOSAL_REJECT: 'Từ chối đề xuất hậu cần (qua email)',
  // Đối tác
  CREATE_PARTNER: 'Tạo đối tác',
  UPDATE_PARTNER: 'Cập nhật đối tác',
  APPROVE_PARTNER: 'Duyệt đối tác',
  REJECT_PARTNER: 'Từ chối đối tác',
  CREATE_PARTNER_ALIAS: 'Tạo tên gọi khác của đối tác',
  DEACTIVATE_PARTNER_ALIAS: 'Vô hiệu hóa tên gọi khác của đối tác',
  CREATE_PARTNER_CONTACT: 'Tạo liên hệ đối tác',
  UPDATE_PARTNER_CONTACT: 'Cập nhật liên hệ đối tác',
  DEACTIVATE_PARTNER_CONTACT: 'Vô hiệu hóa liên hệ đối tác',
  SET_PRIMARY_PARTNER_CONTACT: 'Đặt liên hệ chính của đối tác',
  UPLOAD_PARTNER_DOCUMENT: 'Tải lên tài liệu đối tác',
  LINK_VISIT_GUEST_PARTNER: 'Liên kết khách với đối tác',
  REJECT_VISIT_GUEST_PARTNER_SUGGESTION: 'Từ chối đề xuất liên kết đối tác',
  // Thư viện ảnh
  CREATE_GALLERY_ITEM: 'Tạo mục thư viện ảnh',
  UPDATE_GALLERY_ITEM: 'Cập nhật mục thư viện ảnh',
  CHANGE_GALLERY_ITEM_STATUS: 'Đổi trạng thái mục thư viện ảnh',
  CREATE_GALLERY_LOCATION: 'Tạo địa điểm thư viện ảnh',
  UPDATE_GALLERY_LOCATION: 'Cập nhật địa điểm thư viện ảnh',
  UPDATE_GALLERY_AREA: 'Cập nhật khu vực thư viện ảnh',
  CHANGE_GALLERY_LOCATION_STATUS: 'Đổi trạng thái địa điểm thư viện ảnh',
  BACKFILL_GALLERY_TRANSLATIONS: 'Khôi phục bản dịch thư viện ảnh',
  RETRY_GALLERY_TRANSLATION: 'Thử lại dịch thư viện ảnh',
  // Mẫu lịch trình
  CREATE_AGENDA_TEMPLATE: 'Tạo mẫu lịch trình',
  UPDATE_AGENDA_TEMPLATE: 'Cập nhật mẫu lịch trình',
  DELETE_AGENDA_TEMPLATE: 'Xóa mẫu lịch trình',
  SET_AGENDA_TEMPLATE_DEFAULT: 'Đặt mẫu lịch trình mặc định',
  // Tích hợp API
  ENABLE_API_CONFIGURATION: 'Bật cấu hình API',
  DISABLE_API_CONFIGURATION: 'Tắt cấu hình API',
  CREATE_API_CONFIGURATION: 'Tạo cấu hình API',
  UPDATE_API_CONFIGURATION: 'Cập nhật cấu hình API',
  UPDATE_API_QUOTA: 'Cập nhật hạn mức API',
  // Khác
  RESTORE_EMAIL_TEMPLATE_DEFAULT: 'Khôi phục mẫu email mặc định',
};

const ENTITY_LABEL: Record<string, string> = {
  User: 'Người dùng',
  UserSession: 'Phiên đăng nhập',
  Campus: 'Cơ sở',
  Department: 'Phòng ban',
  AgendaTemplate: 'Mẫu lịch trình',
  ApiConfiguration: 'Cấu hình API',
  ApiUsageQuota: 'Hạn mức sử dụng API',
  Partner: 'Đối tác',
  PartnerAlias: 'Tên gọi khác đối tác',
  PartnerContact: 'Liên hệ đối tác',
  Document: 'Tài liệu',
  GalleryItem: 'Mục thư viện ảnh',
  GalleryLocation: 'Địa điểm thư viện ảnh',
  GalleryArea: 'Khu vực thư viện ảnh',
  GalleryTranslation: 'Bản dịch thư viện ảnh',
  VisitRequest: 'Đơn tham quan',
  VisitRequestCampus: 'Đơn tham quan tại cơ sở',
  VisitParticipant: 'Người tham gia tham quan',
  VisitLogisticsItem: 'Hạng mục hậu cần',
  VisitLogisticsItemHandover: 'Bàn giao hậu cần',
  VisitPhoto: 'Ảnh tham quan',
  VisitPhotoFaceScan: 'Quét khuôn mặt ảnh',
  VisitInstanceAmendment: 'Chỉnh sửa đơn tham quan',
  VisitRequestIdentityChange: 'Thay đổi định danh đơn tham quan',
  VisitGuestPartnerLink: 'Liên kết khách - đối tác',
};

const ROLE_LABEL: Record<string, string> = {
  ADMIN: 'Quản trị viên',
  HO: 'Head Office',
  STAFF: 'Nhân viên',
  DEPARTMENT: 'Phòng ban',
  STUDENT: 'Sinh viên',
  VISITOR: 'Khách',
};

/** Action/entity chưa kịp thêm vào bảng nhãn — chuyển SCREAMING_SNAKE_CASE thành câu dễ đọc hơn
 * (vẫn giữ nguyên từ tiếng Anh gốc) thay vì hiện thẳng mã hệ thống toàn chữ hoa. */
function humanizeCode(raw: string): string {
  const words = raw.replace(/([a-z])([A-Z])/g, '$1 $2').split(/[_\s]+/).filter(Boolean);
  return words
    .map((w, i) => (i === 0 ? w.charAt(0).toUpperCase() + w.slice(1).toLowerCase() : w.toLowerCase()))
    .join(' ');
}

/** Giá trị audit có thể là JSON — pretty-print để đọc diff trước/sau dễ hơn. */
function prettyValue(value?: string | null): string {
  if (value === null || value === undefined || value === '') return '—';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function AuditLogManagement() {
  const navigate = useNavigate();

  const [keyword, setKeyword] = useState('');
  const [action, setAction] = useState('');
  const [entityType, setEntityType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [data, setData] = useState<PaginatedResult<AdminAuditLogItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Detail modal
  const [detail, setDetail] = useState<AdminAuditLogDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailId, setDetailId] = useState<number | null>(null);

  const debouncedKeyword = useDebounce(keyword, 450);
  const debouncedAction = useDebounce(action, 450);
  const debouncedEntity = useDebounce(entityType, 450);

  const query = useMemo(() => ({
    page,
    pageSize,
    keyword: debouncedKeyword.trim() || undefined,
    action: debouncedAction.trim() || undefined,
    entityType: debouncedEntity.trim() || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  }), [page, pageSize, debouncedKeyword, debouncedAction, debouncedEntity, fromDate, toDate]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    adminApi.getAuditLogs(query)
      .then(setData)
      .catch((e: any) => setError(getApiErrorMessage(e, 'Không tải được nhật ký kiểm toán.')))
      .finally(() => setLoading(false));
  }, [query]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [debouncedKeyword, debouncedAction, debouncedEntity, fromDate, toDate, pageSize]);

  const openDetail = (auditLogId: number) => {
    setDetailId(auditLogId);
    setDetail(null);
    setDetailError(null);
    setDetailLoading(true);
    adminApi.getAuditLogDetail(auditLogId)
      .then(setDetail)
      .catch((e: any) => setDetailError(getApiErrorMessage(e, 'Không tải được chi tiết bản ghi.')))
      .finally(() => setDetailLoading(false));
  };

  const closeDetail = () => { setDetailId(null); setDetail(null); setDetailError(null); };

  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91] font-medium">Nhật ký kiểm toán</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] flex items-center gap-3">
            <ScrollText className="w-8 h-8" /> Nhật ký kiểm toán
          </h1>
        </div>
        <button
          onClick={load}
          className="px-4 py-2.5 rounded-xl text-sm font-bold text-[#004c91] border border-[#004c91]/30 hover:bg-blue-50 transition-colors flex items-center gap-2 cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Làm mới
        </button>
      </div>

      <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-[#004c91] overflow-hidden">
        {/* Filter bar */}
        <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-3 border-b border-[#00386b]">
          <div className="relative flex-1 min-w-[220px]">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="Tìm theo người thực hiện (email / họ tên)..."
              className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
            />
          </div>
          <input
            type="text"
            value={action}
            onChange={(e) => setAction(e.target.value)}
            placeholder="Mã hành động (vd: UPDATE_ACCOUNT_ROLE)"
            title="Lọc theo đúng mã hành động lưu trong hệ thống"
            className="w-64 px-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
          <input
            type="text"
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
            placeholder="Mã đối tượng (vd: User)"
            title="Lọc theo đúng mã đối tượng lưu trong hệ thống"
            className="w-40 px-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} className={dateCls} title="Từ ngày" />
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} className={dateCls} title="Đến ngày" />
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse table-fixed">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-3 pl-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">STT</th>
                <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">Thời gian</th>
                <th className="p-3 w-[18%] text-[11px] font-black uppercase tracking-widest">Người thực hiện</th>
                <th className="p-3 w-[19%] text-[11px] font-black uppercase tracking-widest">Hành động</th>
                <th className="p-3 w-[14%] text-[11px] font-black uppercase tracking-widest">Đối tượng</th>
                <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">Cơ sở</th>
                <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">IP</th>
                <th className="p-3 pr-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading ? (
                <tr><td colSpan={8} className="py-16 text-center text-gray-400 text-sm font-normal">
                  <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải nhật ký...
                </td></tr>
              ) : error ? (
                <tr><td colSpan={8} className="py-16 text-center">
                  <p className="text-sm font-normal text-red-500 mb-2">{error}</p>
                  <button onClick={load} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
                </td></tr>
              ) : (data?.items.length ?? 0) === 0 ? (
                <tr><td colSpan={8} className="py-16 text-center text-gray-400 text-sm font-normal">
                  Không có bản ghi nào phù hợp bộ lọc
                </td></tr>
              ) : data!.items.map((log, idx) => (
                <tr key={log.auditLogId} className="hover:bg-blue-50/30 transition-colors">
                  <td className="p-3 pl-4 text-center text-xs font-bold text-gray-400">{(page - 1) * pageSize + idx + 1}</td>
                  <td className="p-3 text-xs text-gray-500 break-words">{formatVietnamDateTime(log.createdAt)}</td>
                  <td className="p-3 break-words">
                    <p className="text-[13px] font-bold text-[#004c91]">{log.actorName || 'Hệ thống'}</p>
                    <p className="text-xs text-gray-500 break-all">{log.actorEmail || '—'}</p>
                  </td>
                  <td className="p-3">
                    <span
                      className="inline-block px-2.5 py-1 rounded-lg text-[11px] font-bold bg-blue-50 text-[#004c91] border border-blue-100 break-words"
                      title={log.action}
                    >
                      {ACTION_LABEL[log.action] ?? humanizeCode(log.action)}
                    </span>
                  </td>
                  <td className="p-3 text-[13px] text-gray-600 break-words" title={log.entityType}>
                    {ENTITY_LABEL[log.entityType] ?? log.entityType}{log.entityId ? ` #${log.entityId}` : ''}
                  </td>
                  <td className="p-3 text-[13px] text-gray-600 break-words">{log.campusName || '—'}</td>
                  <td className="p-3 text-xs text-gray-500 break-words">{log.ipAddress || '—'}</td>
                  <td className="p-3 pr-4 text-center">
                    <button
                      onClick={() => openDetail(log.auditLogId)}
                      className="p-2 text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50 rounded-full transition-all cursor-pointer"
                      title={`Xem chi tiết (${log.changeCount} thay đổi)`}
                    >
                      <Eye className="w-4.5 h-4.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {(data?.totalItems ?? 0) > 0 && !loading && !error && (
          <div className="p-5 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3 bg-gray-50/50">
            <div className="flex items-center gap-2 text-sm font-normal text-gray-500">
              <span>Hiển thị</span>
              <div className="relative">
                <select
                  value={pageSize}
                  onChange={(e) => setPageSize(Number(e.target.value))}
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-normal text-gray-700 bg-white focus:outline-none appearance-none"
                >
                  {[10, 20, 50].map((n) => <option key={n} value={n}>{n}</option>)}
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span>/ trang · Tổng {data!.totalItems} bản ghi</span>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] disabled:opacity-50 cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-normal text-gray-700">{page} / {Math.max(totalPages, 1)}</span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] disabled:opacity-50 cursor-pointer"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Detail modal */}
      {detailId !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-3xl max-h-[85dvh] overflow-y-auto p-6 animate-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-black text-gray-800">Chi tiết bản ghi kiểm toán #{detailId}</h3>
              <button onClick={closeDetail} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 cursor-pointer">
                <X className="w-5 h-5" />
              </button>
            </div>

            {detailLoading ? (
              <div className="py-12 text-center text-gray-400 text-sm font-normal">
                <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải chi tiết...
              </div>
            ) : detailError ? (
              <div className="py-12 text-center">
                <p className="text-sm font-normal text-red-500 mb-2">{detailError}</p>
                <button onClick={() => openDetail(detailId)} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
              </div>
            ) : detail && (
              <>
                <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 text-sm mb-6">
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Người thực hiện</dt>
                    <dd className="font-normal text-gray-800">
                      {detail.actorName || 'Hệ thống'}
                      {detail.actorRoleCode ? <span className="ml-1.5 text-[10px] font-black text-[#004c91] bg-blue-50 px-1.5 py-0.5 rounded uppercase">{ROLE_LABEL[detail.actorRoleCode] ?? detail.actorRoleCode}</span> : null}
                    </dd>
                    <dd className="text-xs text-gray-500">{detail.actorEmail || '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Hành động</dt>
                    <dd className="font-normal text-gray-800" title={detail.action}>{ACTION_LABEL[detail.action] ?? humanizeCode(detail.action)}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Đối tượng</dt>
                    <dd className="text-gray-700" title={detail.entityType}>{ENTITY_LABEL[detail.entityType] ?? detail.entityType}{detail.entityId ? ` #${detail.entityId}` : ''}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Cơ sở</dt>
                    <dd className="text-gray-700">{detail.campusName || '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Thời gian</dt>
                    <dd className="text-gray-700">{formatVietnamDateTime(detail.createdAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">IP / Mã yêu cầu</dt>
                    <dd className="text-gray-700">{detail.ipAddress || '—'}{detail.requestId ? ` · ${detail.requestId}` : ''}</dd>
                  </div>
                  {detail.userAgent && (
                    <div className="sm:col-span-2">
                      <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Thiết bị</dt>
                      <dd className="text-xs text-gray-500 break-all">{detail.userAgent}</dd>
                    </div>
                  )}
                </dl>

                <h4 className="text-sm font-black text-gray-700 uppercase tracking-wider mb-3">
                  Thay đổi trước / sau ({detail.changes.length})
                </h4>
                {detail.changes.length === 0 ? (
                  <p className="text-sm text-gray-400 font-normal py-4 text-center">Bản ghi này không kèm thay đổi dữ liệu.</p>
                ) : (
                  <div className="space-y-4">
                    {detail.changes.map((change) => (
                      <div key={change.auditLogChangeId} className="border border-gray-100 rounded-xl overflow-hidden">
                        <div className="px-4 py-2 bg-slate-50 flex items-center justify-between">
                          <span className="text-xs font-black text-gray-600 uppercase tracking-wider">{change.fieldName}</span>
                          {change.isMasked && (
                            <span className="inline-flex items-center gap-1 text-[10px] font-black text-red-500 bg-red-50 border border-red-100 px-2 py-0.5 rounded-md uppercase">
                              <ShieldAlert className="w-3 h-3" /> Đã che dữ liệu nhạy cảm
                            </span>
                          )}
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-gray-100">
                          <div className="p-3">
                            <p className="text-[10px] font-bold text-red-400 uppercase tracking-wider mb-1">Trước</p>
                            <pre className="text-xs text-gray-600 whitespace-pre-wrap break-all font-mono bg-red-50/40 rounded-lg p-2 max-h-48 overflow-y-auto">{prettyValue(change.oldValue)}</pre>
                          </div>
                          <div className="p-3">
                            <p className="text-[10px] font-bold text-emerald-500 uppercase tracking-wider mb-1">Sau</p>
                            <pre className="text-xs text-gray-600 whitespace-pre-wrap break-all font-mono bg-emerald-50/40 rounded-lg p-2 max-h-48 overflow-y-auto">{prettyValue(change.newValue)}</pre>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
