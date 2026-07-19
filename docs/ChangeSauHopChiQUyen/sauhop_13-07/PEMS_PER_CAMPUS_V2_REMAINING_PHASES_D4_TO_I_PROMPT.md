# PEMS Per-Campus Form v2 — Prompt triển khai toàn bộ phần còn lại từ `7ff70224`

> Đây là prompt bàn giao tự chứa cho AI Agent/developer tiếp theo. Hãy đọc toàn bộ tài liệu này, kiểm tra repository thật rồi triển khai liên tục. Không cần đọc lại hội thoại cũ.
>
> Trạng thái chương trình: **IN PROGRESS**. Không được đổi `FINAL_IMPLEMENTATION_REPORT.md` thành `FINAL` cho tới khi đạt toàn bộ Definition of Done ở cuối prompt.

---

# 1. Vai trò và chế độ thực thi

Bạn là **Senior Software Architect + Senior Full-stack Engineer + Database Engineer** chịu trách nhiệm hoàn thành chương trình **PEMS Per-Campus Form v2** trên repository `quangthoai04/PEMS`.

Đây là terminal program task:

1. Không dừng sau mỗi file, handler, test hoặc commit để hỏi “có tiếp tục không”.
2. Khi một functional slice đã xanh, tự chuyển sang slice tiếp theo theo roadmap trong prompt.
3. Chỉ dừng khi:
   - thiếu business decision thật sự chưa được tài liệu khóa;
   - cần mutation/destructive action trên database thật;
   - gặp conflict không thể giải quyết an toàn;
   - platform hard-limit buộc phải checkpoint.
4. Build/test failure thông thường không phải blocker. Phải điều tra, sửa và chạy lại.
5. Không viết hàng loạt production code rồi để chưa build/test.
6. Không làm lại phần đã hoàn thành nếu code/schema/test tại HEAD chứng minh behavior đã tồn tại.
7. Không tự push, merge, rebase shared history hoặc mở PR nếu người dùng chưa yêu cầu.
8. Chỉ báo cáo cuối session khi slice đang thực hiện đã xanh và commit, hoặc có hard blocker/hard-limit thật sự.

Mục tiêu cuối:

- một `visit_requests` cha;
- nhiều campus instance độc lập;
- mỗi campus có lịch, form detail, member set, operational contact và lifecycle riêng;
- registrant và primary contact quản lý request theo exact relation;
- primary-contact claim/transfer không cấp quyền trước explicit verification;
- chỉnh sửa sau duyệt dùng safe edit/amendment;
- mọi mutation có concurrency, revision, audit, notification và test;
- list/search/report/export/email không dùng campus nhỏ nhất làm dữ liệu nghiệp vụ cho request mixed;
- frontend v2 hoàn chỉnh;
- rollout/cutover có flag, metric và rollback;
- chuẩn bị contract cleanup nhưng không tự chạy destructive migration trên DB thật.

---

# 2. Repository, HEAD và checkpoint bắt buộc

## 2.1. Trạng thái được báo cáo gần nhất

- Repository: `quangthoai04/PEMS`.
- Branch triển khai: `Cảnh-Iter1` trên remote; nếu local dùng tên không dấu thì phải xác minh tracking branch, không tự tạo nhánh khác.
- Expected HEAD: `7ff70224`.
- Commit: `feat(delegations): add v2 primary-contact claim, cancel-3A and claim expiry job`.
- Commit gồm 29 files, `+2315/-34`, production code + SQL + tests + report/progress trong một functional slice.
- Author/committer bắt buộc: `Tcanh12 <canhnvthe186121@fpt.edu.vn>`.
- Không có AI attribution.
- Unit: `474/474`.
- Architecture: `14/14`.
- Full IntegrationTests: `352/352` trên `pems_it_regression` dựng mới từ master đã có ENUM identity token.
- Claim tests: `7/7`.
- v2 test group: `46/46`.
- `appsettings.Testing.json` đã restore về `pems_test`.
- `v2_requests`, `identity_changes`, claim tokens đều `0` ở các DB đã kiểm tra.
- Hai feature flags v2 vẫn mặc định `OFF`.
- Working tree được báo cáo chỉ còn hai plan/handoff docs untracked có chủ đích.

## 2.2. Preflight trước khi sửa code

Chạy và ghi nhận:

```bash
git status --short
git branch --show-current
git log --oneline --decorate -20
git show -s --format=fuller 7ff70224
git config --local --get user.name
git config --local --get user.email
```

Yêu cầu:

- HEAD phải chứa `7ff70224`; nếu đã có commit mới thì đọc diff và report trước khi quyết định.
- Không reset/revert thay đổi của người dùng.
- Không add/commit hai plan/handoff docs đang untracked nếu người dùng chưa yêu cầu.
- Nếu local author chưa đúng, chỉ được set **repository-local**, tuyệt đối không sửa global config:

```bash
git config user.name "Tcanh12"
git config user.email "canhnvthe186121@fpt.edu.vn"
```

## 2.3. Tài liệu/code phải đọc

Dùng `rg --files` để tìm đúng path hiện tại:

- `PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md` — được phép đọc, không tự add nếu đang untracked.
- `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT.md` nếu có.
- `FINAL_IMPLEMENTATION_REPORT.md`, đặc biệt §4 và §6.
- `IMPLEMENTATION_PROGRESS.md`.
- `PR3_PRE_PR4_AUDIT_MAP.md`.
- `PR3_TEST_REPORT.md`.
- `docs/database/scripts/percampus_v2_migration/`.
- canonical business rules, permission rules/matrix và use-case docs.
- source/tests thật liên quan identity, amendment, list/search/report/export/email và frontend.

Thứ tự nguồn sự thật khi mâu thuẫn:

1. Code/schema/test thực tế tại HEAD.
2. Các quyết định đã khóa trong prompt này.
3. Report/progress mới nhất.
4. Master plan.
5. Tài liệu legacy.

Phải cập nhật report/progress để phản ánh đúng:

