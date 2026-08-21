# PEMS Email Template Fidelity + SMTP Diagnostics — Final Fix Plan

**Date:** 2026-08-21  
**Repository:** `quangthoai04/PEMS`  
**Target branch:** `Dev`  
**Baseline reviewed:** `3cc6d30b...` plus the already-landed Resend diagnostics/retry/idempotency patch described in the latest implementation report.  
**Scope:** only the remaining email-system closure work. Do **not** reopen already-closed Email Action concurrency/CSP/participant/logistics fixes unless new tests prove a regression.

---

## 1. Goal

Close the remaining gaps in the PEMS email system so that:

1. Editing a template does not destroy HTML/style outside the text actually changed.
2. Visible content shown in Template Preview matches visible content in the real sent email.
3. Runtime action blocks inject only the technical action control/link/token they own and do not secretly add business prose outside the configured template.
4. SMTP failures are classified with useful machine codes instead of collapsing into one generic `SMTP_SEND_FAILED`.
5. Retry/fallback behavior remains safe and cannot create duplicate mail.
6. Final Preview shows all system-owned visible content that will actually be delivered, including the branded shell.
7. The database template remains the authoritative source of editable recipient-facing wording.

---

## 2. Already done — do not reimplement

The following areas are considered closed unless regression tests fail:

- Public email-action CSP carve-out.
- Participant `ASSIGNED` response support for Department Staff.
- Shared participant Portal/Email transition core.
- MySQL locking / `READ COMMITTED` serialized transaction work.
- Participant and Logistics concurrency tests.
- Parent lifecycle cancel-vs-response race handling.
- Logistics Staff decline terminal state = `DECLINED`.
- Proposal decision moved Portal-only.
- Canonical deep-link fixes.
- BUG-12 production base-URL validation.
- Resend granular error classification.
- Resend safe retry policy.
- Resend durable `Idempotency-Key`.
- Resend Test Connection diagnostics.
- Manual email history preserving machine codes.
- No blind SMTP fallback after Resend has been contacted.

These areas may receive only minimal compatibility changes required by this plan.

---

# 3. Remaining problems

## 3.1 P0 — Rich-text editor can destructively round-trip canonical template HTML

### Current behavior

Canonical templates contain email-safe layout HTML such as:

```html
<div style="
  margin:18px 0;
  padding:14px 16px;
  background:#fff7ed;
  border:1px solid #fed7aa;
  border-radius:8px;
  color:#9a3412;
  line-height:1.6">

  <strong>Lưu ý bảo mật:</strong>
  Không chia sẻ liên kết này...
</div>
```

The current shared Quill-based editor does not fully represent all structural/container styles used by the shipped templates.

Observed behavior:

```text
DB canonical HTML
    ↓
Template screen loads
    ↓
Preview still uses untouched formData → style survives
    ↓
Quill displays a simplified representation
    ↓
User edits one character
    ↓
Quill serializes its representation
    ↓
formData is replaced by simplified HTML
    ↓
Preview now loses the original callout style
    ↓
Save can persist the style loss to DB
```

This is a data-integrity problem, not merely a visual mismatch.

### Required fix

Implement a canonical editor representation that can safely round-trip every structural block used by the system templates.

Preferred design:

- Introduce first-class safe block/container representations for email callouts/panels.
- At minimum preserve:
  - `margin`
  - `padding`
  - `background` / `background-color`
  - `border`
  - `border-radius`
  - `color`
  - `line-height`
  - supported `text-align`
  - nested inline formatting
- Preserve existing table, divider, variable, template-block, and system-action handling.
- Do not enable arbitrary raw HTML editing.
- Do not simply whitelist unrestricted `style=""`.
- Do not fix only the orange security panel. Audit all canonical templates for styled structural containers.

Possible implementation direction:

```text
stored canonical HTML
    ↕
email editor structural-node conversion
    ↕
Quill-safe node/blot model
```

The conversion must be semantic and deterministic.

### Hard acceptance rule

For any supported canonical template:

```text
open
→ edit exactly one text character in location X
→ serialize
```

must preserve all unrelated structure and styles.

The result may differ in harmless canonical serialization details, but must be semantically equivalent everywhere except the edited content.

---

# 4. P0 — Remove hidden recipient-facing prose from runtime system action blocks

## 4.1 Confirm-email example

Current runtime block contains content such as:

```text
[Xác nhận email]

Hoặc mở liên kết:
http://.../confirm-email?token=...
```

