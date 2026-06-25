/**
 * AgendaSetupPanel
 * Host-facing setup for a campus instance: auto-selects the default agenda template
 * (campus scope → GLOBAL fallback), lets the host pick another, previews the template that
 * WILL be applied (absolute time = planned_start_at + offsets) and applies it into visit_agendas.
 *
 * Template items are read from the setup response's selectableTemplates (each option embeds its
 * items). We deliberately do NOT call the management detail endpoint here — that is gated to
 * HO / Staff Leader, while the setup user is usually a plain-Staff Host. Backend remains the
 * source of truth for the computed times. Notifications use the parent's top-right toast.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Wand2, Clock, MapPin, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import agendaTemplatesApi from '../api/agendaTemplatesApi';
import { VISIT_TYPE_LABELS } from '../types/agendaTemplates.types';
import type { AgendaSetupForInstance } from '../types/agendaTemplates.types';

type NotifyType = 'success' | 'error' | 'warning' | 'info';

function apiMessage(e: unknown, fallback: string): string {
  const anyErr = e as { response?: { data?: { message?: string } } };
  return anyErr?.response?.data?.message ?? fallback;
}

function statusOf(e: unknown): number | undefined {
  return (e as { response?: { status?: number } })?.response?.status;
}

function fmtDateTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getDate())}/${p(d.getMonth() + 1)} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

function hhmm(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getHours())}:${p(d.getMinutes())}`;
}

interface Props {
  visitInstanceId: number;
  /** Called after a successful apply so the parent can refresh its agenda view. */
  onApplied?: () => void | Promise<void>;
  /** Reuse the parent page's top-right toast. Falls back to react-hot-toast when omitted. */
  notify?: (type: NotifyType, msg: string) => void;
}