- D-1 INITIAL_CLAIM: hoàn tất.
- D-2 cancel-3A: hoàn tất.
- D-3 expiry/redaction cho INITIAL_CLAIM: hoàn tất.
- D-4 TRANSFER 24h: **chưa hoàn tất**.
- Vì vậy Phase D tổng thể vẫn `IN PROGRESS` cho tới khi D-4 xanh.
- `OTP_FALLBACK`: deferred/out-of-scope có chủ đích vì Product chưa bật non-Google; không tự triển khai lại scope này.

---

# 3. Phần đã hoàn thành — không được redo

## 3.1. Database/persistence/read foundation

- Additive schema/backfill/verify/rollback package đã có.
- `visit_instance_form_details`, `visit_instance_guest_members`, revision/amendment/identity tables đã tồn tại.
- Composite FK chống cross-request member links.
- v2 detail là source of truth; global request fields chỉ compatibility projection.
- v1 dual-read giữ compatibility; request v2 mixed không được v1-edit/fallback làm mất dữ liệu.
- Read-detail consumers và bảy command/export consumers đã được migrate/test.

## 3.2. Create/edit/resubmit v2

- Authenticated create v2 và public OTP verify create v2.
- Structural validator, write/read flag gates, idempotency, fingerprint v2.
- N campus detail/member snapshots độc lập, baseline revisions, audit và post-commit notifier.
- Pending edit v2 có request/instance row version, add/remove campus, copy-on-write và sibling no-op.
- Resubmit v2 chỉ all-REJECTED, giữ nguyên campus set + `visitInstanceId`, snapshot quyết định cũ, reroute leader và concurrent one-winner.

## 3.3. Identity/cancel/job đã xong tại `7ff70224`

- INITIAL_CLAIM tạo token single-use chỉ lưu hash.
- Email invitation gửi sau commit từ cả hai create flow; first-create-only, best-effort và resend phục hồi được.
- Anonymous generic email-action handler từ chối claim context.
- Public masked landing GET không mutation.
- Authenticated explicit POST accept/decline với exact Google email.
- Accept transaction lock claim, link `visitor_user_id`, set `ACTIVE`, burn token, event/audit masked; không reset quyết định campus.
- Registrant resend/replace pending contact, cap resend 5 và restart 72h.
- Cancel-3A cho registrant khi INITIAL_CLAIM còn `PENDING_CONFIRMATION`, vẫn giữ reason/24h/lifecycle guards.
- Cancel đóng pending claim/token trong cùng transaction.
- Hosted job expire INITIAL_CLAIM 72h và redact failure PII sau 90 ngày, batch/idempotent, giữ minimal audit.

Không sửa lại các behavior này trừ khi test D-4/E–I chứng minh bug thật.

---

# 4. Invariant nghiệp vụ và kỹ thuật không được phá

1. **HO chỉ monitor/read-only**; không có centralized multi-campus approval.
2. **Staff Leader duyệt theo từng campus** thuộc `primary_campus_id`.
3. Admin không có business action cho visit.
4. Exact relation cấp quyền trên đúng request/instance, không nâng role toàn cục.
5. Primary contact là request-level account relation; operational contact chỉ là per-campus snapshot, không tự có account/quyền.
6. v2 mới lấy `visit_instance_form_details` làm source of truth; không fallback global nếu v2 detail thiếu.
7. Với request mixed, không dùng compatibility projection/campus nhỏ nhất làm nội dung nghiệp vụ.
8. `has_mixed_campus_details` do backend tính từ normalized copyable form/member content; campus/time khác riêng không làm mixed.
9. Thời lượng tối thiểu 30 phút phải đồng nhất FE/BE/DB; `29m59s` fail, `30m00s` pass.
10. Member v2 độc lập theo campus; legacy shared member phải copy-on-write.
11. Scope/authorization phải thực hiện trước projection/search/pagination; không trả all-campus rồi filter ở client.
12. `allowedActions` chỉ phục vụ UX; mọi command phải re-authorize từ DB.
13. Mọi mutation mới phải có audit header + field/event rows trong cùng transaction.
14. Không log raw OTP/token/session token/full pending snapshot/full PII.
15. Token chỉ lưu hash, có expiry, one-time use, rotate/supersede khi resend.
16. Notification/email external chỉ sau commit; retry/idempotency không tạo duplicate business mutation.
17. Không hard-delete/lock account cũ sau transfer.
18. Không reset approval/campus decision vì identity change hoặc amendment pending.
19. Hai v2 flags vẫn default OFF; không tự bật production/appsettings.
20. Không mutation `pems_db`, `pems_test`, `pems_pr3_test`; test integration chỉ dùng disposable DB fresh như `pems_it_regression`.

Mười global compatibility fields phải được audit ở Phase F/I:

```text
delegation_name
visit_type
visit_type_other
purpose
working_content
working_language
transportation_note
media_consent_status
media_consent_note
note_to_fptu
```

`contact_person_*` là authoritative request-level primary-contact snapshot, không phải compatibility projection.

---

# 5. Roadmap bắt buộc còn lại

Thứ tự triển khai:

1. **D-4 — Primary-contact TRANSFER 24h**.
2. **E — Safe edit + amendment + history/audit completeness**.
3. **F — List/search/dashboard/report/export/email v2-safe**.
4. **G — Frontend v2 hoàn chỉnh**.
5. **H — E2E/final verification/rollout drill**.
6. **I — Contract-drop preparation trên disposable DB, không chạy destructive thật**.

Không nhảy sang Phase E khi TRANSFER chưa xanh. Không đánh dấu toàn chương trình FINAL sau một phase trung gian.

---

# 6. D-4 — Primary-contact TRANSFER 24h

## 6.1. Mục tiêu

Cho registrant hoặc primary contact đang `ACTIVE` đề xuất chuyển quyền primary contact sang email mới. Owner cũ giữ nguyên quyền cho tới khi người mới đăng nhập đúng Google email và bấm explicit accept. Apply thành công mới swap relation.

TRANSFER **không phụ thuộc OTP_FALLBACK**. TRANSFER mặc định dùng Google SSO exact-email như INITIAL_CLAIM.