The line `Hoặc mở liên kết: ...` is not authored in the template. It is inserted by backend runtime code.

That violates the desired rule:

> Recipient-facing prose must not appear unexpectedly outside the configured template.

## 4.2 System-wide audit required

Audit every runtime action/system block, including but not limited to:

- `ConfirmEmailBlock`
- participant accept/decline blocks
- Department Staff assignment response blocks
- contact-role invitation blocks
- logistics request action blocks
- logistics assignee action blocks
- detail-link blocks
- login blocks
- visit-detail blocks
- proposal/detail-only blocks
- reminder/detail action blocks
- any legacy helper still used by a production caller

Look specifically for hidden prose such as:

- `Hoặc mở liên kết: ...`
- action expiry explanations
- login instructions
- “không cần đăng nhập”
- “yêu cầu đăng nhập”
- “xem thông tin mới nhất”
- fallback URL text
- extra security notes
- “hành động khác” explanations
- reply/contact instructions
- any visible sentence not present in the DB template or an explicitly previewed protected shell

## 4.3 New ownership rule

Runtime system blocks may own:

- `<a>` or equivalent action control
- real runtime URL
- one-time token embedded in that URL
- action label from shared action metadata
- minimal accessibility attributes required for the control

They must **not** own arbitrary business prose.

If a sentence is required for the recipient to understand the action, move that sentence into the template body.

Example target:

```html
<p>Bấm nút bên dưới để xác nhận địa chỉ email này.</p>

{{actionBlock}}

<p>
  Liên kết xác nhận có hiệu lực trong
  <strong>{{expiresInHours}}</strong> giờ
  và chỉ dùng được một lần.
</p>
```

Runtime:

```text
{{actionBlock}}
    ↓
[Xác nhận email]
```

No extra URL paragraph.

---

# 5. P0 — Preview and real send must have visible-content parity

## Current defect

Preview and real send are rendered through different helper methods:

```text
Preview:
DisabledConfirmEmailBlock()

Runtime:
ConfirmEmailBlock(realUrl)
```

The current tests prove selected labels and security properties, but do not guarantee full visible-text/layout parity.

## Required architecture

Move toward one action specification / one rendering definition.

Conceptual model:

```csharp
ActionBlockSpec
{
    ActionKind
    Label
    ButtonCount
    Layout
    VisibleTextOwnedByBlock
}
```

Then:

```text
ActionBlockSpec
        ├── Preview renderer
        │      clickable = false
        │      token/url = none
        │
        └── Runtime renderer
               clickable = true
               runtime URLs attached
```

The visible wording and structure must come from the same spec.

### Required parity contract

After normalizing away only security/runtime differences:

- `href`
- one-time token value
- disabled vs clickable element
- generated IDs/nonces if any

preview and runtime must have the same:

- visible text
- action labels
- button count
- action order
- surrounding block structure
- spacing semantics

### Example

Correct:

```text
Preview:
[Xác nhận email]

Send:
[Xác nhận email]
```

Only the runtime version has `href`.

Incorrect:

```text
Preview:
[Xác nhận email]

Send:
[Xác nhận email]
Hoặc mở liên kết...
Liên kết có hiệu lực...
```

---

# 6. P1 — Branded shell must be part of the preview contract

## Current issue

Final delivery includes system-owned shell content around the DB body, such as:

```text
PEMS — Campus Visit
FPT University

[TEMPLATE BODY]

© ... PEMS — FPT University
Không trả lời email này.
```

The administrator does not currently edit all of this in the template, but the recipient sees it.

This creates a mismatch between Template Preview and actual delivered email.

## Required decision

The shell may remain system-owned and non-editable, but it must be explicit.

Preferred approach:

- Keep shell centralized in one backend renderer.
- Expose the shell as protected preview content.
- Template editor/final preview clearly indicates:
  - editable template body
  - protected system header/footer
- Do not duplicate shell text in frontend hard-code.
- Preview should obtain the final composed shell from backend/shared metadata whenever practical.

The administrator does **not** need permission to edit the shell unless product requirements change.

## Acceptance

Final Preview must show all visible system-owned content that will be sent.

No recipient-visible header/footer sentence may appear only after the operator presses Send.

---

# 7. P1 — SMTP granular diagnostics

## Current gap

Resend now has granular diagnostics, but SMTP remains much coarser.

The SMTP path must distinguish at least the important operational classes without pretending to know more than the protocol proves.

