/**
 * AgendaSetupPanel
 * Host-facing setup for a campus instance: auto-selects the default agenda template
 * (campus scope → GLOBAL fallback), lets the host pick another, previews the resulting
 * concrete agenda (absolute date + time = planned_start_at + offsets) and applies it into
 * visit_agendas. Backend is the source of truth for the computed times.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Wand2, Clock, MapPin, Loader2, AlertTriangle, Star, Globe, Building2 } from 'lucide-react';
import toast from 'react-hot-toast';
import agendaTemplatesApi from '../api/agendaTemplatesApi';
import {
  VISIT_TYPE_LABELS,
} from '../types/agendaTemplates.types';
import type { AgendaSetupForInstance, AgendaTemplateDto } from '../types/agendaTemplates.types';

function apiMessage(e: unknown, fallback: string): string {
  const anyErr = e as { response?: { data?: { message?: string } } };
  return anyErr?.response?.data?.message ?? fallback;
}

function fmtDateTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getDate())}/${p(d.getMonth() + 1)} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

interface Props {
  visitInstanceId: number;
  /** Called after a successful apply so the parent can refresh its agenda view. */
  onApplied?: () => void | Promise<void>;
}

export function AgendaSetupPanel({ visitInstanceId, onApplied }: Props) {
  const [setup, setSetup] = useState<AgendaSetupForInstance | null>(null);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [preview, setPreview] = useState<AgendaTemplateDto | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [replaceExisting, setReplaceExisting] = useState(true);
  const [applying, setApplying] = useState(false);

  const loadSetup = useCallback(async () => {
    setLoading(true);
    try {
      const s = await agendaTemplatesApi.getSetupForInstance(visitInstanceId);
      setSetup(s);
      setSelectedId(s.defaultTemplateId ?? (s.selectableTemplates[0]?.agendaTemplateId ?? null));
      setReplaceExisting(s.hasExistingAgenda);
    } catch (e) {
      toast.error(apiMessage(e, 'Không tải được thiết lập lịch trình.'));
      setSetup(null);
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId]);

  useEffect(() => { void loadSetup(); }, [loadSetup]);

  // Preview the selected template's items (with absolute time relative to planned_start_at).
  useEffect(() => {
    if (selectedId == null) { setPreview(null); return; }
    let cancelled = false;
    setPreviewLoading(true);
    agendaTemplatesApi.detail(selectedId)
      .then((d) => { if (!cancelled) setPreview(d); })
      .catch(() => { if (!cancelled) setPreview(null); })
      .finally(() => { if (!cancelled) setPreviewLoading(false); });
    return () => { cancelled = true; };
  }, [selectedId]);

  const apply = async () => {
    if (!setup || selectedId == null || applying) return;
    setApplying(true);
    try {
      const res = await agendaTemplatesApi.apply({
        visitInstanceId,
        agendaTemplateId: selectedId,
        replaceExisting,
      });
      toast.success(res.message);
      if (res.visitTypeMismatch) {
        toast(`Lưu ý: mẫu thuộc loại "${VISIT_TYPE_LABELS[res.templateVisitType]}", khác loại hình của đơn "${VISIT_TYPE_LABELS[res.requestVisitType]}".`, { icon: '⚠️' });
      }
      await loadSetup();
      if (onApplied) await onApplied();
    } catch (e) {
      toast.error(apiMessage(e, 'Áp dụng mẫu lịch trình thất bại.'));
    } finally {
      setApplying(false);
    }
  };

  if (loading) {
    return (
      <div className="mb-4 p-4 rounded-xl border border-gray-200 bg-white flex items-center gap-2 text-gray-400 text-sm">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải thiết lập lịch trình…
      </div>
    );
  }
  if (!setup) return null;

  const planned = setup.plannedStartAt;
  const base = new Date(planned);

  return (
    <div className="mb-5 p-4 md:p-5 rounded-xl border border-[#004c91]/20 bg-blue-50/40">
      <div className="flex items-center gap-2 mb-3">
        <Wand2 className="w-5 h-5 text-[#004c91]" />
        <h4 className="font-bold text-[#004c91]">Áp dụng mẫu Agenda</h4>
        <span className="ml-auto text-xs font-medium text-gray-500 inline-flex items-center gap-1">
          Loại hình đơn: <span className="px-2 py-0.5 bg-white rounded-full border border-gray-200">{VISIT_TYPE_LABELS[setup.visitType]}</span>
        </span>
      </div>

      {setup.selectableTemplates.length === 0 ? (
        <p className="text-sm text-gray-500">Chưa có mẫu agenda khả dụng cho cơ sở này. Vui lòng tạo mẫu ở mục “Quản lý mẫu Agenda”, hoặc nhập lịch trình thủ công bên dưới.</p>
      ) : (
        <>
          <div className="flex flex-col md:flex-row md:items-end gap-3">
            <div className="flex-1">
              <label className="block text-xs font-bold text-gray-500 mb-1">Chọn mẫu</label>
              <select
                value={selectedId == null ? '' : String(selectedId)}
                onChange={(e) => setSelectedId(e.target.value === '' ? null : Number(e.target.value))}
                disabled={!setup.canApply}
                className="w-full px-3 py-2 bg-white rounded-lg border border-gray-300 focus:border-[#004c91] outline-none text-sm font-medium disabled:opacity-60"
              >
                {setup.selectableTemplates.map((t) => (
                  <option key={t.agendaTemplateId} value={t.agendaTemplateId}>
                    {t.name} · {VISIT_TYPE_LABELS[t.visitType]} · {t.campusId == null ? 'GLOBAL' : `Cơ sở #${t.campusId}`}{t.isDefault ? ' ★ mặc định' : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex items-center gap-3">
              {setup.hasExistingAgenda && (
                <label className="inline-flex items-center gap-2 text-sm font-medium text-gray-600 select-none">
                  <input type="checkbox" checked={replaceExisting} onChange={(e) => setReplaceExisting(e.target.checked)} disabled={!setup.canApply} />
                  Thay thế lịch trình hiện tại
                </label>
              )}
              <button
                onClick={apply}
                disabled={!setup.canApply || selectedId == null || applying}
                className="px-4 py-2 flex items-center gap-2 bg-[#004c91] hover:bg-[#00386b] text-white font-bold rounded-lg shadow-sm transition-colors outline-none disabled:opacity-50"
              >
                {applying ? <Loader2 className="w-4 h-4 animate-spin" /> : <Wand2 className="w-4 h-4" />} Áp dụng
              </button>
            </div>
          </div>

          {setup.defaultScope && (
            <p className="mt-2 text-xs text-gray-500 inline-flex items-center gap-1">
              {setup.defaultScope === 'GLOBAL' ? <Globe className="w-3.5 h-3.5" /> : <Building2 className="w-3.5 h-3.5" />}
              Mẫu mặc định: phạm vi {setup.defaultScope === 'GLOBAL' ? 'toàn hệ thống' : 'theo cơ sở'} <Star className="w-3 h-3 text-[#f37021] fill-[#f37021]" />
            </p>
          )}

          {setup.hasExistingAgenda && (
            <div className="mt-3 p-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-sm flex items-start gap-2">
              <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
              <span>Cơ sở này đã có lịch trình. Áp dụng mẫu mới {replaceExisting ? 'sẽ thay thế' : 'cần bật “Thay thế” để ghi đè'} agenda hiện tại.</span>
            </div>
          )}

          {!setup.canApply && (
            <p className="mt-3 text-xs text-gray-500">Chỉ Host phụ trách mới được áp dụng mẫu, và chỉ trong giai đoạn chuẩn bị (trước tiếp khách).</p>
          )}

          {/* Preview: absolute date + time computed from planned_start_at + template offsets */}
          <div className="mt-4">
            <div className="text-xs font-bold text-gray-500 mb-2">
              Xem trước (theo giờ bắt đầu dự kiến {fmtDateTime(planned)})
              {previewLoading && <Loader2 className="inline w-3 h-3 animate-spin ml-2" />}
            </div>
            <div className="space-y-2">
              {preview?.items
                ? [...preview.items]
                    .sort((a, b) => a.startOffsetMinutes - b.startOffsetMinutes || a.displayOrder - b.displayOrder)
                    .map((it) => {
                      const start = new Date(base.getTime() + it.startOffsetMinutes * 60_000);
                      const end = new Date(start.getTime() + it.durationMinutes * 60_000);
                      return (
                        <div key={it.agendaTemplateItemId} className="flex gap-3 p-2.5 rounded-lg bg-white border border-gray-100 text-sm">
                          <span className="shrink-0 w-28 text-[#f37021] font-bold flex items-center gap-1"><Clock className="w-3.5 h-3.5" /> {fmtDateTime(start.toISOString()).slice(-5)}–{fmtDateTime(end.toISOString()).slice(-5)}</span>
                          <span className="flex-1">
                            <span className="font-semibold text-gray-800">{it.title}</span>
                            {it.location && <span className="ml-2 text-gray-400 inline-flex items-center gap-1"><MapPin className="w-3 h-3" />{it.location}</span>}
                          </span>
                        </div>
                      );
                    })
                : <p className="text-xs text-gray-400">Không có mục để xem trước.</p>}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default AgendaSetupPanel;
