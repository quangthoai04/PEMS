import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { Search, Plus, Edit2, Check, X, ShieldAlert, Loader2, Lock, Info, RotateCcw } from 'lucide-react';
import { emailsApi } from '../../../features/emails/api/emailsApi';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';
import { ContactSettingsPanel } from '../../../features/emails/components/ContactSettingsPanel';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import {
  SYSTEM_BLOCK_LABELS,
  TEMPLATE_ERROR_CODES,
  applySamples,
  applySystemBlocks,
  errorCodeOf,
  issuesFromError,
  validateContent,
  type ContractState,
  type TemplateContentField,
  type TemplateContentIssue,
} from '../../../features/emails/types/templateContract';

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    [{ align: [] }],
    ['clean']
  ]
};

type EditorLanguage = 'VI' | 'EN';

interface TemplateForm {
  templateCode: string;
  name: string;
  description: string;
  subjectVi: string;
  bodyVi: string;
  subjectEn: string;
  bodyEn: string;
  status: string;
  /**
   * The optimistic-concurrency token, held exactly as the server gave it. Never derived, never
   * defaulted: a fabricated revision is an unconditional overwrite wearing a version number.
   */
  revision: number | null;
  /** Whether PEMS ships a default for this code — decides whether restore is offered at all. */
  hasShippedDefault: boolean;
  updatedAt: string | null;
}

const EMPTY_FORM: TemplateForm = {
  templateCode: '', name: '', description: '',
  subjectVi: '', bodyVi: '', subjectEn: '', bodyEn: '',
  status: 'ACTIVE', revision: null, hasShippedDefault: false, updatedAt: null,
};

/**
 * The system email template catalog (UC-42/43/44).
 *
 * Two things changed here in G11 and both were user-visible defects rather than tidying.
 *
 * G11-J — the variable contract. This screen used to hold its own list of eleven variables and check
 * every template against it. `ACCOUNT_EMAIL_CONFIRMATION` declares fullName, roleName, campusName and
 * expiresInHours, none of which were in that list, so an untouched canonical template opened with a
 * warning on every variable it legitimately used — and the sidebar simultaneously offered logistics
 * variables that template can never be given a value for. The contract is now fetched per template
 * code, and nothing is validated until it has arrived.
 *
 * G11-I — the catalog is fixed. "Thêm mẫu mới" and the per-row status toggle are gone, and not only
 * from the UI: the backend refuses both with EMAIL_TEMPLATE_CATALOG_FIXED. A template code exists
 * because a caller in a release sends it, so one invented here would be a row nothing can address; and
 * deactivating a system template does not switch a feature off, it breaks one — the renderer refuses an
 * inactive template, so turning off ACCOUNT_EMAIL_CONFIRMATION would leave every new account
 * unconfirmed with nothing on screen explaining why. That used to be one click, on a table row, with no
 * confirmation.
 */
/**
 * How many templates this screen asks for in one request.
 *
 * The catalog is fixed at thirty (G11-I) and is not paged here — it is loaded whole and filtered in the
 * browser. The number is a generous ceiling rather than exactly thirty so that adding a template in a
 * future release does not silently hide it; if the catalog ever outgrows this, the completeness check in
 * `fetchData` says so on screen instead of quietly showing a prefix.
 */
const CATALOG_PAGE_SIZE = 200;

