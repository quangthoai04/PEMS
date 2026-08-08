# PEMS — ONE-SHOT FINAL CLOSURE PROMPT v11
## Hoàn thiện toàn bộ phần còn lại từ đầu đến cuối trong MỘT LƯỢT
### Không dừng sau từng Phase · Không xin phép tiếp tục · Chỉ dừng đúng subtask khi có blocker nghiệp vụ thật

> Repository: `PEMS`
>
> Branch: `Cảnh-Iter1`
>
> Mục tiêu của prompt này: **thực hiện toàn bộ phần còn lại của v8 → v10 liên tục từ đầu đến cuối trong một lượt làm việc**, chạy đủ test/gates, audit diff cuối cùng và chỉ báo cáo khi đã hoàn thành hết phần có thể hoàn thành.
>
> **KHÔNG dừng sau PHASE D để hỏi "có làm tiếp không".**
>
> **KHÔNG dừng sau khi build xong một chức năng để chờ người dùng nói "tiếp tục".**
>
> **KHÔNG gửi báo cáo giữa chừng nếu không có blocker nghiệp vụ thực sự.**
>
> **KHÔNG commit trừ khi người dùng yêu cầu rõ.**

---

# 0. EXECUTION MODE — ONE SHOT / CONTINUOUS

Bạn phải làm việc theo chế độ:

```text
PREFLIGHT
→ RESUBMIT FRONTEND
→ PROFILE SYNC VERIFY/CLOSE
→ ACCOUNT TESTS
→ AUTHORIZATION MATRIX
→ AMENDMENT TESTS
→ FEEDBACK TESTS
→ FILE TESTS
→ TRANSFER TESTS
→ RECOVERY TEST GAPS
→ RUNBOOK VERIFY
→ POST-COMMIT REGRESSION
→ FULL GATES
→ FINAL DIFF AUDIT
→ FINAL REPORT
```

Không dừng giữa các bước để xin phép tiếp tục.

Nếu một subtask gặp blocker thật:

```text
1. Ghi nhận BLOCKED cùng exact evidence.
2. Không tự suy diễn business rule.
3. Tiếp tục làm TẤT CẢ subtask độc lập còn lại.
4. Chỉ hỏi người dùng khi blocker thực sự ngăn không thể triển khai đúng.
5. Nếu blocker không chặn các phần khác, tuyệt đối không kết thúc toàn bộ lượt làm việc sớm.
```

Mục tiêu là khi trả lời cuối cùng, hoặc:

```text
A. toàn bộ Definition of Done đã hoàn tất;
```

hoặc:

```text
B. tất cả phần không bị blocker đã hoàn tất,
   và chỉ còn đúng các blocker cần quyết định của user.
```

Không được kết thúc kiểu:

> "PHASE D xong, nói tôi nếu muốn làm PHASE E."

---

# 1. PREFLIGHT — bắt buộc trước khi sửa

Trước mọi thay đổi:

```text
git branch --show-current
git rev-parse HEAD
git status --short
git stash list
```

Xác nhận:

```text
Branch = Cảnh-Iter1
```

Ghi lại:

```text
Start HEAD
WIP count
modified files
untracked files
stash count
```

Quy tắc:

```text
- Preserve toàn bộ WIP.
- Không reset.
- Không checkout đè file.
- Không clean.
- Không discard.
- Không drop/apply/rewrite stash ngoài nhu cầu kiểm chứng baseline an toàn.
- Nếu cần chứng minh pre-existing failure bằng stash, phải bảo toàn đúng toàn bộ stash/WIP và verify sau khi restore.
- Không commit.
```

Audit **working tree hiện tại**, không chỉ HEAD.

---

# 2. CURRENT VERIFIED BASELINE — KHÔNG LÀM LẠI

Các phần sau đã được triển khai/verify ở các vòng trước. Chỉ sửa nếu phát hiện regression thật:

## Contact management

```text
- Existing Operational Contact không còn editable trong Visit Request Edit.
- Detail View là nơi quản lý Operational Contact.
- New campus vẫn có thể nhập initial contact.
- Same-email metadata update không tạo confirmation/token/transfer.
- Changed-email dùng INITIAL_CONFIRMATION hoặc TRANSFER canonical.
- Current contact A vẫn giữ quyền khi B đang pending transfer.
- contactFullName đã dùng tên thật, không dùng email.
```

