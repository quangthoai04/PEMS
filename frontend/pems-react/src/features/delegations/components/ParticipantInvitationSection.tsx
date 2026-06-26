/**
 * VisitProcess "Thành phần tham gia" — real participant coordination (replaces the old hard-coded
 * demo UI). The Host (read-only card) is shown to everyone; the invite panels (Staff IC / Student /
 * Department) are shown only to the official host while the instance is live. Nothing here fakes an
 * invitee's response — statuses come straight from the DB; the host can only invite / withdraw.
 */
import React, { useEffect, useRef, useState } from 'react';
import {
  Search, Loader2, AlertCircle, Mail, Phone, Building2,
  Users, UserCheck, GraduationCap, Trash2, Send,
} from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import type {
  VisitParticipantListItem, VisitProcessHost, ParticipantCandidate, SupportDepartment,
} from '../types/delegations.types';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface Props {
  visitInstanceId: number;
  relation: string;            // HOST | STAFF_LEADER | HO | IC_SUPPORT | ...
  instanceStatus: string;      // ASSIGNED | BEFORE_VISIT | ...
  currentUserId: number | null;
  host: VisitProcessHost | null;
  participants: VisitParticipantListItem[];
  onChanged: () => void | Promise<void>;
  pushToast: ToastFn;
}