## Proposed machine codes

```text
SMTP_DISABLED
SMTP_MISCONFIGURED
SMTP_AUTH_FAILED
SMTP_RECIPIENT_REJECTED
SMTP_RATE_LIMITED
SMTP_TLS_FAILED
SMTP_CONNECTION_FAILED
SMTP_TIMEOUT
SMTP_PROVIDER_REJECTED
SMTP_NETWORK_UNKNOWN
```

Existing legacy `SMTP_SEND_FAILED` may remain for compatibility with historical rows, but should no longer be the normal catch-all for new sends if the exception can be classified safely.

## Classification principles

Do not classify purely from exception message string unless no typed protocol information exists.

Prefer:

- typed SMTP exceptions
- SMTP status codes
- inner exception types
- TLS/authentication exception types
- socket/network exception types

Be conservative.

Example:

```text
authentication rejected before message submission
→ SMTP_AUTH_FAILED
→ definitive failure
```

But:

```text
connection dropped after DATA was transmitted
→ SMTP_NETWORK_UNKNOWN
→ ambiguous
```

## No unsafe SMTP retry

Unlike Resend HTTP API, ordinary SMTP delivery does not give PEMS a provider-level idempotency mechanism equivalent to `Idempotency-Key`.

Therefore:

- do not automatically retry ambiguous SMTP outcomes
- do not automatically switch to Resend after an ambiguous SMTP outcome
- do not automatically switch to SMTP after an ambiguous Resend outcome

Any retry must be limited to cases where the implementation can prove the message was not accepted, or must remain disabled.

## SMTP rate/quota handling

If the SMTP provider returns an explicit temporary rate/quota status:

- classify it separately where evidence is strong
- surface an actionable safe message
- do not infer Gmail/Microsoft/provider-specific quota semantics without actual protocol evidence

---

# 8. P1 — Unified delivery-history semantics

After SMTP classification is added, all sending pipelines must persist the same machine-code format.

Use:

```csharp
EmailAttemptRecord.Format(delivery)
```

for failures across:

- system email
- manual email
- direct email paths
- report email
- recovery/resend path
- any obsolete production caller still reachable

Audit all callers of:

```text
IEmailService.TrySendAsync
IEmailService.SendAsync
EmailService
ResendEmailService
```

Do not persist:

- raw provider response bodies
- API keys
- credentials
- tokens
- OTPs
- raw one-time action URLs
- full exception dumps in DB history

Server logs may retain technical stack information subject to existing logging policy, but still must not leak credentials/tokens.

---

# 9. Canonical pipeline target

The final email system should behave like this:

```text
DB TEMPLATE
   │
   ├── editable recipient-facing wording
   ├── variables
   └── {{systemBlock}} placeholders
   │
   ▼
CANONICAL RENDERER
   │
   ├── variable substitution
   ├── runtime action controls
   └── protected branded shell
   │
   ├──────────────► FINAL PREVIEW
   │                 same visible content
   │
   ▼
DELIVERY
   │
   ├── Resend
   │      granular classification + safe retry + idempotency
   │
   └── SMTP
          granular classification + conservative ambiguity handling
```

The system must no longer behave as:

```text
DB template
+ hidden action prose
+ hidden shell prose
+ editor HTML loss
+ preview-only renderer
+ send-only renderer
```

---

# 10. Detailed implementation phases

## Phase A — Template structural fidelity (P0)

### Audit

Enumerate every active system template and identify:

- styled `<div>` containers
- bordered callouts
- colored panels
- tables
- horizontal rules
- styled paragraphs
- nested spans
- variables
- template system blocks

Produce a compatibility matrix:

```text
HTML structure/style
→ editor currently preserves?
→ editor currently displays?
→ editor currently round-trips?
```

### Implement

Add the missing safe structural editor representation.

Prefer one generalized but constrained mechanism over many template-specific hacks.

Do not write logic such as:

```text
if text contains "Lưu ý bảo mật"
    add orange border
```

That would be presentation reconstruction, not content fidelity.

### Tests

Add:

- open/save-without-edit semantic equivalence tests
- one-character-edit preservation tests
- callout container preservation
- table preservation
- multiple styled-container preservation
- VI and EN body preservation
- template switch preservation
- restore-default preservation

Test canonical templates, not only hand-crafted toy HTML.

---

## Phase B — Runtime content ownership cleanup (P0)

Audit all `EmailComposition` / action-block helpers.

For each visible sentence, classify ownership:

```text
TEMPLATE
SYSTEM ACTION CONTROL
PROTECTED SHELL
```

No sentence may have unclear ownership.

Move action-related explanatory prose into DB defaults where it is currently required but hidden in backend code.

Remove unnecessary prose such as the direct raw URL fallback unless a current business requirement explicitly requires it.

For `ACCOUNT_EMAIL_CONFIRMATION`, remove:

```text
Hoặc mở liên kết: {confirmUrl}
```

unless product explicitly decides to keep a visible fallback URL. If kept, it must become visible/configurable/previewable template content rather than hidden runtime prose.

Update:

- shipped defaults JSON
- canonical SQL seed
- sync scripts
- parity tests
- restore-default assets

Follow the repository's existing canonical-template source-of-truth chain; do not hand-edit only one representation and leave drift.

---

## Phase C — Preview/runtime parity (P0)

Refactor action rendering so preview and runtime share the same action specification.

Required tests for every action-bearing system template:

- same visible text
- same labels
- same count/order
- preview has no live token
- preview has no live href
- runtime has expected href
- stripping `href`/token-only differences produces equivalent structure

Add a system-wide test that enumerates every registered action template rather than testing only one or two hard-coded cases.

---

## Phase D — SMTP diagnostics (P1)

Implement a shared SMTP classifier.

Possible abstraction:

```csharp
SmtpDeliveryClassification
{
    Code
    SafeMessage
    IsDefinitiveFailure
    IsRetryable
    IsAmbiguous
}
```

Use the classifier inside the real SMTP sender.

If SMTP Test Connection has independent exception mapping, refactor it to use the same classifier.

Do not create two separate classification tables.

### Required SMTP tests

At minimum:

- SMTP disabled
- SMTP config missing
- auth failure
- invalid recipient / mailbox rejected
- temporary server rejection
- TLS failure
- connection refused
- DNS/network failure if testable via abstraction
- timeout
- ambiguous send exception
- legacy generic fallback classification
- history stores machine code
- no automatic retry on ambiguous result
- no cross-provider blind fallback

---

## Phase E — Final preview + shell parity (P1)

Ensure the final preview uses the same final composition path as actual delivery, except for secrets.

For token-bearing email:

Preview must use inert fake/synthetic controls:

```text
no real token
no live URL
no token persistence
no mutation
```

But all visible copy and layout must match runtime.

Template editor can retain a lightweight draft preview, but there must also be a clearly identified **Final Preview** whose composition path matches send.

---

# 11. Test matrix

## 11.1 Template integrity

```text
Open canonical template → no edit → no semantic content/style drift
Open → edit one character → unrelated style unchanged
Open → edit inside orange security callout → callout remains
Open → edit above blue confirmation callout → callout remains
VI edit → EN unchanged
EN edit → VI unchanged
Restore default → exact shipped semantic default
Save → reload → same rendered appearance
```

## 11.2 Action parity

For all action-bearing templates:

```text
Preview visible text == Runtime visible text
Preview action labels == Runtime action labels
Preview button count == Runtime button count
Preview contains no live URL/token
Runtime contains correct runtime URL
```

## 11.3 Hidden prose

Assert real runtime email does not contain known hidden-prose regressions unless present in template:

```text
"Hoặc mở liên kết:"
```

and any other phrases removed during the audit.

## 11.4 SMTP

```text
auth
recipient reject
rate/temp reject
TLS
connection
timeout
network ambiguity
misconfiguration
disabled
```

Check machine codes and safe messages.

## 11.5 End-to-end final preview

For representative templates:

- ACCOUNT confirmation
- AUTH OTP
- participant invitation
- Department Staff assignment
- Logistics request
- Logistics assignee assignment
- reminder/detail-only email
- report/manual email

Compare preview and sent HTML after removing only intentional runtime-secret differences.

---

# 12. Security constraints

Must preserve all of the following:

- No real token generated for ordinary template preview.
- No real action URL in editor preview.
- No OTP/token/action URL written to logs.
- No arbitrary raw HTML editor.
- No arbitrary JavaScript/style injection.
- Existing HTML sanitizer remains authoritative.
- Existing public email action CSP remains unchanged unless a failing regression test proves otherwise.
- No reopening GET mutation.
- No blind cross-provider fallback.
- No duplicate retry risk introduced.
- No dev credentials changed.
- No production base-URL validation regression.

---

# 13. Data / migration considerations