export function AgendaSetupPanel({ visitInstanceId, onApplied, notify: propNotify }: Props) {
  const [setup, setSetup] = useState<AgendaSetupForInstance | null>(null);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [replaceExisting, setReplaceExisting] = useState(false);
  const [applying, setApplying] = useState(false);

  const notify = useCallback((type: NotifyType, msg: string) => {
    if (propNotify) { propNotify(type, msg); return; }
    if (type === 'success') toast.success(msg);
    else if (type === 'error') toast.error(msg);
    else toast(msg, { icon: type === 'warning' ? '⚠️' : 'ℹ️' });
  }, [propNotify]);

  const loadSetup = useCallback(async () => {
    setLoading(true);
    try {
      const s = await agendaTemplatesApi.getSetupForInstance(visitInstanceId);
      setSetup(s);
      setSelectedId(s.defaultTemplateId ?? (s.selectableTemplates[0]?.agendaTemplateId ?? null));
      // Never pre-check replace — host must opt in to overwriting an existing agenda.
      setReplaceExisting(false);
    } catch (e) {
      notify('error', apiMessage(e, 'Không tải được thiết lập lịch trình.'));
      setSetup(null);
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId, notify]);

  useEffect(() => { void loadSetup(); }, [loadSetup]);

  const apply = async () => {
    if (!setup || selectedId == null || applying) return;

    // Guard before hitting the API: an existing agenda is only overwritten when the host opts in.
    if (setup.hasExistingAgenda && !replaceExisting) {
      notify('warning', 'Cơ sở này đã có lịch trình. Vui lòng chọn “Thay thế lịch trình hiện tại” nếu muốn áp dụng mẫu mới.');
      return;
    }

    setApplying(true);
    try {
      const res = await agendaTemplatesApi.apply({ visitInstanceId, agendaTemplateId: selectedId, replaceExisting });
      notify('success', 'Đã áp dụng mẫu lịch trình vào chuyến tiếp khách.');
      if (res.visitTypeMismatch) {
        notify('warning', `Lưu ý: mẫu thuộc loại “${VISIT_TYPE_LABELS[res.templateVisitType]}”, khác loại hình “${VISIT_TYPE_LABELS[res.requestVisitType]}” khách đã chọn.`);
      }
      await loadSetup();
      if (onApplied) await onApplied();
    } catch (e) {
      if (statusOf(e) === 409) {
        notify('error', 'Cơ sở này đã có lịch trình. Vui lòng chọn “Thay thế lịch trình hiện tại”.');
      } else {
        notify('error', apiMessage(e, 'Không thể áp dụng mẫu lịch trình. Vui lòng thử lại.'));
      }
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
  const disabled = !setup.canApply || applying;
  const selectedTemplate = setup.selectableTemplates.find((t) => t.agendaTemplateId === selectedId) ?? null;
  const applyLabel = applying
    ? 'Đang áp dụng…'
    : (setup.hasExistingAgenda && replaceExisting ? 'Thay thế lịch trình' : 'Áp dụng');

  const previewItems = selectedTemplate
    ? [...selectedTemplate.items].sort((a, b) => a.startOffsetMinutes - b.startOffsetMinutes || a.displayOrder - b.displayOrder)
    : [];

  const scopeLabel = (campusId?: number | null) => (campusId == null ? 'GLOBAL' : `Cơ sở #${campusId}`);

  return (
    <div className="mb-5 p-4 md:p-5 rounded-xl border border-[#004c91]/20 bg-blue-50/40">
      {/* Header */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="flex min-w-0 items-center gap-2">
          <Wand2 className="h-5 w-5 shrink-0 text-[#004c91]" />
          <h4 className="text-base font-bold text-[#004c91]">Áp dụng mẫu Agenda</h4>
        </div>
        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-semibold text-slate-600">
          <span className="text-slate-400">Theo form đăng ký:</span>
          <span>{VISIT_TYPE_LABELS[setup.visitType]}</span>
        </div>
      </div>

      {setup.selectableTemplates.length === 0 ? (
        <p className="mt-3 text-sm text-gray-500">Chưa có mẫu agenda khả dụng cho cơ sở này. Vui lòng tạo mẫu ở mục “Quản lý mẫu Agenda”, hoặc nhập lịch trình thủ công bên dưới.</p>
      ) : (
        <>
          {/* Row 1 — full-width dropdown */}
          <div className="mt-4">
            <label className="mb-1.5 block text-xs font-bold text-slate-500">Chọn mẫu</label>
            <select
              value={selectedId == null ? '' : String(selectedId)}
              onChange={(e) => setSelectedId(e.target.value === '' ? null : Number(e.target.value))}
              disabled={disabled}
              className="h-11 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10 disabled:opacity-60"
            >
              {setup.selectableTemplates.map((t) => (
                <option key={t.agendaTemplateId} value={t.agendaTemplateId}>
                  {t.name}{t.isDefault ? ' ★' : ''}
                </option>
              ))}
            </select>
            {selectedTemplate && (
              <p className="mt-2 text-xs font-medium text-slate-500">
                {VISIT_TYPE_LABELS[selectedTemplate.visitType]} · {scopeLabel(selectedTemplate.campusId)}
                {selectedTemplate.isDefault ? ' · Mặc định' : ''}
              </p>
            )}
          </div>

          {/* Row 2 — replace toggle + apply button */}
          <div className="mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            {setup.hasExistingAgenda ? (
              <label className="inline-flex items-center gap-2 text-sm font-semibold text-slate-600 select-none">
                <input
                  type="checkbox"
                  checked={replaceExisting}
                  disabled={disabled}
                  onChange={(e) => {
                    const v = e.target.checked;
                    setReplaceExisting(v);
                    if (v) notify('warning', 'Bạn đang chọn thay thế lịch trình hiện tại. Agenda cũ sẽ bị thay bằng mẫu mới.');
                  }}
                />
                <span>Thay thế lịch trình hiện tại</span>
              </label>
            ) : (
              <span className="text-sm font-medium text-slate-400">Chưa có lịch trình hiện tại.</span>
            )}
            <button
              onClick={apply}
              disabled={disabled || selectedId == null}
              className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-[#004c91] px-5 text-sm font-bold text-white hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-60"
            >
              {applying ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />} {applyLabel}
            </button>
          </div>

          {setup.hasExistingAgenda && !replaceExisting && (
            <p className="mt-2 text-xs font-medium text-slate-500">Có lịch trình hiện tại — tick “Thay thế” nếu muốn ghi đè.</p>
          )}
          {!setup.canApply && (
            <p className="mt-2 text-xs text-slate-500">Chỉ Host phụ trách mới được áp dụng mẫu, và chỉ trong giai đoạn chuẩn bị (trước tiếp khách).</p>
          )}

          {/* Preview of the template that WILL be applied (not the saved agenda below) */}
          <div className="mt-5">
            <h4 className="text-sm font-bold text-slate-700">Xem trước mẫu sẽ áp dụng</h4>
            <p className="text-xs font-medium text-slate-400">Tính theo giờ bắt đầu dự kiến: {fmtDateTime(planned)}</p>

            {selectedId == null ? (
              <p className="mt-3 text-xs text-slate-400">Chọn một mẫu để xem trước lịch trình.</p>
            ) : previewItems.length === 0 ? (
              <p className="mt-3 text-xs text-slate-400">Mẫu này chưa có mục lịch trình.</p>
            ) : (
              <div className="mt-3 rounded-xl border border-slate-200 bg-white overflow-hidden">
                <div className="hidden sm:grid grid-cols-[130px_minmax(0,1fr)_170px] border-b border-slate-100 px-4 py-2 text-[11px] font-bold uppercase tracking-wide text-slate-400">
                  <span>Thời gian</span>
                  <span>Nội dung</span>
                  <span>Địa điểm</span>
                </div>
                {previewItems.map((it) => {
                  const start = new Date(base.getTime() + it.startOffsetMinutes * 60_000);
                  const end = new Date(start.getTime() + it.durationMinutes * 60_000);
                  return (
                    <div
                      key={it.agendaTemplateItemId}
                      className="grid grid-cols-1 sm:grid-cols-[130px_minmax(0,1fr)_170px] gap-0.5 sm:gap-4 px-4 py-2.5 text-sm border-b border-slate-50 last:border-0"
                    >
                      <span className="font-semibold text-[#f37021] sm:text-slate-700 inline-flex items-center gap-1">
                        <Clock className="h-3.5 w-3.5 shrink-0" /> {hhmm(start)} – {hhmm(end)}
                      </span>
                      <span className="min-w-0 font-medium text-slate-800">{it.title}</span>
                      <span className="min-w-0 truncate text-slate-500 inline-flex items-center gap-1">
                        {it.location ? (<><MapPin className="h-3 w-3 shrink-0" />{it.location}</>) : '—'}
                      </span>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export default AgendaSetupPanel;
