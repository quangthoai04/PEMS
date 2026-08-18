import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { Search, Plus, Edit2, Check, X, ShieldAlert, Loader2, Lock, Info, RotateCcw, ChevronLeft, ChevronRight } from 'lucide-react';
import { emailsApi } from '../../../features/emails/api/emailsApi';
import {
  EmailRichTextEditor, type EmailRichTextEditorHandle,
} from '../../../features/emails/components/EmailRichTextEditor';
import { canonicalizeEmailHtml } from '../../../features/emails/utils/emailHtmlCanonicalizer';
import { buildTemplateDraftPreview } from '../../../features/emails/utils/templateDraftPreview';
import 'react-quill-new/dist/quill.snow.css';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';
import {
  SYSTEM_BLOCK_LABELS,
  TEMPLATE_ERROR_CODES,
  isSenderVariable,
  describeSystemBlocks,
  errorCodeOf,
  issuesFromError,
  removeSystemBlock,
  validateContent,
  type ContractState,
  type TemplateContentField,
  type TemplateContentIssue,
} from '../../../features/emails/types/templateContract';

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

/** Matches the `description` column and `UpdateEmailTemplateCommandValidator.DescriptionMaxLength`. */
const DESCRIPTION_MAX_LENGTH = 500;

/**
 * The six content fields "Lưu thay đổi" writes, alongside the contact settings.
 *
 * Named as a list because it answers a question that must not drift: what the one save button writes.
 * There used to be two lists for two buttons, and an operator who changed both and pressed one was left
 * believing they had saved everything.
 */
const CONTENT_FIELDS = ['name', 'description', 'subjectVi', 'bodyVi', 'subjectEn', 'bodyEn'] as const;

type ContentSnapshot = Record<(typeof CONTENT_FIELDS)[number], string>;

const contentOf = (form: TemplateForm): ContentSnapshot => ({
  name: form.name,
  description: form.description,
  subjectVi: form.subjectVi,
  bodyVi: form.bodyVi,
  subjectEn: form.subjectEn,
  bodyEn: form.bodyEn,
});

/** The two fields the rich-text editor owns; everything else in the snapshot is plain text. */
const BODY_FIELDS = ['bodyVi', 'bodyEn'] as const;

type BodyField = (typeof BODY_FIELDS)[number];

const isBodyField = (field: string): field is BodyField =>
  field === 'bodyVi' || field === 'bodyEn';

const LANGUAGE_LABELS: Record<EditorLanguage, string> = { VI: 'Tiếng Việt', EN: 'English' };

/**
 * Which tab an issue belongs to.
 *
 * This is the whole fix for the invisible refusal: an issue carries a FIELD, and a field belongs to a
 * language, but the screen only ever rendered issues under the field of the tab that happened to be
 * open. An English body missing `{{contactInformationBlock}}` therefore disabled "Lưu thay đổi" while
 * every message explaining why sat on a tab nobody was looking at.
 */
const languageOfField = (field: TemplateContentField | string): EditorLanguage | null => {
  if (field === 'subjectVi' || field === 'bodyVi') return 'VI';
  if (field === 'subjectEn' || field === 'bodyEn') return 'EN';
  return null;
};

/** Where the caret was in a plain-text input. */
type TextSelection = { start: number; end: number };

/**
 * Which control the operator was last writing in, per language.
 *
 * Needed because clicking a variable chip takes the focus away from whatever they were editing, so by
 * the time the insert runs neither the subject input nor the editor is focused any more. Reading
 * `document.activeElement` at that point answers "the chip", which is why insertion used to fall
 * through to appending at the end of the body — a variable landing three paragraphs below the sentence
 * it was meant for.
 *
 * Held per language because the two tabs are two different documents: a caret remembered in the
 * Vietnamese body must never be applied to the English one.
 */
type InsertTarget = 'subject' | 'body';

/**
 * The differences that are NOT an edit.
 *
 * "Có thay đổi chưa lưu" has to mean the operator changed something, so the comparison is made between
 * normalised values rather than between the exact strings. A body that arrives with CRLF line endings
 * and comes back from the editor with LF is the same body; so is one the API stored as null and the form
 * holds as ''.
 */
const normalizeText = (value: string | null | undefined) =>
  (value ?? '').replace(/\r\n?/g, '\n').trim();

/**
 * As above, but for a body — compared by MEANING rather than by spelling.
 *
 * <b>What this replaces.</b> The screen used to compare body strings and carry a per-language
 * `pendingEditorNormalization` flag to absorb the one change event the editor emits on mount, because a
 * controlled Quill re-serialises whatever html it is handed: `<br>` becomes `<br />`, style declarations
 * are reordered, a trailing empty paragraph appears. Without the flag the screen said "có thay đổi chưa
 * lưu" before the operator had touched anything; with it, the FIRST genuine edit to a body that had not
 * yet rendered was silently taken into the baseline instead of being reported as unsaved.
 *
 * The canonicaliser removes the need for either. Two bodies that say the same thing compare equal however
 * they are spelled, so the editor's own rendering of stored html is simply not a change — and every real
 * edit is, including the first one.
 */
const normalizeHtml = (value: string | null | undefined) => {
  const text = normalizeText(value);
  return /^<p>(?:\s|<br\s*\/?>)*<\/p>$/i.test(text) ? '' : canonicalizeEmailHtml(text);
};

const normalizeField = (field: (typeof CONTENT_FIELDS)[number], value: string) =>
  isBodyField(field) ? normalizeHtml(value) : normalizeText(value);

/**
 * The whole editable state of one template, reduced to a value that can be compared for equality.
 *
 * This is the single baseline the screen's ONE dirty flag is derived from. It replaces two independent
 * flags — one for content, one for the contact card — which between them could not answer "does this
 * screen have unsaved work" without the reader knowing which of two buttons they were being warned about.
 *
 * Everything is normalised on the way in, for the same reason the content fields always were: a
 * difference that no save would write is not an unsaved change. Null and '' are the same heading, CRLF
 * and LF are the same body, and an empty Quill document is the same as an empty stored one. Comparing
 * raw values would leave the screen claiming unsaved work the moment a template was opened.
 */
// The snapshot is content only now. It used to carry the contact settings as a second half, because
// one "có thay đổi chưa lưu" flag had to cover a form and a configuration card saved by the same
// button. There is one kind of thing on this screen again.
type EditorSnapshot = {
  content: ContentSnapshot;
};

const normalizeEditorSnapshot = (form: TemplateForm): EditorSnapshot => {
  const content = contentOf(form);

  return {
    content: CONTENT_FIELDS.reduce((acc, field) => {
      acc[field] = normalizeField(field, content[field]);
      return acc;
    }, {} as ContentSnapshot),
  };
};