export function TemplateManagement({ pushToast }: { pushToast: (type: 'success' | 'error', msg: string) => void }) {
  const quillRef = useRef<any>(null);
  const subjectInputRef = useRef<HTMLInputElement>(null);
  const [data, setData] = useState<any[]>([]);
  /** Set when the server reports more templates than it returned — see `fetchData`. */
  const [truncated, setTruncated] = useState<{ shown: number; total: number } | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<TemplateForm>(EMPTY_FORM);
  const [language, setLanguage] = useState<EditorLanguage>('VI');
  const [submitting, setSubmitting] = useState(false);
  const [restoring, setRestoring] = useState(false);
  const [varSearch, setVarSearch] = useState('');

  /**
   * The contact block as card 4's CURRENT draft would render it — empty string when the policy renders
   * nothing (NONE, or both channels hidden), which the preview shows as the block simply not being
   * there. Owned here rather than inside the panel because the pane that displays it is up here.
   */
  const [contactBlockPreview, setContactBlockPreview] = useState('');
  const [contract, setContract] = useState<ContractState>({ status: 'idle' });
  const [serverIssues, setServerIssues] = useState<TemplateContentIssue[]>([]);
  const [confirmState, setConfirmState] = useState<{isOpen: boolean; onConfirm: () => void; message: string; title: string; variant?: 'warning' | 'danger' | 'default'}>({isOpen: false, onConfirm: () => {}, message: '', title: ''});

  /**
   * Guards against a stale contract landing after the operator has moved on. Without it, opening
   * template A then B quickly could leave B being validated against A's variables — the same class of
   * bug as the hard-coded list, just harder to see.
   */
  const contractRequestId = useRef(0);

  const subjectField: TemplateContentField = language === 'EN' ? 'subjectEn' : 'subjectVi';
  const bodyField: TemplateContentField = language === 'EN' ? 'bodyEn' : 'bodyVi';

  const contentByField = useMemo(() => ({
    subjectVi: formData.subjectVi,
    bodyVi: formData.bodyVi,
    subjectEn: formData.subjectEn,
    bodyEn: formData.bodyEn,
  }), [formData.subjectVi, formData.bodyVi, formData.subjectEn, formData.bodyEn]);

  /**
   * Client-side issues, computed ONLY when the contract for THIS template is ready. In every other
   * state the list is empty: an operator must not be shown warnings derived from a contract that has
   * not arrived, or from a different template's.
   */
  const localIssues = useMemo<TemplateContentIssue[]>(() => {
    if (contract.status !== 'ready') return [];
    return validateContent(contract.contract, contentByField);
  }, [contract, contentByField]);

  const issues = useMemo(
    () => [...localIssues, ...serverIssues],
    [localIssues, serverIssues],
  );

  /**
   * A row kept only because a sent email or draft still points at it, rather than a registered system
   * template. The API refuses to save one (EMAIL_TEMPLATE_CATALOG_FIXED), so the editor presents it
   * read-only instead of letting an operator type into fields whose save cannot succeed.
   */
  const isHistorical = contract.status === 'ready' && !contract.contract.isSystemTemplate;

  const issuesForField = useCallback(
    (field: TemplateContentField) => issues.filter(i => i.field === field),
    [issues],
  );

  const blockingIssues = useMemo(
    () => issues.filter(i => i.severity === 'ERROR'),
    [issues],
  );

  const loadContract = useCallback(async (templateCode: string) => {
    const requestId = ++contractRequestId.current;
    setContract({ status: 'loading', templateCode });

    try {
      const res = await emailsApi.getEmailTemplateContract(templateCode, 'VI');
      // An API built before the contract was split answers without the block lists. Treated as "this
      // template declares no blocks" rather than allowed to reach the renderer as undefined, where
      // reading .length would blank the whole screen — the same class of failure as the contact card's,
      // and not one to reintroduce while fixing it.
      res.data.requiredSystemBlocks ??= [];
      res.data.optionalSystemBlocks ??= [];
      res.data.systemBlockPreviews ??= {};
      if (requestId !== contractRequestId.current) return;   // superseded
      setContract({ status: 'ready', templateCode, contract: res.data });
    } catch {
      if (requestId !== contractRequestId.current) return;
      setContract({
        status: 'error',
        templateCode,
        message: 'Không tải được danh sách biến của mẫu này. Vui lòng thử lại — nội dung vẫn xem được nhưng chưa kiểm tra biến.',
      });
    }
  }, []);

  const fetchData = async () => {
    setIsLoading(true);
    try {
      // The page size is passed EXPLICITLY. `ViewEmailTemplateListQuery` defaults to PageSize = 10, so
      // calling with no arguments returned the first ten of thirty — and this screen filters in the
      // browser, over what was loaded. The other twenty were therefore not merely off-screen: searching
      // for one returned "Không tìm thấy mẫu email nào", which reads as "no such template" rather than
      // "not loaded". This is the only surface offering Chỉnh sửa and Phục hồi mặc định, and both are
      // HO-only, so a template missing from this list could not be corrected or restored at all — which
      // is precisely the repair path the fixed catalog (G11-I) exists to guarantee.
      const res = await emailsApi.getEmailTemplateList({ page: 1, pageSize: CATALOG_PAGE_SIZE });
      const items = res.data.items || res.data.templates || [];
      setData(items);

      // Never render a subset that looks complete. If the server says the catalog is larger than what
      // came back, the honest thing is to say so — the failure this replaces was silent, and silence is
      // what made it survive a full verification round.
      const reported = res.data.totalItems;
      setTruncated(
        typeof reported === 'number' && reported > items.length
          ? { shown: items.length, total: reported }
          : null,
      );
    } catch {
      pushToast('error', 'Không thể tải danh sách mẫu email');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleEdit = async (id: number) => {
    try {
      const res = await emailsApi.getEmailTemplateDetail(id);
      const t = res.data;
      setFormData({
        templateCode: t.templateCode || '',
        name: t.name || '',
        description: t.description || '',
        subjectVi: t.subjectVi || '',
        bodyVi: t.bodyVi || '',
        subjectEn: t.subjectEn || '',
        bodyEn: t.bodyEn || '',
        status: t.status || 'ACTIVE',
        // The concurrency token, straight from the server. It used to be `updatedAt ?? createdAt`,
        // which could not distinguish two saves inside the same second — the column has no
        // sub-second part, so both stored the same stamp and the later save silently won.
        revision: typeof t.revision === 'number' ? t.revision : null,
        hasShippedDefault: t.hasShippedDefault === true,
        updatedAt: t.updatedAt ?? t.createdAt ?? null,
      });
      setEditingId(t.emailTemplateId || id);
      setServerIssues([]);
      setLanguage('VI');
      setShowForm(true);
      if (t.templateCode) void loadContract(t.templateCode);
      else setContract({ status: 'idle' });
    } catch {
      pushToast('error', 'Không thể tải chi tiết mẫu email');
    }
  };

  const handleSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();

    // Reachable with the button disabled: Enter in a text input submits the form.
    if (isHistorical) {
      pushToast('error', 'Mẫu lịch sử không thuộc danh mục mẫu hệ thống nên không thể chỉnh sửa.');
      return;
    }

    if (!formData.name.trim()) {
      pushToast('error', 'Vui lòng nhập tên mẫu');
      return;
    }

    // A save while the contract is still in flight would be validated by the backend anyway, but the
    // operator would get a round-trip failure instead of being told to wait a moment.
    if (contract.status === 'loading') {
      pushToast('error', 'Đang tải danh sách biến của mẫu, vui lòng đợi một chút.');
      return;
    }

    if (blockingIssues.length > 0) {
      pushToast('error', `Còn ${blockingIssues.length} vấn đề cần sửa trước khi lưu.`);
      return;
    }

    void executeSubmit();
  };

  const executeSubmit = async () => {
    if (!editingId) return;

    setSubmitting(true);
    setServerIssues([]);
    try {
      const res = await emailsApi.updateEmailTemplate(editingId, {
        name: formData.name,
        description: formData.description || null,
        subjectVi: formData.subjectVi || null,
        bodyVi: formData.bodyVi || null,
        subjectEn: formData.subjectEn || null,
        bodyEn: formData.bodyEn || null,
        expectedRevision: formData.revision,
      });

      // Keep the new token: without it the editor's own next save would be refused as a conflict with
      // the change it just made.
      setFormData(prev => ({
        ...prev,
        revision: typeof res.data?.revision === 'number' ? res.data.revision : prev.revision,
        updatedAt: res.data?.updatedAt ?? prev.updatedAt,
      }));
      pushToast('success', 'Đã cập nhật mẫu email');
      setShowForm(false);
      fetchData();
    } catch (err: any) {
      const code = errorCodeOf(err);
      const fromServer = issuesFromError(err);

      if (fromServer.length > 0) {
        setServerIssues(fromServer);
        pushToast('error', `Còn ${fromServer.length} vấn đề cần sửa trước khi lưu.`);
      } else if (code === TEMPLATE_ERROR_CODES.concurrencyConflict) {
        pushToast('error', err.response?.data?.message
          ?? 'Mẫu này đã được người khác cập nhật. Vui lòng mở lại để xem nội dung mới nhất.');
      } else {
        pushToast('error', err.response?.data?.message || 'Có lỗi xảy ra khi lưu mẫu email');
      }
    } finally {
      setSubmitting(false);
    }
  };

  /**
   * Restore to the wording PEMS ships. Confirmed first, and the confirmation names the template and
   * says both languages go — an operator who has spent an afternoon on the Vietnamese copy must not
   * discover after the fact that "restore" meant that too.
   *
   * On a concurrency conflict nothing is retried. Restoring harder would overwrite exactly the change
   * the conflict is warning about; the only correct move is to reload and look.
   */
  const handleRestoreDefault = () => {
    if (!editingId || formData.revision === null) return;

    setConfirmState({
      isOpen: true,
      title: 'Phục hồi nội dung mặc định',
      variant: 'danger',
      message:
        `Phục hồi mẫu "${formData.templateCode}" về nội dung mặc định của hệ thống?\n\n` +
        'Tên, mô tả, tiêu đề và nội dung của CẢ tiếng Việt và tiếng Anh sẽ bị thay bằng bản hệ thống. ' +
        'Các thay đổi bạn đã lưu trước đó sẽ không còn trên mẫu (vẫn được ghi lại trong lịch sử thao tác).\n\n' +
        'Mã mẫu, phân loại và trạng thái không thay đổi.',
      onConfirm: () => {
        setConfirmState(prev => ({ ...prev, isOpen: false }));
        void executeRestoreDefault();
      },
    });
  };

  const executeRestoreDefault = async () => {
    if (!editingId || formData.revision === null) return;

    setRestoring(true);
    setServerIssues([]);
    try {
      const res = await emailsApi.restoreEmailTemplateDefault(editingId, formData.revision);
      const t = res.data;

      // Show what is now stored, with the new token, in one step. Leaving the operator's discarded text
      // on screen next to a bumped revision would invite them to "save" it straight back.
      setFormData(prev => ({
        ...prev,
        name: t?.name ?? prev.name,
        description: t?.description ?? '',
        subjectVi: t?.subjectVi ?? '',
        bodyVi: t?.bodyVi ?? '',
        subjectEn: t?.subjectEn ?? '',
        bodyEn: t?.bodyEn ?? '',
        revision: typeof t?.revision === 'number' ? t.revision : prev.revision,
        updatedAt: t?.updatedAt ?? prev.updatedAt,
      }));
      pushToast('success', 'Đã phục hồi nội dung mặc định của mẫu email');
      fetchData();
    } catch (err: any) {
      const code = errorCodeOf(err);
      const fromServer = issuesFromError(err);

      if (fromServer.length > 0) {
        // The shipped default itself failed the contract — a seed defect, not the operator's mistake.
        setServerIssues(fromServer);
        pushToast('error', 'Nội dung mặc định của mẫu này không hợp lệ; chưa phục hồi. Vui lòng báo quản trị hệ thống.');
      } else if (code === TEMPLATE_ERROR_CODES.concurrencyConflict) {
        pushToast('error', err.response?.data?.message
          ?? 'Mẫu này đã được người khác cập nhật. Vui lòng mở lại để xem nội dung mới nhất rồi phục hồi.');
      } else if (code === TEMPLATE_ERROR_CODES.defaultUnavailable) {
        pushToast('error', 'Mẫu này không có nội dung mặc định để phục hồi.');
      } else {
        pushToast('error', err.response?.data?.message || 'Có lỗi xảy ra khi phục hồi nội dung mặc định');
      }
    } finally {
      setRestoring(false);
    }
  };

  const handleCancel = () => {
    setConfirmState({
      isOpen: true,
      title: 'Đóng trình sửa',
      message: 'Đóng và bỏ các thay đổi chưa lưu?',
      variant: 'danger',
      onConfirm: () => {
        setConfirmState(prev => ({...prev, isOpen: false}));
        setShowForm(false);
      }
    });
  };

  const insertVariable = (name: string) => {
    const token = `{{${name}}}`;
    const editor = quillRef.current?.getEditor();

    if (editor && editor.hasFocus()) {
      const range = editor.getSelection(true);
      editor.insertText(range.index, token);
      setFormData(prev => ({ ...prev, [bodyField]: editor.root.innerHTML }));
    } else if (document.activeElement === subjectInputRef.current) {
      const input = subjectInputRef.current!;
      const start = input.selectionStart || 0;
      const end = input.selectionEnd || 0;
      const current = formData[subjectField];
      setFormData(prev => ({
        ...prev,
        [subjectField]: current.substring(0, start) + token + current.substring(end),
      }));
    } else {
      setFormData(prev => ({ ...prev, [bodyField]: prev[bodyField] + token }));
    }
  };

  const filteredData = data.filter(item => {
    const term = searchQuery.toLowerCase();
    const matchSearch = (item.name || '').toLowerCase().includes(term) || (item.templateCode || '').toLowerCase().includes(term);
    const matchStatus = statusFilter ? item.status === statusFilter : true;
    return matchSearch && matchStatus;
  });

  const renderIssueList = (field: TemplateContentField) => {
    const fieldIssues = issuesForField(field);
    if (fieldIssues.length === 0) return null;

    return (
      <ul className="mt-1.5 space-y-1" data-testid={`issues-${field}`}>
        {fieldIssues.map((issue, idx) => (
          <li key={`${issue.code}-${issue.variableName ?? idx}`} className="flex items-start gap-1.5 text-xs text-red-700">
            <ShieldAlert className="w-3.5 h-3.5 mt-0.5 flex-shrink-0 text-red-500" />
            <span>
              {issue.variableName && (
                <span className="font-mono font-bold">{`{{${issue.variableName}}}`}</span>
              )}{' '}
              {issue.messageVi}
              <span className="ml-1 text-[10px] font-mono text-red-400">{issue.code}</span>
            </span>
          </li>
        ))}
      </ul>
    );
  };

  if (showForm) {
    const ready = contract.status === 'ready' ? contract.contract : null;
    const previewSubject = ready ? applySamples(ready, formData[subjectField]) : formData[subjectField];

    // Variables first, then blocks. Order matters only in that a block's sample markup is trusted and
    // must not itself be scanned for variables — substituting it last means it never is.
    //
    // The contact block comes from card 4's live draft rather than the contract, so unticking a toggle
    // changes this pane immediately; the rest of the samples were built by the backend with the same
    // helpers the send uses.
    const previewBody = ready
      ? applySystemBlocks(
          ready,
          applySamples(ready, formData[bodyField]),
          contactBlockPreview ? { contactInformationBlock: contactBlockPreview } : {},
        )
      : formData[bodyField];
    const visibleVariables = (ready?.variables ?? []).filter(v =>
      v.label.toLowerCase().includes(varSearch.toLowerCase()) ||
      v.name.toLowerCase().includes(varSearch.toLowerCase()));

    return (
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div className="flex items-center justify-between border-b border-gray-100 pb-4 mb-6">
          <div>
            <h2 className="text-xl font-bold text-[#004c91]">Chỉnh sửa nội dung mẫu email</h2>
            <p className="text-xs text-gray-500 mt-1">
              Mã mẫu <span className="font-mono font-bold">{formData.templateCode}</span> do hệ thống quản lý và không thể thay đổi.
            </p>
          </div>
          <button onClick={() => setShowForm(false)} className="text-gray-400 hover:text-gray-700" aria-label="Đóng">
            <X className="w-6 h-6" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="space-y-6">
          {/*
            Two explicit columns rather than three-with-a-span, and `items-start` rather than the
            default stretch.

            The aside used to be a stretched grid item whose first card carried `h-full`. That card
            then took the whole row height — set by the much taller editor column — and the contact
            card below it was laid out PAST the bottom of the column, over the action bar. Padding
            alone would only have moved that collision, so the stretch is what is removed: each card
            is now as tall as its own content, and the column is sized in px instead of a fraction so
            a long template code or a wide select cannot widen it at the editor's expense.

            `pb-24` reserves the action bar's height beneath the last card, which is what lets the bar
            be sticky without covering anything.
          */}
          <div className="grid grid-cols-1 gap-6 pb-24 xl:grid-cols-[minmax(0,1fr)_380px] xl:items-start">
            <div className="min-w-0 space-y-6">
              {/* 1. Thông tin chung */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2">1. Thông tin chung</h3>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Mã mẫu</label>
                    <div className="flex items-center gap-2 w-full rounded-lg border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-600">
                      <Lock className="w-3.5 h-3.5 text-gray-400" />
                      <span className="font-mono">{formData.templateCode}</span>
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor="tpl-name">Tên mẫu *</label>
                    <input id="tpl-name" type="text" value={formData.name} disabled={isHistorical} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500" />
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Trạng thái</label>
                    <div className="flex items-center gap-2 w-full rounded-lg border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-600">
                      <Lock className="w-3.5 h-3.5 text-gray-400" />
                      {formData.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor="tpl-desc">Mô tả quản trị</label>
                    <input id="tpl-desc" type="text" value={formData.description} disabled={isHistorical} onChange={e => setFormData({...formData, description: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500" />
                  </div>
                </div>
              </div>

              {/* 2. Nội dung email */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <div className="flex items-center justify-between border-b border-gray-200 pb-2 mb-4">
                  <h3 className="font-bold text-gray-800 text-base">2. Nội dung email</h3>
                  <div className="flex rounded-md overflow-hidden border border-gray-300" role="group" aria-label="Ngôn ngữ">
                    {(['VI', 'EN'] as EditorLanguage[]).map(lang => (
                      <button
                        key={lang}
                        type="button"
                        onClick={() => setLanguage(lang)}
                        aria-pressed={language === lang}
                        className={`px-3 py-1 text-xs font-bold transition-colors ${language === lang ? 'bg-[#004c91] text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
                      >
                        {lang === 'VI' ? 'Tiếng Việt' : 'English'}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor="tpl-subject">Tiêu đề (Subject)</label>
                    <input
                      id="tpl-subject"
                      ref={subjectInputRef}
                      type="text"
                      value={formData[subjectField]}
                      disabled={isHistorical}
                      onChange={e => setFormData({...formData, [subjectField]: e.target.value})}
                      className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500"
                    />
                    {renderIssueList(subjectField)}
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Nội dung (Body)</label>
                    <div className="border border-gray-300 rounded-lg overflow-hidden bg-white">
                      <ReactQuill
                        ref={quillRef}
                        theme="snow"
                        value={formData[bodyField]}
                        readOnly={isHistorical}
                        onChange={v => setFormData(prev => ({ ...prev, [bodyField]: v }))}
                        modules={QUILL_MODULES}
                        className="min-h-[250px]"
                      />
                    </div>
                    {renderIssueList(bodyField)}
                  </div>
                </div>
              </div>

              {/* Xem trước hiển thị */}
              <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-sm mt-6">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2">
                  Xem trước hiển thị
                  <span className="ml-2 text-[11px] font-normal text-gray-500">
                    (dữ liệu mẫu do hệ thống cung cấp)
                  </span>
                </h3>

                <div className="border border-gray-200 rounded-lg p-4 text-sm text-gray-700 min-h-[150px] shadow-inner bg-gray-50/30">
                  <div className="font-bold border-b border-gray-100 pb-2 mb-2">
                    {previewSubject || <span className="text-gray-400 italic">Chưa có tiêu đề...</span>}
                  </div>
                  {/* Sanitised after variable substitution: the samples are inserted into the markup,
                      so the substituted result is what this browser must be protected from. */}
                  {/* `prose prose-sm` used to be here. It is a no-op today — the Tailwind typography
                      plugin is not installed — but it is markup that says "restyle this", and the one
                      thing a preview of an email must not do is restyle it. Replaced by the isolation
                      class, which only undoes inherited layout and lets a wide table scroll. */}
                  <div className="pems-email-body max-w-none" data-testid="preview-body" dangerouslySetInnerHTML={{
                    __html: sanitizeHtml(previewBody || '<span class="text-gray-400 italic">Chưa có nội dung...</span>')
                  }} />
                </div>
              </div>
            </div>

            <aside className="min-w-0 space-y-6">
              {/* 3. Biến của mẫu này. No `h-full`: see the grid comment above — it was the cause of
                  the contact card overflowing onto the action bar. The card is as tall as its own
                  content, and the variable list below keeps its own scroll. */}
              <div className="bg-[#f8fbff] p-5 rounded-lg border border-[#cce0ff] flex flex-col">
                <h3 className="font-bold text-[#004c91] mb-1 text-base border-b border-[#cce0ff] pb-2">
                  3. Biến của mẫu này
                </h3>
                <p className="text-[11px] text-gray-500 mb-3">
                  Chỉ những biến dưới đây mới có giá trị khi gửi mẫu <span className="font-mono">{formData.templateCode}</span>.
                </p>

                {contract.status === 'loading' && (
                  <div className="flex items-center gap-2 text-sm text-gray-500 py-4" data-testid="contract-loading">
                    <Loader2 className="w-4 h-4 animate-spin" /> Đang tải danh sách biến…
                  </div>
                )}

                {contract.status === 'error' && (
                  <div className="text-xs text-orange-800 bg-orange-50 border-l-4 border-orange-400 p-3 rounded" data-testid="contract-error">
                    {contract.message}
                  </div>
                )}

                {ready && !ready.isSystemTemplate && (
                  <div className="text-xs text-gray-700 bg-gray-100 border-l-4 border-gray-400 p-3 rounded space-y-2" data-testid="contract-not-system">
                    <p className="font-bold">Mẫu lịch sử — chỉ xem, không sửa được.</p>
                    <p>
                      Mẫu này không còn thuộc danh mục mẫu hệ thống. Bản ghi được giữ lại vì lịch sử
                      email hoặc bản nháp còn tham chiếu đến nó.
                    </p>
                    {/* Said plainly because the alternative is worse: the screen used to check this
                        template's variables against an empty list and mark every one of them sai. */}
                    <p>
                      Hệ thống không quản lý danh sách biến cho mẫu này, nên không thể kiểm tra biến
                      trong tiêu đề và nội dung. Các biến hiển thị bên dưới được giữ nguyên như đã lưu.
                    </p>
                  </div>
                )}

                {ready && ready.isSystemTemplate && (
                  <>
                    {ready.carriesSecret && (
                      <div className="mb-3 flex items-start gap-1.5 text-[11px] text-amber-800 bg-amber-50 border border-amber-200 rounded p-2">
                        <Info className="w-3.5 h-3.5 mt-0.5 flex-shrink-0" />
                        <span>Mẫu này chứa mã hoặc liên kết dùng một lần nên không được phép CC/BCC khi gửi.</span>
                      </div>
                    )}

                    {/*
                      System blocks, listed as blocks. They are NOT in the variable list below and must
                      not be: the backend builds their markup and an operator can neither author it nor
                      supply a value. Previously only {{actionBlock}} was named here, so the contact
                      block — mandatory on fourteen templates — appeared nowhere on this screen at all,
                      and an operator who deleted it learnt of its existence from a refusal.
                    */}
                    {(ready.requiredSystemBlocks.length > 0 || ready.optionalSystemBlocks.length > 0) && (
                      <div className="mb-3 text-[11px] text-blue-900 bg-blue-50 border border-blue-200 rounded p-2"
                           data-testid="system-blocks-notice">
                        <div className="flex items-start gap-1.5 mb-1">
                          <Lock className="w-3.5 h-3.5 mt-0.5 flex-shrink-0" />
                          <span className="font-semibold">Khối hệ thống — nội dung do hệ thống dựng</span>
                        </div>
                        <ul className="pl-5 space-y-1">
                          {ready.requiredSystemBlocks.map(block => (
                            <li key={block} data-testid={`system-block-required-${block}`}>
                              <span className="font-mono font-bold">{`{{${block}}}`}</span>{' '}
                              <span className="rounded bg-blue-100 px-1 py-0.5 font-semibold">bắt buộc giữ</span>
                              <span className="block text-blue-800">
                                {SYSTEM_BLOCK_LABELS[block]?.hint ?? 'Hệ thống điền nội dung khi gửi.'}
                              </span>
                            </li>
                          ))}
                          {ready.optionalSystemBlocks.map(block => (
                            <li key={block} data-testid={`system-block-optional-${block}`}>
                              <span className="font-mono font-bold">{`{{${block}}}`}</span>{' '}
                              <span className="rounded bg-gray-100 px-1 py-0.5 text-gray-700">tùy chọn</span>
                              <span className="block text-blue-800">
                                {SYSTEM_BLOCK_LABELS[block]?.hint ?? 'Hệ thống điền nội dung khi gửi.'}
                              </span>
                            </li>
                          ))}
                        </ul>
                        <p className="pl-5 mt-1 text-blue-800">
                          Có thể di chuyển vị trí khối trong nội dung, nhưng không sửa được bên trong và
                          không nhập tay giá trị thay thế.
                        </p>
                      </div>
                    )}

                    <div className="relative mb-4 flex-shrink-0">
                      <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                      <input
                        type="text"
                        value={varSearch}
                        onChange={e => setVarSearch(e.target.value)}
                        placeholder="Tìm biến..."
                        aria-label="Tìm biến"
                        className="w-full pl-9 pr-3 py-1.5 text-sm border border-gray-300 rounded-md focus:border-[#004c91] outline-none"
                      />
                    </div>

                    <div className="space-y-2 overflow-y-auto flex-1 pr-2 pb-4"
                         data-testid="variable-sidebar">
                      {visibleVariables.length === 0 && (
                        <div className="text-xs text-gray-400 italic">Không tìm thấy biến</div>
                      )}
                      <div className="flex flex-wrap gap-1.5">
                        {visibleVariables.map(v => (
                          <button
                            key={v.name}
                            type="button"
                            onClick={() => insertVariable(v.name)}
                            className="inline-flex items-center gap-1 bg-white border border-[#004c91] text-[#004c91] hover:bg-[#004c91] hover:text-white transition-colors px-2 py-1 rounded text-[11px] font-bold outline-none"
                            title={`{{${v.name}}} — Ví dụ: ${v.sample}${v.forbiddenInSubject ? ' (không được đặt trong tiêu đề)' : ''}`}
                          >
                            <Plus className="w-3 h-3" /> {v.label}
                            {v.required && <span className="text-[9px] font-normal opacity-70">bắt buộc</span>}
                          </button>
                        ))}
                      </div>
                    </div>
                  </>
                )}
              </div>

              {/*
                4. Contact settings. A separate card because it saves on its own button rather than with
                the content form: the two are different kinds of change — one edits sentences, the other
                decides who the recipient is told to contact — and folding the settings into the content
                save would make every wording fix also rewrite the policy.

                This screen is HO-only (EmailManagement renders it only for HO) and the API enforces that
                independently, so passing canEdit is presentation, not protection.
              */}
              {/* Withheld for a historical row: contact policy is keyed on a registered template code,
                  so offering the panel here would present settings that resolve to nothing at send
                  time — this template has no sender in any release. */}
              {formData.templateCode && !isHistorical && (
                <div className="bg-white p-5 rounded-lg border border-gray-200">
                  <h3 className="font-bold text-[#004c91] mb-1 text-base border-b border-gray-200 pb-2">
                    4. Cấu hình thông tin liên hệ
                  </h3>
                  <p className="text-[11px] text-gray-500 mb-3">
                    Quyết định người nhận <span className="font-mono">{formData.templateCode}</span> nên
                    liên hệ với ai, và email được hiển thị những gì về họ.
                  </p>
                  <ContactSettingsPanel
                    templateCode={formData.templateCode}
                    canEdit
                    language={language}
                    onBlockPreviewChange={setContactBlockPreview}
                  />
                </div>
              )}
            </aside>
          </div>
          {/*
            The action bar sits OUTSIDE the content grid and stays in normal flow — `sticky`, not
            `fixed`. Sticky degrades to a plain block if an ancestor ever gains `overflow: hidden`,
            which leaves the bar at the end of the form; `fixed` in that situation would leave it
            floating over an unrelated part of the page. `-mx-6 px-6` bleeds it to the card's edges
            against the card's own `p-6`.
          */}
          <div className="sticky bottom-0 z-30 -mx-6 flex flex-wrap items-center justify-between gap-3 border-t border-gray-200 bg-white/95 px-6 py-4 backdrop-blur">
            {/*
              Restore lives on the left, away from the save button, because it discards work rather than
              saving it. Offered only when the backend says a shipped default exists for this code
              (hasShippedDefault) and only once a revision is in hand — restoring without a version is an
              unconditional overwrite. This screen is already HO-only (EmailManagement renders it only
              for HO); the API enforces that independently, so hiding the button is presentation, not
              protection.
            */}
            {formData.hasShippedDefault && formData.revision !== null ? (
              <button
                type="button"
                data-testid="restore-default"
                onClick={handleRestoreDefault}
                disabled={restoring || submitting}
                title="Phục hồi tên, mô tả, tiêu đề và nội dung VI/EN về bản hệ thống"
                className="flex items-center gap-2 px-4 py-2 font-bold text-[#b45309] bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 disabled:opacity-50"
              >
                {restoring ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
                Phục hồi mặc định
              </button>
            ) : <span />}

            {/* `ml-auto` keeps the pair on the right even when the bar wraps on a narrow viewport,
                where `justify-between` alone would leave them under the restore button on the left. */}
            <div className="ml-auto flex justify-end gap-3">
              <button type="button" onClick={handleCancel} className="px-4 py-2 font-bold text-gray-600 bg-gray-100 rounded-lg hover:bg-gray-200">Hủy</button>
              <button
                type="submit"
                disabled={submitting || restoring || contract.status === 'loading' || blockingIssues.length > 0 || isHistorical}
                title={isHistorical ? 'Mẫu lịch sử không nằm trong danh mục hệ thống nên không sửa được.' : undefined}
                className="flex items-center gap-2 px-4 py-2 font-bold text-white bg-[#004c91] rounded-lg hover:bg-[#013565] disabled:opacity-50"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                Cập nhật
              </button>
            </div>
          </div>
        </form>
        <ConfirmModal
          isOpen={confirmState.isOpen}
          onClose={() => setConfirmState(prev => ({...prev, isOpen: false}))}
          onConfirm={confirmState.onConfirm}
          title={confirmState.title}
          message={confirmState.message}
          variant={confirmState.variant}
        />
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden flex flex-col">
      <div className="p-4 border-b border-gray-100 flex items-center justify-between bg-gray-50/50">
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
            <input type="text" value={searchQuery} onChange={e => setSearchQuery(e.target.value)} placeholder="Tìm kiếm mẫu..." aria-label="Tìm kiếm mẫu" className="pl-9 pr-3 py-1.5 text-sm border border-gray-300 rounded-md focus:border-[#004c91] outline-none" />
          </div>
          <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)} aria-label="Lọc theo trạng thái" className="border border-gray-300 rounded-md px-3 py-1.5 text-sm outline-none">
            <option value="">Tất cả trạng thái</option>
            <option value="ACTIVE">Đang hoạt động</option>
            <option value="INACTIVE">Tạm khóa</option>
          </select>
        </div>
        {/* No "Thêm mẫu mới": the catalog is fixed in code (G11-I). */}
        <p className="text-xs text-gray-500 max-w-md text-right">
          Danh mục mẫu do hệ thống quản lý. Bạn có thể xem và cập nhật nội dung, không thể thêm hoặc xóa mẫu.
        </p>
      </div>

      {truncated && (
        <p
          role="alert"
          data-testid="catalog-truncated"
          className="border-b border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          <span aria-hidden="true">✕ </span>
          Chỉ tải được {truncated.shown}/{truncated.total} mẫu email. Danh sách bên dưới CHƯA đầy đủ —
          tìm kiếm và các thao tác chỉnh sửa, phục hồi chỉ áp dụng cho phần đã tải. Vui lòng tải lại
          trang; nếu vẫn còn, báo quản trị hệ thống.
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-sm text-left">
          <thead className="bg-[#004c91] text-white text-xs uppercase tracking-wider">
            <tr>
              <th className="px-4 py-3 font-bold text-center w-[60px]">STT</th>
              <th className="px-4 py-3 font-bold">Mã mẫu</th>
              <th className="px-4 py-3 font-bold">Tên mẫu</th>
              <th className="px-4 py-3 font-bold text-center">Trạng thái</th>
              <th className="px-4 py-3 font-bold text-center w-[150px]">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Đang tải...</td></tr>
            ) : filteredData.length === 0 ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Không tìm thấy mẫu email nào</td></tr>
            ) : (
              filteredData.map((item, index) => (
                <tr key={item.emailTemplateId} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-center text-gray-500">{index + 1}</td>
                  <td className="px-4 py-3 font-bold text-[#004c91]">{item.templateCode}</td>
                  <td className="px-4 py-3 font-medium">{item.name}</td>
                  <td className="px-4 py-3 text-center">
                    {/* Read-only: a system template's status is decided by the release that ships its
                        callers, and switching one off breaks the feature rather than disabling it. */}
                    <span className={`px-2.5 py-1 rounded-full text-xs font-bold border ${item.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                      {item.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <button onClick={() => handleEdit(item.emailTemplateId)} className="p-1.5 text-gray-500 hover:text-[#004c91] hover:bg-blue-50 rounded" title="Chỉnh sửa nội dung" aria-label={`Chỉnh sửa ${item.templateCode}`}>
                      <Edit2 className="w-4 h-4" />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
