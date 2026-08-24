# PEMS Visit Request V2 — UI-Only Implementation Plan (Exclude Operational Contact)

## 1. Purpose

This document defines the implementation plan for the **Visit Request V2 UI/UX updates that do not involve Operational Contact logic**.

The goal is to complete and stabilize the purely visual / presentation-oriented improvements first, then handle the Operational Contact redesign in a separate implementation phase.

This separation is intentional to:

- reduce regression risk;
- keep the patch small and reviewable;
- make root-cause analysis easier if a UI regression appears;
- avoid mixing presentation changes with identity / authorization / workflow changes;
- allow the Operational Contact redesign to be reviewed independently.

---

## 2. Scope

This phase includes only the following UI/UX items:

1. Re-layout the Registrant information section.
2. Replace Visit Request help icon `?` with an information icon `i`.
3. Remove the always-visible `72 hours / 30 minutes` schedule hint.
4. Hide visible `(x/200)` member-count badges.
5. Rename Excel import buttons.
6. Make successful Excel import reports compact by default.
7. Keep Excel error reports fully expanded.
8. Update Media Consent wording.
9. Keep Media Consent wire values unchanged.
10. Add accessible full-label help for long Media Consent text.
11. Update VI/EN i18n.
12. Run responsive and frontend regression checks.

---

## 3. Explicitly Out of Scope

This phase MUST NOT modify Operational Contact behavior.

Do **not** change:

- `operationalContactClientMemberKey`;
- `OperationalContactGuestMemberId`;
- `OperationalContactUserId`;
- Operational Contact member selection;
- Operational Contact validation;
- Contact confirmation;
- Contact invitation;
- Replace Contact;
- Transfer Contact;
- ContactLinkPrompt;
- any Operational Contact backend handler/service;
- database schema;
- database migration;
- authorization;
- OTP;
- request lifecycle/status behavior.

This phase also MUST NOT change the following existing business rules:

- 72-hour minimum registration lead time;
- 30-minute minimum visit duration;
- maximum 200 guests/support members;
- Media Consent persisted values `AGREED` / `DECLINED`.

---

## 4. Source-Safety Rule

Before editing:

1. Verify repository.
2. Verify current branch.
3. Verify current HEAD.
4. Check `git status`.
5. Re-read every file that will be changed.
6. If a file has changed since this plan was prepared, adapt to the actual current source instead of using stale line numbers.

Previously verified baseline:

```text
Branch: Canh_iter3_FixBug
HEAD: cee18dca39458c64bb399fa723f2244a0086668e
```

This baseline is informational only. It must be re-checked before implementation.

---

## 5. Phase UI-1 — Registrant Layout

### 5.1 Requirement

Rearrange the Registrant information section on desktop as:

#### Row 1

```text
Họ và tên | Quốc tịch | Đơn vị công tác
```

Suggested grid:

```text
4/12 | 2/12 | 6/12
```

#### Row 2

```text
Chức vụ | Số điện thoại | Email
```

Suggested grid:

```text
4/12 | 4/12 | 4/12
```

### 5.2 Constraints

This is a layout-only change.

Do not change:

- React Hook Form field paths;
- validation rules;
- `CountrySelect`;
- strict nationality behavior;
- phone normalization;
- profile autofill;
- field-change revalidation;
- API payload fields.

### 5.3 Responsive Requirement

On smaller screens, prioritize readability over exact desktop spans.

Expected:

- mobile: fields stack naturally;
- tablet: no overlap / clipped labels;
- desktop: two clean rows;
- nationality must not become unusably narrow.

### 5.4 Acceptance Criteria

- All six Registrant fields still bind to the same form values.
- No validation behavior changes.
- Existing profile autofill continues to work.
- Mobile layout does not overflow horizontally.
- Desktop layout follows the requested grouping.

---

## 6. Phase UI-2 — Help Icon `?` → `i`

### 6.1 Requirement

Within the Visit Request shared help tooltip component:

```text
HelpCircle (?) → Info (i)
```

### 6.2 Scope

Only change the shared HelpTooltip used by Visit Request.

Do not globally replace unrelated `HelpCircle` usages elsewhere in the application.

### 6.3 Accessibility Must Remain Intact

Preserve existing behavior:

- mouse hover opens tooltip;
- mouse leave closes where current behavior expects it;
- keyboard focus exposes tooltip;
- blur closes where appropriate;
- click / touch toggles tooltip;
- Escape closes tooltip;
- `aria-expanded`;
- `aria-describedby`;
- `role="tooltip"`.

If the component comment still describes a `?` icon, update the comment.