## 6.2. Rà contract hiện có trước khi thêm route

Ưu tiên mở rộng convention/endpoint identity đã có ở HEAD; không tạo hai bộ API trùng chức năng. Contract mục tiêu tối thiểu tương đương:

```text
POST /api/v2/visit-requests/{id}/identity-changes
GET  /api/v2/visit-requests/{id}/identity-changes/active
POST /api/v2/visit-requests/{id}/identity-changes/{changeId}/resend
POST /api/v2/visit-requests/{id}/identity-changes/{changeId}/cancel
GET  /api/public/visit-contact-transfers/{opaqueToken}
POST /api/v2/visit-contact-transfers/{opaqueToken}/accept
POST /api/v2/visit-contact-transfers/{opaqueToken}/decline
```

Nếu INITIAL_CLAIM đang dùng route chung `visit-contact-claims`, có thể dùng landing/accept handler chung theo `change_kind`; nhưng response/error/authorization phải rõ và không làm đổi contract đã ship.

## 6.3. Initiate TRANSFER

Trong transaction:

1. Load + lock request và pending identity guard.
2. Re-check actor là registrant đúng request hoặc current primary contact `ACTIVE`.
3. Re-check request v2, current `visitor_user_id`, current owner ACTIVE và lifecycle.
4. Chặn từ `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`, `CANCELLED` trở đi.
5. Chặn self-service khi earliest planned start còn `<24h`.
6. Normalize email bằng `Trim()` + lowercase invariant; không bỏ dot hoặc `+tag` kiểu Gmail.
7. Reject email rỗng, không hợp lệ, không đổi so với current email, internal/non-VISITOR account conflict hoặc target không được phép.
8. Chụp `old_user_id`, `old_email_normalized`, `new_email_normalized`, `new_email_masked`, pending primary-contact snapshot, `expected_request_row_version`, `requested_by`, `reason`.
9. Tạo `change_kind=TRANSFER`, `status=PENDING`, `confirmation_method=GOOGLE_SSO`, expiry đúng 24 giờ.
10. Enforce chỉ một pending identity change/request/relation bằng DB guard + transaction lock; map unique race sang stable `409`.
11. Tạo opaque token bằng CSPRNG, chỉ lưu hash/context/target; không lưu/log raw token.
12. Ghi event/audit `PRIMARY_CONTACT_TRANSFER_REQUESTED` với email masked.
13. Commit rồi mới gửi email/notification; failure external không rollback business transaction và phải retry/resend được.

## 6.4. Landing, accept và decline

- GET public chỉ trả request label tối thiểu, masked invited email, expiry/status và yêu cầu Google login; tuyệt đối không mutation/account enumeration.
- Mở link hoặc login Google thành công **không** tự accept.
- Accept/decline là POST authenticated, có CSRF theo cơ chế hiện hữu và rate limit hợp lý.
- Authenticated account phải là VISITOR ACTIVE và normalized verified Google email phải bằng `new_email_normalized`.
- Sai/missing email trả stable conflict/forbidden không lộ full target email.

Accept transaction:

1. Lock identity change `FOR UPDATE`, request và token row.
2. Re-check token hash/context/target, `PENDING`, chưa expire/supersede/consume.
3. Re-check `expected_request_row_version` theo contract concurrency hiện hữu.
4. Re-check `visit_requests.visitor_user_id == old_user_id`; owner không được đổi ngoài workflow trong lúc pending.
5. Re-check lifecycle/cutoff và exact Google email.
6. Link/provision đúng VISITOR identity theo primitive hiện hữu; không repurpose internal account.
7. Swap duy nhất `visitor_user_id` và primary-contact snapshot sang pending snapshot mới.
8. Giữ `primary_contact_access_status=ACTIVE`, set verified timestamp và bump request row version.
9. Mark change `APPLIED`, set `new_user_id/applied_at`, burn token và invalidate sibling token.
10. Ghi append-only event + audit `PRIMARY_CONTACT_TRANSFER_APPLIED`, masked old/new email và old/new user relation.
11. Không thay đổi request/campus status, decision, coordinator, host, schedule hoặc approval revision.
12. Commit rồi notify old owner, new owner và actor vận hành phù hợp.

Retry accept sau commit phải idempotently trả kết quả đã apply, không swap lần hai. Concurrent accept chỉ một winner; loser nhận stable conflict/idempotent response theo convention đã chốt, không partial write.

Decline:

- Chỉ invited exact account mới decline bằng opaque token.
- Mark `DECLINED`, consume token, append event/audit; owner cũ vẫn ACTIVE.
- Không mutation request relation/approval.

## 6.5. Resend, cancel, supersede và expiry

- Resend chỉ khi TRANSFER còn pending và actor là registrant/current ACTIVE owner.
- Vô hiệu token cũ trước khi phát token mới.
- Giữ/cap resend theo policy hiện hữu (mặc định cap 5 nếu dùng chung claim policy).
- Restart expiry đúng 24h, không 72h.
- Cancel transfer giữ owner cũ ACTIVE, burn token và ghi event/audit.
- Replace bằng target mới phải supersede pending transfer cũ trước khi tạo row/token mới; không để hai pending.
- Hosted job phải phân biệt:
  - `INITIAL_CLAIM`: 72h;
  - `TRANSFER`: 24h.
- Transfer expired không đổi owner cũ.
- Failure states `EXPIRED/DECLINED/CANCELLED/SUPERSEDED` redact sau 90 ngày; `APPLIED` theo audit retention chung.
- Job batch/idempotent, có metric/event; rerun không tạo duplicate event.

## 6.6. Interaction với cancel-3A và authorization cache

- Pending TRANSFER không phải INITIAL_CLAIM pending.
- Trong pending TRANSFER, `primary_contact_access_status` vẫn `ACTIVE`, owner cũ còn quyền.
- Registrant không được hưởng ngoại lệ cancel-3A chỉ vì transfer đang pending.
- Sau apply, owner cũ mất exact request relation ngay nhưng account vẫn ACTIVE.
- Mọi handler phải query relation hiện tại từ DB; không cache ownership dài hạn trong JWT.
- Frontend sau này phải clear edit cache/state của actor vừa mất relation.