A database schema migration is **not expected** for this plan unless implementation discovers that the editor needs a new persisted structural representation.

Template-content updates are expected.

When default wording/HTML changes:

1. update authoritative shipped defaults
2. update canonical schema/seed representation
3. update deployment sync/patch script
4. update parity tests
5. verify restore-default returns the new canonical version
6. make the update idempotent

Do not directly update only the local database and call the task complete.

---

# 14. Observability

## SMTP logs should include

- normalized machine code
- provider status code when available
- masked recipients
- stage of failure where safe
- environment
- correlation/send identifier if already available

Do not log:

- password
- API key
- raw auth response
- full body
- OTP
- token
- real one-time URL

## Template rendering diagnostics

When rendering fails due to unsupported content:

- return a stable machine code
- identify the unsupported structural feature in admin-facing diagnostics
- do not silently strip and continue

Silent destructive normalization is not acceptable.

---

# 15. Files / areas likely affected

This is an audit list, not permission to edit every file.

Frontend likely areas:

```text
frontend/pems-react/src/features/emails/components/EmailRichTextEditor.tsx
frontend/pems-react/src/features/emails/utils/emailEditorFormats.ts
frontend/pems-react/src/features/emails/utils/emailEditor*
frontend/pems-react/src/features/emails/utils/templateDraftPreview.ts
frontend/pems-react/src/features/emails/types/templateContract.ts
frontend/pems-react/src/pages/dashboard/emails/TemplateManagement.tsx
frontend/pems-react/src/features/emails/__tests__/*
```

Backend likely areas:

```text
backend/PEMS.Application/Emails/Common/EmailComposition.cs
backend/PEMS.Application/Emails/Common/EmailActionTemplates.cs
backend/PEMS.Application/Emails/Common/SystemEmailTemplates.cs
backend/PEMS.Application/Emails/Preview/*
backend/PEMS.Application/Emails/Commands/BuildFinalEmailPreview/*
backend/PEMS.Application/Emails/Queries/GetEmailTemplateContract/*
backend/PEMS.Infrastructure/Email/EmailTemplateRenderer.cs
backend/PEMS.Infrastructure/Email/EmailService.cs
backend/PEMS.Application/Common/Interfaces/EmailDeliveryResult.cs
backend/PEMS.Application/Common/Interfaces/IEmailService.cs
```

Template canonical sources:

```text
backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json
docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql
docs/database/scripts/email_template_cc_bcc_sync/*
docs/database/scripts/patches/*
```

Tests:

```text
tests/PEMS.UnitTests/Emails/*
tests/PEMS.IntegrationTests/Emails/*
```

---

# 16. Implementation guards

Do **not**:

- patch only the literal phrase `Hoặc mở liên kết`.
- patch only `ACCOUNT_EMAIL_CONFIRMATION`.
- add frontend CSS that reconstructs lost backend HTML by matching text.
- allow unrestricted arbitrary `<div style="">`.
- keep two independent preview/send action renderers that duplicate visible wording.
- move one-time token generation into editable template data.
- expose a live token in preview.
- introduce SMTP automatic retry for ambiguous outcomes.
- introduce SMTP→Resend or Resend→SMTP fallback after the first provider may already have accepted the message.
- silently normalize/strip unsupported HTML during save.
- claim parity based only on matching button labels.
- hand-edit only one canonical template source and leave JSON/SQL/default restore inconsistent.

---

# 17. Exit gates

The work is complete only when all of the following pass.

## Gate 1 — Build

```text
dotnet build PEMS.slnx
frontend build/typecheck
```

No errors.

## Gate 2 — Template structural fidelity

Automated tests prove:

```text
edit one character
→ unrelated structural HTML/style remains semantically unchanged
```

for real canonical templates.

## Gate 3 — Action parity

Every registered action-bearing template passes:

```text
Visible(Preview) == Visible(Runtime)
```

after removing only runtime-secret/clickability differences.

## Gate 4 — No hidden prose

A system-wide audit/test verifies no backend action helper injects unowned recipient-facing sentences.

## Gate 5 — SMTP classification

All targeted SMTP classification tests pass and machine codes persist correctly.

## Gate 6 — Full regression

Run:

```text
PEMS.UnitTests
PEMS.IntegrationTests
frontend email test suite
```

All green.

## Gate 7 — No security regression

Verify:

- no live preview token
- no token in logs
- no blind fallback
- no unsafe retry
- no raw HTML bypass
- no CSP regression
- no dev credential changes