const STATUS_META: Record<string, { label: string; cls: string }> = {
  INVITED: { label: 'Chờ phản hồi', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  ACCEPTED: { label: 'Đã chấp nhận', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DECLINED: { label: 'Đã từ chối', cls: 'bg-red-50 text-red-700 border-red-200' },
  ASSIGNED: { label: 'Đã phân công', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  REMOVED: { label: 'Đã gỡ', cls: 'bg-slate-100 text-slate-500 border-slate-200' },
};

function StatusBadge({ status }: { status: string }) {
  const meta = STATUS_META[status] ?? { label: status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  return (
    <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
      {meta.label}
    </span>
  );
}

function ConflictBadge({ count, allPrivate }: { count: number; allPrivate: boolean }) {
  if (count <= 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
        Không trùng lịch
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-bold text-amber-700">
      <AlertCircle className="w-3 h-3" />
      {allPrivate ? 'Có lịch cá nhân trùng' : `Có ${count} lịch trùng`}
    </span>
  );
}

/** Generic debounced search dropdown. Loads results for the typed keyword and renders one row per
 * result via `renderRow`. Stays open while focused; the parent owns the "invite" action. */
function SearchDropdown<T>({
  placeholder, search, renderRow, emptyText,
}: {
  placeholder: string;
  search: (keyword: string) => Promise<T[]>;
  renderRow: (item: T, index: number) => React.ReactNode;
  emptyText: string;
}) {
  const [kw, setKw] = useState('');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [items, setItems] = useState<T[]>([]);
  const reqId = useRef(0);

  useEffect(() => {
    if (!open) return;
    const id = ++reqId.current;
    setLoading(true);
    setError(false);
    const t = setTimeout(async () => {
      try {
        const res = await search(kw.trim());
        if (id === reqId.current) setItems(Array.isArray(res) ? res : []);
      } catch {
        if (id === reqId.current) { setItems([]); setError(true); }
      } finally {
        if (id === reqId.current) setLoading(false);
      }
    }, 350);
    return () => clearTimeout(t);
  }, [kw, open, search]);

  return (
    <div className="relative">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          type="text"
          value={kw}
          placeholder={placeholder}
          onFocus={() => setOpen(true)}
          onChange={(e) => setKw(e.target.value)}
          className="w-full rounded-xl border border-gray-200 bg-white py-2.5 pl-9 pr-3 text-sm outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20"
        />
        {loading && <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-gray-400" />}
      </div>
      {open && (
        <div className="mt-2 max-h-72 overflow-y-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          {loading && items.length === 0 ? (
            <div className="px-4 py-3 text-sm text-gray-400">Đang tải...</div>
          ) : error ? (
            <div className="px-4 py-3 text-sm text-red-500">Không thể tải danh sách. Thử lại.</div>
          ) : items.length === 0 ? (
            <div className="px-4 py-3 text-sm text-gray-400">{emptyText}</div>
          ) : (
            items.map((it, i) => renderRow(it, i))
          )}
        </div>
      )}
    </div>
  );
}

export function ParticipantInvitationSection({
  visitInstanceId, relation, instanceStatus, currentUserId, host, participants, onChanged, pushToast,
}: Props) {
  // The host manages invitations only while the instance is in the prep window.
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');
  const [busyId, setBusyId] = useState<string | null>(null);

  const active = participants.filter((p) => p.status !== 'REMOVED');
  const supporters = active.filter((p) => p.participantRole === 'IC_SUPPORT' && !p.isHost);
  const students = active.filter((p) => p.participantRole === 'STUDENT');
  const departments = active.filter((p) => p.participantRole === 'DEPT_SUPPORT'
    && (p.subRole == null || p.subRole.toUpperCase() === 'LEADER'));

  const invite = async (key: string, payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1], displayName: string) => {
    if (busyId) return;
    setBusyId(key);
    try {
      const res = await delegationsApi.inviteVisitParticipant(visitInstanceId, payload);
      pushToast(res.emailQueued ? 'success' : 'info', res.message || `Đã gửi lời mời tới ${displayName}.`);
      await onChanged();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gửi lời mời. Vui lòng thử lại.'));
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (p: VisitParticipantListItem) => {
    if (busyId) return;
    setBusyId(`rm-${p.participantId}`);
    try {
      await delegationsApi.removeVisitParticipant(visitInstanceId, p.participantId);
      pushToast('success', `Đã gỡ ${p.fullName} khỏi danh sách mời.`);
      await onChanged();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gỡ lời mời. Vui lòng thử lại.'));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* ── Host chính (read-only) ── */}
      <div className="rounded-xl border border-gray-200 border-l-[6px] border-l-[#004c91] bg-gradient-to-r from-[#004c91]/[0.03] to-transparent p-5 shadow-sm">
        <h4 className="mb-3 flex items-center gap-2 text-base font-bold text-[#004c91]">
          <UserCheck className="w-5 h-5" /> Host chính
        </h4>
        {host ? (
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-[#004c91] font-bold text-white ring-2 ring-blue-100">
              {host.fullName.charAt(0)}
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2 text-sm font-bold text-[#004c91]">
                {host.fullName}
                {currentUserId != null && host.userId === currentUserId && (
                  <span className="rounded-md bg-blue-100 px-2 py-0.5 text-[11px] font-bold text-[#004c91]">Bạn là Host chính</span>
                )}
              </div>
              <div className="mt-0.5 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-gray-500">
                <span className="inline-flex items-center gap-1"><Mail className="w-3 h-3" /> {host.email}</span>
                {host.phone && <span className="inline-flex items-center gap-1"><Phone className="w-3 h-3" /> {host.phone}</span>}
                {host.departmentName && <span className="inline-flex items-center gap-1"><Building2 className="w-3 h-3" /> {host.departmentName}</span>}
              </div>
            </div>
            <span className="inline-flex items-center rounded-md border border-blue-200 bg-blue-50 px-2 py-0.5 text-[11px] font-bold text-[#004c91]">
              {host.statusLabel || 'Đã được phân công'}
            </span>
          </div>
        ) : (
          <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-700">
            <AlertCircle className="w-4 h-4 shrink-0" /> Chưa xác định Host chính cho cơ sở này.
          </div>
        )}
      </div>

      {!canManage && (
        <p className="text-xs font-medium text-slate-500">
          Chỉ Host phụ trách mới có thể mời thành phần tham gia. Danh sách dưới đây ở chế độ xem.
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        {/* ── Staff hỗ trợ IC ── */}
        <Panel title="Staff hỗ trợ IC" icon={<Users className="w-5 h-5" />}>
          {canManage && (
            <SearchDropdown<ParticipantCandidate>
              placeholder="Tìm theo tên / email..."
              emptyText="Không tìm thấy nhân sự phù hợp."
              search={(kw) => delegationsApi.getParticipantCandidates(visitInstanceId, 'IC_SUPPORT', kw)}
              renderRow={(c) => (
                <CandidateRow
                  key={c.userId}
                  candidate={c}
                  busy={busyId === `ic-${c.userId}`}
                  onInvite={() => invite(`ic-${c.userId}`, { participantType: 'IC_SUPPORT', userId: c.userId }, c.fullName)}
                />
              )}
            />
          )}
          <ParticipantList
            rows={supporters}
            canManage={canManage}
            busyId={busyId}
            onRemove={remove}
            emptyText="Chưa mời Staff hỗ trợ IC nào."
          />
        </Panel>

        {/* ── Sinh viên hỗ trợ ── */}
        <Panel title="Sinh viên hỗ trợ" icon={<GraduationCap className="w-5 h-5" />}>
          {canManage && (
            <SearchDropdown<ParticipantCandidate>
              placeholder="Tìm theo tên / email / mã SV..."
              emptyText="Không tìm thấy sinh viên hợp lệ trong campus này."
              search={(kw) => delegationsApi.getParticipantCandidates(visitInstanceId, 'STUDENT', kw)}
              renderRow={(c) => (
                <CandidateRow
                  key={c.userId}
                  candidate={c}
                  subtitle={c.studentCode ? `MSSV: ${c.studentCode}` : undefined}
                  busy={busyId === `st-${c.userId}`}
                  onInvite={() => invite(`st-${c.userId}`, { participantType: 'STUDENT', userId: c.userId }, c.fullName)}
                />
              )}
            />
          )}
          <ParticipantList
            rows={students}
            canManage={canManage}
            busyId={busyId}
            onRemove={remove}
            emptyText="Chưa mời sinh viên hỗ trợ nào."
          />
        </Panel>

        {/* ── Phòng ban hỗ trợ (mời Trưởng phòng) ── */}
        <Panel title="Phòng ban hỗ trợ" icon={<Building2 className="w-5 h-5" />} wide>
          {canManage && (
            <SearchDropdown<SupportDepartment>
              placeholder="Tìm phòng ban (GENERAL) cùng cơ sở..."
              emptyText="Không tìm thấy phòng ban phù hợp."
              search={(kw) => delegationsApi.getSupportDepartments(visitInstanceId, kw)}
              renderRow={(d) => (
                <div key={d.departmentId} className="flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-bold text-gray-800">{d.departmentName}</div>
                    <div className="truncate text-xs text-gray-500">
                      {d.leaderName ? `Trưởng phòng: ${d.leaderName}` : 'Chưa có trưởng phòng đang hoạt động'}
                      {d.leaderEmail ? ` · ${d.leaderEmail}` : ''}
                    </div>
                    {!d.canInvite && d.disabledReason && (
                      <div className="mt-0.5 text-[11px] font-medium text-amber-600">{d.disabledReason}</div>
                    )}
                  </div>
                  <button
                    type="button"
                    disabled={!d.canInvite || busyId === `dept-${d.departmentId}`}
                    onClick={() => {
                      if (!d.canInvite) {
                        pushToast('warning', d.disabledReason || 'Không thể mời phòng ban này.');
                        return;
                      }
                      invite(`dept-${d.departmentId}`, { participantType: 'DEPT_SUPPORT', departmentId: d.departmentId }, `trưởng phòng ${d.departmentName}`);
                    }}
                    className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40"
                  >
                    {busyId === `dept-${d.departmentId}` ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                    Mời
                  </button>
                </div>
              )}
            />
          )}
          <div className="mt-3 space-y-2">
            {departments.length === 0 ? (
              <p className="text-sm italic text-slate-400">Chưa mời phòng ban hỗ trợ nào.</p>
            ) : departments.map((p) => (
              <div key={p.participantId} className="rounded-xl border border-gray-200 bg-white p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-bold text-gray-800">
                      {p.departmentName || 'Phòng ban'}
                    </div>
                    <div className="truncate text-xs text-gray-500">Trưởng phòng: {p.fullName}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={p.status} />
                    {canManage && p.status === 'INVITED' && (
                      <RemoveButton busy={busyId === `rm-${p.participantId}`} onClick={() => remove(p)} />
                    )}
                  </div>
                </div>
                {p.departmentAssignment?.assignedStaffName && (
                  <div className="mt-2 rounded-lg bg-blue-50/70 px-3 py-1.5 text-xs font-medium text-[#004c91]">
                    Nhân sự xử lý: <span className="font-bold">{p.departmentAssignment.assignedStaffName}</span>
                  </div>
                )}
                {p.status === 'DECLINED' && p.note && (
                  <div className="mt-2 text-xs italic text-red-500">Lý do từ chối: {p.note}</div>
                )}
              </div>
            ))}
          </div>
        </Panel>
      </div>
    </div>
  );
}

function Panel({ title, icon, wide, children }: { title: string; icon: React.ReactNode; wide?: boolean; children: React.ReactNode }) {
  return (
    <div className={`rounded-xl border border-gray-200 bg-white p-5 shadow-sm ${wide ? 'xl:col-span-2' : ''}`}>
      <h4 className="mb-4 flex items-center gap-2 text-base font-bold text-[#004c91]">{icon} {title}</h4>
      {children}
    </div>
  );
}

function CandidateRow({
  candidate, subtitle, busy, onInvite,
}: { candidate: ParticipantCandidate; subtitle?: string; busy: boolean; onInvite: () => void }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0">
      <div className="min-w-0">
        <div className="truncate text-sm font-bold text-gray-800">{candidate.fullName}</div>
        <div className="truncate text-xs text-gray-500">
          {candidate.email}{candidate.departmentName ? ` · ${candidate.departmentName}` : ''}{subtitle ? ` · ${subtitle}` : ''}
        </div>
        <div className="mt-1">
          <ConflictBadge
            count={candidate.conflictCount}
            allPrivate={candidate.hasPrivateConflict && candidate.conflictCount > 0}
          />
        </div>
      </div>
      <button
        type="button"
        disabled={!candidate.canInvite || busy}
        onClick={onInvite}
        className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40"
      >
        {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
        Mời
      </button>
    </div>
  );
}

function ParticipantList({
  rows, canManage, busyId, onRemove, emptyText,
}: {
  rows: VisitParticipantListItem[];
  canManage: boolean;
  busyId: string | null;
  onRemove: (p: VisitParticipantListItem) => void;
  emptyText: string;
}) {
  if (rows.length === 0) return <p className="mt-3 text-sm italic text-slate-400">{emptyText}</p>;
  return (
    <div className="mt-3 space-y-2">
      {rows.map((p) => (
        <div key={p.participantId} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-gray-200 bg-white p-3">
          <div className="min-w-0">
            <div className="truncate text-sm font-bold text-gray-800">{p.fullName}</div>
            <div className="truncate text-xs text-gray-500">{p.email}</div>
            {p.status === 'DECLINED' && p.note && (
              <div className="mt-1 text-xs italic text-red-500">Lý do từ chối: {p.note}</div>
            )}
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={p.status} />
            {canManage && p.status === 'INVITED' && (
              <RemoveButton busy={busyId === `rm-${p.participantId}`} onClick={() => onRemove(p)} />
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

function RemoveButton({ busy, onClick }: { busy: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      title="Gỡ khỏi danh sách mời"
      disabled={busy}
      onClick={onClick}
      className="inline-flex h-7 w-7 items-center justify-center rounded-lg border border-red-200 bg-red-50 text-red-500 outline-none transition-colors hover:bg-red-100 disabled:opacity-40"
    >
      {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Trash2 className="w-3.5 h-3.5" />}
    </button>
  );
}

function apiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string' && data.trim()) return data;
  if (data.message) return data.message;
  return fallback;
}
