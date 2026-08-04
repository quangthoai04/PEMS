import { useCallback, useEffect, useRef, useState } from 'react';
import { AlertTriangle, Ban, Info, Loader2, ShieldAlert } from 'lucide-react';
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
 *
 * <h3>Controlled, and with no buttons of its own</h3>
 *
 * The card used to hold its own draft, its own dirty flag, its own "Lưu cấu hình liên hệ" and its own
 * "Phục hồi mặc định". That made four things true that should not have been: an operator had to remember
 * two saves, a template could be left with a body and a policy contradicting each other because only one
 * of the two calls succeeded, the close warning had to name which of two groups was unsaved, and the one
 * rule that spans both — whether the body may carry `{{contactInformationBlock}}` — was judged by each
 * half against the other half as STORED, so changing both at once was refused whichever way round it was
 * done.
 *
 * The card now renders `value` and reports edits through `onChange`. It still fetches its own metadata —
 * capability, the legal requirement levels, the source and Reply-To options — because that is
 * per-template information the parent has no other reason to hold, and it hands the loaded settings up
 * once through `onLoaded` so the editor can seed both its form and its baseline in one step.
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
  /**
   * The capability from the template contract, when the caller already holds one.
   *
   * The same fact reaches this card by two routes — the contract the editor fetches and this card's own
   * settings request — and they cannot be allowed to disagree in the unsafe direction. `false` here is
   * final: the reason is shown at once, and the settings request is not even made, so a response that
   * omitted `capability` (an API built before the split) can no longer produce a configuration form on a
   * template whose policy the backend refuses to write. Left undefined, the card answers from its own
   * response as before.
   */
  contactSupported?: boolean;
  /** The contract's Vietnamese reason, used when the card renders before its own request has answered. */
  contactReasonVi?: string;
  /** VI or EN — the preview follows the language tab being edited. */
  language?: string;
  /**
   * The configuration currently on screen, owned by the editor.
   *
   * Null while the card's own request is still in flight, and on a template with no configuration. The
   * card renders nothing editable until this arrives, which is also what keeps the editor's baseline
   * honest: form and baseline are seeded together by `onLoaded`, so there is no window in which the
   * screen holds a value it has no baseline for and reports it as an unsaved change.
   */
  value: EmailContactSettingsPayload | null;
  /** One field changed. The editor merges it and re-derives the single dirty flag. */
  onChange: (next: EmailContactSettingsPayload) => void;
  /**
   * The settings as STORED, handed up once per template so the editor can set form and baseline in the
   * same step. Called again after a save or restore re-seeds the card from the server's snapshot.
   */
  onLoaded: (settings: EmailContactSettings) => void;
  /**
   * Asks the editor to switch the level to NONE, which it may refuse or make conditional.
   *
   * The card does NOT apply that change itself, and this is the one place its "just render `value`"
   * contract is bent on purpose. Switching to "Không hiển thị" while a body still carries the block is
   * the one edit that needs a decision the card cannot make — the bodies are the editor's, and deleting
   * from them silently would be an edit the operator never made and cannot see. So the intent is
   * reported and the editor decides whether to apply it directly or to ask first.
   */
  onRequestHide: () => void;
  /**
   * Receives the contact block rendered from the CURRENT draft, so the editor's preview pane updates as
   * toggles change rather than only after a save. '' means this policy renders no block.
   */
  onBlockPreviewChange?: (html: string) => void;
  /**
   * A validation problem the EDITOR found that belongs on this card — today, only "the body still has the
   * block while the level is hidden". Rendered here, next to the radio that caused it, rather than only
   * under the body field: the operator's next action is to change one of the two, and both are on screen.
   */
  crossFieldError?: { message: string; actionLabel?: string; onAction?: () => void } | null;
}

