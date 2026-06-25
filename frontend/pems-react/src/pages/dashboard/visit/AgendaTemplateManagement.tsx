/**
 * AgendaTemplateManagement
 * Quản lý mẫu Agenda theo loại hình visit (visit_type) và phạm vi (GLOBAL / theo cơ sở).
 * Mỗi mục lịch trình dùng "bắt đầu sau (phút)" tính từ giờ bắt đầu chuyến + thời lượng (phút) —
 * không dùng giờ tuyệt đối.
 * Wired tới API thật: /api/agenda-templates (CRUD + đặt mặc định).
 */

import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Plus, Trash2, Edit2, Save, X, Settings2, Clock, MapPin, User, FileText,
  Star, Globe, Building2, Loader2, AlertCircle,
} from 'lucide-react';
import toast, { Toaster } from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import agendaTemplatesApi from '../../../features/agenda-templates/api/agendaTemplatesApi';
import { agendaTemplatesAdapter } from '../../../features/agenda-templates/adapters/agendaTemplatesAdapter';
import {
  VISIT_TYPES, VISIT_TYPE_LABELS,
} from '../../../features/agenda-templates/types/agendaTemplates.types';
import type {
  AgendaTemplateDetailDto, AgendaTemplateStatus, AgendaTemplateSummary, VisitType,
} from '../../../features/agenda-templates/types/agendaTemplates.types';

interface EditorItem {
  uid: string;
  // Kept as digit STRINGS while editing (see sanitizeDigits/normalizeNonNegativeInteger) so we fully
  // control what can be typed and how leading zeros collapse. Converted to real numbers only in the
  // save payload.
  startOffsetMinutes: string;
  durationMinutes: string;
  title: string;
  description: string;
  location: string;
  responsibleRoleLabel: string;
}

interface EditorState {
  agendaTemplateId?: number;
  campusId: number | null;
  visitType: VisitType;
  name: string;
  description: string;
  status: AgendaTemplateStatus;
  items: EditorItem[];
}

interface CampusOption { id: number; name: string; }

function apiMessage(e: unknown, fallback: string): string {
  const anyErr = e as { response?: { data?: { message?: string } } };
  return anyErr?.response?.data?.message ?? fallback;
}

function httpStatus(e: unknown): number | undefined {
  return (e as { response?: { status?: number } })?.response?.status;
}

// ── Numeric input normalization ──
// Minute fields are kept as STRINGS in the editor so we fully control what the user can type
// (digits only) and how leading zeros collapse. Without this, type="number" + Number() lets values
// like "0002" / "003434" linger in the UI and slip into the payload.
const sanitizeDigits = (value: string) => value.replace(/[^\d]/g, '');
const normalizeNonNegativeInteger = (value: string): string => {
  const digits = sanitizeDigits(value);
  if (!digits) return '';
  return String(Number(digits)); // "0002" -> "2", "0000" -> "0"
};

let uidSeq = 1;
const newUid = () => `it-${uidSeq++}`;

const emptyEditor = (): EditorState => ({
  campusId: null,
  visitType: 'CAMPUS_TOUR',
  name: '',
  description: '',
  status: 'ACTIVE',
  items: [{ uid: newUid(), startOffsetMinutes: '0', durationMinutes: '30', title: '', description: '', location: '', responsibleRoleLabel: '' }],
});