## 6.7. Error codes tối thiểu

Tái sử dụng constants hiện hữu khi phù hợp; không tạo chuỗi tùy tiện. Ít nhất phải ổn định cho:

```text
IDENTITY_CHANGE_ALREADY_PENDING
IDENTITY_CHANGE_EMAIL_UNCHANGED
IDENTITY_CHANGE_TARGET_NOT_ALLOWED
IDENTITY_CHANGE_EXPIRED
IDENTITY_CHANGE_CONFLICT
IDENTITY_GOOGLE_EMAIL_MISMATCH
IDENTITY_CHANGE_SUPERSEDED
CONTACT_EMAIL_INTERNAL_ACCOUNT_CONFLICT
CONTACT_ACCOUNT_NOT_ACTIVE
```

## 6.8. Test D-4 bắt buộc

- Registrant initiate thành công; ACTIVE owner initiate thành công.
- Unrelated/Staff Leader/Host/HO/Admin forbidden.
- Same email, invalid/internal/inactive target rejected.
- Cutoff `<24h`, DURING/AFTER/CLOSED/CANCELLED blocked.
- Unique pending guard dưới sequential và concurrent create.
- GET masked/no mutation/no enumeration.
- Wrong/missing Google account không cấp quyền.
- Before accept: owner cũ có quyền, target chưa có quyền.
- After accept: new owner có quyền, old owner mất relation, old account vẫn ACTIVE.
- Campus decision/status/host/schedule/revision không đổi.
- Accept replay idempotent; concurrent apply one winner; no partial write.
- Decline/cancel/expire giữ owner cũ.
- Resend supersede token cũ, cap/cooldown và expiry 24h.
- Job boundary 23:59:59/24h; redaction 90d; rerun idempotent.
- Transfer pending không mở cancel-3A cho registrant.
- Audit/event/notification complete, masked, no duplicate on retry.
- Full Unit/Architecture/Integration regression xanh.

Chỉ sau các test trên mới cập nhật Phase D = completed. `OTP_FALLBACK` vẫn ghi deferred, feature disabled và không cản Phase D nếu Product đã chốt out-of-scope.

---

# 7. Phase E — Safe edit, amendment, history và audit completeness

## 7.1. Audit/revision foundation trước business mutation

Rà code thật xem đã có abstraction tương đương `IVisitAuditWriter` chưa. Nếu chưa, tạo abstraction dùng chung để mọi mutation ghi:

- audit header;
- stable field path old/new hoặc summarized/masked value;
- `visit_request_id`, optional `visit_instance_id`;
- source type/id;
- actor/relation;
- reason;
- correlation id;
- timestamp từ application clock.

Không dump full member list/full PII/pending JSON vào generic audit. Member audit dùng member id + `ADDED/UPDATED/REMOVED` + field path. Audit/history phải nằm cùng transaction business mutation.

## 7.2. Backend là nguồn phân loại field duy nhất

Implement/test classifier centralized; frontend chỉ hiển thị dự đoán.

| Loại | Field/action | Behavior |
|---|---|---|
| Safe/correction | registrant full name/org/job/phone; primary-contact name/org/phone; transportation note; note to FPTU; media note/consent | Apply ngay, revision + field audit + notify |
| Privacy urgent | `media_consent_status -> DECLINED` | Apply ngay kể cả `<24h`, HIGH/URGENT notify |
| Approval-sensitive | delegation name, visit type/other, purpose, working content, guest/support list, working language, operational contact phối hợp, logistics-impacting requirement | Tạo amendment; active snapshot giữ nguyên |
| Structural | add/remove/change campus, schedule | Route theo lifecycle: pending-edit, cancel/add hoặc amendment; không silent overwrite approved data |

Primary-contact email tuyệt đối không đi qua safe edit; phải dùng identity workflow. Operational contact email vẫn là per-campus snapshot và có thể là approval-sensitive.

## 7.3. API contract mục tiêu

Ưu tiên convention thực tế, nhưng behavior tối thiểu tương đương:

```text
PATCH /api/v2/visit-requests/{id}/safe-details
POST  /api/v2/visit-requests/{id}/instances/{instanceId}/amendments
GET   /api/v2/visit-requests/{id}/instances/{instanceId}/amendments/active
POST  /api/v2/visit-requests/{id}/instances/{instanceId}/amendments/{amendmentId}/withdraw
POST  /api/v2/visit-instances/{instanceId}/amendments/{amendmentId}/approve
POST  /api/v2/visit-instances/{instanceId}/amendments/{amendmentId}/reject
GET   /api/v2/visit-requests/{id}/history
```

Nếu dùng một PATCH tự split safe/sensitive, response phải trả rõ:

```text
appliedChanges[]
amendmentsCreated[]
conflicts[]
requestRowVersion
instanceRowVersions
```

Client không được tưởng toàn payload đã apply.

## 7.4. Safe edit transaction

1. Lock request/detail liên quan.
2. Authorize registrant hoặc ACTIVE primary contact bằng exact relation.
3. Re-check lifecycle, cutoff, request/instance row versions.
4. Normalize/diff; reject field ngoài safe allowlist.
5. Privacy urgent media withdrawal được phép kể cả `<24h`; các safe field khác tuân cutoff đã khóa.
6. Ghi request/instance revision snapshot trước mutation.
7. Apply target-only; bump form/request/instance row versions.
8. Recompute mixed/fingerprint/compatibility projection khi field liên quan thay đổi.
9. Audit header + từng stable field diff trong cùng transaction.
10. Commit rồi notify Staff Leader/Host liên quan; urgent withdrawal dùng priority phù hợp.

Safe concurrent edit không được xóa/overwrite amendment pending. Campus A edit không đổi B.

## 7.5. Amendment submit

