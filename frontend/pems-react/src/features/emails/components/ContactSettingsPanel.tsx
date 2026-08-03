import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertTriangle, Ban, Info, Loader2, Save, ShieldAlert, RotateCcw } from 'lucide-react';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';
import {
  emailsApi,
  type EmailContactCapability,
  type EmailContactSettings,
  type EmailContactSettingsPayload,
} from '../api/emailsApi';
import { TEMPLATE_ERROR_CODES, errorCodeOf } from '../types/templateContract';
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


/**
 * Why the settings could not be loaded, in terms of what the operator has to DO about it.
 *
 * Every one of these used to arrive as "Không tìm thấy dữ liệu cần xử lý." — the generic 404 sentence
 * from the toast helper's HTTP-status table. That sentence was not merely unhelpful, it was misleading:
 * the failure it described most often was a running API built before this endpoint existed, where
 * nothing is missing from the data at all and the fix is to restart the API. Routing 404s carry no
 * body, so there is no `errorCode` to read and the ABSENCE of one is itself the evidence.
 */
type LoadFailure = { title: string; detail: string; action: string; kind: string };

function classifyLoadFailure(err: unknown): LoadFailure {
  const status = (err as { response?: { status?: number } })?.response?.status;
  const code = errorCodeOf(err);

  if (status === 401) {
    return {
      kind: 'unauthenticated',
      title: 'Phiên đăng nhập đã hết hạn',
      detail: 'Máy chủ từ chối yêu cầu vì chưa có phiên đăng nhập hợp lệ.',
      action: 'Đăng nhập lại rồi mở lại màn hình này.',
    };
  }

  if (status === 403) {
    return {
      kind: 'forbidden',
      title: 'Không có quyền xem cấu hình liên hệ',
      detail: 'Chỉ tài khoản Head Office được xem và sửa cấu hình khối liên hệ của mẫu email.',
      action: 'Nếu anh/chị cần quyền này, đề nghị Head Office cấp.',
    };
  }

  if (code === TEMPLATE_ERROR_CODES.contactPolicyStoreUnavailable) {
    return {
      kind: 'store-unavailable',
      title: 'Database chưa có bảng chính sách liên hệ',
      detail: 'Máy chủ đọc được yêu cầu nhưng không truy vấn được bảng email_contact_policies.',
      action: 'Chạy patch docs/database/scripts/patches/'
        + '2026-08-03_email_contact_information_block.sql trên database đang dùng, rồi tải lại.',
    };
  }

  if (code === TEMPLATE_ERROR_CODES.templateNotFound) {
    return {
      kind: 'template-not-catalogued',
      title: 'Mẫu email này không có trong danh mục hệ thống',
      detail: 'Dòng template trong database không khớp mã nào mà ứng dụng đăng ký, nên không có chính '
        + 'sách liên hệ nào áp cho nó.',
      action: 'Chạy patch docs/database/scripts/patches/'
        + '2026-08-03_email_template_catalog_alignment.sql để đưa danh mục về đúng 31 mẫu canonical.',
    };
  }

  // A 404 with no error code is a ROUTING 404: the request never reached a handler. Distinguished from
  // every "not found" above by the absence of a body, which is exactly what an old binary produces.
  if (status === 404) {
    return {
      kind: 'endpoint-missing',
      title: 'API đang chạy chưa có chức năng này',
      detail: 'Đường dẫn /api/email-templates/{mã}/contact-settings trả về 404 mà không kèm mã lỗi — '
        + 'nghĩa là bản build đang chạy được tạo trước khi endpoint này tồn tại. Dữ liệu không thiếu gì.',
      action: 'Build lại backend và khởi động lại API, rồi tải lại trang.',
    };
  }

  return {
    kind: 'server-error',
    title: 'Máy chủ gặp lỗi khi đọc cấu hình liên hệ',
    detail: getApiErrorMessage(err, 'Không có thông tin chi tiết từ máy chủ.'),
    action: 'Xem log của API để biết nguyên nhân; nếu lặp lại, báo lại kèm mã lỗi ở trên.',
  };
}



