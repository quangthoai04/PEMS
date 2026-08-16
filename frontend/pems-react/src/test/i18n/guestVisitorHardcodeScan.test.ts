/**
 * i18n gate — Phase D3/D4/D5 (Guest/Visitor hardcode + raw-enum + backend-message-leak scan).
 *
 * Scope is INTENTIONALLY limited to the curated file list below — every page/component confirmed
 * Guest- or VISITOR-reachable across the full i18n implementation (see
 * `docs/CanhIter3FixBug/PEMS_GUEST_VISITOR_FULL_I18N_IMPLEMENTATION_PLAN.md` and memory
 * `visitor-i18n-coverage-gap`). Scanning the whole repo would bury real findings under hundreds of
 * legitimate Staff/HO/Department-only hardcoded-Vietnamese lines, which is explicitly out of scope.
 *
 * Files with heavy Staff/Host-only content are deliberately EXCLUDED from this automated scan — a
 * naive whole-file Vietnamese-literal check on them would be dominated by false positives from
 * legitimate out-of-scope code:
 *   - `VisitProcess.tsx`, `VisitRequestManagement.tsx` — mostly Staff/Host tooling with a few
 *     genuinely Visitor-reachable sections; those sections were hand-audited instead (see memory).
 *   - `VisitContributionPage.tsx`, `VisitProcessSummaryPage.tsx` — the route guard technically
 *     allows BUSINESS_ROLES, but access is actually decided by the backend to Host / an
 *     ACCEPTED-ASSIGNED internal participant / a Department user with a logistics relation (see
 *     each file's own top-of-file doc comment) — structurally never VISITOR. Only their entry
 *     shell (loading/access-denied/breadcrumb) is Visitor-reachable and was translated; the deep
 *     report content past that shell is legitimately Vietnamese-only and out of scope, so scanning
 *     these two files whole would misreport it as a gap.
 *
 * This is a heuristic regression gate, not a full static analyzer: it flags any line containing a
 * Vietnamese-diacritic character that is not a comment and not inside a translation call, then
 * requires every such line to be explicitly allow-listed with a reason. A NEW unexplained
 * hardcoded-Vietnamese line added later fails the test until triaged (fixed or allow-listed).
 */
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const SRC_ROOT = path.resolve(__dirname, '../../');

/** Guest/Visitor-reachable files, relative to src/. */
const SCOPED_FILES = [
  'pages/account/ConfirmEmailPage.tsx',
  'pages/identity/VisitContactInvitationPage.tsx',
  'features/visit-request/components/OtpVerificationModal.tsx',
  'features/visit-request/components/v2/CampusVisitCard.tsx',
  'components/layout/DashboardLayout.tsx',
  'components/dashboard/Sidebar.tsx',
  'components/layout/Header.tsx',
  'pages/dashboard/profile/Profile.tsx',
  'pages/dashboard/visit/VisitorVisitDetailPage.tsx',
  'features/feedbacks/components/VisitFeedbackModal.tsx',
  'pages/dashboard/visit/VisitFeedbackPage.tsx',
  'features/feedbacks/components/FeedbackGroupSection.tsx',
  'features/feedbacks/components/FeedbackTargetRow.tsx',
  'features/feedbacks/components/CompactStarRating.tsx',
  'features/feedbacks/hooks/useVisitFeedback.ts',
  'pages/dashboard/visit/VisitRequestDetail.tsx',
  'pages/dashboard/visit/CreateVisitRequestEntry.tsx',
  'pages/dashboard/visit/MyContactInvitationsPage.tsx',
  'pages/PartnersPage.tsx',
  'pages/PartnerDetailPage.tsx',
  'pages/CampusDetailVisitPage.tsx',
  'pages/NewsPage.tsx',
  'pages/NewsDetailPage.tsx',
  'features/notifications/components/NotificationBellButton.tsx',
  'features/notifications/components/NotificationDetailModal.tsx',
  'features/notifications/components/NotificationFilterBar.tsx',
  'features/notifications/context/NotificationsContext.tsx',
  'features/notifications/utils/resolveNotificationText.ts',
  'pages/notifications/NotificationsPage.tsx',
];