---

# 18. Final acceptance examples

## ACCOUNT_EMAIL_CONFIRMATION

Template contains:

```text
Xin chào {{fullName}}
...
{{actionBlock}}
...
Liên kết xác nhận có hiệu lực trong {{expiresInHours}} giờ...
Lưu ý bảo mật...
```

Preview:

```text
Xin chào Nguyễn Văn A
...
[Xác nhận email]
...
Liên kết xác nhận có hiệu lực trong 24 giờ...
Lưu ý bảo mật...
```

Real sent email:

```text
Xin chào Nguyễn Văn A
...
[Xác nhận email]
...
Liên kết xác nhận có hiệu lực trong 24 giờ...
Lưu ý bảo mật...
```

The real button has a token URL; preview does not.

There is no hidden:

```text
Hoặc mở liên kết: ...
```

unless that wording is explicitly in the editable template.

## Structural edit

Before:

```text
[orange security callout]
```

Admin changes one word in an earlier paragraph.

After Preview + Save + Reload:

```text
[orange security callout]
```

must remain structurally/stylistically equivalent.

## SMTP

Instead of:

```text
SMTP_SEND_FAILED
```

the system records useful classifications such as:

```text
SMTP_AUTH_FAILED
SMTP_RECIPIENT_REJECTED
SMTP_TLS_FAILED
SMTP_TIMEOUT
SMTP_NETWORK_UNKNOWN
```

without introducing unsafe retries.

---

# 19. Suggested execution order

Use this exact order:

```text
Phase A — editor structural fidelity
Phase B — runtime hidden-prose ownership cleanup
Phase C — preview/runtime parity
Phase D — SMTP diagnostics
Phase E — branded-shell/final-preview parity
Full regression
```

Reason: the editor integrity issue can permanently alter stored template HTML, so it is the highest-risk remaining defect.

---

# 20. Agent execution prompt

```text
@GitHub

Read the latest Dev HEAD before making changes.

Implement the remaining "PEMS Email Template Fidelity + SMTP Diagnostics — Final Fix Plan".

Do not reopen already-completed Resend retry/idempotency, public email-action concurrency/CSP, participant/logistics state fixes, BUG-12, or deep-link work unless a new regression test proves a problem.

Priority order:

1. Fix destructive rich-text round-tripping first.
   Audit every active canonical email template, not only ACCOUNT_EMAIL_CONFIRMATION.
   Editing one character must not remove unrelated containers, borders, padding, colors, tables or other supported email-safe structure.
   Do not reconstruct styling from text and do not enable arbitrary raw HTML.

2. Audit all runtime action/system blocks and remove hidden recipient-facing prose.
   In particular remove the hard-coded "Hoặc mở liên kết: {url}" behavior from confirm-email unless the wording is explicitly moved into the DB template.
   Runtime action blocks should own the technical button/link/token, not hidden business prose.

3. Refactor preview and runtime action rendering to one shared action specification.
   Preview and sent email must have identical visible content/layout after removing only href/token/clickability differences.
   Add enumeration-based parity tests for every registered action-bearing system template.

4. Add granular SMTP diagnostics using a shared classifier.
   Distinguish disabled/misconfigured/auth/recipient/rate-limit-or-temporary/TLS/connection/timeout/provider-rejected/network-unknown where protocol evidence supports it.
   Preserve conservative ambiguity semantics.
   Do not add blind SMTP retries and do not add cross-provider failover after a provider may have accepted the message.

5. Make the branded shell explicit in Final Preview.
   It may remain protected/system-owned, but no visible header/footer sentence should appear only after Send.

6. Keep canonical template sources synchronized:
   defaults JSON → canonical schema/seed → deployment sync/patch → restore-default parity tests.
   Do not hand-edit only one source.

7. Run build and tests after each phase.
   Stop and report if a gate fails.
   Do not commit or push.

Final report must include:
- exact starting SHA
- files changed
- canonical-template structures audited
- editor round-trip strategy
- hidden prose removed/moved
- preview/runtime parity strategy
- SMTP error-code mapping
- ambiguity/retry/fallback rules
- template data/default changes
- all tests added
- build/unit/integration/frontend test counts
- any deliberate non-changes
- suggested commit message
```

---

# 21. Suggested commit message after successful review

```text
fix(email): preserve template fidelity and classify SMTP delivery failures
```

Alternative:

```text
fix(email): align template preview with sent content and harden SMTP diagnostics
```
