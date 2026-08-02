import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, Info, Loader2, Save } from 'lucide-react';
import {
  emailsApi,
  type EmailContactSettings,
  type EmailContactSettingsPayload,
} from '../api/emailsApi';
import { getApiErrorMessage } from '../../../shared/utils/toast';

/**
 * "Cấu hình thông tin liên hệ" — who the recipient of this template should contact, and what the mail
 * may show about them.
 *
 * The one thing this screen deliberately cannot do is name a person or type an address. Every control
 * is a choice between backend-defined options or a visibility toggle; the contact's actual details are
 * resolved when the mail is sent, from the visit, the campus or the department. That is what stops a
 * template from being edited to attribute somebody else's mailbox to the Host, and it is why there is no
 * free-text field here except the two headings.
 */

const REQUIREMENT_LABELS: Record<string, { title: string; hint: string }> = {
  NONE: {
    title: 'Không hiển thị',
    hint: 'Email này không kèm khối liên hệ.',
  },
  OPTIONAL: {
    title: 'Tùy chọn',
    hint: 'Hiện khối nếu tìm được đầu mối; không tìm được thì vẫn gửi bình thường.',
  },
  REQUIRED: {
    title: 'Bắt buộc',
    hint: 'Hiện khối, và CHẶN gửi nếu không tìm được đầu mối nào. Dùng cho email có câu bảo người nhận đi liên hệ.',
  },
};

const SOURCE_LABELS: Record<string, string> = {
  HOST: 'Người phụ trách tiếp đón (theo đúng cơ sở của chuyến thăm)',
  SENDER: 'Người bấm gửi',
  HOST_THEN_SENDER: 'Người phụ trách; nếu chưa có thì người bấm gửi',
  CAMPUS_DEFAULT: 'Đầu mối của cơ sở',
  DEPARTMENT_DEFAULT: 'Trưởng phòng ban phụ trách',
  SUPPORT_CONTACT: 'Bộ phận quản trị hệ thống',
};

const REPLY_TO_LABELS: Record<string, string> = {
  NONE: 'Giữ Reply-To mặc định của hệ thống',
  CONTACT: 'Thư trả lời gửi về đầu mối ở trên',
  SENDER: 'Thư trả lời gửi về người bấm gửi',
};

interface Props {
  templateCode: string;
  /** HO only. Everyone else sees the settings read-only. */
  canEdit: boolean;
}