## 72h

```text
Create                  → 72h
PRE-APPROVAL Edit       → 72h
Resubmit after Reject   → 72h
Approved Amendment      → KHÔNG dùng registration 72h
```

Passive time `<72h` không tự expire/reject/email.

## UI/platform fixes

```text
- dynamic campus count/source-of-truth
- controlled campus selector
- đúng campusId display/state/payload
- Edit success toast đúng 1 lần
- Resubmit success toast đúng 1 lần ở flow cũ
```

## Recovery

```text
- Reject recovery keyed theo exact rejection business event/audit id.
- Expiry recovery keyed theo identityChangeId.
- Machine error code tồn tại trong sent_emails.error_message.
- OUTCOME_UNKNOWN không auto retry.
- backoff tồn tại.
- max auto attempts = 5.
- MySQL GET_LOCK per event.
- business commit không bị báo rollback giả chỉ vì notification fail.
- runbook đã tồn tại.
```

## Per-instance Resubmit backend

Đã có endpoint:

```text
POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/resubmit
```

Đã có backend tests cho:

```text
- sibling isolation
- aggregate PARTIALLY_APPROVED
- sibling contact denied
- random VISITOR denied
- registrant allowed
- 72h refusal
- stale instance row version
```

Không thay bằng request-wide Resubmit.

## Profile Sync backend/read support

Đã có:

```text
ProfileDifference
```

trên instance-scoped Operational Contact state.

Detection identity:

```text
instance.OperationalContactUserId == actorId
```

Phone compare theo canonical digits.

Prompt component đã có:

```text
ContactProfileSyncPrompt.tsx
```

Profile update phải reuse canonical:

```text
POST /api/profiles/updateprofile
```

Approved sync fields:

```text
full_name
phone
```

Không sync:

```text
email
organization
jobTitle
```

---

# 3. ACCOUNT / CONTACT ACCEPTANCE LIFECYCLE — ĐÃ CHỐT

Giữ lifecycle hiện tại:

```text
GET invitation/token detail
→ anonymous masked view

Accept / Decline
→ authenticated
```

Flow:

```text
Invitee nhận link
→ mở link
→ đăng nhập SSO
→ canonical SSO provision/reuse account
→ authenticated Accept
→ bind OperationalContactUserId
```

Đây là security rule có chủ ý:

```text
possession of link alone != authority to take campus
```

Không thay đổi thành anonymous Accept.

Không tạo password/local-auth flow.

Không tạo account khi registrant chỉ nhập email.

Known constraint:

```text
Nếu email của invitee không thể authenticate qua SSO hỗ trợ,
họ không thể hoàn tất Accept.
```

Chỉ ghi constraint này ở final report. Không tự xây auth mới.

---

# 4. ACCOUNT ELIGIBILITY — ĐÃ CHỐT THEO CODE CANONICAL HIỆN TẠI

Giữ:

```text
ACTIVE
+
normalized email match invitation target
```

Internal account hiện được phép làm Operational Contact.

Không:

```text
- tạo duplicate VISITOR account
- convert role
- reactivate inactive user
- tạo account thứ hai cho cùng normalized email
```

Chỉ reopen nếu phát hiện evidence mới mâu thuẫn trực tiếp.

---

# 5. PHASE D — HOÀN THIỆN PER-INSTANCE RESUBMIT FRONTEND

Đây là feature gap lớn nhất hiện tại.

Backend đã xong nhưng browser chưa sử dụng được.

## 5.1 API client

Thêm/reuse frontend API method cho:

```text
POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/resubmit
```

Payload phải dùng đúng contract backend, bao gồm target instance row version nếu backend yêu cầu.

Không gọi request-wide Resubmit endpoint cho Operational Contact.

## 5.2 Action resolution

Khi:

```text
instance.status == REJECTED
AND
current user có quyền instance theo data/backend action state
```

hiển thị CTA phù hợp.

Không hardcode:

```text
role == VISITOR
```

để suy ra quyền.

Prefer action/capability returned/resolved từ canonical backend/read model nếu hiện có.

## 5.3 Operational Contact UX

Flow phải usable:

```text
Operational Contact A
→ mở HN bị Reject
→ xem rejection reason/status
→ Edit allowed HN instance-local fields
→ Resubmit HN
→ chỉ HN quay lại review
```