### 6.4 Acceptance Criteria

- Icon visually becomes `i`.
- Existing tooltip content is unchanged.
- Mouse, keyboard and touch interaction still works.
- No unrelated application icons change.

---

## 7. Phase UI-3 — Remove Standing Schedule Hint

### 7.1 Requirement

Remove the always-visible text equivalent to:

```text
* Đăng ký trước ít nhất 72 giờ, mỗi buổi tối thiểu 30 phút.
```

### 7.2 Important Constraint

Only remove the persistent UI hint.

Keep all actual business rules:

```text
72-hour validation      → KEEP
30-minute validation    → KEEP
backend enforcement     → KEEP
frontend constants      → KEEP
contextual tooltip      → KEEP
```

### 7.3 Acceptance Criteria

- The permanent hint no longer appears in the form.
- Selecting an invalid date within the 72-hour restriction still fails.
- Visit duration below 30 minutes still fails.
- Existing contextual schedule help remains available.

---

## 8. Phase UI-4 — Hide Member Count Badges

### 8.1 Requirement

Change visible headings from forms such as:

```text
Danh sách khách (5/200)
Nhân sự hỗ trợ (3/200)
```

to:

```text
Danh sách khách
Nhân sự hỗ trợ
```

### 8.2 Constraints

Only hide the visible count/max badge.

Keep maximum 200 enforcement in:

- Zod schema;
- Add buttons;
- Excel import validation;
- backend validation.

### 8.3 Acceptance Criteria

- `(x/200)` is no longer displayed.
- 201st member cannot be added.
- Excel import exceeding the limit is still rejected.
- Backend limit is unchanged.

---

## 9. Phase UI-5 — Excel Import Button Labels

### 9.1 Vietnamese Labels

Guest:

```text
Nhập DS khách
```

Support:

```text
Nhập DS hỗ trợ
```

### 9.2 English

Provide equivalent concise English labels via i18n.

Do not hard-code labels directly in JSX.

### 9.3 Acceptance Criteria

- Both Guest and Support import buttons use new labels.
- VI and EN both work.
- No Excel behavior changes.

---

## 10. Phase UI-6 — Compact Excel Success Result

### 10.1 Requirement

A successful Excel import should be compact by default.

Guest example:

```text
✓ Nhập thành công 12 khách    [Xem chi tiết]    [×]
```

Support example:

```text
✓ Nhập thành công 4 nhân sự hỗ trợ    [Xem chi tiết]    [×]
```

### 10.2 Correct Count Source

Use:

```text
report.validRows
```

Do NOT use:

```text
resultingCount
```

Reason:

If 5 existing guests are already present and 10 new rows are imported:

```text
resultingCount = 15
report.validRows = 10
```

The correct success message is:

```text
Nhập thành công 10 khách
```

not:

```text
Nhập thành công 15 khách
```

### 10.3 Expand / Collapse

Default:

```text
showDetails = false
```

Click:

```text
Xem chi tiết
```

→ display the current detailed success report.

Then label becomes:

```text
Ẩn chi tiết
```

Click again:

→ collapse.

Dismiss `×` should preserve existing dismiss behavior.

### 10.4 State Isolation

Guest import and Support import must keep independent state.

Do not let expanding or dismissing one section change the other section.

### 10.5 Acceptance Criteria

- Success state is compact by default.
- `report.validRows` is shown.
- Detailed success report can be expanded.
- Detailed success report can be collapsed.
- Dismiss still works.
- Guest and Support states are independent.

---

## 11. Phase UI-7 — Excel Error Result Must Stay Expanded

### 11.1 Requirement

Do NOT apply the compact-success treatment to error cases.

If import contains errors, keep the detailed error report immediately visible.

Keep existing information such as:

- filename;
- valid row count;
- error count;
- error details;
- row-level feedback;
- report download, if currently supported;
- retry / choose another file;
- dismiss.

### 11.2 Acceptance Criteria

```text
SUCCESS → compact by default
ERROR   → expanded by default
```

The user must not have to click "Xem chi tiết" to discover why an Excel import failed.

---

## 12. Phase UI-8 — Media Consent Wording

### 12.1 Persisted Contract

Do NOT change API / database enum values.

They remain:

```text
AGREED
DECLINED
```

### 12.2 Vietnamese Labels

`AGREED`:

```text
Cho phép sử dụng hình ảnh để truyền thông
```

`DECLINED`:

```text
Không cho phép
```

### 12.3 English

Add equivalent English wording through i18n.

### 12.4 Important Wire Rule

Correct:

```json
{
  "mediaConsentStatus": "AGREED"
}
```