export function AgendaTemplateManagement() {
  const navigate = useNavigate();

  const [filterVisitType, setFilterVisitType] = useState<VisitType | ''>('');
  const [templates, setTemplates] = useState<AgendaTemplateSummary[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const [activeId, setActiveId] = useState<number | null>(null);
  const [detail, setDetail] = useState<AgendaTemplateDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const [editor, setEditor] = useState<EditorState | null>(null);
  const [saving, setSaving] = useState(false);
  const [settingDefaultId, setSettingDefaultId] = useState<number | null>(null);

  const [campuses, setCampuses] = useState<CampusOption[]>([]);
  const activeIdRef = useRef<number | null>(null);
  activeIdRef.current = activeId;

  const loadList = useCallback(async () => {
    setListLoading(true);
    setListError(null);
    try {
      const res = await agendaTemplatesApi.list({ visitType: filterVisitType || undefined });
      setTemplates(res.templates);
      if (res.templates.length > 0 && activeIdRef.current == null) {
        setActiveId(res.templates[0].agendaTemplateId);
      }
    } catch (e) {
      setListError(apiMessage(e, 'Không tải được danh sách mẫu agenda.'));
    } finally {
      setListLoading(false);
    }
  }, [filterVisitType]);

  useEffect(() => { void loadList(); }, [loadList]);

  // Active campuses for the scope dropdown (best-effort; GLOBAL always available).
  useEffect(() => {
    (async () => {
      try {
        const { data } = await httpClient.get(API_ENDPOINTS.campuses.active);
        const raw: any[] = Array.isArray(data) ? data : (data?.campuses ?? data?.items ?? []);
        setCampuses(raw.map((c) => ({ id: c.campusId ?? c.id, name: c.name ?? c.campusName ?? `Cơ sở #${c.campusId ?? c.id}` })));
      } catch {
        setCampuses([]);
      }
    })();
  }, []);

  const loadDetail = useCallback(async (id: number) => {
    setDetailLoading(true);
    try {
      const d = await agendaTemplatesApi.detail(id);
      setDetail(d);
    } catch (e) {
      toast.error(apiMessage(e, 'Không tải được chi tiết mẫu agenda.'));
      setDetail(null);
    } finally {
      setDetailLoading(false);
    }
  }, []);

  useEffect(() => {
    if (activeId != null && editor == null) void loadDetail(activeId);
  }, [activeId, editor, loadDetail]);

  const campusName = (campusId: number | null | undefined) =>
    campusId == null ? 'Toàn hệ thống' : (campuses.find((c) => c.id === campusId)?.name ?? `Cơ sở #${campusId}`);

  // ── Editor actions ───────────────────────────────────────────────────────
  const startCreate = () => {
    setEditor(emptyEditor());
    setDetail(null);
  };

  const startEdit = () => {
    if (!detail) return;
    setEditor({
      agendaTemplateId: detail.agendaTemplateId,
      campusId: detail.campusId ?? null,
      visitType: detail.visitType,
      name: detail.name,
      description: detail.description ?? '',
      status: detail.status,
      items: agendaTemplatesAdapter.toInputItems(detail.items).map((i) => ({
        uid: newUid(),
        startOffsetMinutes: String(i.startOffsetMinutes),
        durationMinutes: String(i.durationMinutes),
        title: i.title,
        description: i.description ?? '',
        location: i.location ?? '',
        responsibleRoleLabel: i.responsibleRoleLabel ?? '',
      })),
    });
  };

  const cancelEdit = () => {
    setEditor(null);
    if (activeId != null) void loadDetail(activeId);
  };

  const patchEditor = (patch: Partial<EditorState>) => setEditor((e) => (e ? { ...e, ...patch } : e));
  const patchItem = (uid: string, patch: Partial<EditorItem>) =>
    setEditor((e) => (e ? { ...e, items: e.items.map((it) => (it.uid === uid ? { ...it, ...patch } : it)) } : e));
  const addItem = () =>
    setEditor((e) => (e ? { ...e, items: [...e.items, { uid: newUid(), startOffsetMinutes: '0', durationMinutes: '30', title: '', description: '', location: '', responsibleRoleLabel: '' }] } : e));
  const removeItem = (uid: string) =>
    setEditor((e) => (e ? { ...e, items: e.items.filter((it) => it.uid !== uid) } : e));

  const save = async () => {
    if (!editor) return;
    if (saving) return; // guard against double-submit

    // ── Front-end validation (no API call on failure) ──
    const name = editor.name.trim();
    if (!name) {
      // Distinguish "empty" from "whitespace-only" so the message is precise.
      toast.error(editor.name.length > 0
        ? 'Tên mẫu Agenda không được chỉ chứa khoảng trắng.'
        : 'Vui lòng nhập tên mẫu Agenda.');
      return;
    }
    if (editor.items.length === 0) {
      toast.error('Vui lòng thêm ít nhất 1 mục lịch trình.');
      return;
    }
    for (const it of editor.items) {
      const offset = normalizeNonNegativeInteger(it.startOffsetMinutes);
      if (offset === '') { toast.error('Vui lòng nhập số phút bắt đầu.'); return; }
      const duration = normalizeNonNegativeInteger(it.durationMinutes);
      if (duration === '') { toast.error('Vui lòng nhập thời lượng.'); return; }
      if (Number(duration) <= 0) { toast.error('Thời lượng phải là số nguyên lớn hơn 0.'); return; }
      if (!it.title.trim()) { toast.error('Vui lòng nhập tiêu đề mục lịch trình.'); return; }
    }

    // Normalize to REAL numbers right before sending — never ship "0002" / "003434" strings.
    const payload = {
      campusId: editor.campusId,
      visitType: editor.visitType,
      name,
      description: editor.description.trim() || null,
      status: editor.status,
      items: editor.items.map((i, idx) => ({
        displayOrder: idx + 1,
        startOffsetMinutes: Number(normalizeNonNegativeInteger(i.startOffsetMinutes) || '0'),
        durationMinutes: Number(normalizeNonNegativeInteger(i.durationMinutes) || '0'),
        title: i.title.trim(),
        description: i.description.trim() || null,
        location: i.location.trim() || null,
        responsibleRoleLabel: i.responsibleRoleLabel.trim() || null,
      })),
    };

    setSaving(true);
    try {
      if (editor.agendaTemplateId) {
        await agendaTemplatesApi.update(editor.agendaTemplateId, payload);
        toast.success('Cập nhật mẫu Agenda thành công. Các thay đổi của mẫu đã được lưu.');
        const id = editor.agendaTemplateId;
        setEditor(null);
        setActiveId(id);
        await Promise.all([loadList(), loadDetail(id)]);
      } else {
        const res = await agendaTemplatesApi.create(payload);
        toast.success('Tạo mẫu Agenda thành công. Mẫu mới đã được thêm vào danh sách.');
        setEditor(null);
        setActiveId(res.agendaTemplateId);
        await Promise.all([loadList(), loadDetail(res.agendaTemplateId)]);
      }
    } catch (e) {
      // Keep the form intact on failure so the user can fix and retry.
      const status = httpStatus(e);
      if (status === 409) {
        toast.error('Tên mẫu Agenda đã tồn tại. Vui lòng nhập tên khác.');
      } else if (status === 403) {
        toast.error('Bạn không có quyền lưu mẫu Agenda.');
      } else if (status === 400 || status === 422) {
        toast.error(apiMessage(e, 'Vui lòng kiểm tra lại thông tin mẫu Agenda.'));
      } else {
        toast.error('Đã xảy ra lỗi hệ thống. Không thể lưu mẫu Agenda lúc này. Vui lòng thử lại sau.');
      }
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!detail) return;
    if (!window.confirm(`Xóa mẫu "${detail.name}"?`)) return;
    try {
      await agendaTemplatesApi.remove(detail.agendaTemplateId);
      toast.success('Đã xóa mẫu lịch trình.');
      setActiveId(null);
      setDetail(null);
      await loadList();
    } catch (e) {
      toast.error(apiMessage(e, 'Không thể thực hiện thao tác. Vui lòng thử lại.'));
    }
  };

  const setAsDefault = async () => {
    if (!detail) return;
    try {
      await agendaTemplatesApi.setDefault({
        campusId: detail.campusId ?? null,
        visitType: detail.visitType,
        agendaTemplateId: detail.agendaTemplateId,
      });
      toast.success('Đã đặt mẫu mặc định cho loại chuyến thăm này.');
      await Promise.all([loadList(), loadDetail(detail.agendaTemplateId)]);
    } catch (e) {
      toast.error(apiMessage(e, 'Không thể đặt mẫu mặc định. Vui lòng thử lại.'));
    }
  };

  const handleSetDefaultFromList = async (template: AgendaTemplateSummary) => {
    if (template.isDefault) return;
    if (template.status === 'INACTIVE') {
      toast.error('Không thể đặt template INACTIVE làm mặc định.');
      return;
    }
    setSettingDefaultId(template.agendaTemplateId);
    try {
      await agendaTemplatesApi.setDefault({
        campusId: template.campusId ?? null,
        visitType: template.visitType,
        agendaTemplateId: template.agendaTemplateId,
      });
      toast.success('Đã đặt mẫu mặc định cho loại chuyến thăm này.');
      await Promise.all([loadList(), activeId === template.agendaTemplateId ? loadDetail(template.agendaTemplateId) : Promise.resolve()]);
    } catch (e) {
      toast.error(apiMessage(e, 'Không thể đặt mẫu mặc định. Vui lòng thử lại.'));
    } finally {
      setSettingDefaultId(null);
    }
  };

  const isEditing = editor != null;

  return (
    <div className="flex-1 w-full bg-[#f8fbff] min-h-[calc(100vh-64px)]">
      {/* This page renders its own toasts — without a Toaster mounted here, every toast.success /
          toast.error (save, validation, errors) is silently swallowed = "no feedback". */}
      <Toaster position="top-right" />
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500 px-4 md:px-8 mt-4">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/visit')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý campus</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý mẫu Agenda</span>
      </div>

      <div className="max-w-[1400px] mx-auto px-4 md:px-8 pb-12 w-full">
        <div className="mb-8 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý mẫu Agenda</h1>
            <p className="text-gray-500 mt-2 font-medium">Tạo và quản lý lịch trình mẫu theo loại hình visit và phạm vi cơ sở.</p>
          </div>
          <div className="flex items-center gap-3">
            <select
              value={filterVisitType}
              onChange={(e) => setFilterVisitType(e.target.value as VisitType | '')}
              className="px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm"
            >
              <option value="">Tất cả loại hình</option>
              {VISIT_TYPES.map((vt) => <option key={vt} value={vt}>{VISIT_TYPE_LABELS[vt]}</option>)}
            </select>
            {!isEditing && (
              <button
                onClick={startCreate}
                className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-orange-600 outline-none text-white px-5 py-2.5 rounded-xl font-bold shadow-sm transition-colors"
              >
                <Plus className="w-5 h-5" /> Thêm mẫu mới
              </button>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          {/* Left: template list */}
          <div className={`lg:col-span-4 ${isEditing ? 'opacity-50 pointer-events-none transition-opacity' : ''}`}>
            <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">
              <div className="p-4 border-b border-gray-100 bg-gray-50/50 flex items-center justify-between">
                <h3 className="font-bold text-gray-800 flex items-center gap-2">
                  <Settings2 className="w-5 h-5 text-[#004c91]" /> Danh sách mẫu
                </h3>
                {listLoading && <Loader2 className="w-4 h-4 animate-spin text-gray-400" />}
              </div>
              {listError && (
                <div className="p-4 text-sm text-red-600 flex items-center gap-2"><AlertCircle className="w-4 h-4" />{listError}</div>
              )}
              <div className="p-2 space-y-1 max-h-[640px] overflow-y-auto">
                {templates.map((t) => (
                  <button
                    key={t.agendaTemplateId}
                    onClick={() => setActiveId(t.agendaTemplateId)}
                    className={`w-full text-left p-4 rounded-xl transition-all border outline-none ${
                      activeId === t.agendaTemplateId ? 'bg-blue-50 border-[#004c91]/30 shadow-sm relative overflow-hidden' : 'bg-white border-transparent hover:bg-gray-50'
                    }`}
                  >
                    {activeId === t.agendaTemplateId && <div className="absolute left-0 top-0 bottom-0 w-1 bg-[#004c91]" />}
                    <div className="flex items-start justify-between gap-2">
                      <h4 className={`font-bold text-base mb-1 ${activeId === t.agendaTemplateId ? 'text-[#004c91]' : 'text-gray-800'}`}>{t.name}</h4>
                      <button
                        type="button"
                        title={t.isDefault ? 'Mẫu mặc định hiện tại' : (t.status === 'INACTIVE' ? 'Không thể đặt mẫu inactive làm mặc định' : 'Đặt làm mẫu mặc định')}
                        aria-label={t.isDefault ? 'Mẫu mặc định hiện tại' : 'Đặt làm mẫu mặc định'}
                        disabled={settingDefaultId === t.agendaTemplateId || t.status === 'INACTIVE' || settingDefaultId != null}
                        onClick={(event) => {
                          event.stopPropagation();
                          handleSetDefaultFromList(t);
                        }}
                        className={`inline-flex h-9 w-9 items-center justify-center rounded-xl transition-colors shrink-0 ${
                          t.isDefault
                            ? 'text-orange-400 hover:bg-orange-50'
                            : 'text-slate-300 hover:bg-orange-50 hover:text-orange-400'
                        } ${(settingDefaultId === t.agendaTemplateId || t.status === 'INACTIVE' || settingDefaultId != null) ? 'cursor-not-allowed opacity-50 hover:bg-transparent' : ''}`}
                      >
                        <Star className={`w-5 h-5 ${t.isDefault ? 'fill-orange-400 text-orange-400' : 'text-slate-300'}`} />
                      </button>
                    </div>
                    <div className="flex flex-wrap items-center gap-1.5 text-xs font-medium text-gray-500 mb-1">
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-gray-100 rounded-full">{VISIT_TYPE_LABELS[t.visitType]}</span>
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-gray-100 rounded-full">
                        {t.campusId == null ? <Globe className="w-3 h-3" /> : <Building2 className="w-3 h-3" />}
                        {campusName(t.campusId)}
                      </span>
                      {t.status === 'INACTIVE' && <span className="px-2 py-0.5 bg-gray-200 text-gray-600 rounded-full">INACTIVE</span>}
                    </div>
                    <p className="text-xs text-gray-400">{t.itemCount} mục lịch trình</p>
                  </button>
                ))}
                {!listLoading && templates.length === 0 && (
                  <div className="text-center py-10 text-gray-400 text-sm font-medium">Chưa có mẫu agenda nào.</div>
                )}
              </div>
            </div>
          </div>

          {/* Right: detail / editor */}
          <div className="lg:col-span-8">
            {!isEditing ? (
              <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden h-full">
                {detailLoading ? (
                  <div className="flex items-center justify-center h-full min-h-[400px] text-gray-400"><Loader2 className="w-8 h-8 animate-spin" /></div>
                ) : detail ? (
                  <div className="p-6 md:p-8">
                    <div className="flex flex-wrap justify-between items-start gap-4 mb-6">
                      <div>
                        <div className="flex flex-wrap items-center gap-2 mb-3">
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-gray-100 text-gray-600 rounded-full text-xs font-bold border border-gray-200">{VISIT_TYPE_LABELS[detail.visitType]}</span>
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-blue-50 text-[#004c91] rounded-full text-xs font-bold border border-blue-100">
                            {detail.campusId == null ? <Globe className="w-3.5 h-3.5" /> : <Building2 className="w-3.5 h-3.5" />}{campusName(detail.campusId)}
                          </span>
                          {detail.isDefault && <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-orange-50 text-[#f37021] rounded-full text-xs font-bold border border-orange-100"><Star className="w-3.5 h-3.5 fill-[#f37021]" /> Mặc định</span>}
                          {detail.status === 'INACTIVE' && <span className="px-3 py-1 bg-gray-200 text-gray-600 rounded-full text-xs font-bold">INACTIVE</span>}
                        </div>
                        <h2 className="text-2xl font-bold text-gray-900">{detail.name}</h2>
                        {detail.description && <p className="text-gray-500 mt-2 font-medium leading-relaxed">{detail.description}</p>}
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {!detail.isDefault && detail.status === 'ACTIVE' && !detail.isDeleted && (
                          <button onClick={setAsDefault} className="flex items-center gap-1.5 px-4 py-2 bg-[#f37021]/10 text-[#f37021] font-bold rounded-xl hover:bg-[#f37021] hover:text-white transition-colors outline-none">
                            <Star className="w-4 h-4" /> Đặt mặc định
                          </button>
                        )}
                        <button onClick={startEdit} className="flex items-center gap-1.5 px-4 py-2 bg-[#004c91]/10 text-[#004c91] font-bold rounded-xl hover:bg-[#004c91] hover:text-white transition-colors outline-none">
                          <Edit2 className="w-4 h-4" /> Chỉnh sửa
                        </button>
                        <button onClick={remove} className="flex items-center gap-1.5 px-4 py-2 bg-red-50 text-red-600 font-bold rounded-xl hover:bg-red-600 hover:text-white transition-colors outline-none">
                          <Trash2 className="w-4 h-4" /> Xóa
                        </button>
                      </div>
                    </div>

                    <div className="mt-8">
                      <h3 className="text-lg font-bold text-gray-800 mb-4 border-b border-gray-100 pb-2">Chi tiết lịch trình</h3>
                      <div className="space-y-4">
                        {[...detail.items].sort((a, b) => a.startOffsetMinutes - b.startOffsetMinutes || a.displayOrder - b.displayOrder).map((item) => (
                          <div key={item.agendaTemplateItemId} className="flex gap-4 p-4 rounded-xl border border-gray-100 bg-gray-50">
                            <div className="shrink-0 w-40 flex flex-col pt-0.5">
                              <span className="text-[#f37021] font-bold text-sm flex items-center gap-1.5"><Clock className="w-4 h-4" /> Bắt đầu sau {item.startOffsetMinutes} phút</span>
                              <span className="text-xs text-gray-400 mt-0.5 ml-5">Thời lượng {item.durationMinutes} phút</span>
                            </div>
                            <div className="flex-1">
                              <h4 className="font-bold text-gray-900 mb-2">{item.title}</h4>
                              {item.description && <p className="text-sm text-gray-500 mb-2">{item.description}</p>}
                              <div className="flex flex-wrap gap-4 text-sm font-medium text-gray-500">
                                {item.responsibleRoleLabel && <span className="flex items-center gap-1.5"><User className="w-4 h-4" /> Gợi ý phụ trách: {item.responsibleRoleLabel}</span>}
                                {item.location && <span className="flex items-center gap-1.5"><MapPin className="w-4 h-4" /> {item.location}</span>}
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col items-center justify-center h-full min-h-[400px] text-gray-400">
                    <FileText className="w-16 h-16 text-gray-200 mb-4" />
                    <p className="font-medium">Chọn một mẫu hoặc tạo mẫu mới.</p>
                  </div>
                )}
              </div>
            ) : (
              <AgendaEditor
                editor={editor!}
                campuses={campuses}
                saving={saving}
                onPatch={patchEditor}
                onPatchItem={patchItem}
                onAddItem={addItem}
                onRemoveItem={removeItem}
                onSave={save}
                onCancel={cancelEdit}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

interface EditorProps {
  editor: EditorState;
  campuses: CampusOption[];
  saving: boolean;
  onPatch: (patch: Partial<EditorState>) => void;
  onPatchItem: (uid: string, patch: Partial<EditorItem>) => void;
  onAddItem: () => void;
  onRemoveItem: (uid: string) => void;
  onSave: () => void;
  onCancel: () => void;
}

function AgendaEditor({ editor, campuses, saving, onPatch, onPatchItem, onAddItem, onRemoveItem, onSave, onCancel }: EditorProps) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-[#004c91]/20 overflow-hidden ring-4 ring-[#004c91]/5">
      <div className="p-6 md:p-8">
        <div className="flex justify-between items-center mb-6 border-b border-gray-100 pb-4">
          <h2 className="text-xl font-bold text-[#004c91]">{editor.agendaTemplateId ? 'Chỉnh sửa mẫu Agenda' : 'Tạo mẫu Agenda mới'}</h2>
          <div className="flex items-center gap-2">
            <button type="button" onClick={onCancel} disabled={saving} className="px-4 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-100 transition-colors outline-none disabled:opacity-50">Hủy</button>
            <button type="button" onClick={onSave} disabled={saving} className="px-5 py-2 flex items-center gap-2 bg-[#004c91] hover:bg-[#00386b] text-white font-bold rounded-xl shadow-sm transition-colors outline-none disabled:opacity-60">
              {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} {saving ? 'Đang lưu...' : 'Lưu'}
            </button>
          </div>
        </div>

        <div className="space-y-5">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1.5">Loại hình visit <span className="text-red-500">*</span></label>
              <select value={editor.visitType} onChange={(e) => onPatch({ visitType: e.target.value as VisitType })}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm">
                {VISIT_TYPES.map((vt) => <option key={vt} value={vt}>{VISIT_TYPE_LABELS[vt]}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1.5">Phạm vi</label>
              <select value={editor.campusId == null ? '' : String(editor.campusId)} onChange={(e) => onPatch({ campusId: e.target.value === '' ? null : Number(e.target.value) })}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm">
                <option value="">Toàn hệ thống (GLOBAL)</option>
                {campuses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="md:col-span-2">
              <label className="block text-sm font-bold text-gray-700 mb-1.5">Tên mẫu <span className="text-red-500">*</span></label>
              <input type="text" value={editor.name} onChange={(e) => onPatch({ name: e.target.value })} maxLength={150}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm" />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1.5">Trạng thái</label>
              <select value={editor.status} onChange={(e) => onPatch({ status: e.target.value as AgendaTemplateStatus })}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm">
                <option value="ACTIVE">ACTIVE</option>
                <option value="INACTIVE">INACTIVE</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1.5">Mô tả</label>
            <textarea value={editor.description} onChange={(e) => onPatch({ description: e.target.value })}
              className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] outline-none font-medium text-gray-900 bg-white shadow-sm min-h-[72px]" />
          </div>

          <div className="pt-4 border-t border-gray-100">
            <div className="flex items-center justify-between mb-1">
              <h3 className="text-base font-bold text-gray-800">Các mục lịch trình</h3>
              <button type="button" onClick={onAddItem} className="px-3 py-1.5 bg-[#f37021]/10 text-[#f37021] font-bold rounded-lg hover:bg-[#f37021] hover:text-white transition-colors flex items-center gap-1.5 text-sm">
                <Plus className="w-4 h-4" /> Thêm mục
              </button>
            </div>
            <p className="text-xs text-gray-400 mb-4">“Bắt đầu sau” là số phút tính từ giờ bắt đầu dự kiến của chuyến. “Thời lượng” phải lớn hơn 0. Khi áp dụng mẫu vào chuyến thật, hệ thống sẽ tự tính giờ bắt đầu và giờ kết thúc.</p>

            <div className="space-y-3">
              {editor.items.map((item, idx) => (
                <div key={item.uid} className="relative group rounded-2xl border border-slate-200 bg-slate-50/60 p-4 transition-colors">
                  <div className="grid grid-cols-1 gap-4 lg:grid-cols-[140px_130px_minmax(0,1fr)_44px] lg:items-start">
                    <div>
                      <label className="mb-1.5 block min-h-[32px] text-xs font-bold leading-tight text-slate-500">Bắt đầu sau (phút)</label>
                      <input type="text" inputMode="numeric" value={item.startOffsetMinutes}
                        onChange={(e) => onPatchItem(item.uid, { startOffsetMinutes: sanitizeDigits(e.target.value) })}
                        onBlur={(e) => onPatchItem(item.uid, { startOffsetMinutes: normalizeNonNegativeInteger(e.target.value) })}
                        className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                    </div>
                    <div>
                      <label className="mb-1.5 block min-h-[32px] text-xs font-bold leading-tight text-slate-500">Thời lượng (phút)</label>
                      <input type="text" inputMode="numeric" value={item.durationMinutes}
                        onChange={(e) => onPatchItem(item.uid, { durationMinutes: sanitizeDigits(e.target.value) })}
                        onBlur={(e) => onPatchItem(item.uid, { durationMinutes: normalizeNonNegativeInteger(e.target.value) })}
                        className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                    </div>
                    <div>
                      <label className="mb-1.5 block min-h-[32px] text-xs font-bold leading-tight text-slate-500">Tiêu đề <span className="text-red-500">*</span></label>
                      <input type="text" value={item.title} onChange={(e) => onPatchItem(item.uid, { title: e.target.value })} maxLength={255}
                        className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                    </div>
                    <div className="flex justify-end lg:block">
                      <button type="button" onClick={() => onRemoveItem(item.uid)} className="lg:mt-[38px] inline-flex h-11 w-11 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-400 transition-colors hover:border-red-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-50">
                        <Trash2 className="w-5 h-5" />
                      </button>
                    </div>
                  </div>

                  <div className="mt-4">
                    <label className="mb-1.5 block min-h-[16px] text-xs font-bold leading-tight text-slate-500">Mô tả</label>
                    <input type="text" value={item.description} onChange={(e) => onPatchItem(item.uid, { description: e.target.value })}
                      className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                  </div>

                  <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
                    <div>
                      <label className="mb-1.5 block min-h-[16px] text-xs font-bold leading-tight text-slate-500">Địa điểm</label>
                      <input type="text" value={item.location} onChange={(e) => onPatchItem(item.uid, { location: e.target.value })} maxLength={255}
                        className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                    </div>
                    <div>
                      <label className="mb-1.5 block min-h-[16px] text-xs font-bold leading-tight text-slate-500">Vai trò phụ trách gợi ý</label>
                      <input type="text" value={item.responsibleRoleLabel} onChange={(e) => onPatchItem(item.uid, { responsibleRoleLabel: e.target.value })} maxLength={150}
                        placeholder="VD: IC Host, IC Support, Student Support"
                        className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition-colors focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10" />
                      <p className="mt-1.5 text-xs font-medium text-slate-400">Nhập vai trò nên phụ trách mục này trong mẫu, ví dụ: IC Host, IC Support, Student Support. Đây chỉ là gợi ý, không phải phân công người cụ thể.</p>
                    </div>
                  </div>

                  <div className="mt-3 text-xs font-medium text-slate-400">
                    Bắt đầu sau {normalizeNonNegativeInteger(item.startOffsetMinutes) || '0'} phút · Thời lượng {normalizeNonNegativeInteger(item.durationMinutes) || '0'} phút (mục #{idx + 1})
                  </div>
                </div>
              ))}
              {editor.items.length === 0 && (
                <div className="text-center py-8 border-2 border-dashed border-gray-200 rounded-xl">
                  <p className="text-sm font-medium text-gray-500">Chưa có mục nào. Hãy thêm mục để hoàn thành mẫu agenda.</p>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

export default AgendaTemplateManagement;