1. Lock request/instance/detail + active amendment guard.
2. Authorize registrant/ACTIVE contact.
3. Chặn từ `DURING_VISIT` trở đi và chặn self-service `<24h`.
4. Validate request/instance/form/approval base revisions và old values.
5. Classifier phải xác nhận patch chứa approval-sensitive/structural behavior hợp lệ.
6. Enforce một pending amendment/instance bằng DB unique guard + lock.
7. Lưu immutable proposal/change rows gồm stable field path, normalized old/new, reason và base revisions.
8. Không mutation active `visit_instance_form_details`, member links, schedule hoặc approved snapshot.
9. Ghi audit `VISIT_AMENDMENT_SUBMITTED` và notify current Staff Leader; Host/original reviewer chỉ nhận visibility/notification, không có approve permission.

## 7.6. Approve/reject/withdraw/expire

Approve transaction:

1. Lock amendment, request, instance, detail và member rows cần thiết.
2. Authorize **current Staff Leader đúng campus**, không chỉ original reviewer.
3. Re-check `PENDING_APPROVAL`, lifecycle, base `form_revision`, `approval_revision`, row versions và old values.
4. Snapshot current active state vào revision history.
5. Apply patch/member copy-on-write target-only.
6. Bump `form_revision`, `approval_revision`, request/instance/detail row versions.
7. Recompute mixed/fingerprint/compatibility projection.
8. Mark amendment approved và ghi `VISIT_INSTANCE_FORM_REVISION_APPLIED` + field audit.
9. Không reset instance/request approval status hay campus khác.
10. Đồng bộ/invalidate calendar/reminder và tạo logistics-impact signal cho Host xử lý; không tự thay business assignment ngoài rule hiện hữu.
11. Commit rồi notify requester/current leader/Host/actors liên quan.

Reject:

- Current Staff Leader đúng campus; reason bắt buộc.
- Mark rejected + event/audit; active snapshot/revisions không đổi.

Withdraw:

- Registrant/ACTIVE contact đúng request; chỉ pending.
- Mark withdrawn + event/audit; active snapshot không đổi.

Expire:

- Khi lifecycle chuyển `DURING_VISIT` hoặc job/cutoff phù hợp, pending amendment phải expire atomically/trước transition.
- Active snapshot giữ nguyên; job idempotent.

## 7.7. History projection

- Business history DTO đã mask và scope, không trả raw IP/UA/token/security metadata.
- Registrant/ACTIVE contact xem toàn request của mình.
- Staff Leader chỉ campus mình.
- Host/participant/department/student chỉ instance relation.
- HO read-only toàn bộ; Admin không có business mutation.
- Timeline phân biệt active revision, proposed amendment, decision và identity event; không trình bày proposal như đã apply.

## 7.8. Error codes tối thiểu

```text
AMENDMENT_ALREADY_PENDING
AMENDMENT_NOT_EDITABLE
AMENDMENT_BASE_REVISION_CONFLICT
AMENDMENT_APPROVER_SCOPE_FORBIDDEN
AMENDMENT_WINDOW_EXPIRED
VISIT_FORM_CONCURRENCY_CONFLICT
VISIT_INSTANCE_SCOPE_FORBIDDEN
```

## 7.9. Test Phase E bắt buộc

- Classifier table-driven cho mọi field/path, unknown field fail closed.
- Safe edit apply + revision + exact audit diff.
- Non-safe field bị reject hoặc route amendment rõ ràng.
- Urgent media decline apply `<24h`; reverse/other edits không lạm dụng ngoại lệ.
- Active snapshot bất biến trước approval.
- Một pending amendment/instance dưới race.
- Current leader đúng campus approve/reject; leader campus khác/HO/Admin/Host forbidden.
- Base revision/old-value conflict `409`, no partial write.
- Approve target-only; sibling campus unchanged; approval status không reset.
- Member amendment copy-on-write không phá minutes/feedback/OCR/gallery/partner links.
- Reject/withdraw/expire giữ active snapshot/revision cũ.
- Safe concurrent edit không xóa amendment.
- Calendar/reminder/logistics signals đúng và idempotent.
- History masking/scope/IDOR.
- Audit completeness: header + expected field/event rows cùng commit.
- Full backend regression xanh.

---

# 8. Phase F — List, search, dashboard, report, export và email v2-safe

## 8.1. Inventory bắt buộc

Đọc `PR3_PRE_PR4_AUDIT_MAP.md`, sau đó chạy repository-wide `rg` cho cả 10 global compatibility fields. Phân loại từng reference thành:

- request-level legitimate metadata;
- instance-context phải dùng target detail;
- aggregate phải group per-campus;
- compatibility-only write/projection;
- legacy/dead/test/docs.

Không kết thúc Phase F nếu còn reference production chưa phân loại. Tạo **zero-unclassified-reference report** trong report/progress của cùng functional commit.

## 8.2. Surfaces phải migrate/audit

- delegation/request lists;
- department/staff calendars;
- dashboards và progress;
- invitation/participant lists;
- eligible news instances;
- host candidates/related visitor details;
- HO, Staff Leader, Department reports;
- invoice;
- export/print;
- email preview/template/content;
- conflict labels và notifications.

## 8.3. Projection rules

- Parent request xuất hiện một lần.
- Request common data render một lần.
- Instance context chỉ dùng detail/member của instance được authorize.
- Aggregate response chứa per-campus sections hoặc safe summary; không lấy smallest campus làm đại diện.
- Mixed request hiển thị rõ `Khác nhau theo cơ sở`.
- v1 record giữ byte-compatible behavior khi requirement nói vậy.
- v2 missing detail trả stable `409 VISIT_FORM_DETAIL_MISSING`; không fallback global.
- Không trả all-campus payload rồi filter frontend.
- Query count bounded, tránh N+1; test/EXPLAIN critical query.

## 8.4. Search 5A scope-before-keyword

1. Xây `authorizedInstances` từ exact role/sub-role/relation/campus trước keyword.
2. Parent fields chỉ search trên request actor có quyền.
3. Per-campus fields chỉ join authorized instances.
4. Group request, tính match context từ authorized rows, rồi mới sort/page.
5. Hidden instance keyword không được ảnh hưởng hit/count/order/score/badge/context.
6. Parent trả safe match context như campus + field category; không trả raw hidden snippet/PII.
7. Không log raw keyword trong telemetry.