Không cho A thao tác sibling DN/HCM.

## 5.4 Registrant

Không regress existing registrant flow.

Nếu registrant dùng request-wide Resubmit ở một case canonical khác, giữ đúng behavior đó.

Không vô tình chuyển toàn bộ registrant semantics sang instance-only nếu current business rule chưa yêu cầu.

## 5.5 Error handling

Map đúng canonical backend errors:

```text
- unauthorized
- target not rejected
- stale instance row version
- 72h lead-time violation
- validation errors
```

Dùng i18n VI/EN.

Không duplicate hardcoded Vietnamese ngoài translation system.

## 5.6 Toast

Successful per-instance Resubmit:

```text
exactly 1 toast
```

Không duplicate StrictMode.
Không replay sau refresh/back-forward.

## 5.7 FE tests — bắt buộc

Viết:

```text
FE-RESUBMIT-01
HN current contact + HN REJECTED
→ Resubmit CTA visible

FE-RESUBMIT-02
DN sibling contact viewing HN
→ HN Resubmit CTA unavailable

FE-RESUBMIT-03
random VISITOR
→ unavailable

FE-RESUBMIT-04
Operational Contact submits
→ calls /instances/{instanceId}/resubmit
→ never calls old request-wide endpoint

FE-RESUBMIT-05
success
→ exactly one toast

FE-RESUBMIT-06
72h error
→ correct localized message

FE-RESUBMIT-07
stale row version
→ conflict UI/message
```

Nếu UI architecture dùng shared action resolver, test resolver + rendered behavior.

---

# 6. PHASE E — HOÀN TẤT PROFILE SYNC END-TO-END

Backend/read support và prompt component đã có, nhưng phải kiểm tra feature có thực sự usable end-to-end.

Approved fields:

```text
snapshot.fullName → users.full_name
snapshot.phone    → users.phone
```

Chỉ khi chính account holder chủ động chọn update.

## 6.1 Prompt location

Prompt chỉ xuất hiện cho:

```text
current authenticated actor
==
instance.OperationalContactUserId
```

và chỉ khi:

```text
ProfileDifference != none
```

Không show cho:
- registrant đang xem contact của người khác;
- sibling contact;
- staff quản lý request;
- random Visitor.

## 6.2 Copy

VI:

> **Thông tin liên hệ trong yêu cầu này khác hồ sơ PEMS của bạn. Bạn có muốn cập nhật hồ sơ cá nhân không?**

Buttons:

```text
Giữ nguyên hồ sơ
Cập nhật hồ sơ cá nhân
```

Có EN translation tương đương.

## 6.3 Keep profile

```text
Giữ nguyên hồ sơ
→ không update users
→ không update snapshot
→ không email
→ không status transition
→ không identity change
```

Không ép user phải sync.

## 6.4 Update profile

Reuse canonical self-profile endpoint/service.

Payload chỉ chứa field cần sync:

```text
full_name
phone
```

Nếu canonical endpoint yêu cầu DTO rộng hơn, đọc current profile trước và preserve các field không được phép thay đổi; không gửi giá trị rỗng vô ý làm mất gender/nationality nếu DTO semantics là replace.

Đây là điểm phải test kỹ.

## 6.5 Forbidden

Không sync:

```text
email
organization
jobTitle
role
status
primaryCampusId
```

Không:
- confirmation;
- transfer;
- 72h;
- Amendment;
- historical snapshot rewrite.

## 6.6 UX after sync

Sau success:

```text
- account profile refresh/update
- ProfileDifference biến mất
- prompt đóng
- feedback success đúng 1 lần
```

Không cần reload toàn app nếu query/cache có thể invalidate/refetch canonical.

## 6.7 Tests

Backend/integration + FE:

```text
PROFILE-SYNC-01 same profile → no prompt
PROFILE-SYNC-02 different full_name → prompt
PROFILE-SYNC-03 different phone → prompt
PROFILE-SYNC-04 formatting-equivalent phone → no false prompt
PROFILE-SYNC-05 Keep profile → no mutation
PROFILE-SYNC-06 Update → only full_name + phone
PROFILE-SYNC-07 email/org/jobTitle unchanged
PROFILE-SYNC-08 historical snapshots unchanged
PROFILE-SYNC-09 registrant cannot sync another account
PROFILE-SYNC-10 sibling/random visitor denied
PROFILE-SYNC-11 no confirmation/transfer/72h side effects
PROFILE-SYNC-12 successful sync removes difference/prompt
```