export function ContactSettingsPanel({ templateCode, canEdit }: Props) {
  const [settings, setSettings] = useState<EmailContactSettings | null>(null);
  const [draft, setDraft] = useState<EmailContactSettingsPayload | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const load = useCallback(async () => {
    setStatus('loading');
    setSaveError('');
    try {
      const res = await emailsApi.getEmailContactSettings(templateCode);
      const data = res.data;
      setSettings(data);
      setDraft(toPayload(data));
      setStatus('ready');
    } catch (err) {
      setMessage(getApiErrorMessage(err, 'Không tải được cấu hình thông tin liên hệ.'));
      setStatus('error');
    }
  }, [templateCode]);

  useEffect(() => { void load(); }, [load]);

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    setSaveError('');
    try {
      const res = await emailsApi.updateEmailContactSettings(templateCode, draft);
      setSettings(res.data);
      setDraft(toPayload(res.data));
    } catch (err) {
      // The backend refuses contradictory combinations by name (both channels hidden, Reply-To
      // pointing at a hidden address, REQUIRED without the placeholder in the body). Relayed verbatim
      // rather than replaced with "Lưu thất bại", which would hide the one sentence that says what to fix.
      setSaveError(getApiErrorMessage(err, 'Không lưu được cấu hình thông tin liên hệ.'));
    } finally {
      setSaving(false);
    }
  };

  if (status === 'loading') {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500 py-4" data-testid="contact-settings-loading">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải cấu hình thông tin liên hệ…
      </div>
    );
  }

  if (status === 'error' || !settings || !draft) {
    return (
      <div className="text-xs text-orange-800 bg-orange-50 border-l-4 border-orange-400 p-3 rounded"
           data-testid="contact-settings-error">
        {message}
      </div>
    );
  }

  const set = <K extends keyof EmailContactSettingsPayload>(key: K, value: EmailContactSettingsPayload[K]) =>
    setDraft(prev => (prev ? { ...prev, [key]: value } : prev));

  const dirty = JSON.stringify(draft) !== JSON.stringify(toPayload(settings));
  const showsBlock = draft.requirement !== 'NONE';

  // Warn BEFORE a save is attempted. The backend refuses this combination too, but a warning that
  // appears while the operator is choosing is worth more than a refusal after they press save.
  const missingPlaceholder =
    draft.requirement === 'REQUIRED' && !(settings.bodyCarriesBlockVi && settings.bodyCarriesBlockEn);

  const noChannel = showsBlock && !draft.showEmail && !draft.showPhone;

  return (
    <div className="space-y-4" data-testid="contact-settings-panel">
      <div className="flex items-start gap-2 text-[11px] text-gray-600 bg-[#f8fbff] border border-[#cce0ff] rounded p-3">
        <Info className="w-3.5 h-3.5 shrink-0 mt-0.5 text-[#004c91]" />
        <span>
          Hệ thống tự điền họ tên, email và số điện thoại của đầu mối khi gửi. Ở đây chỉ chọn{' '}
          <strong>lấy đầu mối từ đâu</strong> và <strong>hiển thị những trường nào</strong> — không nhập
          tay được địa chỉ liên hệ.
          {settings.isDefault && <span className="block mt-1 text-gray-500">Đang dùng cấu hình mặc định của hệ thống.</span>}
        </span>
      </div>

      {/* Mức bắt buộc */}
      <fieldset disabled={!canEdit} className="space-y-1.5">
        <legend className="block text-sm font-bold text-gray-700 mb-1">Mức hiển thị</legend>
        {settings.availableRequirements.map(value => (
          <label key={value}
                 className="flex items-start gap-2 rounded-lg border border-gray-200 px-3 py-2 cursor-pointer hover:bg-gray-50">
            <input
              type="radio"
              name={`requirement-${templateCode}`}
              className="mt-1"
              checked={draft.requirement === value}
              onChange={() => set('requirement', value as EmailContactSettingsPayload['requirement'])}
            />
            <span className="text-xs">
              <span className="font-semibold text-gray-800">{REQUIREMENT_LABELS[value]?.title ?? value}</span>
              <span className="block text-gray-500">{REQUIREMENT_LABELS[value]?.hint}</span>
            </span>
          </label>
        ))}
      </fieldset>

      {missingPlaceholder && (
        <div className="flex items-start gap-2 text-xs text-orange-800 bg-orange-50 border-l-4 border-orange-400 p-3 rounded"
             data-testid="contact-settings-missing-placeholder">
          <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
          <span>
            Nội dung email chưa có <code className="font-mono">{settings.blockPlaceholder}</code>
            {!settings.bodyCarriesBlockVi && !settings.bodyCarriesBlockEn
              ? ' (cả tiếng Việt và tiếng Anh)'
              : !settings.bodyCarriesBlockVi ? ' (tiếng Việt)' : ' (tiếng Anh)'}
            . Hãy thêm khối vào nội dung trước khi đặt mức <strong>Bắt buộc</strong>.
          </span>
        </div>
      )}

      {showsBlock && (
        <>
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor={`source-${templateCode}`}>
              Lấy đầu mối từ
            </label>
            <select
              id={`source-${templateCode}`}
              disabled={!canEdit}
              value={draft.contactSource}
              onChange={e => set('contactSource', e.target.value as EmailContactSettingsPayload['contactSource'])}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
            >
              {settings.availableSources.map(value => (
                <option key={value} value={value}>{SOURCE_LABELS[value] ?? value}</option>
              ))}
            </select>
          </div>

          <fieldset disabled={!canEdit}>
            <legend className="block text-sm font-bold text-gray-700 mb-1">Hiển thị trường</legend>
            <div className="grid grid-cols-2 gap-1.5">
              {([
                ['showEmail', 'Email công việc'],
                ['showPhone', 'Số điện thoại'],
                ['showDepartment', 'Phòng ban'],
                ['showCampus', 'Cơ sở'],
                ['showSender', 'Dòng “Được gửi bởi”'],
              ] as const).map(([key, label]) => (
                <label key={key} className="flex items-center gap-2 text-xs text-gray-700">
                  <input type="checkbox" checked={draft[key]} onChange={e => set(key, e.target.checked)} />
                  {label}
                </label>
              ))}
            </div>
            {noChannel && (
              <p className="mt-1.5 text-xs text-red-700" data-testid="contact-settings-no-channel">
                Phải bật ít nhất một trong hai: email hoặc số điện thoại — nếu không, khối liên hệ hiện ra
                mà người nhận vẫn không có cách nào liên hệ.
              </p>
            )}
          </fieldset>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor={`heading-vi-${templateCode}`}>
                Tiêu đề khối (VI)
              </label>
              <input
                id={`heading-vi-${templateCode}`}
                type="text"
                maxLength={150}
                disabled={!canEdit}
                value={draft.headingVi}
                onChange={e => set('headingVi', e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
              />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor={`heading-en-${templateCode}`}>
                Tiêu đề khối (EN)
              </label>
              <input
                id={`heading-en-${templateCode}`}
                type="text"
                maxLength={150}
                disabled={!canEdit}
                value={draft.headingEn}
                onChange={e => set('headingEn', e.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor={`replyto-${templateCode}`}>
              Reply-To
            </label>
            <select
              id={`replyto-${templateCode}`}
              disabled={!canEdit}
              value={draft.replyToSource}
              onChange={e => set('replyToSource', e.target.value as EmailContactSettingsPayload['replyToSource'])}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
            >
              {settings.availableReplyToSources.map(value => (
                <option key={value} value={value}>{REPLY_TO_LABELS[value] ?? value}</option>
              ))}
            </select>
            {draft.replyToSource === 'CONTACT' && !draft.showEmail && (
              <p className="mt-1.5 text-xs text-red-700" data-testid="contact-settings-replyto-hidden">
                Reply-To trỏ về đầu mối nhưng email của đầu mối đang bị ẩn — người nhận sẽ không thấy thư
                trả lời sẽ đi đâu.
              </p>
            )}
          </div>
        </>
      )}

      {saveError && (
        <div className="text-xs text-red-800 bg-red-50 border-l-4 border-red-400 p-3 rounded"
             data-testid="contact-settings-save-error">
          {saveError}
        </div>
      )}

      {canEdit && (
        <button
          type="button"
          onClick={() => void save()}
          disabled={!dirty || saving || noChannel}
          className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          Lưu cấu hình liên hệ
        </button>
      )}
    </div>
  );
}

function toPayload(s: EmailContactSettings): EmailContactSettingsPayload {
  return {
    requirement: s.requirement,
    contactSource: s.contactSource,
    showEmail: s.showEmail,
    showPhone: s.showPhone,
    showDepartment: s.showDepartment,
    showCampus: s.showCampus,
    showSender: s.showSender,
    headingVi: s.headingVi,
    headingEn: s.headingEn,
    replyToSource: s.replyToSource,
  };
}