Scope:

- Registrant/ACTIVE contact: toàn bộ campus của own request.
- HO: toàn bộ, read-only.
- Staff Leader: campus `primary_campus_id`.
- Host/participant/department/student: instance có relation.
- Admin/unrelated: forbidden theo convention.

## 8.5. Export/print/email security

- Group per campus, sanitize HTML/text và spreadsheet formula injection.
- Recipient chỉ nhận content được authorize.
- Không gửi full old/new PII trong notification; link tới authorized detail/diff.
- Token/action context identity không đi qua generic handler.
- Retry email không duplicate business mutation.

## 8.6. Test Phase F bắt buộc

- v1 unchanged, v2 same/mixed/missing detail.
- Staff Leader/Host/Department/Student không thấy sibling campus.
- HO read-only all; Admin/unrelated forbidden.
- Hidden-keyword side-channel matrix cho hit/count/order/context.
- Request parent không duplicate vì multi-campus joins.
- Pagination sau grouping đúng total.
- Report totals và per-campus sections đúng.
- Invoice/export/print/email đúng target instance hoặc aggregate sections.
- Sanitization/XSS/formula injection.
- Query-count/EXPLAIN không N+1/full-scan ngoài ngưỡng chấp nhận.
- Repository-wide zero-unclassified global-field references.
- Full backend regression xanh.

---

# 9. Phase G — Frontend v2 hoàn chỉnh

## 9.1. API/types/form state

- TypeScript contracts khớp API v2/error codes/row versions/allowedActions.
- Request-level: registrant + primary contact.
- `campusVisits[]`: schedule, form detail, guest/support arrays, operational contact, requirements và stable client key.
- Dùng `useFieldArray`; không dùng array index làm React key.
- Copy/apply-all là deep copy một lần, không shared object reference.
- Accordion đóng không unregister/mất field; nested error badge focus/expand đúng block.
- Remove dirty campus có confirm; copy overwrite liệt kê campus bị ảnh hưởng.
- Excel import bắt buộc chọn campus đích, giới hạn 5MB/rows, sanitize/formula protection.
- Draft v2 lưu schema version/client keys/timestamp; v1->v2 duplicate snapshot; không overwrite draft v2 mới hơn.
- Default max 10 campus, 200 GUEST + 200 EXTERNAL_SUPPORT mỗi campus, cấu hình FE/BE đồng nhất.

## 9.2. Time UX

- Hiển thị timezone từng campus và duration.
- End trống có thể suggest start +30m; end đã dirty không tự đổi.
- FE validation hỗ trợ UX nhưng server vẫn authoritative.
- Map `VISIT_DURATION_TOO_SHORT` về đúng campus field.
- Test keyboard, `vi-VN`, mobile và timezone; không cộng cứng UTC+7.

## 9.3. Read-only/review

- Reuse component như `CampusVisitDetailCard` cho post-submit, Visitor, Staff Leader, HO, Host.
- Component chỉ render payload đã scope từ server, không tự suy quyền bằng role string.
- Request common data một lần; campus status/leader/host/revision/detail theo từng card.
- Mixed label rõ ràng.
- Legacy request-level `409 FORM_VERSION_UPGRADE_REQUIRED` route sang UI v2, không show raw technical error.
- Export/print/email preview cũng per-campus.

## 9.4. Identity UX

- Phân biệt rõ primary contact và operational contact.
- INITIAL_CLAIM: pending/active/expired, replace email, resend cooldown/cap, cancel invitation.
- TRANSFER: confirm ai mất quyền, target phải xác nhận, owner cũ giữ quyền tới apply, account cũ không bị xóa.
- Landing hiển thị masked email, yêu cầu đúng Google account và nút đổi account khi mismatch.
- Chỉ explicit POST `Đồng ý làm đầu mối` mới apply.
- Sau transfer apply, actor cũ phải clear edit state/query cache và rời edit page nếu mất relation.
- Không chỉ disable email input; phải có action identity riêng.

## 9.5. Safe edit/amendment/history UX

- Hiển thị dự đoán `Cập nhật ngay` vs `Gửi duyệt`, nhưng response server là kết luận cuối.
- Tách `Nội dung đang hiệu lực` và `Đề xuất thay đổi`.
- Staff Leader xem old/new field, requester, reason, base revision; reject bắt buộc reason.
- Visitor/Host/HO xem timeline theo scope và masked business DTO.
- Approve/reject/withdraw invalidate request/instance/calendar/logistics caches.
- Conflict row-version/base-revision có recovery/reload UX, không silent overwrite.

## 9.6. Accessibility/i18n/performance

- Đủ VI/EN cho label/status/dialog/error code mới.
- Keyboard/ARIA/focus trap/accordion/modal đúng.
- Responsive 390px.
- Cache reference data; không gọi N request catalog/host cho N campus block.
- Không render hidden campus data rồi CSS-hide.

## 9.7. Frontend test gate

Nếu chưa có, bổ sung Vitest + React Testing Library và `test:unit`; Playwright config/spec/script phải chạy được.

Test tối thiểu:

- Zod/RHF shape, deep copy, nested errors, dirty state.
- Draft v1->v2/reload/remove/copy/apply-all.
- Read-only same/mixed campus.
- Server error path đúng campus.
- Identity pending/active/expired/conflict; wrong Google account; explicit accept; transfer rights transition.
- Safe/amendment active-vs-proposed, approve/reject/withdraw, history.
- Search context không render hidden data.
- Accessibility keyboard/focus và 390px.

---

# 10. Phase H — E2E, final verification và rollout drill

## 10.1. Database safety

- Không mutation `pems_db`, `pems_test`, `pems_pr3_test`.
- Tạo disposable DB fresh từ master hiện hành, ví dụ `pems_it_regression`.
- Dùng trap/finally để restore `appsettings.Testing.json` về `pems_test` kể cả test fail.
- Test fresh import, upgrade snapshot v1, backfill rerun, verify counts/orphans/checksums, constraints, identity/amendment pending guards, expiry/redaction và rollback strategy.
- Không chạy DOWN destructive nếu có v2 write không thể reconstruct.
- Sau test verify disposable cleanup/row count và ba DB persistent không mutation.