---

# 7. PHASE F — HOÀN THIỆN ACCOUNT-01..06, ĐẶC BIỆT ACCOUNT-02

Hiện trạng:

```text
ACCOUNT-01 PASS
ACCOUNT-03 PASS
ACCOUNT-04 PASS
ACCOUNT-05 PASS
ACCOUNT-06 PASS
ACCOUNT-02 PARTIAL
```

Không cần viết lại test đã tốt nếu coverage đủ.

## 7.1 ACCOUNT-02 — bắt buộc closure

Phải có integration proof gần nhất có thể với actual auth stack:

```text
pending invitation target = B email
B chưa bind
→ canonical SSO/user provisioning/reuse equivalent trong integration fixture
→ authenticated B
→ POST Accept
→ same transaction/path binds B.UserId
→ B can access assigned instance
```

Cũng prove:

```text
anonymous POST Accept
→ unauthorized
→ no binding
```

và:

```text
authenticated C
C.email != target email
→ EmailMismatch / canonical denial
→ no binding
```

### Nếu true external Google SSO E2E không thể chạy trong local integration

Không được bỏ ACCOUNT-02.

Thay vào đó test tại application/API boundary với auth principal tương đương sau SSO provisioning, và report rõ:

```text
covered:
SSO-provisioned authenticated-account → Accept → bind

not externally exercised:
Google provider handshake itself
```

Không gọi test partial nếu application lifecycle từ authenticated provisioned account đến binding đã được chứng minh đầy đủ.

---

# 8. PHASE G — FULL BACKEND INSTANCE AUTHORIZATION MATRIX

Audit code là chưa đủ. Viết regression tests.

Fixture:

```text
Request R

HN instance → Operational Contact A
DN instance → Operational Contact B
Random VISITOR C
Registrant R
```

Test backend handler/API guard, không chỉ UI.

| Action | Registrant | A on HN | B on DN | Random C |
|---|---:|---:|---:|---:|
| View HN | current policy | ALLOW | DENY | DENY |
| Edit HN instance-local | current policy | ALLOW | DENY | DENY |
| Resubmit HN | current policy | ALLOW | DENY | DENY |
| Feedback HN | current policy | ALLOW | DENY | DENY |
| Amendment HN | current policy | ALLOW | DENY | DENY |
| Preview HN file | current policy | ALLOW | DENY | DENY |
| Download HN file | current policy | ALLOW | DENY | DENY |
| Transfer HN | current policy | ALLOW if current | DENY | DENY |
| Resend HN transfer | current policy | ALLOW if current | DENY | DENY |
| Cancel HN transfer | current policy | ALLOW if current | DENY | DENY |
| Mutate DN by A | current policy | DENY | — | DENY |
| Add/remove campus | current policy | DENY | DENY | DENY |
| Approve/Reject | role policy | DENY | DENY | DENY |

Không grant chỉ vì `VISITOR`.

---

# 9. PHASE H — AMENDMENT PERMISSION TEST CLOSURE

Confirmed business rule:

```text
current Operational Contact
→ may create Amendment for assigned APPROVED instance
```

Tests:

```text
AMEND-CONTACT-01
A current HN contact, HN APPROVED
→ create HN Amendment allowed

AMEND-CONTACT-02
A → DN Amendment denied

AMEND-CONTACT-03
random VISITOR denied

AMEND-CONTACT-04
registration 72h does NOT block Amendment

AMEND-CONTACT-05
canonical Amendment action cutoff still applies

AMEND-CONTACT-06
HN Amendment does not mutate sibling instance

AMEND-CONTACT-07
after contact transfer A→B accepted:
A denied
B allowed
```

Nếu current handler is request-wide unexpectedly and cannot target instance without a model change:

```text
mark BLOCKED
show exact evidence
continue other phases
```

Chỉ hỏi user sau khi hoàn thành independent work.

---

# 10. PHASE I — FEEDBACK / RESPONSE TEST CLOSURE

Confirmed:

```text
assigned current Operational Contact
→ may feedback/respond for assigned instance
```

Tests:

```text
FEEDBACK-CONTACT-01 A → HN allowed
FEEDBACK-CONTACT-02 A → DN denied
FEEDBACK-CONTACT-03 random C → HN denied
FEEDBACK-CONTACT-04 feedback remains HN-scoped
FEEDBACK-CONTACT-05 after transfer A→B:
                     A denied, B allowed
```

Nếu storage/handler thực sự request-wide và write lan sibling:

```text
mark BLOCKED with evidence
continue independent phases
```

---

# 11. PHASE J — FILE PREVIEW / DOWNLOAD AUTHORIZATION CLOSURE

Confirmed:

```text
current Operational Contact
→ may access files owned by assigned instance
```

Authorization must resolve:

```text
fileId
→ owning business object
→ visitInstanceId
→ actor relation
```

Không:

```text
role == VISITOR
```

Không:

```text
knows fileId → allowed
```

Tests:

```text
FILE-CONTACT-01 A preview HN file → allowed
FILE-CONTACT-02 A download HN file → allowed
FILE-CONTACT-03 A preview DN file → denied
FILE-CONTACT-04 A download DN file → denied
FILE-CONTACT-05 random C → denied
FILE-CONTACT-06 guessed/direct fileId cannot bypass
FILE-CONTACT-07 after transfer A→B:
                A loses contact-derived file access
                B gains it
```

Nếu có file category thật sự request-wide/shared và ownership không map được instance:

```text
mark only that category BLOCKED
continue testing all other file categories
```

---

# 12. PHASE K — TRANSFER / RESEND / CANCEL / RIGHTS HANDOVER

Confirmed.

## 12.1 Initiate

A = current HN contact.

```text
A may initiate transfer to B
```

Sibling/random visitor denied.

## 12.2 Pending state

While B pending:

```text
OperationalContactUserId = A
A retains instance rights
B has no contact-derived instance rights
```

B pending identity must not be mixed into A current identity.

## 12.3 Resend

A may resend pending transfer.

Preserve canonical:

```text
cooldown
max resend
token version
expiry
```

Unauthorized actors denied.

## 12.4 Cancel

A may cancel:

```text
B pending → CANCELLED
A remains current
```

## 12.5 Accept

B authenticates with matching eligible account and accepts:

```text
B → OperationalContactUserId
B gains instance rights
A loses current-contact-derived rights
```

No sibling effects.

## 12.6 Decline / Expiry

```text
A remains current
B gains nothing
```

## 12.7 Tests

At minimum:

```text
TRANSFER-AUTH-01 current A initiate allowed
TRANSFER-AUTH-02 sibling contact denied
TRANSFER-AUTH-03 random VISITOR denied
TRANSFER-AUTH-04 pending B has no instance rights
TRANSFER-AUTH-05 A retains rights while B pending
TRANSFER-AUTH-06 A resend allowed
TRANSFER-AUTH-07 unauthorized resend denied
TRANSFER-AUTH-08 A cancel allowed
TRANSFER-AUTH-09 unauthorized cancel denied
TRANSFER-AUTH-10 Accept hands rights A → B
TRANSFER-AUTH-11 decline preserves A
TRANSFER-AUTH-12 expiry preserves A
```

---

# 13. PHASE M — COMPLETE RECOVERY REGRESSION GAPS

Existing recovery architecture should not be redesigned unless a test finds a defect.

Ensure explicit tests for all:

## 13.1 Repeated Reject

```text
Reject #1 → SENT
Resubmit
Reject #2 → initial delivery fails
```

Expected:

```text
Reject #2 independently recoverable
Reject #1 SENT cannot suppress #2
```

After #2 succeeds:

```text
later sweep → no duplicate
```

## 13.2 OUTCOME_UNKNOWN

Examples:

```text
SMTP_SEND_FAILED / RESEND_SEND_FAILED where provider acceptance is uncertain
stale QUEUED + NULL error from crash window
```

Expected:

```text
NO automatic retry
```

## 13.3 Proven pre-outbound failure

Examples:

```text
SMTP_DISABLED
SMTP_MISCONFIGURED
RESEND_MISCONFIGURED
RESEND_CREDENTIAL_ERROR
render/config failure before outbound
```

Expected:

```text
eligible for controlled automatic retry
```

## 13.4 EXHAUSTION-01 — currently missing explicit test

Prove:

```text
attempts reach cap 5
→ no sixth auto send
→ terminal/loud observable condition/log exists
→ runbook can locate the event
```

Do not silently drop.

## 13.5 CONCURRENCY-01 — currently missing explicit test

Prove MySQL `GET_LOCK` behavior at integration level if feasible:

```text
worker A and worker B
attempt same business event concurrently
→ only one owns send claim / one outbound attempt
```

If deterministic concurrent outbound cannot be exercised with current fixture:

```text
test lock acquisition behavior directly against MySQL
+
test sender requires acquired lock
```

Do not merely assert source code contains `GET_LOCK`.

## 13.6 Expiry recovery

```text
identity change expires
DB state = EXPIRED committed
notification fails
```

Expected:

```text
still EXPIRED
token invalid
safe retry only according to outcome classification
```

---

# 14. VERIFY RUNBOOK AGAINST FINAL CODE

Existing:

```text
docs/Ver2Carnh/configEmail/EMAIL_NOTIFICATION_RECOVERY_RUNBOOK.md
```

Verify accuracy after all test/fix changes.

It must contain current truth for:

```text
SENT
PROVEN_NOT_DISPATCHED
CONFIG/RENDER PRE-OUTBOUND
OUTCOME_UNKNOWN
RETRY_EXHAUSTED
```

And explicit:

```text
OUTCOME_UNKNOWN
→ NEVER blind retry
```

Verify:
- Reject lookup uses exact rejection event/audit id.
- Expiry lookup uses identityChangeId.
- cap = 5.
- actual backoff values match code.
- queries refer to existing columns.
- no instruction to replay Reject mutation.
- no instruction to revert EXPIRED.

Update doc only if stale.

---

# 15. POST-COMMIT REGRESSION

Explicitly prove:

## Reject

```text
Reject transaction commits
→ notification later fails
→ API/business result remains Reject success
→ campus remains REJECTED
→ notification failure is separate
```

## Expiry

```text
expiry commits
→ notification later fails
→ state remains EXPIRED
→ token remains invalid
```

No email before business commit.

---

# 16. FULL REGRESSION — PRESERVE ALL PREVIOUS RULES

Before final report, audit no regression to:

```text
Contact Edit separation
same-email metadata-only
changed-email confirmation/transfer
A current while B pending
contactFullName
Create 72h
PRE-APPROVAL Edit 72h
Resubmit 72h
Amendment no registration 72h
passive <72h no auto action
campus max source-of-truth
controlled campus select
single Edit toast
single Resubmit toast
Reject per-campus email
Expiry email
exact-event recovery
OUTCOME_UNKNOWN
```

---

# 17. FULL GATES — CHẠY TẤT CẢ, KHÔNG DỪNG SAU MỘT SUITE

Run:

```text
dotnet build

backend unit tests

architecture tests

VisitRequests integration tests

Emails integration tests

frontend typecheck

frontend unit tests

frontend build
```

Nếu project có solution-wide integration suite canonical khác, chạy thêm nếu phù hợp với baseline đã dùng.

## Failure policy

Nếu test fail:

```text
1. xác định failure do WIP mới hay pre-existing
2. sửa nếu do WIP
3. rerun affected suite
4. rerun full relevant gate
```

Không gọi failure là "pre-existing" chỉ dựa vào ký ức/report cũ nếu dễ verify lại.

Nếu cần baseline proof:
- preserve WIP safely;
- verify against clean HEAD;
- restore exactly;
- confirm stash/WIP count/content.

Không phá 11 stashes hiện có.

---

# 18. FINAL DIFF AUDIT — BẮT BUỘC

Trước final report:

```text
git status --short
git diff --stat
git diff
```

Audit tất cả changed/untracked files liên quan.

Check:

```text
- no debug code
- no temporary bypass
- no TODO pretending completed
- no disabled authorization
- no hardcoded test user
- no accidental schema file
- no duplicate endpoint
- no legacy Resubmit accidentally used by Operational Contact
- no untranslated new UI strings
- no duplicate toast
- no unsafe file authorization
- no auto-retry OUTCOME_UNKNOWN
```

Search repository for relevant leftovers:

```text
request-wide resubmit references
role == VISITOR authorization shortcuts
OperationalContactUserId comparisons
ProfileDifference
ContactProfileSyncPrompt
OUTCOME_UNKNOWN
GET_LOCK
retry cap
```