Incorrect:

```json
{
  "mediaConsentStatus": "Cho phép sử dụng hình ảnh để truyền thông"
}
```

Only the presentation label changes.

---

## 13. Phase UI-9 — Long Media Consent Label UX

### 13.1 Requirement

The new `AGREED` label is long.

The closed native select may visually truncate it to avoid layout overflow.

Example:

```text
Cho phép sử dụng hình ảnh...
```

### 13.2 Full Label Accessibility

The user must still be able to inspect the full selected label.

Reuse the existing Visit Request `HelpTooltip`.

Full text must be accessible through:

- hover;
- keyboard focus;
- click / touch.

Do not rely solely on a bare HTML `title` attribute.

Do not replace the native select with a custom listbox unless the actual source proves it necessary.

### 13.3 Acceptance Criteria

- Select remains usable on desktop/mobile.
- Closed selected label does not break layout.
- Full selected label is accessible.
- Native options still show their full labels.
- Persisted value remains `AGREED` / `DECLINED`.

---

## 14. i18n

Update both Vietnamese and English Visit Request locale files.

Expected UI keys may include equivalents for:

```text
excel.importGuests
excel.importSupport
excel.successGuests
excel.successSupport
excel.showDetails
excel.hideDetails
card.mediaAgreed
card.mediaDeclined
```

Exact key names should follow the existing repository convention.

Do not add business text directly into components where i18n is already used.

Do not delete existing locale keys unless deletion is necessary and proven safe.

---

## 15. Expected Files to Review / Potentially Change

The implementation must locate the exact current paths before editing.

Likely affected frontend areas:

```text
VisitRequestFormV2.tsx
CampusVisitCard.tsx
ExcelImportPanel.tsx
components/shared/HelpTooltip.tsx
VisitDateTimeRangePicker.tsx
locales/vi/visitRequestV2.json
locales/en/visitRequestV2.json
Visit Request frontend tests
```

Important:

`CampusVisitCard.tsx` also contains Operational Contact code.

When editing this file:

- only modify sections required by this UI phase;
- do not refactor Operational Contact;
- do not change member identity/contact logic while working on count badges, Excel or Media Consent.

---

## 16. No Backend Changes Expected

This phase is intended to be frontend-only.

Expected:

```text
Backend business logic changed: NO
Database schema changed: NO
Database migration added: NO
API contract changed: NO
```

If implementation unexpectedly requires a backend change, STOP and report:

1. exact reason;
2. source evidence;
3. affected endpoint/service;
4. why frontend-only is insufficient.

Do not silently expand the scope.

---

## 17. Regression Checklist

Before reporting done, verify at minimum:

### Registrant

- [ ] Full name still binds correctly.
- [ ] Nationality still validates correctly.
- [ ] Organization still binds correctly.
- [ ] Job title still binds correctly.
- [ ] Phone still normalizes/validates correctly.
- [ ] Email still validates correctly.
- [ ] Profile autofill still works.
- [ ] Desktop layout matches requested grouping.
- [ ] Mobile layout does not overflow.

### Help Tooltip

- [ ] `?` is replaced by `i`.
- [ ] Hover works.
- [ ] Keyboard focus works.
- [ ] Click/touch works.
- [ ] Escape works.
- [ ] `aria-expanded` preserved.
- [ ] `aria-describedby` preserved.
- [ ] No unrelated icons changed.

### Schedule

- [ ] Permanent 72h/30m hint removed.
- [ ] 72-hour validation still works.
- [ ] 30-minute minimum still works.
- [ ] Contextual tooltip still exists.

### Members

- [ ] Guest `(x/200)` hidden.
- [ ] Support `(x/200)` hidden.
- [ ] Max 200 still enforced.
- [ ] Add button still disables/refuses at max.
- [ ] Excel max still enforced.

### Excel

- [ ] Guest button wording updated.
- [ ] Support button wording updated.
- [ ] Success is compact by default.
- [ ] Success uses `report.validRows`.
- [ ] "Xem chi tiết" expands.
- [ ] "Ẩn chi tiết" collapses.
- [ ] Dismiss works.
- [ ] Error remains fully expanded.
- [ ] Guest state independent from Support state.
- [ ] Support state independent from Guest state.

### Media Consent

- [ ] `AGREED` label updated.
- [ ] `DECLINED` label updated.
- [ ] Wire value remains `AGREED`.
- [ ] Wire value remains `DECLINED`.
- [ ] Long label does not break layout.
- [ ] Full label accessible via hover.
- [ ] Full label accessible via keyboard.
- [ ] Full label accessible via click/touch.
- [ ] Native select remains functional.