const VIETNAMESE_DIACRITIC = /[À-ỹ]/; // covers Latin Extended-A/B + Vietnamese combining ranges used by all VI text in this repo

/** file -> set of 1-indexed line numbers known to hold legitimate Vietnamese content (proper
 * nouns, business codes, or comments already excluded by the comment check) with the reason why
 * they are not a translation gap. Add an entry here ONLY after confirming the line is genuinely
 * out of scope — never to silence a real miss. */
const ALLOWLIST: Record<string, Record<number, string>> = {
  'components/layout/Header.tsx': {
    // Language-switcher item labels: by UX convention these always show a language's own native
    // name ("Tiếng Việt"/"English"), never translated into whichever language is currently active
    // — same as every other language picker (a French picker still shows "Deutsch", not "German").
    178: "language-switcher item label — always shown in the language's own name, by convention",
  },
  'pages/CampusDetailVisitPage.tsx': {
    // CAMPUS_FALLBACK.description (hn/hcm/dn/ct/qn) is only ever read as a `t(..., {defaultValue})`
    // fallback at the one call site (`campusDescriptions.${routeId}`) — confirmed the i18n key
    // exists for every route id in both locales, so this Vietnamese text is dead/unreachable, not a
    // translation gap. Do not delete without re-checking the call site first.
    67: 'CAMPUS_FALLBACK.hn.description — unreachable defaultValue, see comment above',
    72: 'CAMPUS_FALLBACK.hcm.description — unreachable defaultValue, see comment above',
    76: 'CAMPUS_FALLBACK.dn.description — unreachable defaultValue, see comment above',
    80: 'CAMPUS_FALLBACK.ct.description — unreachable defaultValue, see comment above',
    84: 'CAMPUS_FALLBACK.qn.description — unreachable defaultValue, see comment above',
  },
};

/**
 * Marks each line as comment-or-not, tracking `/* ... *\/` and `{/* ... *\/}` block-comment state
 * across lines (a single-line startsWith check misses multi-line JSX comment bodies, whose
 * continuation lines are free prose with no `*`/`//` prefix). Approximate on purpose — this is a
 * lint-style heuristic, not a real parser — but good enough that no genuine JSX text line is ever
 * misclassified as a comment (the failure mode this errs toward is "flag too much", never "miss a
 * real hardcoded string by mistaking it for a comment").
 */
function classifyCommentLines(lines: string[]): boolean[] {
  const isComment: boolean[] = new Array(lines.length).fill(false);
  let inBlock = false;
  lines.forEach((rawLine, idx) => {
    const line = rawLine.trim();
    if (inBlock) {
      isComment[idx] = true;
      if (line.includes('*/')) inBlock = false;
      return;
    }
    if (line.startsWith('//')) {
      isComment[idx] = true;
      return;
    }
    const blockStart = line.indexOf('/*');
    if (blockStart !== -1) {
      isComment[idx] = true;
      // Only stays open past this line if there is no matching closer AFTER the opener.
      if (line.indexOf('*/', blockStart + 2) === -1) inBlock = true;
      return;
    }
  });
  return isComment;
}

/** A line is considered "translation-covered" if the Vietnamese text sits inside a `t(`/`tt(`
 * call (the i18next key path itself, e.g. `t('feedback:overallGroup.title')`, or a `defaultValue`
 * fallback) rather than a bare literal handed straight to JSX/props. */