Do not mechanically delete code; inspect meaning.

---

# 19. TRUE BLOCKER POLICY — KHÔNG NGẮT CẢ LƯỢT

Only genuine unresolved business-model blockers qualify.

Examples:

```text
- Feedback storage is truly request-wide and instance scope is impossible without a new business rule.
- Amendment is structurally request-wide and cannot target instance.
- shared file ownership has no defined policy.
- new schema is genuinely required.
```

When found:

```text
BLOCKED-1:
exact files/functions/schema
current behavior
why requirement cannot be implemented safely
options
```

Then continue ALL independent work.

At the end:

- If every blocker can be resolved from existing canonical evidence, resolve it and finish.
- If user decision is truly required, final report should contain the smallest set of decision questions.

Do not stop early just because one blocker exists.

---

# 20. DO NOT DO

```text
- Do not ask "shall I continue?"
- Do not stop after PHASE D.
- Do not stop after tests for one feature.
- Do not commit.
- Do not reset/clean/discard WIP.
- Do not authorize by VISITOR role alone.
- Do not give Operational Contact whole-request ownership.
- Do not use request-wide Resubmit for contact instance action.
- Do not mutate sibling campuses.
- Do not create account from typed email.
- Do not add anonymous Accept.
- Do not invent local/password auth.
- Do not silently overwrite account profile.
- Do not sync email/org/jobTitle.
- Do not rewrite historical snapshots.
- Do not apply registration 72h to Amendment.
- Do not allow direct fileId bypass.
- Do not give pending B rights.
- Do not auto retry OUTCOME_UNKNOWN.
- Do not add recovery Admin UI/endpoint.
- Do not add schema without explicit business need/approval.
- Do not mark audit as a substitute for authorization tests.
- Do not report partial work as complete.
```

---