### Operational Contact Isolation

- [ ] No Operational Contact behavior changed.
- [ ] No contact key logic changed.
- [ ] No contact validation changed.
- [ ] No contact confirmation workflow changed.
- [ ] No Replace/Transfer logic changed.
- [ ] No backend contact code changed.

---

## 18. Test Execution

After each major UI phase:

```bash
npm run lint
```

Run focused frontend tests for the affected component.

At the end run the full Visit Request frontend test suite using the actual repository command/path.

Also run:

```bash
npm run audit:responsive
```

If the guessed test command or path does not exist:

1. locate the actual package/test configuration;
2. run the equivalent command;
3. do not skip the test.

Every test result reported must be based on actual execution.

---

## 19. Manual Responsive Verification

Check at least:

```text
Mobile
Tablet
Desktop
```

Pay special attention to:

- Registrant two-row layout;
- long organization values;
- nationality selector;
- Excel success banner;
- Excel detail expansion;
- Guest/Support section headings;
- Media Consent select;
- HelpTooltip placement;
- long Vietnamese labels.

Reject any result with:

- horizontal overflow;
- overlapping labels;
- buttons overlapping text;
- clipped validation messages;
- broken select width;
- unreadable mobile layout.

---

## 20. Implementation Order

Execute in this order:

```text
1. Pre-flight / source refresh
2. Registrant layout
3. Help icon ? → i
4. Remove standing schedule hint
5. Hide member count badges
6. Rename Excel buttons
7. Implement compact Excel success
8. Verify Excel errors remain expanded
9. Update Media Consent wording
10. Add accessible full-label tooltip behavior
11. Update VI/EN i18n
12. Focused lint/tests
13. Full Visit Request frontend regression
14. Responsive audit
15. Final diff self-review
16. Final report
```

Do not mix Operational Contact redesign into any of these steps.

---

## 21. Final Self-Review

Before reporting completion, inspect the final diff and answer:

1. Did any Operational Contact logic change?
   - Expected: NO.

2. Did any backend business logic change?
   - Expected: NO.

3. Did API payload shape change?
   - Expected: NO.

4. Did `AGREED` / `DECLINED` wire values change?
   - Expected: NO.

5. Did the 72-hour rule change?
   - Expected: NO.

6. Did the 30-minute rule change?
   - Expected: NO.

7. Did max 200 change?
   - Expected: NO.

8. Does Excel success use `report.validRows`?
   - Expected: YES.

9. Are Excel errors still immediately visible?
   - Expected: YES.

10. Are VI and EN complete?
    - Expected: YES.

11. Did responsive audit actually run?
    - Expected: YES.

12. Did frontend regression tests actually run?
    - Expected: YES.

If any answer is unknown, continue investigation before reporting done.

---

## 22. Final Report Format

The implementation report should contain:

### A. Baseline

- Repository
- Branch
- HEAD
- Working-tree status before implementation

### B. Files Changed

List exact files.

### C. Requirement Mapping

Map every requirement in this plan to the code that implements it.

### D. UI Changes

Report:

- Registrant layout
- Help icon
- Schedule hint
- Member count
- Excel labels
- Excel success
- Excel error
- Media Consent

### E. i18n

List changed VI/EN keys.

### F. Regression Protection

Explicitly state:

```text
Operational Contact behavior changed: NO
Backend logic changed: NO
Database schema changed: NO
Migration added: NO
API contract changed: NO
```

### G. Test Commands

Report exact commands run.

### H. Test Results

For every command:

- PASS / FAIL
- test count where available
- failure cause if any
- fix applied
- rerun result

### I. Responsive Audit

Report actual output/result.

### J. Deviations

If implementation differs from this plan because current source required a different safe implementation, document:

- what changed;
- why;
- source evidence;
- whether behavior remains equivalent.

### K. Residual Risks

Do not claim "zero risk".

Use:

```text
No known residual regression identified within the tested scope.
```

only if that statement is supported by the actual test and review results.

---

## 23. Commit Rule

Do not commit, push or merge unless explicitly requested after review.

Expected completion state:

```text
Implementation complete
Tests complete
Responsive audit complete
Self-review complete
Changes uncommitted for review
```

---

## 24. Summary of This Phase

This phase intentionally delivers only:

```text
Registrant layout
Help icon
Schedule-hint cleanup
Member-count cleanup
Excel UI cleanup
Media Consent UI cleanup
VI/EN
Responsive stabilization
Frontend regression
```

Operational Contact is intentionally deferred to a separate, independently reviewed phase.