function looksTranslationCovered(line: string): boolean {
  return /\b(t|tt|i18n\.t)\s*\(/.test(line);
}

/** Strips a trailing `// vietnamese note` end-of-line comment (this codebase's dominant comment
 * style) so a Vietnamese explanatory note after real code doesn't get flagged as hardcoded UI
 * text. Deliberately simple — does not attempt to understand string literals — so it only strips
 * on a `<code> // <comment>` shape (whitespace before `//`, not `://` as in a URL). */
function stripTrailingLineComment(line: string): string {
  const match = line.match(/\s\/\/(?!\/)/);
  if (!match || match.index === undefined) return line;
  if (line[match.index - 1] === ':') return line; // guards against `http://`-style false strips
  return line.slice(0, match.index);
}

describe('Guest/Visitor hardcoded-text scan (Phase D3/D4 gate)', () => {
  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} has no un-allow-listed hardcoded Vietnamese line`, () => {
      if (!fs.existsSync(absPath)) {
        throw new Error(`Scoped file no longer exists at src/${relPath} — update SCOPED_FILES.`);
      }
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const allowed = ALLOWLIST[relPath] ?? {};
      const offenders: string[] = [];

      lines.forEach((line, idx) => {
        const lineNo = idx + 1;
        if (isComment[idx]) return;
        const codePortion = stripTrailingLineComment(line);
        if (!VIETNAMESE_DIACRITIC.test(codePortion)) return;
        if (looksTranslationCovered(codePortion)) return;
        if (allowed[lineNo]) return;
        offenders.push(`  L${lineNo}: ${line.trim().slice(0, 140)}`);
      });

      expect(offenders, `Unexplained hardcoded Vietnamese in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});

describe('Guest/Visitor raw-enum scan (Phase D4 gate)', () => {
  // Fields whose raw backend value (roleCode, status code, etc.) must never reach JSX text
  // directly — it must always be routed through an i18n label map first.
  const RAW_ENUM_PATTERN = /\{(?:item\.|user\.|data\.)?(roleCode|status|partnerType|participantStatus|visitType|workingLanguage|mediaConsent)\}/;

  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} never interpolates a raw enum/status/role directly into JSX text`, () => {
      if (!fs.existsSync(absPath)) return;
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const offenders: string[] = [];
      lines.forEach((line, idx) => {
        if (isComment[idx]) return;
        if (looksTranslationCovered(line)) return; // e.g. t(`ns.key.${status}`) — status is part of the i18n KEY, not raw output
        if (RAW_ENUM_PATTERN.test(line)) offenders.push(`  L${idx + 1}: ${line.trim().slice(0, 140)}`);
      });
      expect(offenders, `Raw enum/status/role interpolated directly in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});

describe('Guest/Visitor backend-message-leak scan (Phase D5 gate)', () => {
  // A RAW system message reaching the UI: reading `.message` straight off an API response/error
  // object and rendering it, instead of going through getApiErrorMessage/translateErrorCode or a
  // fixed localized string. `feedbackApiError`/`getApiErrorMessage` wrap this safely, so a call
  // site that already routes through them is fine even though the literal text `.message` appears
  // near it — the checks below specifically target the UNWRAPPED direct-read patterns.
  const RAW_MESSAGE_PATTERNS = [
    /toast\.(success|error)\(\s*(response|result|data|res)\??\.(data\??\.)?message\s*\)/,
    /(setError|setMessage)\(\s*(response|result|data|res)\??\.(data\??\.)?message\s*\)/,
  ];

  for (const relPath of SCOPED_FILES) {
    const absPath = path.join(SRC_ROOT, relPath);

    it(`${relPath} never renders a raw unwrapped backend message`, () => {
      if (!fs.existsSync(absPath)) return;
      const lines = fs.readFileSync(absPath, 'utf-8').split('\n');
      const isComment = classifyCommentLines(lines);
      const offenders: string[] = [];
      lines.forEach((line, idx) => {
        if (isComment[idx]) return;
        if (RAW_MESSAGE_PATTERNS.some((re) => re.test(line))) {
          offenders.push(`  L${idx + 1}: ${line.trim().slice(0, 140)}`);
        }
      });
      expect(offenders, `Raw unwrapped backend message in src/${relPath}:\n${offenders.join('\n')}`).toEqual([]);
    });
  }
});