export function ContactSettingsPanel({
  templateCode,
  canEdit,
  contactSupported,
  contactReasonVi,
  language = 'VI',
  value,
  onChange,
  onLoaded,
  onRequestHide,
  onBlockPreviewChange,
  crossFieldError,
}: Props) {
  const [settings, setSettings] = useState<EmailContactSettings | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [failure, setFailure] = useState<LoadFailure | null>(null);

  /**
   * Settled before anything is requested. A template that cannot carry the block has no settings to
   * fetch: the endpoint would answer with a policy the send path ignores, and a failure of that request
   * would be reported as a problem with a card that has nothing to show anyway.
   */
  const knownUnsupported = contactSupported === false;

  // Held in a ref so the load effect does not re-run every time the editor re-renders with a new closure.
  // Without this the card would re-fetch on each keystroke in the body, and each response would call
  // onLoaded again — re-seeding the baseline from the server and quietly discarding unsaved edits.
  const reportLoaded = useRef(onLoaded);
  reportLoaded.current = onLoaded;

  const load = useCallback(async () => {
    setStatus('loading');
    try {
      const res = await emailsApi.getEmailContactSettings(templateCode);
      setSettings(res.data);
      reportLoaded.current(res.data);
      setStatus('ready');
    } catch (err) {
      setFailure(classifyLoadFailure(err));
      setStatus('error');
    }
  }, [templateCode]);

  useEffect(() => {
    if (knownUnsupported) return;
    void load();
  }, [load, knownUnsupported]);

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

    // Known unsupported before any request: the preview pane must not keep another template's block.
    if (knownUnsupported) {
      onBlockPreviewChange('');
      return;
    }

    if (!value) return;

    // A template that cannot carry the block renders nothing, and the backend says so too — asking it
    // is a round trip whose answer is already known, and whose failure would be reported as an empty
    // pane for a reason that has nothing to do with this template.
    if (settings?.capability === 'UNSUPPORTED') {
      onBlockPreviewChange('');
      return;
    }

    // Hidden renders nothing, and the preview must agree: showing a contact card over a policy of
    // "Không hiển thị" would tell an operator their setting had not taken effect. Answered here rather
    // than by asking the backend, because the answer is not in doubt.
    if (value.requirement === 'NONE') {
      onBlockPreviewChange('');
      return;
    }

    let cancelled = false;
    const timer = setTimeout(() => {
      void (async () => {
        try {
          const res = await emailsApi.previewEmailContactBlock(templateCode, { ...value, language });
          if (!cancelled) onBlockPreviewChange(res.data.html ?? '');
        } catch {
          if (!cancelled) onBlockPreviewChange('');
        }
      })();
    }, 250);

    return () => { cancelled = true; clearTimeout(timer); };
  }, [value, templateCode, language, onBlockPreviewChange, settings?.capability, knownUnsupported]);

  /**
   * A template that cannot carry the block gets a sentence, not a form.
   *
   * Every control below would be inert on one: the requirement has no legal value other than NONE, the
   * source resolves nothing, and the toggles decide the visibility of a block that never renders. Showing
   * them anyway is what produced the reported defect — an operator set "Tùy chọn" on
   * ACCOUNT_EMAIL_CONFIRMATION, saved it, added the block the setting had just invited, and met
   * EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED with nothing on screen explaining it.
   *
   * Checked before the loading state, because when the caller already knows the answer there is nothing
   * being loaded — and a spinner that resolves into a form is exactly what must not happen here.
   */
  const capability: EmailContactCapability =
    knownUnsupported ? 'UNSUPPORTED' : (settings?.capability ?? 'SUPPORTED');

  if (capability === 'UNSUPPORTED' && (knownUnsupported || (status === 'ready' && settings))) {
    return (
      <div className="space-y-3" data-capability={capability}>
        <div className="flex items-start gap-2 text-xs text-gray-700 bg-gray-50 border border-gray-200 rounded p-3"
             data-testid="contact-settings-unsupported">
          <Ban className="w-4 h-4 shrink-0 mt-0.5 text-gray-500" />
          <span className="space-y-1">
            <span className="block font-semibold">Mẫu này không dùng khối thông tin liên hệ.</span>
            <span className="block">
              {settings?.capabilityReasonVi
                ?? contactReasonVi
                ?? 'Nội dung email không kèm đầu mối liên hệ nào.'}
            </span>
            <span className="block text-gray-500">Không có cấu hình cần chỉnh sửa.</span>
          </span>
        </div>
        {/*
          The one thing an unsupported template still has to be able to say. There is no form here, so a
          body that has kept the block from an older release — or from a hand edit — would otherwise be
          reported only under the body field on the far side of the screen, where the reader has just been
          told this card has nothing to configure. The warning and its action belong next to that sentence.
        */}
        {crossFieldError && <CrossFieldError {...crossFieldError} />}
      </div>
    );
  }

  if (status === 'loading') {
    return (
      <div className="flex items-center gap-2 text-sm text-gray-500 py-4" data-testid="contact-settings-loading">
        <Loader2 className="w-4 h-4 animate-spin" /> Đang tải cấu hình thông tin liên hệ…
      </div>
    );
  }

  if (status === 'error' || !settings || !value) {
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

  const set = <K extends keyof EmailContactSettingsPayload>(
    key: K, fieldValue: EmailContactSettingsPayload[K],
  ) => {
    onChange({ ...value, [key]: fieldValue });
  };

  /**
   * Choosing a level. NONE is routed through the editor rather than applied here.
   *
   * Every other level is a change to this card alone, so it is applied directly. NONE may require the
   * bodies to change too, and the bodies are not this card's to edit — so the editor is asked, and it is
   * the editor that decides whether to apply it at once (nothing to remove) or to confirm first.
   */
  const chooseRequirement = (next: string) => {
    if (next === 'NONE') {
      onRequestHide();
      return;
    }
    set('requirement', next as EmailContactSettingsPayload['requirement']);
  };

  const showsBlock = value.requirement !== 'NONE';

  const missingPlaceholder =
    value.requirement === 'REQUIRED' && !(settings.bodyCarriesBlockVi && settings.bodyCarriesBlockEn);

  const noChannel = showsBlock && !value.showEmail && !value.showPhone;

  return (
    <div className="space-y-4" data-testid="contact-settings-panel">
      <div className="flex items-start gap-2 text-[11px] text-gray-600 bg-[#f8fbff] border border-[#cce0ff] rounded p-3">
        <Info className="w-3.5 h-3.5 shrink-0 mt-0.5 text-[#004c91]" />
        <span>
          Hệ thống tự động lấy thông tin của đầu mối khi gửi email. Bạn chỉ cần chọn nguồn đầu mối và các thông tin cần hiển thị; không nhập thủ công địa chỉ liên hệ.
        </span>
      </div>

      {/*
        The contradiction, next to the control that causes it and above the explanatory note below.
        Message and action are separate elements — never one concatenated string — so the sentence reads
        as a sentence and the button reads as a button. The defect this replaces rendered them joined:
        "…EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWEDXóa khối không hợp lệ".
      */}
      {crossFieldError && <CrossFieldError {...crossFieldError} />}

      {value.requirement === 'NONE' && !crossFieldError && (
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
        {settings.availableRequirements.map(level => (
          <label key={level}
                 className="flex items-start gap-2 rounded-lg border border-gray-200 px-3 py-2 cursor-pointer hover:bg-gray-50">
            <input
              type="radio"
              name={`requirement-${templateCode}`}
              data-testid={`contact-requirement-${level}`}
              className="mt-1"
              checked={value.requirement === level}
              onChange={() => chooseRequirement(level)}
            />
            <span className="text-xs">
              <span className="font-semibold text-gray-800">{REQUIREMENT_LABELS[level]?.title ?? level}</span>
              <span className="block text-gray-500">{REQUIREMENT_LABELS[level]?.hint}</span>
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
            {/* "Lấy đầu mối từ" until this release. What the field chooses is where the recipient's
                contact details are read from when the mail is sent; "đầu mối" is how the policy is
                discussed internally and does not say that on a screen. */}
            <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor={`source-${templateCode}`}>
              Nguồn thông tin liên hệ
            </label>
            <select
              id={`source-${templateCode}`}
              disabled={!canEdit}
              value={value.contactSource}
              onChange={e => set('contactSource', e.target.value as EmailContactSettingsPayload['contactSource'])}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
            >
              {settings.availableSources.map(source => (
                <option key={source} value={source}>{SOURCE_LABELS[source] ?? source}</option>
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
                  <input
                    type="checkbox"
                    checked={value[key as keyof EmailContactSettingsPayload] as boolean}
                    onChange={e => set(key as keyof EmailContactSettingsPayload, e.target.checked as never)}
                  />
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
                value={value.headingVi}
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
                value={value.headingEn}
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
              value={value.replyToSource}
              onChange={e => set('replyToSource', e.target.value as EmailContactSettingsPayload['replyToSource'])}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-50"
            >
              {settings.availableReplyToSources.map(source => (
                <option key={source} value={source}>{REPLY_TO_LABELS[source] ?? source}</option>
              ))}
            </select>
            {value.replyToSource === 'CONTACT' && !value.showEmail && (
              <p className="mt-1.5 text-xs text-red-700" data-testid="contact-settings-replyto-hidden">
                Reply-To trỏ về đầu mối nhưng email của đầu mối đang bị ẩn — người nhận sẽ không thấy thư
                trả lời sẽ đi đâu.
              </p>
            )}
          </div>
        </>
      )}
    </div>
  );
}

/**
 * One refusal: an icon, a sentence, and — separately — a button.
 *
 * Three elements rather than one string, which is the whole point of extracting it. The failure this
 * replaces rendered a raw error code and an action label concatenated into a single run of text
 * ("…EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWEDXóa khối không hợp lệ"), which is unreadable and not
 * clickable. No code is shown here at all: a stable code is for matching in software, and the sentence
 * beside it already says what a person has to do.
 */
function CrossFieldError({
  message, actionLabel, onAction,
}: { message: string; actionLabel?: string; onAction?: () => void }) {
  return (
    <div className="rounded border-l-4 border-red-400 bg-red-50 p-3 text-xs text-red-800 space-y-2"
         data-testid="contact-settings-cross-field-error">
      <div className="flex items-start gap-2">
        <ShieldAlert className="w-4 h-4 shrink-0 mt-0.5 text-red-500" />
        <span>{message}</span>
      </div>
      {actionLabel && onAction && (
        <div className="pl-6">
          <button
            type="button"
            data-testid="contact-settings-remove-block"
            onClick={onAction}
            className="rounded border border-red-300 bg-white px-2 py-1 text-[11px] font-semibold text-red-700 hover:bg-red-100"
          >
            {actionLabel}
          </button>
        </div>
      )}
    </div>
  );
}

/** The ten fields a save writes, taken off the fuller response the GET returns. */
export function toContactPayload(s: EmailContactSettings): EmailContactSettingsPayload {
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