interface Props {
  templateCode: string;
  /** HO only. Everyone else sees the settings read-only. */
  canEdit: boolean;
  /** VI or EN — the preview follows the language tab being edited. */
  language?: string;
  /**
   * Receives the contact block rendered from the CURRENT draft, so the editor's preview pane updates as
   * toggles change rather than only after a save. '' means this policy renders no block.
   */
  onBlockPreviewChange?: (html: string) => void;
  /**
   * Reports whether this card holds unsaved changes.
   *
   * The contact settings and the template content are two separate saves against two separate endpoints,
   * so "there are unsaved changes" is two answers, not one. The editor asks for this one so it can name
   * which group is dirty when the screen is closed, instead of the single vague sentence that left an
   * operator guessing which of the two buttons they had forgotten to press.
   */
  onDirtyChange?: (dirty: boolean) => void;
  /** Toast notification callback */
  pushToast?: (type: 'success' | 'error', msg: string) => void;
}

export function ContactSettingsPanel({
  templateCode,
  canEdit,
  language = 'VI',
  onBlockPreviewChange,
  onDirtyChange,
  pushToast,
}: Props) {
  const [settings, setSettings] = useState<EmailContactSettings | null>(null);
  const [draft, setDraft] = useState<EmailContactSettingsPayload | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [failure, setFailure] = useState<LoadFailure | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [restoring, setRestoring] = useState(false);
  const [confirmState, setConfirmState] = useState({
    isOpen: false,
    title: '',
    message: '',
    variant: 'danger' as 'danger' | 'warning',
    onConfirm: () => {},
  });

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
      setFailure(classifyLoadFailure(err));
      setStatus('error');
    }
  }, [templateCode]);

  useEffect(() => { void load(); }, [load]);

  /**
   * Whether this card holds unsaved changes — computed here, above the early returns, because the editor
   * has to be told even while the card is loading or has failed (in both of those states the honest
   * answer is "nothing unsaved", and a stale `true` from a previous template would warn about work that
   * does not exist).
   */
  const dirty = Boolean(draft && settings)
    && JSON.stringify(draft) !== JSON.stringify(toPayload(settings!));

  const reportDirty = useRef(onDirtyChange);
  reportDirty.current = onDirtyChange;

  useEffect(() => { reportDirty.current?.(dirty); }, [dirty]);

  // A card that unmounts, or moves to another template, is no longer holding anything. Without this the
  // editor would keep warning about unsaved contact settings after the operator closed the very card
  // that had them.
  useEffect(() => () => reportDirty.current?.(false), [templateCode]);

  /**
   * Re-render the block whenever the draft changes.
   *
   * Debounced because typing a heading would otherwise fire a request per keystroke. Rendering happens
   * on the backend deliberately: the block's markup and its field-visibility rules live in
   * EmailContactHtmlRenderer, and a copy of them in this component would be a second implementation
   * that drifts — with the operator having no way to tell which one the recipient gets.
   *
   * A failed preview clears the pane rather than leaving the previous policy's block on screen, which
   * would show toggles that are no longer set.
   */
  useEffect(() => {
    if (!onBlockPreviewChange) return;
    if (!draft) return;

    // A template that cannot carry the block renders nothing, and the backend says so too — asking it
    // is a round trip whose answer is already known, and whose failure would be reported as an empty
    // pane for a reason that has nothing to do with this template.
    if (settings?.capability === 'UNSUPPORTED') {
      onBlockPreviewChange('');
      return;
    }

    let cancelled = false;
    const timer = setTimeout(() => {
      void (async () => {
        try {
          const res = await emailsApi.previewEmailContactBlock(templateCode, { ...draft, language });
          if (!cancelled) onBlockPreviewChange(res.data.html ?? '');
        } catch {
          if (!cancelled) onBlockPreviewChange('');
        }
      })();
    }, 250);

    return () => { cancelled = true; clearTimeout(timer); };
  }, [draft, templateCode, language, onBlockPreviewChange, settings?.capability]);

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    setSaveError('');
    try {
      const res = await emailsApi.updateEmailContactSettings(templateCode, draft);
      setSettings(res.data);
      setDraft(toPayload(res.data));
      if (pushToast) pushToast('success', 'Đã cập nhật cấu hình thông tin liên hệ.');
    } catch (err) {
      // The backend refuses contradictory combinations by name (both channels hidden, Reply-To
      // pointing at a hidden address, REQUIRED without the placeholder in the body). Relayed verbatim
      // rather than replaced with "Lưu thất bại", which would hide the one sentence that says what to fix.
      setSaveError(getApiErrorMessage(err, 'Không lưu được cấu hình thông tin liên hệ.'));
    } finally {
      setSaving(false);
    }
  };

  // Named for what it DOES, not for what it might look like it does. The backend writes the shipped
  // default values onto this template's own row; it does not delete the row to let campus/system
  // configuration flow through again. Those are two different operations and only one of them is
  // implemented, so the wording must not promise the other.
  const handleRestoreDefault = () => {
    setConfirmState({
      isOpen: true,
      title: 'Phục hồi về cấu hình mặc định của mẫu',
      variant: 'danger',
      message:
        'Mức hiển thị, nguồn đầu mối, các trường hiển thị, tiêu đề khối và Reply-To sẽ được đặt lại '
        + 'thành giá trị mặc định của mẫu này. Nội dung email không thay đổi.',
      onConfirm: () => {
        setConfirmState(prev => ({ ...prev, isOpen: false }));
        void executeRestoreDefault();
      },
    });
  };

  const executeRestoreDefault = async () => {
    setRestoring(true);
    setSaveError('');
    try {
      const res = await emailsApi.restoreEmailContactSettingsDefault(templateCode);
      setSettings(res.data);
      setDraft(toPayload(res.data));
      if (pushToast) pushToast('success', 'Đã phục hồi về cấu hình mặc định của mẫu.');
      // The debounce timer in useEffect will automatically re-render the preview using the new draft.
    } catch (err) {
      setSaveError(getApiErrorMessage(err, 'Không phục hồi được cấu hình thông tin liên hệ.'));
    } finally {
      setRestoring(false);
    }
  };

  if (status === 'loading') {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500 py-4" data-testid="contact-settings-loading">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải cấu hình thông tin liên hệ…
      </div>
    );
  }

  /**
   * A template that cannot carry the block gets a sentence, not a form.
   *
   * Every control below would be inert on one: the requirement has no legal value other than NONE, the
   * source resolves nothing, the toggles decide the visibility of a block that never renders, and both
   * buttons are refused by the API. Showing them anyway is what produced the reported defect — an
   * operator set "Tùy chọn" on ACCOUNT_EMAIL_CONFIRMATION, saved it, added the block the setting had
   * just invited, and met EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED with nothing on screen explaining it.
   */
  const capability: EmailContactCapability = settings?.capability ?? 'SUPPORTED';

  if (status === 'ready' && settings && capability === 'UNSUPPORTED') {
    return (
      <div className="flex items-start gap-2 text-xs text-gray-700 bg-gray-50 border border-gray-200 rounded p-3"
           data-testid="contact-settings-unsupported"
           data-capability={capability}>
        <Ban className="w-4 h-4 shrink-0 mt-0.5 text-gray-500" />
        <span className="space-y-1">
          <span className="block font-semibold">Mẫu này không dùng khối thông tin liên hệ.</span>
          <span className="block">
            {settings.capabilityReasonVi
              ?? 'Nội dung email không kèm đầu mối liên hệ nào.'}
          </span>
          <span className="block text-gray-500">Không có cấu hình cần chỉnh sửa.</span>
        </span>
      </div>
    );
  }

  if (status === 'error' || !settings || !draft) {
    const f = failure ?? {
      kind: 'unknown',
      title: 'Không tải được cấu hình thông tin liên hệ',
      detail: '',
      action: 'Thử tải lại.',
    };

    return (
      <div className="text-xs text-orange-900 bg-orange-50 border-l-4 border-orange-400 p-3 rounded space-y-1.5"
           data-testid="contact-settings-error"
           data-failure-kind={f.kind}>
        <div className="flex items-start gap-2">
          <ShieldAlert className="w-4 h-4 shrink-0 mt-0.5" />
          <span className="font-semibold">{f.title}</span>
        </div>
        {f.detail && <p className="pl-6 text-orange-800">{f.detail}</p>}
        <p className="pl-6"><strong>Cần làm:</strong> {f.action}</p>
        <div className="pl-6 pt-1">
          <button
            type="button"
            onClick={() => void load()}
            className="rounded border border-orange-400 px-2 py-1 text-[11px] font-semibold hover:bg-orange-100"
          >
            Tải lại
          </button>
        </div>
      </div>
    );
  }

  const set = <K extends keyof EmailContactSettingsPayload>(key: K, value: EmailContactSettingsPayload[K]) => {
    setDraft(prev => (prev ? { ...prev, [key]: value } : prev));
  };

  const showsBlock = draft.requirement !== 'NONE';

  const missingPlaceholder =
    draft.requirement === 'REQUIRED' && !(settings.bodyCarriesBlockVi && settings.bodyCarriesBlockEn);

  const noChannel = showsBlock && !draft.showEmail && !draft.showPhone;

  return (
    <div className="space-y-4" data-testid="contact-settings-panel">
      <div className="flex items-start gap-2 text-[11px] text-gray-600 bg-[#f8fbff] border border-[#cce0ff] rounded p-3">
        <Info className="w-3.5 h-3.5 shrink-0 mt-0.5 text-[#004c91]" />
        <span>
          Hệ thống tự động lấy thông tin của đầu mối khi gửi email. Bạn chỉ cần chọn nguồn đầu mối và các thông tin cần hiển thị; không nhập thủ công địa chỉ liên hệ.
        </span>
      </div>

      {draft.requirement === 'NONE' && (
        <div className="flex items-start gap-2 text-xs text-gray-700 bg-gray-50 border border-gray-200
                        rounded p-3"
             data-testid="contact-settings-no-contact">
          <Ban className="w-4 h-4 shrink-0 mt-0.5 text-gray-500" />
          <span>
            <strong>Không hiển thị thông tin liên hệ.</strong> Mẫu này gửi đi không kèm khối liên hệ nào
            — đúng với các email mang mã dùng một lần hoặc email mà chính người phụ trách là người nhận.
            Chọn <em>Tùy chọn</em> hoặc <em>Bắt buộc</em> ở trên nếu muốn bật khối.
          </span>
        </div>
      )}

      {/* Mức bắt buộc */}
      <fieldset disabled={!canEdit} className="space-y-1.5">
        <legend className="block text-sm font-bold text-gray-700 mb-1">
          Mức hiển thị
        </legend>
        {/*
          The levels come from the backend, already narrowed by capability: a template whose text tells
          the recipient to make contact is not offered "Không hiển thị", because choosing it would leave
          the instruction with no address — and the API refuses that write anyway, so offering it here
          would only be a button that fails. The reason is stated below rather than left to be inferred
          from a missing option.
        */}
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
        {capability === 'REQUIRED' && (
          <p className="text-[11px] text-gray-600" data-testid="contact-settings-level-locked">
            {settings.capabilityReasonVi
              ?? 'Nội dung mẫu này có câu yêu cầu người nhận liên hệ, nên email phải kèm khối thông tin liên hệ.'}
            {' '}Vì vậy không chọn được mức <em>Không hiển thị</em>.
          </p>
        )}
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
                  <input type="checkbox" checked={draft[key as keyof EmailContactSettingsPayload] as boolean} onChange={e => set(key as keyof EmailContactSettingsPayload, e.target.checked as never)} />
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

      {dirty && (
        <p className="text-xs font-semibold text-amber-700" data-testid="contact-settings-dirty">
          ● Cấu hình liên hệ có thay đổi chưa lưu
        </p>
      )}

      {canEdit && (
        <div className="flex items-center gap-3 mt-4">
          <button
            type="button"
            onClick={() => void save()}
            disabled={!dirty || saving || restoring || noChannel}
            className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-4 py-2 text-sm font-semibold text-white hover:bg-blue-800 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            Lưu cấu hình liên hệ
          </button>
          <button
            type="button"
            onClick={handleRestoreDefault}
            disabled={saving || restoring}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {restoring ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
            Phục hồi về cấu hình mặc định của mẫu
          </button>
        </div>
      )}

      <ConfirmModal
        isOpen={confirmState.isOpen}
        title={confirmState.title}
        message={confirmState.message}
        variant={confirmState.variant}
        onConfirm={confirmState.onConfirm}
        onClose={() => setConfirmState(prev => ({ ...prev, isOpen: false }))}
      />
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
