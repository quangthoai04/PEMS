/**
 * PartnerDetail — hồ sơ đối tác chạy DB thật (docs/PARTNER_canh/01 §10.2).
 * UI khôi phục theo layout gốc (cover/logo banner, card "Thông tin cơ bản",
 * "Lịch sử hợp tác" + "Văn bản & Tài liệu", "Danh sách người liên hệ", "Tên gọi khác")
 * nhưng toàn bộ dữ liệu/handler đều là API thật: get detail, approve/reject, contacts
 * (CRUD + set-primary), aliases, documents, OCR "Quét danh thiếp" (module 02).
 */
import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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

// Cover/logo placeholders restored from the original PartnerDetail UI — shown until the
// partner's own logoFileId/coverFileId resolves (or when it has none).
import coverImage from '../../../assets/images/banner_partner.png';
const logoModules = import.meta.glob('../../../assets/Logo/*', { eager: true });
const logoList = Object.values(logoModules).map((m: any) => m.default || m) as string[];

const inputCls =
  'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white';

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

  const [partner, setPartner] = useState<PartnerDetailType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Approval panel
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
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
  const [cPrimary, setCPrimary] = useState(false);

  // Aliases
  const [aliases, setAliases] = useState<PartnerAlias[]>([]);
  const [newAlias, setNewAlias] = useState('');

  // Documents
  const [documents, setDocuments] = useState<PartnerDocument[]>([]);
  const [docTitle, setDocTitle] = useState('');
  const [docFile, setDocFile] = useState<File | null>(null);
  const [docBusy, setDocBusy] = useState(false);

  // OCR modal
  const [scanOpen, setScanOpen] = useState(false);

  // Cover/logo live behind the authenticated /api/files/{id}/content route; fall back to the
  // original static banner/logo assets when the partner has no image yet.
  const fetchedCover = useAuthenticatedImage(
    partner?.coverFileId ? `/api/files/${partner.coverFileId}/content` : null,
  );
  const fetchedLogo = useAuthenticatedImage(
    partner?.logoFileId ? `/api/files/${partner.logoFileId}/content` : null,
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
  }, [loadContacts, loadAliases, loadDocuments]);

  const approve = async () => {
    if (!id) return;
    setBusy(true);
    setActionError(null);
    try {
      await partnersApi.approvePartner(id, undefined, makePublic);
      await load();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Duyệt thất bại.');
    } finally { setBusy(false); }
  };

  const reject = async () => {
    if (!id || !rejectReason.trim()) return;
    setBusy(true);
    setActionError(null);
    try {
      await partnersApi.rejectPartner(id, rejectReason.trim());
      setRejectOpen(false);
      setRejectReason('');
      await load();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Từ chối thất bại.');
    } finally { setBusy(false); }
  };

  const openContactForm = (contact?: PartnerContact) => {
    setEditingContact(contact ?? null);
    setCName(contact?.fullName ?? '');
    setCEmail(contact?.email ?? '');
    setCPhone(contact?.phone ?? '');
    setCTitle(contact?.jobTitle ?? '');
    setCDepartment(contact?.departmentName ?? '');
    setCPrimary(contact?.isPrimary ?? false);
    setContactFormOpen(true);
  };

  const saveContact = async () => {
    if (!id || !cName.trim()) return;
    setBusy(true);
    setActionError(null);
    try {
      if (editingContact) {
        await partnersApi.updateContact(id, editingContact.contactId, {
          fullName: cName.trim(),
          email: cEmail.trim() || null,
          phone: cPhone.trim() || null,
          jobTitle: cTitle.trim() || null,
          departmentName: cDepartment.trim() || null,
        });
      } else {
        await partnersApi.createContact(id, {
          fullName: cName.trim(),
          email: cEmail.trim() || null,
          phone: cPhone.trim() || null,
          jobTitle: cTitle.trim() || null,
          departmentName: cDepartment.trim() || null,
          isPrimary: cPrimary,
        });
      }
      setContactFormOpen(false);
      await loadContacts();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Lưu người liên hệ thất bại.');
    } finally { setBusy(false); }
  };

  const deactivateContact = async (contact: PartnerContact) => {
    if (!id) return;
    if (!window.confirm(`Vô hiệu hoá người liên hệ "${contact.fullName}"?`)) return;
    setBusy(true);
    try {
      await partnersApi.deactivateContact(id, contact.contactId);
      await loadContacts();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Vô hiệu hoá thất bại.');
    } finally { setBusy(false); }
  };

  const setPrimary = async (contact: PartnerContact) => {
    if (!id) return;
    setBusy(true);
    try {
      await partnersApi.setPrimaryContact(id, contact.contactId);
      await loadContacts();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Đặt liên hệ chính thất bại.');
    } finally { setBusy(false); }
  };

  const addAlias = async () => {
    if (!id || !newAlias.trim()) return;
    setBusy(true);
    setActionError(null);
    try {
      await partnersApi.createAlias(id, newAlias.trim());
      setNewAlias('');
      await loadAliases();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Thêm tên gọi khác thất bại.');
    } finally { setBusy(false); }
  };

  const removeAlias = async (alias: PartnerAlias) => {
    if (!id) return;
    setBusy(true);
    try {
      await partnersApi.deactivateAlias(id, alias.partnerAliasId);
      await loadAliases();
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Xoá tên gọi khác thất bại.');
    } finally { setBusy(false); }
  };

  const uploadDocument = async () => {
    if (!id || !docFile || !docTitle.trim()) return;
    setDocBusy(true);
    setActionError(null);
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
    } catch (e: any) {
      setActionError(e?.response?.data?.message || 'Tải tài liệu thất bại.');
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
      {actionError && (
        <div className="mb-5 bg-red-50 border border-red-100 text-red-600 text-sm rounded-xl px-4 py-3">
          {actionError}
        </div>
      )}

      {/* Cover & Logo Section */}
      <div className="relative mb-10 w-full h-[220px] sm:h-[300px] md:h-[380px] lg:h-[440px] rounded-[24px] bg-gray-100 shadow-sm overflow-hidden">
        <img
          src={fetchedCover ?? coverImage}
          alt="Cover"
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />

        <div className="absolute top-4 right-4 sm:top-5 sm:right-5">
          <ProfileStatusBadge status={partner.profileStatus} />
        </div>

        {/* Logo overlay */}
        <div className="absolute -bottom-2 left-0 right-0 p-4 sm:p-6 flex items-end">
          <div className="w-20 h-20 sm:w-28 sm:h-28 rounded-[20px] bg-white shadow-xl p-2 border-4 border-white overflow-hidden flex items-center justify-center flex-shrink-0">
            {(fetchedLogo || logoList[0]) ? (
              <img src={fetchedLogo ?? logoList[0]} alt="Logo" className="w-full h-full object-contain" />
            ) : (
              <Globe className="w-10 h-10 text-gray-300" />
            )}
          </div>
          <div className="ml-4 sm:ml-6 mb-2 sm:mb-4 text-white z-10 min-w-0">
            <h1 className="text-xl sm:text-2xl md:text-3xl font-bold tracking-tight drop-shadow-md truncate">{partner.name}</h1>
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
          <h2 className="text-lg font-bold text-white uppercase tracking-wider">Thông tin cơ bản</h2>
        </div>
        <div className="p-4 sm:p-6 md:p-8">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-x-8 gap-y-6">
            <Field label="Mã đối tác" value={partner.partnerCode || '—'} />
            <Field label="Tên viết tắt" value={partner.shortName || '—'} />
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
              value={partner.address || '—'}
              className="md:col-span-2"
            />
            <Field label="Campus sở hữu" value={partner.ownerCampusName} />

            <Field
              label="Người tạo"
              value={`${partner.creatorName || '—'} · ${new Date(partner.createdAt).toLocaleString('vi-VN')}`}
              className="md:col-span-3"
            />

            {partner.reviewedAt && (
              <Field
                label="Kết quả duyệt"
                value={`${partner.reviewerName || '—'} · ${new Date(partner.reviewedAt).toLocaleString('vi-VN')}${partner.reviewNote ? ` · ${partner.reviewNote}` : ''}`}
                className="md:col-span-3"
              />
            )}
          </div>

          {/* Mô tả chung — full-width gray card, matches original row-3 style */}
          <div className="rounded-2xl bg-gray-50/80 p-5 border border-gray-100 mt-6">
            <span className="block text-[13px] font-bold text-[#004c91] uppercase tracking-wider mb-2">Mô tả chung</span>
            <div className="text-[15px] font-medium text-gray-700 leading-relaxed whitespace-pre-line">
              {partner.description || 'Chưa có mô tả.'}
            </div>
          </div>
        </div>
      </div>

      {/* Grid: Lịch sử hợp tác & Văn bản/Tài liệu */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
        {/* Lịch sử hợp tác */}
        <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden flex flex-col">
          <div className="bg-[#004c91] px-6 py-4 flex items-center gap-2.5">
            <History className="w-6 h-6 text-white" />
            <h2 className="text-lg font-bold text-white uppercase tracking-wider">Lịch sử hợp tác</h2>
          </div>
          <div className="p-6 flex-1 bg-white flex items-center justify-center min-h-[160px]">
            <p className="text-sm text-gray-400 font-medium text-center">Chưa có dữ liệu lịch sử hợp tác.</p>
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
              documents.map((d) => (
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
                  <a
                    href={`${httpClient.defaults.baseURL}/files/${d.fileId}/download`}
                    target="_blank" rel="noreferrer"
                    className="p-2 rounded-lg text-gray-400 hover:bg-white hover:text-[#004c91] transition-colors flex-shrink-0"
                    title="Tải xuống"
                  >
                    <Download className="w-4 h-4" />
                  </a>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* Danh sách người liên hệ */}
      <div className="bg-white rounded-3xl shadow-[0_4px_24px_rgba(0,0,0,0.03)] border border-gray-100 overflow-hidden mb-8">
        <div className="bg-[#00a651] px-6 py-4 flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-2.5">
            <div className="bg-white/20 p-1.5 rounded-lg text-white">
              <Users className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-white uppercase tracking-wider">Danh sách người liên hệ</h2>
          </div>
          {canManage && (
            <button
              onClick={() => openContactForm()}
              className="flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white px-4 py-2 rounded-xl transition-colors font-bold text-sm outline-none shadow-sm cursor-pointer"
            >
              <Plus className="w-4 h-4" /> Thêm liên hệ
            </button>
          )}
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
                {contactsLoading ? (
                  <tr>
                    <td colSpan={7} className="p-4 sm:p-6 md:p-8 text-center text-gray-400 font-medium bg-gray-50/50">
                      <Loader2 className="w-5 h-5 animate-spin inline-block mr-2" /> Đang tải...
                    </td>
                  </tr>
                ) : contacts.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="p-4 sm:p-6 md:p-8 text-center text-gray-500 font-medium bg-gray-50/50">
                      Danh sách trống
                    </td>
                  </tr>
                ) : (
                  contacts.map((c) => (
                    <tr key={c.contactId} className="hover:bg-gradient-to-r hover:from-[#eaffe4] hover:to-[#ceefda]/40 transition-colors group">
                      <td className="p-3 pl-4">
                        <span className="flex items-center gap-1.5 font-bold text-gray-800 text-sm">
                          {c.isPrimary && <Star className="w-3.5 h-3.5 text-amber-400 fill-amber-400 flex-shrink-0" />}
                          <span className={c.status === 'INACTIVE' ? 'text-gray-400 line-through' : ''}>{c.fullName}</span>
                        </span>
                      </td>
                      <td className="p-3 text-center text-sm text-gray-700">{c.email || '—'}</td>
                      <td className="p-3 text-center text-sm text-gray-700">{c.phone || '—'}</td>
                      <td className="p-3 text-center text-sm text-gray-700">
                        {[c.jobTitle, c.departmentName].filter(Boolean).join(' - ') || '—'}
                      </td>
                      <td className="p-3 text-center">
                        <span className="inline-block px-2 py-0.5 rounded-full text-[11px] font-bold bg-slate-100 text-slate-500">
                          {c.sourceType === 'BUSINESS_CARD_OCR'
                            ? `OCR danh thiếp${c.ocrConfidence != null ? ` ${c.ocrConfidence}%` : ''}`
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
                  ))
                )}
              </tbody>
            </table>
          </div>
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
                onClick={() => setContactFormOpen(false)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 bg-gray-50/50">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="md:col-span-2">
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Họ tên *</label>
                  <input className={`${inputCls} bg-white`} value={cName} onChange={(e) => setCName(e.target.value)} maxLength={150} />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Email</label>
                  <input className={`${inputCls} bg-white`} type="email" value={cEmail} onChange={(e) => setCEmail(e.target.value)} maxLength={150} />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Số điện thoại</label>
                  <input className={`${inputCls} bg-white`} value={cPhone} onChange={(e) => setCPhone(e.target.value)} maxLength={50} />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Chức danh</label>
                  <input className={`${inputCls} bg-white`} value={cTitle} onChange={(e) => setCTitle(e.target.value)} maxLength={150} />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Phòng ban</label>
                  <input className={`${inputCls} bg-white`} value={cDepartment} onChange={(e) => setCDepartment(e.target.value)} maxLength={150} />
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
              <button onClick={() => setContactFormOpen(false)} disabled={busy}
                className="px-4 py-2 bg-white hover:bg-gray-100 text-gray-700 font-bold rounded-xl transition-colors border border-gray-200 outline-none cursor-pointer">
                Hủy
              </button>
              <button onClick={() => void saveContact()} disabled={!cName.trim() || busy}
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
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-[#00a651]">
              <h3 className="text-xl font-bold text-white">Thông tin chi tiết</h3>
              <button
                onClick={() => setViewContact(null)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-6 bg-gray-50/50">
              <div className="flex items-center gap-4 pb-4 border-b border-gray-200">
                <div className="w-14 h-14 bg-gradient-to-br from-[#eaffe4] to-[#ceefda] rounded-xl flex items-center justify-center text-[#00a651] font-black text-2xl shrink-0 shadow-sm border border-[#00a651]/20">
                  {viewContact.fullName ? viewContact.fullName.charAt(0).toUpperCase() : '?'}
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
                    {viewContact.ocrConfidence != null && ` • Độ tin cậy ${viewContact.ocrConfidence}%`}
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
            </div>

            <div className="p-5 border-t border-gray-100 bg-white flex justify-end">
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

      {/* OCR scan modal — preselects this partner */}
      <BusinessCardScanModal
        open={scanOpen}
        onClose={() => setScanOpen(false)}
        context={{ partnerId: partner.partnerId }}
        onConfirmed={() => { void loadContacts(); }}
      />
    </div>
  );
}