# 21. REQUIRED FINAL REPORT — CHỈ BÁO CÁO SAU KHI ĐÃ CHẠY HẾT

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP before/after:
Stashes before/after:
Nothing committed:
```

## 2. Resubmit frontend

```text
Components:
API client:
Endpoint:
Actor visibility:
Operational Contact flow:
Sibling denial:
72h:
row version:
toast:
FE tests:
```

## 3. Profile sync

```text
Detection:
Prompt:
Canonical endpoint:
Fields synced:
Fields excluded:
Authorization:
Keep profile:
Historical snapshots:
FE/backend tests:
```

## 4. Account lifecycle tests

Report:

```text
ACCOUNT-01
ACCOUNT-02
ACCOUNT-03
ACCOUNT-04
ACCOUNT-05
ACCOUNT-06
```

For ACCOUNT-02 explicitly state what portion of SSO is simulated vs externally exercised.

## 5. Authorization matrix

Return final tested matrix for:

```text
Registrant
Assigned HN Operational Contact
Sibling DN Operational Contact
Random VISITOR
```

## 6. Amendment

```text
own instance:
sibling:
random visitor:
72h:
canonical cutoff:
post-transfer rights:
```

## 7. Feedback

```text
own instance:
sibling:
random:
post-transfer:
scope:
```

## 8. Files

```text
ownership resolution:
preview:
download:
direct fileId:
sibling:
post-transfer:
shared-file blocker if any:
```

## 9. Transfer

```text
initiate:
pending A:
pending B:
resend:
cancel:
accept:
decline:
expiry:
rights handover:
```

## 10. Recovery

```text
repeated Reject:
safe retry:
OUTCOME_UNKNOWN:
EXHAUSTION-01:
CONCURRENCY-01:
expiry:
```

## 11. Runbook

```text
path:
verified:
updated:
```

## 12. Post-commit regression

```text
Reject:
Expiry:
```

## 13. Changed files

Every file + reason.

## 14. Test/gate results

```text
dotnet build
backend unit
architecture
VisitRequests integration
Emails integration
frontend typecheck
frontend unit
frontend build
```

Report exact pass/fail counts.

## 15. Final diff audit

State:
- no debug leftovers;
- no unsafe auth shortcut;
- no schema changes unless explicitly approved;
- no legacy endpoint misuse;
- no unlocalized UI;
- no accidental commit.

## 16. Remaining debt / constraints

Expected known constraint:

```text
Production authentication is SSO-only.
Invitee unable to authenticate with supported SSO cannot Accept.
```

Only list other debt if genuinely unresolved.

## 17. Final completion verdict

Exactly one:

```text
COMPLETE
```

if all Definition of Done items are satisfied.

Or:

```text
COMPLETE EXCEPT BLOCKED BUSINESS DECISIONS
```

with exact blockers.

Do not use "mostly done", "can continue next", or "say the word".

---

# 22. DEFINITION OF DONE — ONE-SHOT CLOSURE

## Resubmit

- [ ] New per-instance Resubmit frontend API client exists.
- [ ] Operational Contact can reach Resubmit in browser.
- [ ] Calls new instance endpoint.
- [ ] Never calls request-wide endpoint for contact action.
- [ ] Sibling/random actor denied.
- [ ] Target instance row version handled.
- [ ] 72h error surfaced.
- [ ] Exactly one success toast.
- [ ] FE tests pass.

## Profile Sync

- [ ] Prompt wired into real instance UI.
- [ ] Only assigned authenticated holder sees it.
- [ ] Difference detection canonical.
- [ ] Full name + phone only.
- [ ] Keep profile works.
- [ ] Update profile works through canonical self-profile path.
- [ ] No email/org/jobTitle changes.
- [ ] Historical snapshots unchanged.
- [ ] Difference disappears after sync.
- [ ] Tests pass.

## Account

- [ ] ACCOUNT-01 pass.
- [ ] ACCOUNT-02 lifecycle closure pass.
- [ ] ACCOUNT-03 pass.
- [ ] ACCOUNT-04 pass.
- [ ] ACCOUNT-05 pass.
- [ ] ACCOUNT-06 pass.

## Authorization

- [ ] View instance matrix tested.
- [ ] Edit instance matrix tested.
- [ ] Resubmit matrix tested.
- [ ] Amendment matrix tested.
- [ ] Feedback matrix tested.
- [ ] File preview/download matrix tested.
- [ ] Transfer matrix tested.
- [ ] Resend matrix tested.
- [ ] Cancel matrix tested.
- [ ] Random VISITOR gets no rights from role alone.
- [ ] Sibling contact isolation proven.

## Amendment

- [ ] Assigned contact allowed.
- [ ] Sibling denied.
- [ ] Random denied.
- [ ] Registration 72h excluded.
- [ ] Canonical cutoff preserved.
- [ ] Sibling data unchanged.
- [ ] Transfer rights handover tested.

## Feedback

- [ ] Own instance allowed.
- [ ] Sibling denied.
- [ ] Random denied.
- [ ] Instance scope proven.
- [ ] Transfer handover tested.

## Files

- [ ] Own instance preview allowed.
- [ ] Own instance download allowed.
- [ ] Sibling denied.
- [ ] Random denied.
- [ ] guessed fileId cannot bypass.
- [ ] Transfer handover tested.

## Transfer

- [ ] Current A initiate allowed.
- [ ] B pending has no rights.
- [ ] A retains rights while pending.
- [ ] Resend authorization tested.
- [ ] Cancel authorization tested.
- [ ] Accept transfers rights A→B.
- [ ] Decline preserves A.
- [ ] Expiry preserves A.

## Recovery

- [ ] Repeated Reject test passes.
- [ ] Old Reject SENT cannot suppress later Reject.
- [ ] Safe pre-outbound retry tested.
- [ ] OUTCOME_UNKNOWN no-auto-retry tested.
- [ ] EXHAUSTION-01 tested.
- [ ] CONCURRENCY-01 tested.
- [ ] Expiry recovery tested.
- [ ] Runbook matches implementation.
- [ ] Post-commit semantics tested.

## Regression

- [ ] Create 72h preserved.
- [ ] PRE-APPROVAL Edit 72h preserved.
- [ ] Resubmit 72h preserved.
- [ ] Amendment registration-72h exclusion preserved.
- [ ] Contact metadata/identity split preserved.
- [ ] A/B transfer semantics preserved.
- [ ] campus max/select preserved.
- [ ] toast fixes preserved.
- [ ] email recovery safety preserved.

## Delivery

- [ ] All required gates run.
- [ ] Final diff audited.
- [ ] WIP preserved.
- [ ] Stashes untouched.
- [ ] Nothing committed unless explicitly requested.
- [ ] Final report returned only after the entire continuous pass is complete.