## 10.2. Quality gates

```bash
dotnet build backend/PEMS.sln
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj

cd frontend/pems-react
npm ci
npm run lint
npm run test:unit
npm run build
npx playwright test
```

Đường dẫn/solution thật có thể khác; dùng `rg --files` để tìm. Không ghi pass nếu command chưa chạy. Baseline trước phần còn lại là Unit `474`, Architecture `14`, Integration `352`; discovered count có thể tăng nhưng không được giảm âm thầm.

## 10.3. E2E/regression matrix

- Public OTP create 2 campus same/mixed.
- Authenticated create.
- Pending edit add/remove/update A không đổi B.
- All-rejected resubmit giữ IDs/history.
- INITIAL_CLAIM + replace/resend/decline/expire/cancel-3A.
- TRANSFER before/after rights, wrong Google account, replay/concurrency/expiry.
- Safe edit + urgent media decline.
- Amendment submit/approve/reject/withdraw/expire + sibling isolation.
- List/search/dashboard/calendar/report/export/email per-campus.
- Role matrix/IDOR/hidden keyword.
- Missing detail, 29m59s/30m, v1 compatibility.
- Minutes, feedback, partner links, face/OCR/gallery member references sau copy-on-write.
- No raw secret/PII logs, XSS/formula injection.
- Concurrency/idempotency/rollback/no duplicate notification.
- Query bounds/N+1/payload size.

## 10.4. Rollout/observability

Tài liệu hóa và nếu có staging thì diễn tập:

1. Backup + preflight trên clone.
2. Additive SQL trước, feature OFF.
3. Backend dual-read/write gated.
4. Backfill + verify.
5. Frontend v2 flag OFF.
6. Canary internal/test account.
7. Theo dõi create/edit/resubmit/identity/amendment/search/audit/email/query metrics.
8. Gradual enable.
9. Rollback bằng flags; không drop bảng/cột.

Flags vẫn mặc định OFF trong code/appsettings. Không tự bật production.

---

# 11. Phase I — Contract cleanup preparation, không chạy destructive thật

Chỉ chuẩn bị khi:

- zero legacy runtime reads của 10 global fields;
- mọi v1 data đã backfill/cutover;
- không còn old client/draft phụ thuộc contract;
- export/rollback được chứng minh;
- report zero-unclassified-reference hoàn tất.

Deliverables:

1. Read-only preflight chứng minh readiness và liệt kê blocker.
2. Guarded destructive migration/drop 10 global fields/index/check liên quan.
3. Fresh-create SQL clean v2 sau contract drop.
4. Verify script + rollback/export strategy.
5. Test **chỉ trên disposable MySQL**.
6. Documentation ghi rõ migration chưa được phép apply production.

Không tự apply contract-drop trên `pems_db`, staging thật hoặc production. Nếu readiness chưa đạt, script phải fail closed và report blocker; Phase I có thể hoàn thành ở mức “prepared/tested, not executed”.

---

# 12. Security, privacy, authorization và audit gates chung

- Public responses chống account enumeration.
- Token/OTP/session/raw Google credential không log hoặc audit.
- Identity/safe/amendment routes authenticated phải `[Authorize]`; chỉ landing/OTP public route đúng contract mới anonymous.
- CORS không mở rộng tùy tiện; CSRF theo auth mechanism hiện hữu.
- Normalize/validate/sanitize ở server; output HTML/email/print escape đúng.
- IDOR request/instance/member/campus/amendment/change IDs trả 403/404 theo convention, không leak existence/content.
- Audit generic dùng masked/summarized diff; immutable authorized history giữ snapshot cần thiết.
- Notification không mang full old/new purpose/member/contact PII.
- Không dùng `changeId` tuần tự làm proof of ownership; public invitation phải dùng opaque token.
- Không dùng status user `INACTIVE/LOCKED` để biểu diễn invitation pending.
- Không tự xóa orphan/member nếu downstream history/FK tồn tại; phase đầu report thay vì destructive cleanup.

Audit actions tối thiểu còn lại:

```text
PRIMARY_CONTACT_TRANSFER_REQUESTED
PRIMARY_CONTACT_TRANSFER_APPLIED
VISIT_SAFE_FIELDS_UPDATED
VISIT_AMENDMENT_SUBMITTED
VISIT_AMENDMENT_APPROVED
VISIT_AMENDMENT_REJECTED
VISIT_AMENDMENT_WITHDRAWN
VISIT_AMENDMENT_EXPIRED
VISIT_INSTANCE_FORM_REVISION_APPLIED
```

Mọi action phải có integration test chứng minh audit header + expected change/event rows trong cùng commit.

---

# 13. Commit policy bắt buộc

## 13.1. Metadata

Không để trong author, committer, message hoặc trailers:

```text
Claude
Claude Code
Claude Opus
AI Agent
Generated by AI
Co-Authored-By của AI
Assisted-by
Generated-With
```

Không tự đổi global git config. Author/committer phải là:

```text
Tcanh12 <canhnvthe186121@fpt.edu.vn>
```

Sau mỗi commit verify:

```bash
git show -s --format=fuller HEAD
git log -1 --format="%an <%ae>%n%cn <%ce>%n%B"
```

Nếu metadata sai và commit chưa push, amend ngay. Không rewrite pushed/shared history nếu người dùng chưa yêu cầu.

## 13.2. Functional slice, không commit lẻ

- Không commit một file/handler/report riêng nếu nó thuộc cùng behavior.
- Gom production code + DTO/validator + SQL/config + tests + report/progress của cùng functional slice.
- Không tạo docs-only/hash-fixup/test-count/comment/mock commit riêng; nếu quên và chưa push thì amend.
- Không sửa file không liên quan chỉ để tăng file count.
- Một-file commit chỉ chấp nhận khi thay đổi thật sự độc lập và bản chất chỉ cần một file; ghi lý do trong report.
- Mỗi phase lớn ưu tiên 1–3 commit review/test/rollback độc lập.

