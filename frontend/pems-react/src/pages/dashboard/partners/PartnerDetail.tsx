/**
 * PartnerDetail — hồ sơ đối tác chạy DB thật (docs/PARTNER_canh/01 §10.2).
 * UI khôi phục theo layout gốc (cover/logo banner, card "Thông tin cơ bản",
 * "Lịch sử hợp tác" + "Văn bản & Tài liệu", "Danh sách người liên hệ", "Tên gọi khác")
 * nhưng toàn bộ dữ liệu/handler đều là API thật: get detail, approve/reject, contacts
 * (CRUD + set-primary), aliases, documents, OCR "Quét danh thiếp" (module 02).
 */
import React, { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft, Info, History, FileText, Plus, Trash2, MapPin, Globe, CheckCircle,
  Edit3, Check, Eye, X, Loader2, AlertTriangle, Star, ScanLine, Download, Users, Tag,
} from 'lucide-react';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type {
  PartnerAlias,
  PartnerContact,
  PartnerDetail as PartnerDetailType,
  PartnerDocument,
  PartnerProfileStatus,
  PartnerVisitHistoryItem,
} from '../../../features/partners/types/partners.types';
import {
  PARTNER_TYPE_LABELS,
  PROFILE_STATUS_LABELS,
  VISIBILITY_LABELS,
} from '../../../features/partners/types/partners.types';
import { BusinessCardScanModal } from '../../../features/business-card-ocr/components/BusinessCardScanModal';
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import { useAuthenticatedImage } from '../../../shared/hooks/useAuthenticatedImage';
import { downloadAuthenticatedFile } from '../../../shared/utils/fileDownload';
import { formatVietnamDate, formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { FilePreviewModal } from '../../../shared/components/files/FilePreviewModal';
import {
  showLoadingToast,
  updateToastSuccess,
  updateToastError,
  dismissToast,
} from '../../../shared/utils/toast';
import { fieldErrorsOf, firstFieldError } from '../../../features/visit-request/utils/visitV2Actions';
import { focusFirstInvalidField } from '../../../features/visit-request/utils/formErrorNavigation';

// Cover placeholder restored from the original PartnerDetail UI — shown until the partner's own
// coverFileId resolves (or when it has none, or the fetch/render fails).
import coverImage from '../../../assets/images/banner_partner.png';

const inputCls =
  'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';

/** Người liên hệ: 5 field mirror đúng rule backend (`CreatePartnerContactCommandValidator` /
 * `UpdatePartnerContactCommandValidator`) — chỉ Họ tên bắt buộc, Email đúng định dạng nếu có nhập. */
type ContactFieldKey = 'fullName' | 'email' | 'phone' | 'jobTitle' | 'departmentName';
type ContactFieldErrors = Partial<Record<ContactFieldKey, string>>;
/** Tên property C# đúng như FluentValidation trả về trong `errors` dict — case-insensitive lookup. */
const CONTACT_FIELD_BACKEND_MAP: Record<ContactFieldKey, string> = {
  fullName: 'FullName', email: 'Email', phone: 'Phone', jobTitle: 'JobTitle', departmentName: 'DepartmentName',
};
const CONTACT_EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const contactInputCls = (hasError?: boolean) =>
  `w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-1 bg-white ${
    hasError
      ? 'border-red-400 focus:border-red-500 focus:ring-red-300'
      : 'border-gray-300 focus:border-[#004c91] focus:ring-[#004c91]'
  }`;

const VISIT_STATUS_CONFIG: Record<string, { label: string; bg: string; text: string; border: string }> = {
  DRAFT: { label: 'Bản nháp', bg: 'bg-gray-50', text: 'text-gray-600', border: 'border-gray-200' },
  SUBMITTED: { label: 'Chờ duyệt', bg: 'bg-amber-50', text: 'text-amber-700', border: 'border-amber-200' },
  ASSIGNED: { label: 'Đã phân công', bg: 'bg-blue-50', text: 'text-blue-700', border: 'border-blue-200' },
  BEFORE_VISIT: { label: 'Chuẩn bị tiếp đón', bg: 'bg-indigo-50', text: 'text-indigo-700', border: 'border-indigo-200' },
  DURING_VISIT: { label: 'Đang tiếp đón', bg: 'bg-orange-50', text: 'text-[#f37021]', border: 'border-orange-200' },
  AFTER_VISIT: { label: 'Hoàn tất tiếp đón', bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200' },
  COMPLETED: { label: 'Hoàn thành', bg: 'bg-emerald-50', text: 'text-emerald-700', border: 'border-emerald-200' },
  CLOSED: { label: 'Đã đóng', bg: 'bg-slate-100', text: 'text-slate-600', border: 'border-slate-200' },
  CANCELLED: { label: 'Đã hủy', bg: 'bg-rose-50', text: 'text-rose-700', border: 'border-rose-200' },
};

/** Logo fallback when a partner has no cover/logo file (or it failed to load): initials badge. */
function getInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  return words.length === 1 ? words[0].slice(0, 2).toUpperCase() : (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

function ContactAvatarImage({ fileId, url, alt, className = 'w-full h-full object-cover' }: { fileId?: number | null; url?: string | null; alt: string; className?: string }) {
  const targetUrl = url || (fileId ? API_ENDPOINTS.files.content(fileId) : null);
  const objectUrl = useAuthenticatedImage(targetUrl);
  if (!objectUrl) return <span>{alt ? alt.charAt(0).toUpperCase() : '?'}</span>;
  return <img src={objectUrl} alt={alt} className={className} />;
}

/** Label/value slot matching the original "Thông tin cơ bản" card styling. */
function Field({
  label, value, icon, className = '',
}: { label: string; value: React.ReactNode; icon?: React.ReactNode; className?: string }) {
  return (
    <div className={className}>
      <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-1.5">{label}</span>
      <div className="text-[15px] font-medium text-gray-900 flex items-center gap-2">
        {icon}
        <span className="break-words">{value}</span>
      </div>
    </div>
  );
}

function ProfileStatusBadge({ status }: { status: PartnerProfileStatus }) {
  const styles: Record<PartnerProfileStatus, string> = {
    APPROVED: 'text-[#0aa14f] bg-[#eaffe4] border border-[#ceefda]',
    PENDING_APPROVAL: 'text-yellow-600 bg-yellow-50 border border-yellow-200',
    REJECTED: 'text-red-600 bg-red-50 border border-red-200',
    DRAFT: 'text-gray-500 bg-gray-100 border border-gray-200',
  };
  return (
    <span className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl font-bold text-sm ${styles[status]}`}>
      {status === 'APPROVED' && <CheckCircle className="w-4 h-4" />}
      {PROFILE_STATUS_LABELS[status]}
    </span>
  );
}

export function PartnerDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  const { hash } = useLocation();

  const [partner, setPartner] = useState<PartnerDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [displayLang, setDisplayLang] = useState<'vi' | 'en'>('vi');


  // Approval panel
  const [busy, setBusy] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [makePublic, setMakePublic] = useState(false);

  // Contacts
  const [contacts, setContacts] = useState<PartnerContact[]>([]);
  const [contactsLoading, setContactsLoading] = useState(false);
  const [contactFormOpen, setContactFormOpen] = useState(false);
  const [editingContact, setEditingContact] = useState<PartnerContact | null>(null);
  // Read-only "Xem chi tiết" modal — restores the original contact-detail popup (UI only).
  const [viewContact, setViewContact] = useState<PartnerContact | null>(null);
  const [cName, setCName] = useState('');
  const [cEmail, setCEmail] = useState('');
  const [cPhone, setCPhone] = useState('');
  const [cTitle, setCTitle] = useState('');
  const [cDepartment, setCDepartment] = useState('');
  const [cNote, setCNote] = useState('');
  const [cPrimary, setCPrimary] = useState(false);
  const [contactFieldErrors, setContactFieldErrors] = useState<ContactFieldErrors>({});

  // Contacts Pagination & Search
  const [contactSearch, setContactSearch] = useState('');
  const [contactPage, setContactPage] = useState(1);
  const contactsPerPage = 10;

  // Aliases
  const [aliases, setAliases] = useState<PartnerAlias[]>([]);
  const [newAlias, setNewAlias] = useState('');

  // Documents
  const [documents, setDocuments] = useState<PartnerDocument[]>([]);
  const [docTitle, setDocTitle] = useState('');
  const [docFile, setDocFile] = useState<File | null>(null);
  const [docBusy, setDocBusy] = useState(false);
  const [previewDoc, setPreviewDoc] = useState<PartnerDocument | null>(null);
  // Chặn double-click khi tải file (thao tác có độ trễ) — id tài liệu / 'card' đang tải, null = rảnh.
  const [downloadingKey, setDownloadingKey] = useState<number | 'card' | null>(null);
  const handleDownloadDoc = async (key: number | 'card', path: string, fallbackName: string) => {
    if (downloadingKey) return;
    setDownloadingKey(key);
    try {
      await downloadAuthenticatedFile(path, fallbackName);
    } finally {
      setDownloadingKey(null);
    }
  };

  // OCR modal
  const [scanOpen, setScanOpen] = useState(false);

  // Cover/logo live behind the authenticated /api/files/{id}/content route; fall back to the
  // original static banner asset / initials badge when the partner has no image, or the fetch/
  // render fails (onError below), so the UI never shows a broken image.
  const [coverImgError, setCoverImgError] = useState(false);
  const [logoImgError, setLogoImgError] = useState(false);
  const fetchedCover = useAuthenticatedImage(
    partner?.coverFileId ? `/api/files/${partner.coverFileId}/content` : null,
  );
  const fetchedLogo = useAuthenticatedImage(
    partner?.logoFileId ? `/api/files/${partner.logoFileId}/content` : null,
  );
  useEffect(() => { setCoverImgError(false); }, [fetchedCover]);
  useEffect(() => { setLogoImgError(false); }, [fetchedLogo]);
  const showCoverFallback = !fetchedCover || coverImgError;
  const showLogoFallback = !fetchedLogo || logoImgError;

  // Scanned business-card image for the contact "Xem chi tiết" modal, when the contact came from OCR.
  const scannedCardUrl = useAuthenticatedImage(
    viewContact?.scannedCardFileId ? `/api/files/${viewContact.scannedCardFileId}/content` : null,
  );

  const canManage = partner?.allowedActions.includes('MANAGE_CHILDREN') ?? false;
  const canDecide = (partner?.allowedActions.includes('APPROVE') ?? false)
    && partner?.profileStatus === 'PENDING_APPROVAL';

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      setPartner(await partnersApi.getPartnerDetail(id));
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không tải được hồ sơ đối tác.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  const loadContacts = useCallback(async () => {
    if (!id) return;
    setContactsLoading(true);
    try { setContacts(await partnersApi.getContacts(id)); }
    catch { setContacts([]); }
    finally { setContactsLoading(false); }
  }, [id]);

  const [visitHistory, setVisitHistory] = useState<PartnerVisitHistoryItem[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);

  const loadVisitHistory = useCallback(async () => {
    if (!id) return;
    setLoadingHistory(true);
    try { setVisitHistory(await partnersApi.getVisitHistory(id)); }
    catch { setVisitHistory([]); }
    finally { setLoadingHistory(false); }
  }, [id]);

  const loadAliases = useCallback(async () => {
    if (!id) return;
    try { setAliases(await partnersApi.getAliases(id)); } catch { setAliases([]); }
  }, [id]);

  const loadDocuments = useCallback(async () => {
    if (!id) return;
    try { setDocuments(await partnersApi.getDocuments(id)); } catch { setDocuments([]); }
  }, [id]);

  useEffect(() => { void load(); }, [load]);
  // Original layout shows contacts/documents/aliases as stacked cards (no tabs), so all three
  // load together once the partner id is known instead of lazily per-tab.
  useEffect(() => {
    void loadContacts();
    void loadAliases();
    void loadDocuments();
    void loadVisitHistory();
  }, [loadContacts, loadAliases, loadDocuments, loadVisitHistory]);

  // A `#contacts` deep link (from the minutes screen, where the partner is already settled and only
  // the contact is missing) has to actually land on the contacts card. React Router does not scroll
  // to a hash on its own, and the card only exists once the partner has loaded — hence the dependency
  // on `partner` rather than on the hash alone.
  useEffect(() => {
    if (!hash || !partner) return;
    const target = document.getElementById(hash.slice(1));
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [hash, partner]);

  const approve = async () => {
    if (!id) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang duyệt hồ sơ đối tác...', 'partner-detail-approve');
    try {
      await partnersApi.approvePartner(id, undefined, makePublic);
      await load();
      updateToastSuccess(toastId, 'Đã duyệt hồ sơ đối tác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể duyệt hồ sơ đối tác.');
    } finally { setBusy(false); }
  };

  const reject = async () => {
    if (!id || !rejectReason.trim()) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang từ chối hồ sơ đối tác...', 'partner-detail-reject');
    try {
      await partnersApi.rejectPartner(id, rejectReason.trim());
      setRejectOpen(false);
      setRejectReason('');
      await load();
      updateToastSuccess(toastId, 'Đã từ chối hồ sơ đối tác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể từ chối hồ sơ đối tác.');
    } finally { setBusy(false); }
  };

  const openContactForm = (contact?: PartnerContact) => {
    setEditingContact(contact ?? null);
    setCName(contact?.fullName ?? '');
    setCEmail(contact?.email ?? '');
    setCPhone(contact?.phone ?? '');
    setCTitle(contact?.jobTitle ?? '');
    setCDepartment(contact?.departmentName ?? '');
    setCNote(contact?.note ?? '');
    setCPrimary(contact?.isPrimary ?? false);
    setContactFieldErrors({});
    setContactFormOpen(true);
  };

  const closeContactForm = () => {
    setContactFormOpen(false);
    setContactFieldErrors({});
  };

  /** Mirror phía client của rule backend — chỉ để UX, backend vẫn validate lại toàn bộ. */
  const validateContactForm = (): ContactFieldErrors => {
    const errors: ContactFieldErrors = {};
    if (!cName.trim()) errors.fullName = 'Họ tên người liên hệ là bắt buộc.';
    const email = cEmail.trim();
    if (email && !CONTACT_EMAIL_RE.test(email)) errors.email = 'Email không hợp lệ.';
    return errors;
  };

  /** Xoá lỗi của MỘT field ngay khi nó hợp lệ trở lại — không đợi submit lại. */
  const clearContactFieldError = (key: ContactFieldKey, value: string) => {
    if (!contactFieldErrors[key]) return;
    if (key === 'fullName' && !value.trim()) return;
    if (key === 'email' && value.trim() && !CONTACT_EMAIL_RE.test(value.trim())) return;
    setContactFieldErrors((prev) => ({ ...prev, [key]: undefined }));
  };

  const saveContact = async () => {
    if (!id) return;
    const clientErrors = validateContactForm();
    if (Object.keys(clientErrors).length > 0) {
      setContactFieldErrors(clientErrors);
      window.setTimeout(() => focusFirstInvalidField(), 60);
      return;
    }
    setContactFieldErrors({});
    setBusy(true);
    const isEdit = !!editingContact;
    const toastId = showLoadingToast('Đang lưu người liên hệ...', 'partner-contact-save');
    try {
      if (editingContact) {
        await partnersApi.updateContact(id, editingContact.contactId, {
          fullName: cName.trim(),
          email: cEmail.trim() || null,
          phone: cPhone.trim() || null,
          jobTitle: cTitle.trim() || null,
          departmentName: cDepartment.trim() || null,
          note: cNote.trim() || null,
        });
      } else {
        await partnersApi.createContact(id, {
          fullName: cName.trim(),
          email: cEmail.trim() || null,
          phone: cPhone.trim() || null,
          jobTitle: cTitle.trim() || null,
          departmentName: cDepartment.trim() || null,
          note: cNote.trim() || null,
          isPrimary: cPrimary,
        });
      }
      setContactFormOpen(false);
      setContactFieldErrors({});
      await loadContacts();
      updateToastSuccess(toastId, isEdit ? 'Đã cập nhật người liên hệ.' : 'Đã thêm người liên hệ.');
    } catch (e: any) {
      // Lỗi field ổn định (FluentValidation) đi kèm form — chỉ dùng toast cho lỗi chung/mạng/conflict.
      const backendFields = fieldErrorsOf(e);
      const mapped: ContactFieldErrors = {};
      if (backendFields) {
        (Object.keys(CONTACT_FIELD_BACKEND_MAP) as ContactFieldKey[]).forEach((key) => {
          const msg = firstFieldError(backendFields, CONTACT_FIELD_BACKEND_MAP[key]);
          if (msg) mapped[key] = msg;
        });
      }
      if (Object.keys(mapped).length > 0) {
        setContactFieldErrors(mapped);
        dismissToast(toastId);
        window.setTimeout(() => focusFirstInvalidField(), 60);
      } else {
        updateToastError(toastId, e, 'Không thể lưu người liên hệ.');
      }
    } finally { setBusy(false); }
  };

  const deactivateContact = async (contact: PartnerContact) => {
    if (!id) return;
    if (!window.confirm(`Vô hiệu hoá người liên hệ "${contact.fullName}"?`)) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang vô hiệu hoá người liên hệ...', 'partner-contact-deactivate');
    try {
      await partnersApi.deactivateContact(id, contact.contactId);
      await loadContacts();
      updateToastSuccess(toastId, 'Đã vô hiệu hoá người liên hệ.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể vô hiệu hoá người liên hệ.');
    } finally { setBusy(false); }
  };

  const setPrimary = async (contact: PartnerContact) => {
    if (!id) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang đặt người liên hệ chính...', 'partner-contact-primary');
    try {
      await partnersApi.setPrimaryContact(id, contact.contactId);
      await loadContacts();
      updateToastSuccess(toastId, 'Đã đặt người liên hệ chính.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể đặt người liên hệ chính.');
    } finally { setBusy(false); }
  };

  const addAlias = async () => {
    if (!id || !newAlias.trim()) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang thêm tên gọi khác...', 'partner-alias-add');
    try {
      await partnersApi.createAlias(id, newAlias.trim());
      setNewAlias('');
      await loadAliases();
      updateToastSuccess(toastId, 'Đã thêm tên gọi khác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể thêm tên gọi khác.');
    } finally { setBusy(false); }
  };

  const removeAlias = async (alias: PartnerAlias) => {
    if (!id) return;
    setBusy(true);
    const toastId = showLoadingToast('Đang xoá tên gọi khác...', 'partner-alias-remove');
    try {
      await partnersApi.deactivateAlias(id, alias.partnerAliasId);
      await loadAliases();
      updateToastSuccess(toastId, 'Đã xoá tên gọi khác.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể xoá tên gọi khác.');
    } finally { setBusy(false); }
  };

  const uploadDocument = async () => {
    if (!id || !docFile || !docTitle.trim()) return;
    setDocBusy(true);
    const toastId = showLoadingToast('Đang tải tài liệu lên...', 'partner-doc-upload');
    try {
      // Upload the binary first (shared files endpoint), then register the document row.
      const form = new FormData();
      form.append('file', docFile);
      form.append('purpose', 'PARTNER_DOCUMENT');
      const { data: uploaded } = await httpClient.post<{ fileId: number }>(
        '/files/upload', form, { headers: { 'Content-Type': 'multipart/form-data' } },
      );
      await partnersApi.addDocument(id, { fileId: uploaded.fileId, title: docTitle.trim() });
      setDocTitle('');
      setDocFile(null);
      await loadDocuments();
      updateToastSuccess(toastId, 'Đã tải tài liệu lên.');
    } catch (e: any) {
      updateToastError(toastId, e, 'Không thể tải tài liệu lên.');
    } finally { setDocBusy(false); }
  };

  if (loading) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-7xl mx-auto w-full py-24 text-center text-gray-400">
        <Loader2 className="w-8 h-8 animate-spin inline-block mr-2" />
        Đang tải hồ sơ đối tác...
      </div>
    );
  }

  if (error || !partner) {
    return (
      <div className="p-4 sm:p-6 md:p-8 max-w-7xl mx-auto w-full py-24 text-center">
        <p className="text-red-500 font-medium">{error || 'Không tìm thấy đối tác.'}</p>
        <button
          onClick={() => navigate('/dashboard/partners')}
          className="mt-4 px-5 py-2 bg-[#004c91] text-white rounded-lg text-sm font-bold cursor-pointer"
        >
          Quay lại danh sách
        </button>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-7xl mx-auto w-full">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6 font-medium flex-wrap">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors cursor-pointer">Dashboard</button>
        <span className="text-gray-400">/</span>
        <button onClick={() => navigate('/dashboard/partners')} className="hover:text-[#004c91] transition-colors cursor-pointer">Quản lý đối tác</button>
        <span className="text-gray-400">/</span>
        <span className="text-[#004c91] truncate max-w-[220px]">{partner.name}</span>
      </div>

      {/* Back button + actions */}
      <div className="mb-6 flex items-center justify-between flex-wrap gap-3">
        <button
          onClick={() => navigate('/dashboard/partners')}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl border border-gray-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] transition-all duration-300 font-bold text-gray-700 outline-none group cursor-pointer"
        >
          <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
          <span>Quay lại</span>
        </button>

        <div className="flex flex-wrap items-center gap-3 ml-auto">
          {/* Language Switcher Toggle */}
          <div className="flex items-center bg-gray-100 p-1 rounded-xl border border-gray-200 shadow-sm">
            <button
              type="button"
              onClick={() => setDisplayLang('vi')}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${displayLang === 'vi' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:text-gray-900'
                }`}
            >
              Tiếng Việt
            </button>
            <button
              type="button"
              onClick={() => setDisplayLang('en')}
              className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${displayLang === 'en' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:text-gray-900'
                }`}
            >
              English
            </button>
          </div>

          {canManage && (
            <button
              onClick={() => setScanOpen(true)}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl border border-[#004c91] bg-white text-[#004c91] hover:bg-[#f0f6ff] shadow-sm transition-all duration-200 font-bold outline-none cursor-pointer"
            >
              <ScanLine className="w-5 h-5" /> Quét danh thiếp
            </button>
          )}
          {partner.allowedActions.includes('EDIT') && (
            <button
              onClick={() => navigate(`/dashboard/partners/${partner.partnerId}/edit`)}
              className="flex items-center gap-2 px-5 py-2.5 rounded-xl border border-[#004c91] bg-white text-[#004c91] hover:bg-[#f0f6ff] shadow-sm transition-all duration-200 font-bold outline-none cursor-pointer"
            >
              <Edit3 className="w-5 h-5" /> Chỉnh sửa
            </button>
          )}
          {canDecide && (
            <>
              <label className="flex items-center gap-1.5 text-xs text-gray-500 font-medium cursor-pointer select-none">
                <input type="checkbox" checked={makePublic} onChange={(e) => setMakePublic(e.target.checked)} className="rounded border-gray-300" />
                Công khai sau duyệt
              </label>
              <button
                onClick={() => void approve()}
                disabled={busy}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#0aa14f] hover:bg-[#088a42] text-white shadow-sm transition-all duration-200 font-bold outline-none disabled:opacity-50 cursor-pointer"
              >
                <Check className="w-5 h-5" /> Duyệt
              </button>
              <button
                onClick={() => setRejectOpen(true)}
                disabled={busy}
                className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-red-500 hover:bg-red-600 text-white shadow-sm transition-all duration-200 font-bold outline-none disabled:opacity-50 cursor-pointer"
              >
                <X className="w-5 h-5" /> Từ chối
              </button>
            </>
          )}
        </div>
      </div>

      {partner.profileStatus === 'REJECTED' && partner.reviewNote && (
        <div className="mb-5 flex items-start gap-2 bg-red-50 border border-red-100 text-red-600 text-sm rounded-xl px-4 py-3">
          <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <span><b>Hồ sơ bị từ chối.</b> Lý do: {partner.reviewNote}</span>
        </div>
      )}
      {/* Cover & Logo Section */}
      <div className="relative mb-10 w-full h-[220px] sm:h-[300px] md:h-[380px] lg:h-[440px] rounded-[24px] bg-gray-100 shadow-sm overflow-hidden">
        <img
          src={showCoverFallback ? coverImage : fetchedCover!}
          alt="Cover"
          className="w-full h-full object-cover"
          onError={() => setCoverImgError(true)}
        />
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />

        <div className="absolute top-4 right-4 sm:top-5 sm:right-5">
          <ProfileStatusBadge status={partner.profileStatus} />
        </div>

        {/* Logo overlay */}
        <div className="absolute -bottom-2 left-0 right-0 p-4 sm:p-6 flex items-end">
          <div className="w-20 h-20 sm:w-28 sm:h-28 rounded-[20px] bg-white shadow-xl p-2 border-4 border-white overflow-hidden flex items-center justify-center flex-shrink-0">
            {showLogoFallback ? (
              <div className="w-full h-full rounded-[14px] bg-gradient-to-br from-[#004c91] to-[#003a70] flex items-center justify-center text-white font-black text-2xl select-none">
                {getInitials(partner.name)}
              </div>
            ) : (
              <img src={fetchedLogo!} alt="Logo" className="w-full h-full object-contain" onError={() => setLogoImgError(true)} />
            )}
          </div>
          <div className="ml-4 sm:ml-6 mb-2 sm:mb-4 text-white z-10 min-w-0">
            <h1 className="text-xl sm:text-2xl md:text-3xl font-bold tracking-tight drop-shadow-md truncate">
              {displayLang === 'en' ? (partner.englishName || partner.name) : partner.name}
            </h1>
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 opacity-90 font-medium text-xs sm:text-sm">
              {partner.partnerCode && <span>Mã: {partner.partnerCode}</span>}
              {partner.country && <span className="flex items-center gap-1"><MapPin className="w-3.5 h-3.5" />{partner.country}</span>}
              {partner.websiteUrl && <span className="flex items-center gap-1 truncate max-w-[220px]"><Globe className="w-3.5 h-3.5 flex-shrink-0" />{partner.websiteUrl}</span>}
            </div>
          </div>
        </div>
      </div>

      {/* Thông tin cơ bản */}
      <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden mb-8">
        <div className="bg-[#004c91] px-6 py-4 flex items-center gap-2.5">
          <Info className="w-6 h-6 text-white" />
          <h2 className="text-lg font-bold text-white uppercase tracking-wider">
            Thông tin cơ bản {displayLang === 'en' ? '(English)' : '(Tiếng Việt)'}
          </h2>
        </div>
        <div className="p-4 sm:p-6 md:p-8">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-x-8 gap-y-6">
            <Field label="Mã đối tác" value={partner.partnerCode || '—'} />
            <Field
              label="Tên viết tắt"
              value={(displayLang === 'en' ? (partner.englishShortName || partner.shortName) : partner.shortName) || '—'}
            />
            <Field label="Loại đối tác" value={PARTNER_TYPE_LABELS[partner.partnerType] ?? partner.partnerType} />

            <Field label="Trạng thái hồ sơ" value={<ProfileStatusBadge status={partner.profileStatus} />} />
            <Field label="Trạng thái hợp tác" value={partner.cooperationStatus} />
            <Field label="Hiển thị" value={VISIBILITY_LABELS[partner.visibility] ?? partner.visibility} />

            <Field label="Quốc gia" icon={<Globe className="w-4 h-4 text-gray-400 flex-shrink-0" />} value={partner.country || '—'} />
            <Field label="Thành phố" value={partner.city || '—'} />
            <Field
              label="Website"
              value={partner.websiteUrl ? (
                <a
                  href={partner.websiteUrl.startsWith('http') ? partner.websiteUrl : `https://${partner.websiteUrl}`}
                  target="_blank" rel="noreferrer"
                  className="text-[#004c91] hover:underline break-words"
                >
                  {partner.websiteUrl}
                </a>
              ) : '—'}
            />

            <Field
              label="Địa chỉ"
              icon={<MapPin className="w-4 h-4 text-gray-400 flex-shrink-0" />}
              value={(displayLang === 'en' ? (partner.englishAddress || partner.address) : partner.address) || '—'}
              className="md:col-span-2"
            />
            <Field label="Campus sở hữu" value={partner.ownerCampusName} />

            <Field
              label="Người tạo"
              value={`${partner.creatorName || '—'} · ${formatVietnamDateTime(partner.createdAt)}`}
              className="md:col-span-3"
            />

            {partner.reviewedAt && (
              <Field
                label="Kết quả duyệt"
                value={`${partner.reviewerName || '—'} · ${formatVietnamDateTime(partner.reviewedAt)}${partner.reviewNote ? ` · ${partner.reviewNote}` : ''}`}
                className="md:col-span-3"
              />
            )}
          </div>

          {/* Mô tả chung — full-width gray card, matches original row-3 style */}
          <div className="rounded-2xl bg-gray-50/80 p-5 border border-gray-100 mt-6">
            <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-2">
              Mô tả chung {displayLang === 'en' ? '(English)' : '(Tiếng Việt)'}
            </span>
            <div className="text-[15px] font-medium text-gray-700 leading-relaxed whitespace-pre-line">
              {(displayLang === 'en' ? (partner.englishDescription || partner.description) : partner.description) || 'Chưa có mô tả.'}
            </div>
          </div>
        </div>
      </div>


      {/* Grid: Lịch sử hợp tác & Văn bản/Tài liệu */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
        {/* Lịch sử hợp tác */}
        <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden flex flex-col">
          <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <History className="w-6 h-6 text-white" />
              <h2 className="text-lg font-bold text-white uppercase tracking-wider">Lịch sử hợp tác</h2>
            </div>
            {visitHistory.length > 0 && (
              <span className="text-xs font-bold text-white/90 bg-white/20 px-2.5 py-1 rounded-full backdrop-blur-sm">
                {visitHistory.length} chuyến thăm
              </span>
            )}
          </div>
          <div className="p-6 flex-1 bg-white flex flex-col">
            {loadingHistory ? (
              <div className="flex items-center justify-center py-12 text-slate-400 gap-2 font-medium text-sm">
                <Loader2 className="w-5 h-5 animate-spin text-[#004c91]" />
                Đang tải lịch sử tiếp đón...
              </div>
            ) : visitHistory.length === 0 ? (
              <div className="py-10 text-center space-y-2 flex flex-col items-center justify-center flex-1">
                <div className="w-12 h-12 rounded-2xl bg-slate-50 flex items-center justify-center text-slate-300 border border-slate-100 mb-1">
                  <History className="w-6 h-6" />
                </div>
                <p className="text-sm font-bold text-slate-600">Chưa có dữ liệu lịch sử hợp tác</p>
                <p className="text-xs text-slate-400 max-w-xs">
                  Các chuyến thăm và đoàn tiếp đón thuộc đối tác này sẽ được tự động ghi nhận tại đây.
                </p>
              </div>
            ) : (
              <div className="max-h-[360px] overflow-y-auto pr-1 flex flex-col gap-3 custom-scrollbar">
                {visitHistory.map((item) => {
                  const statusCfg = VISIT_STATUS_CONFIG[item.status] || {
                    label: item.status,
                    bg: 'bg-blue-50',
                    text: 'text-blue-700',
                    border: 'border-blue-200',
                  };
                  return (
                    <div
                      key={item.visitInstanceId}
                      onClick={() => navigate(`/dashboard/visit?visitRequestId=${item.visitRequestId}`)}
                      className="p-4 rounded-2xl border border-slate-100 hover:border-[#004c91]/40 bg-slate-50/50 hover:bg-white shadow-xs hover:shadow-md transition-all cursor-pointer group flex flex-col gap-2 relative overflow-hidden"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <h4 className="font-extrabold text-slate-800 text-sm group-hover:text-[#004c91] transition-colors truncate">
                            {item.delegationName}
                          </h4>
                          <div className="flex items-center gap-2 mt-1 flex-wrap text-xs text-slate-500">
                            <span className="flex items-center gap-1 font-semibold text-slate-600">
                              <MapPin className="w-3.5 h-3.5 text-[#f37021]" /> {item.campusName}
                            </span>
                            <span>•</span>
                            <span className="flex items-center gap-1 font-medium">
                              <Users className="w-3.5 h-3.5 text-blue-500" /> {item.guestCount} thành viên
                            </span>
                          </div>
                        </div>

                        <span
                          className={`text-[11px] font-extrabold px-2.5 py-1 rounded-full border shrink-0 ${statusCfg.bg} ${statusCfg.text} ${statusCfg.border}`}
                        >
                          {statusCfg.label}
                        </span>
                      </div>

                      <div className="flex items-center justify-between text-xs pt-2 border-t border-slate-100/80 text-slate-500">
                        <span className="font-medium text-slate-500">
                          {formatVietnamDate(item.plannedStartAt)}
                        </span>
                        {item.hostName && (
                          <span className="text-[11px] font-semibold text-slate-600 bg-white px-2 py-0.5 rounded-md border border-slate-200 shadow-2xs">
                            Host: {item.hostName}
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Văn bản & Tài liệu */}
        <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden flex flex-col">
          <div className="bg-[#004c91] px-6 py-4 flex items-center gap-2.5">
            <FileText className="w-6 h-6 text-white" />
            <h2 className="text-lg font-bold text-white uppercase tracking-wider">Văn bản & Tài liệu</h2>
          </div>
          <div className="p-6 flex-1 bg-white flex flex-col gap-4">
            {canManage && (
              <div className="flex flex-wrap items-end gap-3 bg-gray-50/80 p-4 rounded-xl border border-gray-100">
                <div className="flex-1 min-w-[160px]">
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Tiêu đề *</label>
                  <input className={inputCls} value={docTitle} onChange={(e) => setDocTitle(e.target.value)} maxLength={255} />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Tệp *</label>
                  <input
                    type="file"
                    onChange={(e) => setDocFile(e.target.files?.[0] ?? null)}
                    className="text-sm text-gray-600 file:mr-3 file:px-3 file:py-1.5 file:rounded-lg file:border-0 file:bg-[#e6eff7] file:text-[#004c91] file:text-sm file:font-bold file:cursor-pointer"
                  />
                </div>
                <button
                  onClick={() => void uploadDocument()}
                  disabled={docBusy || !docFile || !docTitle.trim()}
                  className="bg-[#004c91] hover:bg-[#003a70] text-white px-4 py-2 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-2"
                >
                  {docBusy && <Loader2 className="w-4 h-4 animate-spin" />} Tải lên
                </button>
              </div>
            )}

            {documents.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-6">Chưa có tài liệu</p>
            ) : (
              <div className="max-h-[250px] overflow-y-auto flex flex-col gap-4 pr-2 custom-scrollbar">
                {documents.map((d) => (
                  <div
                    key={d.documentId}
                    className="bg-gradient-to-r from-blue-50/80 to-[#e6f0fa]/80 p-4 rounded-xl border border-blue-100/50 shadow-sm flex items-center gap-4 group hover:border-[#004c91]/50 transition-colors"
                  >
                    <div className="w-10 h-10 rounded-lg bg-blue-100 flex items-center justify-center text-[#004c91] flex-shrink-0">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="text-gray-800 font-bold text-[15px] truncate group-hover:text-[#004c91] transition-colors">{d.title}</div>
                      <div className="text-xs text-gray-500 font-medium mt-0.5 truncate">
                        {d.originalFilename || '—'}
                        {d.fileSize ? ` • ${(d.fileSize / 1024).toFixed(0)} KB` : ''}
                        {d.creatorName ? ` • ${d.creatorName}` : ''}
                      </div>
                    </div>
                    <div className="flex items-center gap-1 flex-shrink-0">
                      <button
                        onClick={() => setPreviewDoc(d)}
                        className="p-2 rounded-lg text-gray-400 hover:bg-white hover:text-[#004c91] transition-colors cursor-pointer"
                        title="Xem trước"
                      >
                        <Eye className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => void handleDownloadDoc(d.documentId, API_ENDPOINTS.files.download(d.fileId), d.originalFilename || d.title)}
                        disabled={downloadingKey !== null}
                        className="p-2 rounded-lg text-gray-400 hover:bg-white hover:text-[#004c91] transition-colors cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                        title="Tải xuống"
                      >
                        {downloadingKey === d.documentId ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Danh sách người liên hệ — có id để màn biên bản deep-link thẳng vào đây khi thành viên đã
          xác định được đối tác và việc còn lại là bổ sung/cập nhật liên hệ (PART-07). */}
      <div id="contacts" className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden mb-8 scroll-mt-24">
        <div className="bg-[#00a651] px-6 py-4 flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-2.5">
            <div className="bg-white/20 p-1.5 rounded-lg text-white">
              <Users className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-white uppercase tracking-wider">Danh sách người liên hệ</h2>
          </div>
          <div className="flex items-center gap-3">
            <input
              type="text"
              placeholder="Tìm kiếm người liên hệ..."
              value={contactSearch}
              onChange={(e) => {
                setContactSearch(e.target.value);
                setContactPage(1); // Reset to page 1 on search
              }}
              className="px-3 py-2 rounded-lg text-sm bg-white/20 border border-white/30 text-white placeholder-white/70 focus:outline-none focus:bg-white focus:text-gray-800 transition-colors"
            />
            {canManage && (
              <button
                onClick={() => openContactForm()}
                className="flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white px-4 py-2 rounded-xl transition-colors font-bold text-sm outline-none shadow-sm cursor-pointer"
              >
                <Plus className="w-4 h-4" /> Thêm liên hệ
              </button>
            )}
          </div>
        </div>
        <div className="p-6">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[880px] border-collapse">
              <thead>
                <tr className="border-b-2 border-gray-200">
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Tên người liên hệ</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Email</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">SĐT</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Chức vụ / Phòng ban</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Nguồn</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Trạng thái</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider pl-4">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(() => {
                  if (contactsLoading) {
                    return (
                      <tr>
                        <td colSpan={7} className="p-4 sm:p-6 md:p-8 text-center text-gray-400 font-medium bg-gray-50/50">
                          <Loader2 className="w-5 h-5 animate-spin inline-block mr-2" /> Đang tải...
                        </td>
                      </tr>
                    );
                  }

                  const filteredContacts = contacts.filter(c =>
                    c.fullName.toLowerCase().includes(contactSearch.toLowerCase()) ||
                    c.email?.toLowerCase().includes(contactSearch.toLowerCase()) ||
                    c.phone?.includes(contactSearch)
                  );

                  if (filteredContacts.length === 0) {
                    return (
                      <tr>
                        <td colSpan={7} className="p-4 sm:p-6 md:p-8 text-center text-gray-500 font-medium bg-gray-50/50">
                          {contactSearch ? 'Không tìm thấy người liên hệ nào khớp với tìm kiếm.' : 'Danh sách trống'}
                        </td>
                      </tr>
                    );
                  }

                  const totalPages = Math.ceil(filteredContacts.length / contactsPerPage);
                  const startIndex = (contactPage - 1) * contactsPerPage;
                  const paginatedContacts = filteredContacts.slice(startIndex, startIndex + contactsPerPage);

                  return paginatedContacts.map((c) => (
                    <tr key={c.contactId} className="hover:bg-gradient-to-r hover:from-[#eaffe4] hover:to-[#ceefda]/40 transition-colors group">
                      <td className="p-3 pl-4">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-[#00a651]/10 flex items-center justify-center text-[#00a651] font-bold text-xs shrink-0 overflow-hidden border border-[#00a651]/20 shadow-sm">
                            <ContactAvatarImage fileId={c.avatarFileId} url={c.avatarUrl} alt={c.fullName} />
                          </div>
                          <span className="flex items-center gap-1.5 font-bold text-gray-800 text-sm">
                            {c.isPrimary && <Star className="w-3.5 h-3.5 text-amber-400 fill-amber-400 flex-shrink-0" />}
                            <span className={c.status === 'INACTIVE' ? 'text-gray-400 line-through' : ''}>{c.fullName}</span>
                          </span>
                        </div>
                      </td>
                      <td className="p-3 text-center text-sm text-gray-700">{c.email || '—'}</td>
                      <td className="p-3 text-center text-sm text-gray-700">{c.phone || '—'}</td>
                      <td className="p-3 text-center text-sm text-gray-700">
                        {[c.jobTitle, c.departmentName].filter(Boolean).join(' - ') || '—'}
                      </td>
                      <td className="p-3 text-center">
                        <span className="inline-block px-2 py-0.5 rounded-full text-[11px] font-bold bg-slate-100 text-slate-500">
                          {c.sourceType === 'BUSINESS_CARD_OCR'
                            ? `OCR danh thiếp${c.ocrConfidence != null ? ` - ${c.ocrConfidence > 90 ? 'Cao' : c.ocrConfidence >= 60 ? 'Trung bình' : 'Thấp'}` : ''}`
                            : c.sourceType === 'IMPORT' ? 'Import' : 'Nhập tay'}
                        </span>
                      </td>
                      <td className="p-3 text-center">
                        <span className={`inline-block px-2 py-0.5 rounded-full text-[11px] font-bold ${c.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f]' : 'bg-gray-100 text-gray-400'}`}>
                          {c.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'}
                        </span>
                      </td>
                      <td className="p-3">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            onClick={() => setViewContact(c)}
                            className="p-1.5 text-gray-400 hover:text-[#00a651] hover:bg-[#eaffe4] rounded-lg transition-colors border border-transparent hover:border-[#ceefda] outline-none flex items-center justify-center cursor-pointer"
                            title="Xem chi tiết"
                          >
                            <Eye className="w-4 h-4" />
                          </button>
                          {canManage && (
                            <>
                              {!c.isPrimary && c.status === 'ACTIVE' && (
                                <button
                                  onClick={() => void setPrimary(c)}
                                  disabled={busy}
                                  className="p-1.5 text-gray-400 hover:text-amber-500 hover:bg-amber-50 rounded-lg transition-colors border border-transparent hover:border-amber-200 outline-none flex items-center justify-center cursor-pointer disabled:opacity-40"
                                  title="Đặt làm liên hệ chính"
                                >
                                  <Star className="w-4 h-4" />
                                </button>
                              )}
                              <button
                                onClick={() => openContactForm(c)}
                                disabled={busy}
                                className="p-1.5 text-gray-400 hover:text-[#004c91] hover:bg-[#e6eff7] rounded-lg transition-colors border border-transparent hover:border-blue-200 outline-none flex items-center justify-center cursor-pointer disabled:opacity-40"
                                title="Sửa"
                              >
                                <Edit3 className="w-4 h-4" />
                              </button>
                              <button
                                onClick={() => void deactivateContact(c)}
                                disabled={busy}
                                className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-200 outline-none flex items-center justify-center cursor-pointer disabled:opacity-40"
                                title="Vô hiệu hoá"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ));
                })()}
              </tbody>
            </table>
          </div>

          {!contactsLoading && contacts.length > 0 && (() => {
            const filteredContactsCount = contacts.filter(c =>
              c.fullName.toLowerCase().includes(contactSearch.toLowerCase()) ||
              c.email?.toLowerCase().includes(contactSearch.toLowerCase()) ||
              c.phone?.includes(contactSearch)
            ).length;
            const totalPages = Math.ceil(filteredContactsCount / contactsPerPage);
            if (totalPages <= 1) return null;
            return (
              <div className="flex items-center justify-between mt-4 border-t border-gray-100 pt-4">
                <span className="text-sm text-gray-500">
                  Hiển thị {Math.min(filteredContactsCount, (contactPage - 1) * contactsPerPage + 1)} - {Math.min(filteredContactsCount, contactPage * contactsPerPage)} trong {filteredContactsCount}
                </span>
                <div className="flex items-center gap-1">
                  <button
                    disabled={contactPage === 1}
                    onClick={() => setContactPage(p => p - 1)}
                    className="px-3 py-1.5 rounded-lg border border-gray-200 text-gray-600 disabled:opacity-50 disabled:bg-gray-50 cursor-pointer text-sm font-medium hover:bg-gray-50"
                  >
                    Trước
                  </button>
                  <span className="px-3 py-1.5 text-sm font-medium text-gray-700">
                    Trang {contactPage} / {totalPages}
                  </span>
                  <button
                    disabled={contactPage >= totalPages}
                    onClick={() => setContactPage(p => p + 1)}
                    className="px-3 py-1.5 rounded-lg border border-gray-200 text-gray-600 disabled:opacity-50 disabled:bg-gray-50 cursor-pointer text-sm font-medium hover:bg-gray-50"
                  >
                    Sau
                  </button>
                </div>
              </div>
            );
          })()}
        </div>
      </div>

      {/* Tên gọi khác */}
      <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden mb-8">
        <div className="bg-[#004c91]/90 px-6 py-3.5 flex items-center gap-2.5">
          <Tag className="w-5 h-5 text-white" />
          <h2 className="text-base font-bold text-white uppercase tracking-wider">Tên gọi khác</h2>
        </div>
        <div className="p-5">
          <p className="text-xs text-gray-400 mb-3">
            Tên gọi khác giúp hệ thống nhận diện đối tác khi tên tổ chức của khách/OCR không khớp
            chính xác tên chính thức.
          </p>
          {canManage && (
            <div className="flex flex-wrap items-center gap-2 mb-4">
              <input
                className={`${inputCls} max-w-xs`}
                placeholder="Nhập tên gọi khác..."
                value={newAlias}
                onChange={(e) => setNewAlias(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') void addAlias(); }}
                maxLength={255}
              />
              <button
                onClick={() => void addAlias()}
                disabled={busy || !newAlias.trim()}
                className="bg-[#004c91] hover:bg-[#003a70] text-white px-4 py-2 rounded-lg text-sm font-bold transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-1.5"
              >
                <Plus className="w-4 h-4" /> Thêm
              </button>
            </div>
          )}
          {aliases.length === 0 ? (
            <p className="text-sm text-gray-400">Chưa có tên gọi khác</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {aliases.map((a) => (
                <span key={a.partnerAliasId}
                  className="inline-flex items-center gap-2 bg-slate-50 border border-gray-200 rounded-full pl-3.5 pr-2 py-1.5 text-sm font-medium text-gray-600">
                  {a.aliasName}
                  <span className="text-[10px] uppercase text-gray-400 font-bold">{a.source}</span>
                  {canManage && (
                    <button onClick={() => void removeAlias(a)} disabled={busy}
                      className="p-0.5 rounded-full text-gray-300 hover:bg-red-50 hover:text-red-500 transition-colors cursor-pointer">
                      <X className="w-3.5 h-3.5" />
                    </button>
                  )}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Reject modal */}
      {rejectOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-red-500">
              <h3 className="text-xl font-bold text-white">Từ chối đối tác</h3>
              <button
                onClick={() => setRejectOpen(false)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 bg-gray-50/50">
              <p className="text-sm text-gray-500 mb-3">Lý do từ chối là bắt buộc.</p>
              <textarea
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                rows={4}
                placeholder="Nhập lý do từ chối..."
                className={`${inputCls} bg-white`}
              />
            </div>
            <div className="p-4 border-t border-gray-100 bg-white flex justify-end gap-3">
              <button onClick={() => setRejectOpen(false)} disabled={busy}
                className="px-4 py-2 bg-white hover:bg-gray-100 text-gray-700 font-bold rounded-xl transition-colors border border-gray-200 outline-none cursor-pointer">
                Hủy
              </button>
              <button onClick={() => void reject()} disabled={!rejectReason.trim() || busy}
                className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white font-bold rounded-xl transition-colors outline-none cursor-pointer shadow-sm shadow-red-200 disabled:opacity-50">
                {busy ? 'Đang xử lý...' : 'Từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Contact form modal (create/edit) */}
      {contactFormOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-[#004c91]">
              <h3 className="text-xl font-bold text-white">
                {editingContact ? 'Chỉnh sửa người liên hệ' : 'Thêm người liên hệ'}
              </h3>
              <button
                onClick={closeContactForm}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 bg-gray-50/50">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="md:col-span-2" data-field-error={contactFieldErrors.fullName ? 'true' : undefined}>
                  <label htmlFor="pc-fullName" className="block text-xs font-bold text-gray-500 uppercase mb-1">Họ tên *</label>
                  <input
                    id="pc-fullName"
                    data-testid="partner-contact-field-fullName"
                    className={contactInputCls(!!contactFieldErrors.fullName)}
                    value={cName}
                    onChange={(e) => { setCName(e.target.value); clearContactFieldError('fullName', e.target.value); }}
                    maxLength={150}
                    aria-invalid={contactFieldErrors.fullName ? true : undefined}
                    aria-describedby={contactFieldErrors.fullName ? 'pc-fullName-error' : undefined}
                  />
                  {contactFieldErrors.fullName && (
                    <p id="pc-fullName-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">{contactFieldErrors.fullName}</p>
                  )}
                </div>
                <div data-field-error={contactFieldErrors.email ? 'true' : undefined}>
                  <label htmlFor="pc-email" className="block text-xs font-bold text-gray-500 uppercase mb-1">Email</label>
                  <input
                    id="pc-email"
                    data-testid="partner-contact-field-email"
                    className={contactInputCls(!!contactFieldErrors.email)}
                    type="text"
                    inputMode="email"
                    value={cEmail}
                    onChange={(e) => { setCEmail(e.target.value); clearContactFieldError('email', e.target.value); }}
                    maxLength={150}
                    aria-invalid={contactFieldErrors.email ? true : undefined}
                    aria-describedby={contactFieldErrors.email ? 'pc-email-error' : undefined}
                  />
                  {contactFieldErrors.email && (
                    <p id="pc-email-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">{contactFieldErrors.email}</p>
                  )}
                </div>
                <div data-field-error={contactFieldErrors.phone ? 'true' : undefined}>
                  <label htmlFor="pc-phone" className="block text-xs font-bold text-gray-500 uppercase mb-1">Số điện thoại</label>
                  <input
                    id="pc-phone"
                    data-testid="partner-contact-field-phone"
                    className={contactInputCls(!!contactFieldErrors.phone)}
                    value={cPhone}
                    onChange={(e) => { setCPhone(e.target.value); clearContactFieldError('phone', e.target.value); }}
                    maxLength={50}
                    aria-invalid={contactFieldErrors.phone ? true : undefined}
                    aria-describedby={contactFieldErrors.phone ? 'pc-phone-error' : undefined}
                  />
                  {contactFieldErrors.phone && (
                    <p id="pc-phone-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">{contactFieldErrors.phone}</p>
                  )}
                </div>
                <div data-field-error={contactFieldErrors.jobTitle ? 'true' : undefined}>
                  <label htmlFor="pc-jobTitle" className="block text-xs font-bold text-gray-500 uppercase mb-1">Chức danh</label>
                  <input
                    id="pc-jobTitle"
                    data-testid="partner-contact-field-jobTitle"
                    className={contactInputCls(!!contactFieldErrors.jobTitle)}
                    value={cTitle}
                    onChange={(e) => { setCTitle(e.target.value); clearContactFieldError('jobTitle', e.target.value); }}
                    maxLength={150}
                    aria-invalid={contactFieldErrors.jobTitle ? true : undefined}
                    aria-describedby={contactFieldErrors.jobTitle ? 'pc-jobTitle-error' : undefined}
                  />
                  {contactFieldErrors.jobTitle && (
                    <p id="pc-jobTitle-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">{contactFieldErrors.jobTitle}</p>
                  )}
                </div>
                <div data-field-error={contactFieldErrors.departmentName ? 'true' : undefined}>
                  <label htmlFor="pc-department" className="block text-xs font-bold text-gray-500 uppercase mb-1">Phòng ban</label>
                  <input
                    id="pc-department"
                    data-testid="partner-contact-field-departmentName"
                    className={contactInputCls(!!contactFieldErrors.departmentName)}
                    value={cDepartment}
                    onChange={(e) => { setCDepartment(e.target.value); clearContactFieldError('departmentName', e.target.value); }}
                    maxLength={150}
                    aria-invalid={contactFieldErrors.departmentName ? true : undefined}
                    aria-describedby={contactFieldErrors.departmentName ? 'pc-department-error' : undefined}
                  />
                  {contactFieldErrors.departmentName && (
                    <p id="pc-department-error" role="alert" className="mt-1 text-xs font-semibold text-red-600">{contactFieldErrors.departmentName}</p>
                  )}
                </div>
                <div className="md:col-span-2">
                  <label htmlFor="pc-note" className="block text-xs font-bold text-gray-500 uppercase mb-1">Ghi chú</label>
                  <textarea
                    id="pc-note"
                    className={`${inputCls} bg-white`}
                    rows={2}
                    value={cNote}
                    onChange={(e) => setCNote(e.target.value)}
                    placeholder="Ghi chú thêm (tuỳ chọn)..."
                  />
                </div>
                {!editingContact && (
                  <label className="md:col-span-2 flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
                    <input type="checkbox" checked={cPrimary} onChange={(e) => setCPrimary(e.target.checked)} className="rounded border-gray-300" />
                    Đặt làm người liên hệ chính
                  </label>
                )}
              </div>
            </div>
            <div className="p-4 border-t border-gray-100 bg-white flex justify-end gap-3">
              <button onClick={closeContactForm} disabled={busy}
                className="px-4 py-2 bg-white hover:bg-gray-100 text-gray-700 font-bold rounded-xl transition-colors border border-gray-200 outline-none cursor-pointer">
                Hủy
              </button>
              <button onClick={() => void saveContact()} disabled={busy}
                className="px-4 py-2 bg-[#004c91] hover:bg-[#003a70] text-white font-bold rounded-xl transition-colors outline-none cursor-pointer disabled:opacity-50">
                {busy ? 'Đang lưu...' : 'Lưu'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Contact detail modal (read-only "Xem chi tiết") — restores the original popup */}
      {viewContact && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-[#00a651] shrink-0">
              <h3 className="text-xl font-bold text-white">Thông tin chi tiết</h3>
              <button
                onClick={() => setViewContact(null)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-6 bg-gray-50/50 overflow-y-auto">
              <div className="flex items-center gap-4 pb-4 border-b border-gray-200">
                <div className="w-14 h-14 bg-gradient-to-br from-[#eaffe4] to-[#ceefda] rounded-xl flex items-center justify-center text-[#00a651] font-black text-2xl shrink-0 shadow-sm border border-[#00a651]/20 overflow-hidden">
                  <ContactAvatarImage fileId={viewContact.avatarFileId} url={viewContact.avatarUrl} alt={viewContact.fullName} />
                </div>
                <div className="min-w-0">
                  <h4 className="font-black text-gray-900 text-xl tracking-tight truncate">{viewContact.fullName}</h4>
                  <p className="text-sm font-bold text-[#00a651] uppercase tracking-wide mt-1 truncate">
                    {[viewContact.jobTitle, viewContact.departmentName].filter(Boolean).join(' - ') || 'Chưa cập nhật chức vụ'}
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-5 bg-white p-5 rounded-xl border border-gray-100 shadow-sm">
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5">Email</label>
                  <p className="text-[15px] font-bold text-[#004c91] truncate">{viewContact.email || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5">SĐT</label>
                  <p className="text-[15px] font-bold text-gray-800">{viewContact.phone || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5">Nguồn</label>
                  <p className="text-[15px] font-medium text-gray-800">
                    {viewContact.sourceType === 'BUSINESS_CARD_OCR' ? 'Quét danh thiếp (OCR)' : viewContact.sourceType === 'IMPORT' ? 'Import' : 'Nhập tay'}
                    {viewContact.ocrConfidence != null && ` • Độ tin cậy ${viewContact.ocrConfidence > 90 ? 'Cao' : viewContact.ocrConfidence >= 60 ? 'Trung bình' : 'Thấp'}`}
                  </p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5">Trạng thái</label>
                  <p className="text-[15px] font-medium text-gray-800">
                    {viewContact.status === 'ACTIVE' ? 'Hoạt động' : 'Ngừng hoạt động'}
                    {viewContact.isPrimary ? ' • Liên hệ chính' : ''}
                  </p>
                </div>
                <div className="col-span-2">
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5">Ghi chú</label>
                  <p className="text-[15px] font-medium text-gray-800">{viewContact.note || 'Không có ghi chú'}</p>
                </div>
              </div>

              {viewContact.scannedCardFileId && (
                <div className="bg-white p-5 rounded-xl border border-gray-100 shadow-sm">
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Ảnh card đã quét</label>
                  {scannedCardUrl ? (
                    <img
                      src={scannedCardUrl}
                      alt="Card visit"
                      className="w-full max-h-56 object-contain rounded-lg border border-gray-200 bg-gray-50"
                    />
                  ) : (
                    <div className="h-32 flex items-center justify-center text-gray-300 bg-gray-50 rounded-lg border border-gray-200">
                      <Loader2 className="w-6 h-6 animate-spin" />
                    </div>
                  )}
                  <button
                    onClick={() => void handleDownloadDoc('card', API_ENDPOINTS.files.download(viewContact.scannedCardFileId!), 'card-visit')}
                    disabled={downloadingKey !== null}
                    className="mt-3 inline-flex items-center gap-1.5 text-xs font-bold text-[#004c91] hover:underline cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {downloadingKey === 'card' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />} Tải xuống ảnh card
                  </button>
                </div>
              )}
            </div>

            <div className="p-5 border-t border-gray-100 bg-white flex justify-end shrink-0">
              <button
                onClick={() => setViewContact(null)}
                className="px-6 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold rounded-xl transition-colors outline-none cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* OCR scan modal — preselects this partner (name passed through so the modal doesn't
          have to show a bare "#id" badge while it resolves). */}
      <BusinessCardScanModal
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        context={{ partnerId: partner.partnerId, partnerName: partner.name }}
        onConfirmed={() => { void loadContacts(); }}
      />

      {/* The shared preview, in place of a second implementation that lived in this file. A partner
          document is an attachment like any other, and it now gets the same retry, the same storage
          error codes and the same keyboard behaviour as an email's. */}
      <FilePreviewModal
        open={previewDoc != null}
        file={previewDoc && {
          fileId: previewDoc.fileId,
          name: previewDoc.originalFilename || previewDoc.title,
          mimeType: previewDoc.mimeType,
          size: previewDoc.fileSize,
        }}
        onClose={() => setPreviewDoc(null)}
      />
    </div>
  );
}