/**
 * Structural equality over the snapshot.
 *
 * `JSON.stringify` would be shorter and is what the contact card used to do, but it compares KEY ORDER as
 * well as values — so a payload rebuilt from a server response with its properties in a different order
 * would read as an edit. The fields are enumerated instead.
 */
const sameSnapshot = (a: EditorSnapshot, b: EditorSnapshot): boolean =>
  CONTENT_FIELDS.every(field => a.content[field] === b.content[field]);

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

const getTemplateGroup = (code: string) => {
  if (code.startsWith('ACCOUNT_')) return 'Tài khoản';
  if (code.startsWith('AUTH_')) return 'Xác thực';
  if (code.startsWith('DEPT_')) return 'Nhân sự';
  if (code.startsWith('LOGISTICS_')) return 'Hậu cần';
  if (code.startsWith('REPORT_')) return 'Báo cáo';
  if (code.startsWith('VISIT_')) return 'Tiếp khách';
  return 'Khác';
};

export function TemplateManagement({ pushToast }: { pushToast: (type: 'success' | 'error', msg: string) => void }) {
  const bodyEditorRef = useRef<EmailRichTextEditorHandle | null>(null);
  const subjectInputRef = useRef<HTMLInputElement>(null);

  /**
   * The caret, remembered per language and per control, so a variable is inserted where the operator
   * was writing rather than wherever the DOM focus happens to be when they click a chip.
   *
   * All three are refs: nothing renders from a caret position, and putting it in state would re-render
   * the editor on every cursor move — which fights Quill for the caret it is trying to record.
   */
  const subjectSelection = useRef<Record<EditorLanguage, TextSelection | null>>({ VI: null, EN: null });
  // The BODY caret is no longer tracked here — the shared editor remembers its own, per mounted editor,
  // and the editors are keyed by language so the two tabs cannot share one.
  const lastInsertTarget = useRef<Record<EditorLanguage, InsertTarget | null>>({ VI: null, EN: null });

  /**
   * A field to focus once the tab has switched.
   *
   * "Chuyển sang English" has to change the tab AND put the operator in the control that is wrong, and
   * those cannot happen in one pass: the English editor does not exist in the DOM until the language
   * state has been committed and the body editor has remounted under its new key. So the request is
   * parked here and an effect performs it after the render.
   */
  const pendingFocus = useRef<InsertTarget | null>(null);
  const bodyEditorAnchor = useRef<HTMLDivElement>(null);
  const [data, setData] = useState<any[]>([]);
  /** Set when the server reports more templates than it returned — see `fetchData`. */
  const [truncated, setTruncated] = useState<{ shown: number; total: number } | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [groupFilter, setGroupFilter] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
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
  /**
   * The whole template as it is STORED — so "có thay đổi chưa
   * lưu" is a fact rather than "the operator typed something". Re-baselined after every successful save
   * and restore; leaving the old baseline would keep the warning on screen after the work had been saved,
   * which teaches operators to ignore it.
   *
   * A ref rather than state because nothing renders from it directly: it is one half of a comparison, and
   * holding it in state would re-render the editor a second time on every save for no visible change.
   */
  const baselineRef = useRef<EditorSnapshot>(normalizeEditorSnapshot(EMPTY_FORM));
  /** Bumped whenever the baseline moves, so the dirty check recomputes. */
  const [baselineVersion, setBaselineVersion] = useState(0);

  const setBaseline = useCallback((form: TemplateForm) => {
    baselineRef.current = normalizeEditorSnapshot(form);
    setBaselineVersion(v => v + 1);
  }, []);
  // `pendingEditorNormalization` was here: a per-language flag that absorbed the one change event a
  // controlled Quill emits on mount, because the screen compared body STRINGS and the editor's rendering
  // of stored html never matched it character for character. It is gone — `normalizeHtml` now compares
  // meaning, so the editor's re-serialisation is not a change and needs no flag, and the first real edit
  // to an un-rendered body is reported instead of being swallowed into the baseline.
  const [contract, setContract] = useState<ContractState>({ status: 'idle' });
  const [serverIssues, setServerIssues] = useState<TemplateContentIssue[]>([]);
  const [confirmState, setConfirmState] = useState<{
    isOpen: boolean;
    onConfirm: () => void;
    message: string;
    title: string;
    variant?: 'warning' | 'danger' | 'default';
    /** Named for the action, not "Xác nhận" — a dialog that discards work should say which one it is. */
    confirmText?: string;
    cancelText?: string;
  }>({isOpen: false, onConfirm: () => {}, message: '', title: ''});

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
   * The ONE unsaved-changes answer for the whole editor — DERIVED, never a flag that is switched on and
   * left on. An operator who undoes an edit back to what was stored has nothing unsaved, and the screen
   * has to agree with them.
   *
   * `baselineVersion` is in the dependency list rather than the baseline itself because the baseline is a
   * ref: without it the memo would keep the comparison it made against the PREVIOUS baseline and go on
   * reporting unsaved changes after a successful save.
   */
  const isDirty = useMemo(
    // eslint-disable-next-line react-hooks/exhaustive-deps
    () => !sameSnapshot(normalizeEditorSnapshot(formData), baselineRef.current),
    [formData, baselineVersion],
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

  /**
   * The blocking issues of each tab, whichever tab is open.
   *
   * Nothing here filters by the current language on purpose — that filtering is exactly the defect.
   * The save button is disabled by `blockingIssues`, which spans both languages, so the explanation has
   * to span both too or the operator is left with a dead button and a clean-looking form.
   */
  const blockingByLanguage = useMemo<Record<EditorLanguage, TemplateContentIssue[]>>(() => ({
    VI: blockingIssues.filter(i => languageOfField(i.field) === 'VI'),
    EN: blockingIssues.filter(i => languageOfField(i.field) === 'EN'),
  }), [blockingIssues]);

  /** Blocking issues that name no language (a server refusal about the template as a whole). */
  const blockingWithoutLanguage = useMemo(
    () => blockingIssues.filter(i => languageOfField(i.field) === null),
    [blockingIssues],
  );

  /**
   * Switches to the tab that is wrong and puts the caret in the control that is wrong.
   *
   * Nothing is inserted or corrected — the operator is taken to the problem, not relieved of it.
   * Auto-inserting a missing block would be an edit they did not make, on content they may have written
   * deliberately, and the two legitimate repairs (add the block back, or lower the display level) are a
   * decision only they can take.
   */
  const goToIssue = useCallback((lang: EditorLanguage, issue?: TemplateContentIssue) => {
    // The body unless the issue is specifically about a subject: most refusals are body ones, and a
    // caret in the body is the useful place to arrive even for an issue with no field at all.
    pendingFocus.current =
      issue?.field === 'subjectVi' || issue?.field === 'subjectEn' ? 'subject' : 'body';
    setLanguage(lang);
  }, []);

  /**
   * Performs a parked focus request once the tab it belongs to has rendered.
   *
   * Runs on `language` rather than on the request itself: the body editor is keyed by field, so it is a
   * NEW element after a tab switch, and focusing before that remount would put the caret in the editor
   * being unmounted.
   */
  useEffect(() => {
    const target = pendingFocus.current;
    if (!target) return;
    pendingFocus.current = null;

    // Guarded rather than called directly: `scrollIntoView` is missing in jsdom, and an exception here
    // would abort the focus that is the more important half of the action.
    const reveal = (element: Element | null | undefined) =>
      element?.scrollIntoView?.({ block: 'center', behavior: 'smooth' });

    // A frame later: Quill finishes attaching its own DOM inside an effect of its own, and calling
    // focus() in the same tick lands on an editor that is not editable yet.
    const timer = window.setTimeout(() => {
      if (target === 'subject') {
        reveal(subjectInputRef.current);
        subjectInputRef.current?.focus();
        return;
      }

      reveal(bodyEditorAnchor.current);
      try {
        // Focus the editor surface itself. The shared editor owns its Quill instance, so the screen
        // reaches for the DOM node rather than through a handle it no longer holds.
        bodyEditorAnchor.current?.querySelector<HTMLElement>('.ql-editor')?.focus();
      } catch {
        /* an unmounted editor is not worth reporting — the tab still switched, which is the main thing */
      }
    }, 0);

    return () => window.clearTimeout(timer);
  }, [language]);

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
      const loaded: TemplateForm = {
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
      };
      setFormData(loaded);
      setBaseline(loaded);
      setEditingId(t.emailTemplateId || id);
      setServerIssues([]);
      // A different document: nothing remembered about the previous one applies to it.
      resetInsertState();
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

    // Reachable with the button disabled, the same way `isHistorical` is: Enter in a text input submits.
    // A no-op save would still bump the revision and write an audit row saying the template changed.
    if (!isDirty) {
      pushToast('error', 'Chưa có thay đổi nào để lưu.');
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

      // Re-seeded from the STORED snapshot rather than from what was typed. The two are not always the
      // same — headings are trimmed and stripped of markup on the way in, an empty description becomes
      // NULL, a heading equal to the shipped wording becomes "inherit" — and a baseline built from the
      // request would report those normalisations as unsaved changes the instant the save succeeded.
      const saved: TemplateForm = {
        ...formData,
        revision: typeof res.data?.revision === 'number' ? res.data.revision : formData.revision,
        updatedAt: res.data?.updatedAt ?? formData.updatedAt,
        name: res.data?.name ?? formData.name,
        description: res.data?.description ?? '',
        subjectVi: res.data?.subjectVi ?? '',
        bodyVi: res.data?.bodyVi ?? '',
        subjectEn: res.data?.subjectEn ?? '',
        bodyEn: res.data?.bodyEn ?? '',
      };

      setFormData(saved);
      setBaseline(saved);
      pushToast('success', res.data?.message || 'Đã lưu thay đổi');
      closeEditor();
      fetchData();
    } catch (err: any) {
      // The baseline is deliberately NOT moved here. A failed save wrote nothing, so the work is still
      // unsaved and the screen must go on saying so — re-baselining on failure is how a form comes to
      // look clean while holding changes the server has never seen.
      applySaveFailure(err, 'Có lỗi xảy ra khi lưu mẫu email');
    } finally {
      setSubmitting(false);
    }
  };

  /**
   * Turns one refused save into what the operator sees.
   *
   * Shared by save and restore because the failures are the same set and must not be worded two ways.
   * Every branch matches on the stable `errorCode`; none of them puts the code into the sentence — a code
   * is for matching in software, and the message beside it already says what to do.
   */
  const applySaveFailure = (err: any, fallback: string) => {
    const code = errorCodeOf(err);
    const fromServer = issuesFromError(err);
    const serverMessage = err?.response?.data?.message;

    if (fromServer.length > 0) {
      setServerIssues(fromServer);
      pushToast('error', `Còn ${fromServer.length} vấn đề cần sửa trước khi lưu.`);
      return;
    }

    switch (code) {
      case TEMPLATE_ERROR_CODES.concurrencyConflict:
        pushToast('error', serverMessage
          ?? 'Mẫu này đã được người khác cập nhật. Vui lòng mở lại để xem nội dung mới nhất.');
        return;

      case TEMPLATE_ERROR_CODES.defaultUnavailable:
        pushToast('error', 'Mẫu này không có nội dung mặc định để phục hồi.');
        return;

      default:
        pushToast('error', serverMessage || fallback);
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
      title: 'Khôi phục toàn bộ mẫu?',
      variant: 'danger',
      message:
        // Naming the template is not decoration: this dialog is the last point at which an operator can
        // tell they are about to discard an afternoon's wording, and it is reached from a screen where
        // several templates look alike. The list says what goes, item by item, because "restore
        // defaults" on a screen with four groups of fields does not say which of them it means.
        `Thao tác này sẽ khôi phục mẫu "${formData.templateCode}" về bản hệ thống:\n` +
        '• Tên và mô tả mẫu\n' +
        '• Tiêu đề và nội dung tiếng Việt\n' +
        '• Tiêu đề và nội dung tiếng Anh\n' +
        '\nCác tùy chỉnh hiện tại sẽ bị thay thế (vẫn được ghi lại trong lịch sử thao tác).\n\n' +
        'Mã mẫu, phân loại và trạng thái không thay đổi.',
      confirmText: 'Khôi phục mặc định',
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
      const restored: TemplateForm = {
        ...formData,
        name: t?.name ?? formData.name,
        description: t?.description ?? '',
        subjectVi: t?.subjectVi ?? '',
        bodyVi: t?.bodyVi ?? '',
        subjectEn: t?.subjectEn ?? '',
        bodyEn: t?.bodyEn ?? '',
        revision: typeof t?.revision === 'number' ? t.revision : formData.revision,
        updatedAt: t?.updatedAt ?? formData.updatedAt,
        // Falls back to what is on screen rather than to null when the response omits the settings.
        // On a template with no configuration that IS null already, so the two agree; but an API built
      };

      setFormData(restored);
      // The restore WROTE all of this, so nothing is unsaved.
      setBaseline(restored);
      setServerIssues([]);
      // Every field has just been replaced by the shipped wording. An offset measured in the text that
      // was there a moment ago now points into somebody else's sentence.
      resetInsertState();
      pushToast('success', t?.message || 'Đã khôi phục mẫu email về bản mặc định.');
      fetchData();
    } catch (err: any) {
      if (issuesFromError(err).length > 0) {
        // The shipped default itself failed the contract — a seed defect, not the operator's mistake, so
        // the sentence says who can fix it rather than pointing at the fields.
        setServerIssues(issuesFromError(err));
        pushToast('error', 'Nội dung mặc định của mẫu này không hợp lệ; chưa phục hồi. Vui lòng báo quản trị hệ thống.');
        return;
      }
      applySaveFailure(err, 'Có lỗi xảy ra khi khôi phục mặc định');
    } finally {
      setRestoring(false);
    }
  };

  const closeEditor = () => {
    setShowForm(false);
    setFormData(EMPTY_FORM);
    setBaseline(EMPTY_FORM);
    setEditingId(null);
    setServerIssues([]);
    setContract({ status: 'idle' });
    resetInsertState();
  };

  /**
   * One question, asked only when there is something to lose.
   *
   * It used to name which of two groups was unsaved, because there were two saves and an operator could
   * not otherwise tell which button they had forgotten. With one save there is one answer, and the
   * sentence is the plain one. Nothing dirty still means the editor simply closes — a confirmation that
   * appears when there is nothing to confirm is one that stops being read.
   */
  const handleCancel = () => {
    if (!isDirty) {
      closeEditor();
      return;
    }

    setConfirmState({
      isOpen: true,
      title: 'Đóng trình sửa',
      message:
        'Bạn có thay đổi chưa lưu. Rời khỏi trang sẽ làm mất các thay đổi này.\n\n'
        + 'Đóng và bỏ những thay đổi này?',
      variant: 'danger',
      confirmText: 'Đóng và bỏ thay đổi',
      cancelText: 'Ở lại',
      onConfirm: () => {
        setConfirmState(prev => ({...prev, isOpen: false}));
        closeEditor();
      }
    });
  };

  /**
   * The browser's own guard, for the routes this component cannot intercept: a reload, a closed tab, a
   * typed URL, the back button.
   *
   * Registered only while the editor is open AND dirty. A listener that is always attached asks the
   * question on every navigation away from the screen, including the ones where nothing was typed —
   * which trains people to click through it, and is why the prompt is worth attaching only when it is
   * true. `preventDefault` plus a non-empty `returnValue` is what every current browser needs; the string
   * itself is not displayed any more, so the wording lives in the in-app dialog above where it can be.
   */
  useEffect(() => {
    if (!showForm || !isDirty) return;

    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [showForm, isDirty]);

  /**
   * Everything the editor reports for one language.
   *
   * There is no longer a `source` to inspect, and no hydration flag to carry. The dirty check compares
   * bodies by meaning (see `normalizeHtml`), so the editor's own re-serialisation of stored html is not a
   * change and needs no special case — while a real edit is reported from the very first keystroke,
   * including in a body the operator has not yet visited.
   */
  const handleBodyChange = (field: BodyField, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  /** Records where the caret is in the subject of the tab currently shown. */
  const rememberSubjectSelection = () => {
    const input = subjectInputRef.current;
    if (!input) return;
    subjectSelection.current[language] = {
      start: input.selectionStart ?? input.value.length,
      end: input.selectionEnd ?? input.value.length,
    };
    lastInsertTarget.current[language] = 'subject';
  };

  // `rememberBodySelection` was here. Remembering the body caret is now the shared editor's job — it has
  // to do it anyway for its own toolbar, and one copy of that rule is better than two that can disagree
  // about what a null range means.

  /**
   * Records that the operator has moved into the BODY of the tab currently shown.
   *
   * The other half of `rememberSubjectSelection`, and the half that was missing. Moving the caret into
   * the editor left `lastInsertTarget` saying "subject" — set there by the subject input's own focus
   * handler and never contradicted — so every variable picked from the sidebar afterwards was inserted
   * into the subject line, in front of an operator who was looking at the body.
   *
   * No caret is recorded here: WHERE in the body is the editor's business, and it keeps that to itself.
   */
  const rememberBodyTarget = useCallback(() => {
    lastInsertTarget.current[language] = 'body';
  }, [language]);

  /**
   * Forgets where the caret was, in both fields and both languages.
   *
   * Called whenever the document under the editor is replaced — a different template opened, the editor
   * closed, a restore performed. A remembered subject offset belongs to the text it was measured in: 24
   * characters into a heading that has just been replaced by a shorter one is not a position, and a
   * target of "subject" carried into a template the operator has not looked at yet decides where their
   * first variable lands. The body editor needs no equivalent — it is keyed by language and remounts,
   * which resets its own caret with it.
   */
  const resetInsertState = useCallback(() => {
    subjectSelection.current = { VI: null, EN: null };
    lastInsertTarget.current = { VI: null, EN: null };
  }, []);

  /**
   * Inserts a variable at the caret the operator left, in the control they left it in.
   *
   * <b>The old behaviour and why it was wrong.</b> This used to ask the DOM what was focused. By the time
   * a chip's `onClick` runs, the chip is focused and neither the subject nor the editor is — so the first
   * two branches were effectively unreachable from a mouse click and every insert fell into the third,
   * which appended the token to the END of the body. A variable meant for the middle of a greeting
   * arrived after the signature, and in the subject's case it went into the body instead: the wrong
   * field entirely.
   *
   * What replaces it is a remembered position per language and per control, so the insert goes exactly
   * where the caret was, replaces a selected range if there was one, and leaves the caret after the token
   * ready for the next word. The only fallback is a body with no remembered caret — a template opened and
   * never typed in — which inserts at the START of the body rather than the end: an obvious, visible
   * place the operator can move, not one hidden below the fold.
   */
  const insertVariable = (name: string) => {
    const token = `{{${name}}}`;
    const target = lastInsertTarget.current[language] ?? 'body';
    const declared = contract.status === 'ready'
      ? contract.contract.variables.find(v => v.name === name)
      : undefined;

    // A subject is one line of plain text a mail client shows in a list, and the contract marks the
    // variables that may not appear in one — a value that is long, multi-line or confidential. The save
    // already refuses it and the backend refuses it again, but being told at the click is the difference
    // between a correction and a puzzle: the refusal at save time names a field, not the chip that put
    // the placeholder there.
    if (target === 'subject' && declared?.forbiddenInSubject) {
      pushToast('error', `Biến "${declared.label}" không được đặt trong tiêu đề. Hãy dùng biến này trong nội dung.`);
      return;
    }

    if (target === 'subject') {
      const current = formData[subjectField] ?? '';
      const remembered = subjectSelection.current[language];
      const start = Math.min(remembered?.start ?? current.length, current.length);
      const end = Math.min(Math.max(remembered?.end ?? start, start), current.length);
      const caret = start + token.length;

      setFormData(prev => ({
        ...prev,
        [subjectField]: (prev[subjectField] ?? '').substring(0, start)
          + token
          + (prev[subjectField] ?? '').substring(end),
      }));

      // The remembered position moves with the text, so inserting two variables in a row puts the second
      // after the first instead of back over it.
      subjectSelection.current[language] = { start: caret, end: caret };

      // After the state write, so the input holds the new value when the caret is placed. Without the
      // deferral the caret would be set on the pre-insert string and the browser would clamp it.
      window.setTimeout(() => {
        const input = subjectInputRef.current;
        if (!input) return;
        input.focus();
        input.setSelectionRange(caret, caret);
      }, 0);
      return;
    }

    // The body goes through the shared editor, which owns the caret and inserts a CHIP rather than the
    // characters `{{name}}` — an atomic object with no half to leave behind if somebody backspaces
    // through it. It remembers its own last caret for the same reason this screen used to: clicking a
    // chip in the sidebar blurs the editor, and asking a blurred editor where the caret is answers 0.
    const editor = bodyEditorRef.current;
    if (!editor?.isReady()) {
      // No live editor (a mocked one in the unit tests, or a mount still in flight). Falls back to the
      // HEAD of the body, never the tail: an obvious place the operator can move it from.
      setFormData(prev => ({ ...prev, [bodyField]: token + (prev[bodyField] ?? '') }));
      return;
    }

    editor.insertVariable({ name, label: declared?.label ?? name });
    lastInsertTarget.current[language] = 'body';
  };

  const filteredData = data.filter(item => {
    const term = searchQuery.toLowerCase();
    const matchSearch = (item.name || '').toLowerCase().includes(term) || (item.templateCode || '').toLowerCase().includes(term);
    const matchGroup = groupFilter ? getTemplateGroup(item.templateCode) === groupFilter : true;
    return matchSearch && matchGroup;
  });

  const totalPages = Math.max(1, Math.ceil(filteredData.length / pageSize));
  const paginatedData = filteredData.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, groupFilter, pageSize]);

  /**
   * Deletes every occurrence of one system block from one field.
   *
   * Offered next to the refusal that names the block, and only there: an operator told "hãy xóa
   * {{contactInformationBlock}}" is being asked to find a placeholder inside rich-text they may have
   * pasted from elsewhere. The removal is not applied on their behalf — it marks the content dirty and
   * waits for "Lưu thay đổi mẫu" like any other edit, so it stays a change they made.
   */
  const removeBlockFromField = (field: TemplateContentField, block: string) => {
    // No "mark as edited" step any more: the dirty check compares meaning, so a deletion the operator
    // asked for differs from the baseline on its own merits and cannot be mistaken for hydration.
    setFormData(prev => ({ ...prev, [field]: removeSystemBlock(prev[field], block) }));
    // The refusal came from the server for content that no longer exists; keeping it on screen would
    // report a problem the operator has just fixed.
    setServerIssues(prev => prev.filter(i => !(i.field === field && i.variableName === block)));
  };

  /**
   * The issues for one field: an icon, a sentence, and — where one applies — a button on its own line.
   *
   * <b>Message and action are separate elements.</b> They used to be siblings inside one `<span>` with no
   * separator, which rendered as one unbroken run of text ending in the raw error code followed
   * immediately by the button's label: "…EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWEDXóa khối không hợp lệ".
   * The code is gone from the visible text for the same reason — a stable code is for matching in
   * software, and the sentence beside it already says what a person has to do. It stays in the DOM as a
   * `data-` attribute, where the tests and a support engineer can still read it.
   */
  const renderIssueList = (field: TemplateContentField) => {
    const fieldIssues = issuesForField(field);
    if (fieldIssues.length === 0) return null;

    return (
      <ul className="mt-1.5 space-y-1.5" data-testid={`issues-${field}`}>
        {fieldIssues.map((issue, idx) => {
          const action = issueAction(field, issue);

          return (
            <li
              key={`${issue.code}-${issue.variableName ?? idx}`}
              data-error-code={issue.code}
              className="rounded border-l-2 border-red-300 bg-red-50/60 px-2 py-1.5 text-xs text-red-700"
            >
              <div className="flex items-start gap-1.5">
                <ShieldAlert className="w-3.5 h-3.5 mt-0.5 flex-shrink-0 text-red-500" />
                <span>
                  {issue.variableName && (
                    <span className="font-mono font-bold">{`{{${issue.variableName}}} `}</span>
                  )}
                  {issue.messageVi}
                </span>
              </div>
              {action && !isHistorical && (
                <div className="mt-1.5 pl-5">
                  <button
                    type="button"
                    data-testid={action.testId}
                    onClick={action.onClick}
                    className="rounded border border-red-300 bg-white px-2 py-1 text-[11px] font-semibold text-red-700 hover:bg-red-100"
                  >
                    {action.label}
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>
    );
  };

  /**
   * The repair offered next to one issue, or none.
   *
   * A block that can never live on this template is deleted from the field it was written into.
   */
  const issueAction = (field: TemplateContentField, issue: TemplateContentIssue) => {
    if (issue.code === TEMPLATE_ERROR_CODES.systemBlockNotAllowed && issue.variableName) {
      return {
        label: 'Xóa khối không hợp lệ',
        testId: `remove-block-${field}-${issue.variableName}`,
        onClick: () => removeBlockFromField(field, issue.variableName!),
      };
    }

    return null;
  };

  if (showForm) {
    const ready = contract.status === 'ready' ? contract.contract : null;
    // One pipeline, in one place, over the CANONICAL draft — never over the editor's DOM. See
    // `buildTemplateDraftPreview` for the ordering rule and for what this preview does not prove.
    const { subject: previewSubject, bodyHtml: previewBody } = buildTemplateDraftPreview(ready, {
      subject: formData[subjectField],
      body: formData[bodyField],
    });
    const visibleVariables = (ready?.variables ?? []).filter(v =>
      v.label.toLowerCase().includes(varSearch.toLowerCase()) ||
      v.name.toLowerCase().includes(varSearch.toLowerCase()));

    // The sender group is listed last and under its own heading — see the picker below. Derived from the
    // NAME rather than from the contract's `senderVariables` list so the search filter above applies to
    // both groups identically; the two agree by construction, because the backend builds that list from
    // the same six names.
    const senderVariables = visibleVariables.filter(v => isSenderVariable(v.name));
    const businessVariables = visibleVariables.filter(v => !isSenderVariable(v.name));

    // One entry per block, one description each. See `describeSystemBlocks`.
    const systemBlockNotices = ready ? describeSystemBlocks(ready) : [];


    return (
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div className="flex items-center justify-between border-b border-gray-100 pb-4 mb-6">
          {/* The heading alone. The sentence that used to sit under it repeated what the locked "Mã
              mẫu" field below already shows — a disabled input with a padlock says "not yours to
              change" without a paragraph explaining it. */}
          <div>
            <h2 className="text-xl font-bold text-[#004c91]">Chỉnh sửa nội dung mẫu email</h2>
          </div>
          {/* Same path as "Hủy": the X used to close outright, so the operator most likely to lose work
              — the one who reaches for the corner — was the only one never asked. */}
          <button type="button" onClick={handleCancel} className="text-gray-400 hover:text-gray-700" aria-label="Đóng">
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
              {/* 1. Thông tin chung
                  The two read-only facts share a row; the two fields an operator actually writes get the
                  full width. The description used to sit in a half-width single-line input, where a
                  sentence like "Gửi cho tài khoản vừa tạo ở trạng thái chờ xác nhận email…" was cut off
                  at the field edge with no way to read the rest — in the EDITOR, which is the one place
                  the whole text has to be visible and changeable. */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <h3 className="font-bold text-gray-800 mb-4 text-base border-b border-gray-200 pb-2">1. Thông tin chung</h3>
                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-1">Mã mẫu</label>
                      <div className="flex items-center gap-2 w-full rounded-lg border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-600">
                        <Lock className="w-3.5 h-3.5 text-gray-400" />
                        <span className="font-mono">{formData.templateCode}</span>
                      </div>
                    </div>
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-1">Trạng thái</label>
                      <div className="flex items-center gap-2 w-full rounded-lg border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-600">
                        <Lock className="w-3.5 h-3.5 text-gray-400" />
                        {formData.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                      </div>
                    </div>
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1" htmlFor="tpl-name">Tên mẫu *</label>
                    <input id="tpl-name" type="text" maxLength={150} value={formData.name} disabled={isHistorical} onChange={e => setFormData({...formData, name: e.target.value})} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500" />
                  </div>
                  <div>
                    <div className="flex items-baseline justify-between mb-1">
                      <label className="block text-sm font-bold text-gray-700" htmlFor="tpl-desc">Mô tả quản trị</label>
                      {/* The same limit the API enforces (UpdateEmailTemplateCommandValidator), so a long
                          description is stopped here rather than by a refused save. */}
                      <span className="text-[11px] text-gray-500" data-testid="description-counter">
                        {formData.description.length}/{DESCRIPTION_MAX_LENGTH}
                      </span>
                    </div>
                    <textarea
                      id="tpl-desc"
                      rows={3}
                      maxLength={DESCRIPTION_MAX_LENGTH}
                      value={formData.description}
                      disabled={isHistorical}
                      onChange={e => setFormData({...formData, description: e.target.value})}
                      className="w-full min-h-[72px] max-h-[160px] resize-y whitespace-pre-wrap break-words rounded-lg border border-gray-300 px-3 py-2 text-sm leading-relaxed outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500"
                    />
                  </div>
                </div>
              </div>

              {/* 2. Nội dung email */}
              <div className="bg-gray-50/50 p-5 rounded-lg border border-gray-200">
                <div className="flex items-center justify-between border-b border-gray-200 pb-2 mb-4">
                  <h3 className="font-bold text-gray-800 text-base">2. Nội dung email</h3>
                  {/* Each tab carries its OWN error state. A red dot here is the only thing that tells
                      an operator on the Vietnamese tab that the save is being refused by the English
                      one — the messages themselves live under fields they cannot currently see. */}
                  <div className="flex rounded-md overflow-hidden border border-gray-300" role="group" aria-label="Ngôn ngữ">
                    {(['VI', 'EN'] as EditorLanguage[]).map(lang => {
                      const errorCount = blockingByLanguage[lang].length;
                      const active = language === lang;

                      return (
                        <button
                          key={lang}
                          type="button"
                          onClick={() => setLanguage(lang)}
                          aria-pressed={active}
                          aria-invalid={errorCount > 0}
                          data-testid={`language-tab-${lang}`}
                          data-error-count={errorCount}
                          title={errorCount > 0
                            ? `${LANGUAGE_LABELS[lang]}: còn ${errorCount} vấn đề cần sửa trước khi lưu.`
                            : undefined}
                          className={`inline-flex items-center gap-1.5 px-3 py-1 text-xs font-bold transition-colors ${active ? 'bg-[#004c91] text-white' : 'bg-white text-gray-600 hover:bg-gray-50'}`}
                        >
                          {LANGUAGE_LABELS[lang]}
                          {errorCount > 0 && (
                            <span
                              data-testid={`language-tab-error-${lang}`}
                              aria-label={`${errorCount} vấn đề`}
                              className={`inline-flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-black ${active ? 'bg-white text-red-600' : 'bg-red-600 text-white'}`}
                            >
                              {errorCount}
                            </span>
                          )}
                        </button>
                      );
                    })}
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
                      onChange={e => {
                        setFormData({...formData, [subjectField]: e.target.value});
                        rememberSubjectSelection();
                      }}
                      /* `onSelect` covers every way a caret moves in a text input — click, arrow key,
                         drag-select, Home/End — in one event, so the remembered position is current
                         whichever of them the operator used before reaching for a chip. */
                      onSelect={rememberSubjectSelection}
                      onFocus={rememberSubjectSelection}
                      onKeyUp={rememberSubjectSelection}
                      onClick={rememberSubjectSelection}
                      className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91] disabled:bg-gray-100 disabled:text-gray-500"
                    />
                    {renderIssueList(subjectField)}
                  </div>
                  <div>
                    <label className="block text-sm font-bold text-gray-700 mb-1">Nội dung (Body)</label>
                    <div ref={bodyEditorAnchor} className="border border-gray-300 rounded-lg overflow-hidden bg-white">
                      {/*
                        The SHARED editor, in TEMPLATE mode — the same component, formats, sanitiser and
                        canonicaliser the send modal uses in COMPOSE. There used to be two editors here
                        and there, each with its own four-button toolbar, which is two answers to "may I
                        centre this?" when only the recipient's mail client gets a vote.

                        Keyed by language: one editor per body, not one editor re-pointed at the other.
                        A controlled Quill loads a new `value` from inside `shouldComponentUpdate`, where
                        its own props are not yet committed — so a change event for the English body was
                        delivered to the handler still bound to `bodyVi`, and switching tabs overwrote the
                        Vietnamese content with the editor's rendering of the English one. Remounting
                        binds the handler to the language whose content it is about to load.
                      */}
                      <EmailRichTextEditor
                        key={bodyField}
                        ref={bodyEditorRef}
                        mode="TEMPLATE"
                        value={formData[bodyField]}
                        disabled={isHistorical}
                        onChange={(html) => handleBodyChange(bodyField, html)}
                        /* The body saying "I am what is being written in". Without it the screen goes
                           on believing the subject is the target, and the sidebar's variables jump up
                           into the heading. */
                        onEditorActivated={rememberBodyTarget}
                        /* The blocks THIS template may carry, from its contract — so the editor can
                           offer them as objects and never offers one the renderer would refuse. */
                        systemBlocks={systemBlockNotices.map(b => ({
                          name: b.name,
                          label: SYSTEM_BLOCK_LABELS[b.name]?.title ?? b.name,
                        }))}
                        variables={contract.status === 'ready' ? contract.contract.variables : undefined}
                        onNotice={(message) => pushToast('error', message)}
                        minHeight={250}
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
                  <div className="font-normal border-b border-gray-100 pb-2 mb-2">
                    {previewSubject || <span className="text-gray-400 italic">Chưa có tiêu đề...</span>}
                  </div>
                  {/* Already sanitised, by the pipeline that substituted the samples into it: the
                      substituted result is what this browser must be protected from, so the sanitiser
                      runs after substitution — and in exactly one place. */}
                  {/* `prose prose-sm` used to be here. It is a no-op today — the Tailwind typography
                      plugin is not installed — but it is markup that says "restyle this", and the one
                      thing a preview of an email must not do is restyle it. Replaced by the isolation
                      class, which only undoes inherited layout and lets a wide table scroll. */}
                  <div className="pems-email-body max-w-none" data-testid="preview-body" dangerouslySetInnerHTML={{
                    __html: previewBody || '<span class="text-gray-400 italic">Chưa có nội dung...</span>'
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
                      System blocks, listed as blocks — each one ONCE.

                      They are NOT in the variable list below and must not be: the backend builds their
                      markup and an operator can neither author it nor supply a value.

                      The list comes from `describeSystemBlocks`, which is where the one-description rule
                      lives. This markup used to render the action block twice: once from
                      `actionSupported`, with the backend's description of THIS template's action, and
                      again from the required/optional lists, with the generic sentence about
                      "đồng ý / từ chối / xem chi tiết" — buttons ACCOUNT_EMAIL_CONFIRMATION does not
                      have. Two descriptions of one block, the second one untrue.

                      A template with no action spec shows no action block at all, which is the other half
                      of the same rule: the backend leaves it out of both lists, so nothing here announces
                      buttons the send path never attaches.
                    */}
                    {systemBlockNotices.length > 0 && (
                      <div className="mb-3 text-[11px] text-blue-900 bg-blue-50 border border-blue-200 rounded p-2"
                           data-testid="system-blocks-notice">
                        <div className="flex items-start gap-1.5 mb-1">
                          <Lock className="w-3.5 h-3.5 mt-0.5 flex-shrink-0" />
                          <span className="font-semibold">Khối hệ thống — nội dung do hệ thống dựng</span>
                        </div>
                        <ul className="pl-5 space-y-1">
                          {systemBlockNotices.map(block => (
                            <li key={block.name}
                                data-testid={`system-block-${block.required ? 'required' : 'optional'}-${block.name}`}>
                              <span className="font-mono font-bold">{`{{${block.name}}}`}</span>{' '}
                              <span className={`rounded px-1 py-0.5 ${block.required ? 'bg-blue-100 font-semibold' : 'bg-gray-100 text-gray-700'}`}>
                                {block.required ? 'bắt buộc giữ' : 'tùy chọn'}
                              </span>
                              <span className="block text-blue-800">{block.description}</span>
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
                      {/*
                        Two groups, and the split is not cosmetic. The business variables are values the
                        SEND supplies about the message — a delegation name, a time, a quantity. The
                        sender group describes WHO the message is from, and an administrator reaches for
                        it with a different question in mind: "how do I sign this off". Mixed into one
                        alphabetical wall, {{senderPhone}} sat between {{quantity}} and {{roleLabel}} and
                        was found only by searching for a name nobody had told them.

                        The group is absent entirely on a NOT_AVAILABLE template — the backend sends no
                        sender variables in the contract there — so the picker offers nothing the save
                        would refuse.
                      */}
                      {[
                        { key: 'business', title: null as string | null, items: businessVariables },
                        { key: 'sender', title: 'Thông tin người gửi', items: senderVariables },
                      ].filter(g => g.items.length > 0).map(group => (
                        <div key={group.key} className="space-y-1.5">
                          {group.title && (
                            <div
                              data-testid="sender-variable-group"
                              className="pt-1 text-[10px] font-bold uppercase tracking-wide text-gray-400"
                            >
                              {group.title}
                            </div>
                          )}
                          <div className="flex flex-wrap gap-1.5">
                            {group.items.map(v => (
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
                      ))}
                    </div>
                  </>
                )}
              </div>

              {/*
                Card 4 was the contact configuration: who the recipient should be told to contact, at
                which level, and which of their fields the block prints. It is gone with the feature.
                There is nothing to configure any more — the sender is the account that presses send,
                read from authentication — so what used to be a card of controls is now six ordinary
                variables in the picker above, under "Thông tin người gửi".
              */}
            </aside>
          </div>
          {/*
            Why the save is refused, said WHERE the save button is and regardless of which tab is open.

            This is the fix for the defect that started this work: with the contact level at "Bắt buộc"
            and the block present in Vietnamese but missing in English, "Lưu thay đổi" went dead while
            the Vietnamese tab — the one the operator was on — showed a perfectly clean form. Every
            explanation existed, under the English body, on a tab they had no reason to visit.

            Grouped by language rather than listed flat, because the operator's next action is per
            language: go to that tab, fix that field. Each group carries the button that takes them there.
          */}
          {blockingIssues.length > 0 && !isHistorical && (
            <div
              role="alert"
              data-testid="validation-summary"
              className="mb-4 rounded-lg border border-red-300 bg-red-50 p-4"
            >
              <p className="mb-2 flex items-center gap-2 text-sm font-bold text-red-800">
                <ShieldAlert className="h-4 w-4 flex-shrink-0" />
                Không thể lưu — còn {blockingIssues.length} vấn đề cần sửa.
              </p>

              <div className="space-y-2.5">
                {(['VI', 'EN'] as EditorLanguage[]).map(lang => {
                  const langIssues = blockingByLanguage[lang];
                  if (langIssues.length === 0) return null;

                  return (
                    <div key={lang} data-testid={`validation-summary-${lang}`}>
                      <ul className="space-y-1 text-xs text-red-700">
                        {langIssues.map((issue, idx) => (
                          <li key={`${issue.code}-${issue.field}-${issue.variableName ?? idx}`}
                              data-error-code={issue.code}>
                            <span className="font-bold">{LANGUAGE_LABELS[lang]}:</span>{' '}
                            {issue.messageVi}
                          </li>
                        ))}
                      </ul>
                      {/* Offered even for the tab already open: it still moves the caret to the field
                          that is wrong, which on a long template is the useful half of the action. */}
                      <button
                        type="button"
                        data-testid={`goto-issue-${lang}`}
                        onClick={() => goToIssue(lang, langIssues[0])}
                        className="mt-1.5 rounded border border-red-300 bg-white px-2.5 py-1 text-[11px] font-bold text-red-700 hover:bg-red-100"
                      >
                        {language === lang
                          ? `Đến chỗ lỗi trong ${LANGUAGE_LABELS[lang]}`
                          : `Chuyển sang ${LANGUAGE_LABELS[lang]}`}
                      </button>
                    </div>
                  );
                })}

                {/* A refusal the server made about the template as a whole — it names no field, so
                    there is nowhere to jump to and nothing but the sentence to show. */}
                {blockingWithoutLanguage.length > 0 && (
                  <ul className="space-y-1 text-xs text-red-700" data-testid="validation-summary-general">
                    {blockingWithoutLanguage.map((issue, idx) => (
                      <li key={`${issue.code}-${idx}`} data-error-code={issue.code}>{issue.messageVi}</li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          )}

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
            {/* `h-11` on all three buttons, and `whitespace-nowrap` on each label: the bar holds a long
                label next to two short ones, and a button sized by its own text sits at a different
                height from its neighbours the moment one of them wraps. Restore is deliberately NOT
                disabled by a clean form — putting a template back to the shipped wording is a valid thing
                to do whether or not anything has been typed since it was opened. */}
            {formData.hasShippedDefault && formData.revision !== null ? (
              <button
                type="button"
                data-testid="restore-default"
                onClick={handleRestoreDefault}
                disabled={restoring || submitting}
                title="Khôi phục tên, mô tả, tiêu đề, nội dung VI/EN và cấu hình thông tin liên hệ về bản hệ thống"
                className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-lg border border-amber-200 bg-amber-50 px-4 font-bold text-[#b45309] hover:bg-amber-100 disabled:opacity-50"
              >
                {restoring ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
                Khôi phục toàn bộ mặc định
              </button>
            ) : <span />}

            {/* `ml-auto` keeps the pair on the right even when the bar wraps on a narrow viewport,
                where `justify-between` alone would leave them under the restore button on the left. */}
            <div className="ml-auto flex flex-wrap items-center justify-end gap-3">
              {/* ONE indicator, because there is one save. There used to be two — one here and one on the
                  contact card — and between them they could not answer "is there unsaved work on this
                  screen" without the reader knowing which button each referred to. */}
              {isDirty && (
                <span className="text-xs font-normal text-amber-700" data-testid="editor-dirty">
                  ● Có thay đổi chưa lưu
                </span>
              )}
              <button
                type="button"
                onClick={handleCancel}
                className="inline-flex h-11 items-center whitespace-nowrap rounded-lg bg-gray-100 px-4 font-bold text-gray-600 hover:bg-gray-200"
              >
                Hủy
              </button>
              {/*
                The only save on this screen. It writes the six content fields AND the contact
                configuration, in one request that the server runs as one transaction — so a failure in
                either half leaves both unchanged and the revision where it was.

                Disabled while the contract is still loading, while a save is in flight, when nothing has
                changed, and when the screen is holding a validation error it can name. The last of those
                is what stops "Không hiển thị" from being saved over a body that still carries the block:
                the refusal is on screen, and the button that would round-trip it is off.
              */}
              <button
                type="submit"
                data-testid="save-template"
                disabled={submitting || restoring || contract.status === 'loading' || blockingIssues.length > 0 || isHistorical || !isDirty}
                title={isHistorical
                  ? 'Mẫu lịch sử không nằm trong danh mục hệ thống nên không sửa được.'
                  : blockingIssues.length > 0
                    ? `Còn ${blockingIssues.length} vấn đề cần sửa trước khi lưu.`
                    : !isDirty
                      ? 'Chưa có thay đổi nào để lưu.'
                      : 'Lưu tên mẫu, mô tả, tiêu đề và nội dung VI/EN, và cấu hình thông tin liên hệ'}
                className="inline-flex h-11 items-center gap-2 whitespace-nowrap rounded-lg bg-[#004c91] px-4 font-bold text-white hover:bg-[#013565] disabled:opacity-50"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                Lưu thay đổi
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
          confirmText={confirmState.confirmText}
          cancelText={confirmState.cancelText}
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
          <select value={groupFilter} onChange={e => setGroupFilter(e.target.value)} aria-label="Lọc theo nhóm" className="border border-gray-300 rounded-md px-3 py-1.5 text-sm outline-none">
            <option value="">Tất cả nhóm</option>
            <option value="Tài khoản">Tài khoản</option>
            <option value="Xác thực">Xác thực</option>
            <option value="Nhân sự">Nhân sự</option>
            <option value="Hậu cần">Hậu cần</option>
            <option value="Báo cáo">Báo cáo</option>
            <option value="Tiếp khách">Tiếp khách</option>
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
              <th className="px-4 py-3 font-bold">Mô tả quản trị</th>
              <th className="px-4 py-3 font-bold text-center w-[150px]">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Đang tải...</td></tr>
            ) : paginatedData.length === 0 ? (
              <tr><td colSpan={5} className="p-8 text-center text-gray-500">Không tìm thấy mẫu email nào</td></tr>
            ) : (
              paginatedData.map((item, index) => (
                <tr key={item.emailTemplateId} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-center text-gray-500">{(currentPage - 1) * pageSize + index + 1}</td>
                  <td className="px-4 py-3 font-bold text-[#004c91]">{item.templateCode}</td>
                  <td className="px-4 py-3 font-medium">{item.name}</td>
                  <td className="px-4 py-3">
                    <div className="line-clamp-2 text-xs text-gray-600" title={item.description || ''}>
                      {item.description || <span className="text-gray-400 italic">Không có mô tả</span>}
                    </div>
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

      {/* Pagination Footer */}
      {filteredData.length > 0 && (
        <div className="px-4 py-3 border-t border-gray-100 bg-gray-50/50 flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-500">
              Đang xem <span className="font-normal text-gray-900">{(currentPage - 1) * pageSize + 1}</span> - <span className="font-normal text-gray-900">{Math.min(currentPage * pageSize, filteredData.length)}</span> trong <span className="font-normal text-gray-900">{filteredData.length}</span> mẫu
            </span>
            <div className="flex items-center gap-2 border-l border-gray-200 pl-3">
              <label htmlFor="pageSize" className="text-sm text-gray-500">Hiển thị:</label>
              <select id="pageSize" value={pageSize} onChange={e => setPageSize(Number(e.target.value))} className="px-2 py-1 pr-6 border border-gray-300 rounded text-sm outline-none focus:border-[#004c91] bg-white">
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center gap-1">
              <button
                onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
                disabled={currentPage === 1}
                className="p-1.5 rounded text-gray-500 hover:text-[#004c91] hover:bg-blue-50 disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>

              <div className="flex items-center">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => {
                  if (page === 1 || page === totalPages || Math.abs(page - currentPage) <= 1) {
                    return (
                      <button
                        key={page}
                        onClick={() => setCurrentPage(page)}
                        className={`w-7 h-7 rounded text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}
                      >
                        {page}
                      </button>
                    );
                  } else if (Math.abs(page - currentPage) === 2) {
                    return <span key={page} className="px-1 text-gray-400">...</span>;
                  }
                  return null;
                })}
              </div>

              <button
                onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}
                disabled={currentPage === totalPages}
                className="p-1.5 rounded text-gray-500 hover:text-[#004c91] hover:bg-blue-50 disabled:opacity-50 disabled:cursor-not-allowed outline-none"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