Commit grouping khuyến nghị, điều chỉnh theo code thật:

1. `feat(delegations): add v2 primary-contact transfer workflow`
2. `feat(delegations): add safe edits and per-campus amendments`
3. `feat(delegations): complete amendment decisions and revision history`
4. `feat(delegations): make aggregate search and read surfaces v2-safe`
5. `feat(reports): migrate per-campus reports exports and emails`
6. `feat(frontend): add per-campus v2 form and read workflows`
7. `feat(frontend): add identity amendment and history workflows`
8. `test(delegations): add v2 end-to-end and rollout gates` — chỉ khi commit chứa test/config/rollout implementation thực, không chỉ report.
9. `chore(database): prepare guarded v2 contract cleanup`

Không ép đúng số commit nếu dependency/testability yêu cầu khác, nhưng tuyệt đối tránh commit-per-file.

---

# 14. Test và database hygiene sau mỗi slice

Sau mỗi functional slice:

1. Chạy targeted tests mới.
2. Chạy backend build.
3. Chạy full suite liên quan; trước commit phase phải chạy full Unit/Architecture/Integration.
4. Với frontend: lint + unit + build; E2E ở checkpoint phù hợp.
5. Verify audit rows/notifications/idempotency trong test.
6. Verify disposable DB business rows được rollback/cleanup.
7. Verify `pems_db`, `pems_test`, `pems_pr3_test` không mutation.
8. Restore `appsettings.Testing.json` về `pems_test` bằng trap/finally.
9. Verify flags vẫn default OFF.
10. Update `IMPLEMENTATION_PROGRESS.md` và `FINAL_IMPLEMENTATION_REPORT.md` trong cùng functional commit.

Nếu test không chạy được, báo chính xác command/test chưa chạy và lý do; không ghi “pass” từ code review.

---

# 15. Definition of Done cuối

Chỉ đổi report từ `IN PROGRESS` sang `FINAL` khi tất cả đúng:

1. D-4 TRANSFER exact-Google 24h hoàn chỉnh; old owner giữ quyền trước apply, new owner có quyền sau apply, old account không bị xóa/lock.
2. INITIAL_CLAIM 72h, TRANSFER 24h và redaction 90d job/test đầy đủ.
3. Cancel-3A đồng nhất handler/allowedActions/trigger/test; transfer pending không được dùng ngoại lệ.
4. Safe classifier/amendment/history hoàn chỉnh; approved snapshot bất biến trước approval và sibling không reset.
5. Revision/audit/notification đầy đủ trong cùng transaction/post-commit boundary phù hợp.
6. Search scope-before-keyword và side-channel tests xanh.
7. List/dashboard/calendar/report/export/print/email đều v2-safe.
8. Repository-wide zero-unclassified global-field references.
9. Frontend create/edit/resubmit/read/identity/amendment/search/history hoàn chỉnh VI/EN, desktop/mobile/keyboard.
10. 30 phút enforce FE/BE/DB.
11. Unit/Architecture/Integration/frontend unit/build/E2E đều xanh.
12. Downstream minutes/feedback/OCR/face/gallery/partner links không regression.
13. SQL fresh/upgrade/backfill/idempotency/verify/rollback strategy test trên MySQL thật disposable.
14. Flags default OFF; metrics/canary/rollback documented/tested.
15. Contract cleanup prepared/tested disposable nhưng chưa destructive trên DB thật.
16. Persistent DB không mutation ngoài hành động người dùng cho phép; test rows/tokens/identity/amendments sạch.
17. `appsettings.Testing.json` restore đúng.
18. Git status sạch ngoài hai plan docs untracked đã được xác nhận.
19. Commit metadata đúng Tcanh12, không AI attribution, commits gom theo functional slice.
20. `FINAL_IMPLEMENTATION_REPORT.md` liệt kê commits, files/modules, SQL import order, test commands + discovered/pass/fail/skipped, flags, DB hygiene, known limitations, rollout và exact rollback.

`OTP_FALLBACK` được phép ghi deferred/out-of-scope nếu Product vẫn chưa bật non-Google. Không được ghi TRANSFER deferred rồi tuyên bố Phase D/DoD hoàn tất.

---

# 16. Checkpoint/report format nếu buộc phải dừng

Checkpoint phải tự chứa:

- branch + exact HEAD;
- commit list + functional scope;
- files/modules/SQL scripts;
- test command và discovered/pass/fail/skipped;
- DB disposable đã dùng;
- persistent DB row-count/mutation verification;
- `appsettings.Testing.json` restore verification;
- feature flag defaults;
- git status và untracked docs;
- phần completed/deferred/known limitation;
- exact next action;
- author/committer verification.

Không ghi “Phase hoàn tất” nếu còn required item của phase. Không dùng test count cũ nếu clean discovery tại HEAD chứng minh count mới.

---

# 17. Lệnh bắt đầu cho AI Agent

Bắt đầu ngay bằng quy trình sau:

1. Verify branch/HEAD/status/author tại `7ff70224`.
2. Đọc report/progress/master plan/audit map và source/test thật.
3. Sửa report nếu đang ghi “Phase D hoàn tất” trong khi TRANSFER chưa tồn tại.
4. Lập inventory code D-4, không redo INITIAL_CLAIM/cancel-3A/job đã xanh.
5. Triển khai TRANSFER 24h thành một functional slice đầy đủ: production + SQL nếu cần + tests + report/progress.
6. Chạy targeted + full backend gates, verify DB hygiene, commit bằng Tcanh12 và kiểm tra không AI attribution.
7. Tự tiếp tục Phase E → F → G → H → I khi slice trước xanh; không hỏi lại sau mỗi phase.
8. Không push/merge nếu người dùng chưa yêu cầu.

Chỉ trả báo cáo cuối session khi functional slice hiện tại đã xanh/commit hoặc có hard blocker/hard-limit thật sự.
