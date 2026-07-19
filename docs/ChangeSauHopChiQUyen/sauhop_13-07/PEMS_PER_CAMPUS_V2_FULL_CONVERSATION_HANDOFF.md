# PEMS Per-Campus Visit Form V2 — Tài liệu tổng hợp đầy đủ yêu cầu, thiết kế, tiến độ và điểm tiếp tục

> Cập nhật theo toàn bộ chuỗi trao đổi và các báo cáo phiên đến checkpoint gần nhất `a5610e2f`.
>
> **Trạng thái chương trình:** `IN PROGRESS`  
> **Trạng thái implementation:** backend, SQL additive, frontend V2, real-host workflows và Full Browser UI E2E đã hoàn thành sau feature flags.  
> **Chưa hoàn tất:** Phase I candidate mới ở mức draft; zero-unclassified audit đầy đủ, bốn disposable drills, schema diff và các regression gates còn thiếu chưa được thực hiện.  
> **Không có thay đổi production:** flags vẫn mặc định OFF; chưa deploy/canary; chưa drop cột V1 trên DB thật.

---

## 1. Mục đích tài liệu

Tài liệu này được viết để một kỹ sư hoặc AI Agent chưa từng đọc cuộc hội thoại vẫn có thể hiểu:

- PEMS đang thay đổi nghiệp vụ gì và vì sao;
- dữ liệu nào dùng chung ở cấp request, dữ liệu nào phải tách theo từng campus;
- database, backend, API, frontend và state machine V2 được thiết kế ra sao;
- vì sao bảng `visit_requests` vẫn còn các cột V1;
- toàn bộ các phase đã triển khai, commit quan trọng và kết quả test;
- các lỗi production đã được real-stack testing phát hiện;
- phần nào thực sự đã DONE, phần nào còn bị chặn hoặc cần quyết định;
- các ràng buộc Git, DB, feature flag và bảo mật phải giữ nguyên;
- công việc chính xác mà phiên tiếp theo phải thực hiện.

Tài liệu tổng hợp trạng thái **được báo cáo trong các phiên làm việc**. Agent tiếp theo vẫn phải kiểm tra Git/source/test thực tế trước khi mutation vì môi trường có cơ chế auto-push/auto-merge bất đồng bộ.

---

## 2. Tóm tắt điều hành

PEMS ban đầu lưu phần lớn nội dung đăng ký đoàn khách ở cấp `visit_requests`, nghĩa là một request đi nhiều campus vẫn chỉ có một bộ nội dung toàn cục. Mô hình này không đáp ứng yêu cầu mới khi từng campus có thể có:

- thời gian khác nhau;
- mục tiêu, nội dung làm việc và loại hình chuyến thăm khác nhau;
- danh sách khách và hỗ trợ khác nhau;
- đầu mối vận hành, ngôn ngữ, phương tiện và media consent khác nhau;
- trạng thái phê duyệt, host và revision độc lập.

V2 chuyển source of truth của form sang từng `visit_instance_id`. Request vẫn là aggregate cha, nhưng mỗi campus có snapshot form, member set, revision và approval lifecycle riêng.

Đến checkpoint cuối:

- SQL additive V2, backfill, verify và rollback drills đã hoàn thành;
- dual-read/backend compatibility V1/V2 đã phủ phần lớn consumer;
- create, public OTP, pending edit, resubmit, identity claim/transfer, safe edit, amendment, search và jobs đã được triển khai;
- frontend V2 create/read/edit/resubmit/identity/amendment/search đã tồn tại và entry points đã cutover theo capability;
- feature flags vẫn mặc định OFF để giữ production inert;
- real-host API-level A–H `8/8` và chín full-DOM workflows `9/9`, tổng real-stack `17/17`, trên Chromium → React/Vite → published .NET API → disposable MySQL, không mock network;
- pending edit, resubmit, safe edit, member amendment, approve/reject, wrong-campus visibility/denial, withdraw và hidden-context search đều đã được click/fill/submit qua React DOM với HTTP + read API/DB assertions;
- hai xung đột merge-induced đã được reconcile theo canonical rules: guest/support names tiếp tục bị loại khỏi default search; `VisitRequestPhoto` trở lại strict image policy 5 MB + magic bytes;
- Unit đã khôi phục `530/530`; targeted V2 IT đã khôi phục `45/45`;
- Architecture gần nhất `14/14`; Vitest `99`, TypeScript `0`, build và real-stack `17/17` là kết quả trước ba commit mới vì frontend/browser gates chưa rerun trong phiên gần nhất;
- full Integration chưa có kết quả sạch mới; một số phần fail do schema mismatch từ Dev/local test infrastructure, còn kết quả sạch gần nhất trước merge là `400/400`;
- Phase I đã có bốn SQL candidate + README nhưng chưa drill; hiện vẫn **NOT READY FOR EXECUTION** vì V1 fallback, legacy readers/writers, dữ liệu V1 và flags OFF còn tồn tại.

---

## 3. Trạng thái Git gần nhất

### 3.1. Mốc được báo cáo gần nhất

- Local branch: `Canh-Iter1`.
- Remote branch thường hiển thị: `origin/Cảnh-Iter1`.
- Last known local HEAD: `a5610e2f`.
- Tại báo cáo cuối: local `3 ahead / 0 behind` so với `origin/Cảnh-Iter1`.
- Môi trường đã auto-merge Dev giữa phiên bằng pushed merge commit `64c83a59`; parent 1 là checkpoint `5b943b1a`. Commit merge được coi là immutable.

Môi trường từng tự auto-push hoặc auto-merge giữa các phiên. Agent không được suy rằng upstream vẫn giữ đúng mốc trên; luôn phải chạy preflight.

### 3.2. Quy tắc Git bắt buộc

- Không `git push`, merge hoặc mở PR nếu người dùng chưa yêu cầu.
- Không rewrite commit đã xuất hiện trên remote.
- Không rebase/reset/amend/force-push để sửa lịch sử đã push.
- Không đổi `git config --global`.
- Mọi commit dùng:
  - author: `Tcanh12 <canhnvthe186121@fpt.edu.vn>`;
  - committer: cùng identity;
  - không `Co-Authored-By`;
  - không Claude, ChatGPT, AI, Generated hoặc Assisted attribution.
- Chỉ stage explicit pathspec và kiểm tra `git diff --cached --name-status` trước commit.
- Giữ nguyên bốn plan/handoff docs untracked; tài liệu handoff tổng hợp này là file thứ tư. Không stage hoặc commit chúng.

### 3.3. Sự cố lịch sử cần biết

Commit `f9aa43f0` từng vô tình chứa hai plan docs và thiếu `App.tsx`. Khi phát hiện thì commit đã được auto-push, nên không được rewrite. Commit forward-fix `0cec2972` đã:

- gỡ hai plan docs khỏi tree hiện tại nhưng giữ file trên disk dưới dạng untracked;
- thêm route claim/transfer còn thiếu trong `App.tsx`.

Hai docs vẫn tồn tại trong lịch sử cũ của `f9aa43f0`; chỉ force-push mới xóa được và hành động đó bị cấm. Tip hiện tại không tracking chúng nên merge bình thường sẽ không mang chúng vào tree.

---

## 4. Bài toán nghiệp vụ và mô hình form liên cơ sở

## 4.1. Dữ liệu dùng chung ở cấp request

Các dữ liệu này tồn tại một lần cho toàn request:

| Nhóm | Nội dung |
|---|---|
| Identity đăng ký | Registrant/account đã tạo request và quan hệ owner/co-editor |
| Primary contact | Người quản lý yêu cầu, email account-binding, claim/transfer state |
| Partner | Quan hệ đối tác của request nếu có |
| Idempotency | `submissionId`, fingerprint V2, request code |
| Schema/scope | `form_schema_version`, `visit_scope`, `has_mixed_campus_details` |
| Aggregate lifecycle | Request-level status/projection, cancellation, resubmission count |
| Identity history | INITIAL_CLAIM/TRANSFER events, token lifecycle, audit |
| Request history | Request revision/audit/correlation metadata |

Primary contact và operational contact là hai khái niệm hoàn toàn khác nhau:

- **Primary contact:** request-level; liên quan account VISITOR; có quyền quản lý/chỉnh sửa request theo lifecycle.
- **Operational contact:** per-campus form snapshot; phục vụ phối hợp tại campus; không tự cấp account hoặc quyền đăng nhập.

## 4.2. Dữ liệu riêng theo campus

Mỗi `visit_instance_id` có snapshot độc lập:

| Nội dung | Nơi lưu/source of truth V2 |
|---|---|
| Campus | `visit_request_campuses.campus_id` |
| Start/end và timezone | `visit_request_campuses` |
| Tên đoàn khách | `visit_instance_form_details.delegation_name` |
| Visit type/other | `visit_instance_form_details.visit_type`, `visit_type_other` |
| Mục đích | `visit_instance_form_details.purpose` |
| Nội dung làm việc | `visit_instance_form_details.working_content` |
| Khách và hỗ trợ ngoài | `visit_guest_members` + `visit_instance_guest_members` |
| Operational contact | `visit_instance_form_details.operational_contact_*` |
| Ngôn ngữ | `visit_instance_form_details.working_language` |
| Ghi chú phương tiện | `visit_instance_form_details.transportation_note` |
| Media consent/note | `visit_instance_form_details.media_consent_status/note` |
| Note to FPTU | `visit_instance_form_details.note_to_fptu` |
| Revision | `form_revision`, `approval_revision`, row versions và history |
| Campus lifecycle | status, decision, host, coordinator và cancel/close metadata |

## 4.3. Nguyên tắc form liên cơ sở

- Frontend gửi snapshot đã resolve đầy đủ cho từng campus.
- “Sao chép từ campus khác” hoặc “áp dụng cho tất cả” chỉ là thao tác deep-copy một lần trong UI.
- Backend không lưu cờ kế thừa kiểu `sameForAll` và không tự đồng bộ ngầm các campus sau đó.
- Sửa campus A không được làm thay đổi campus B.
- Member rows V2 mới độc lập theo campus; không chia sẻ object/row vì người dùng đã bấm copy.
- Legacy member có thể đang được nhiều instance cùng link; lần sửa đầu dùng copy-on-write, không sửa row shared.
- `has_mixed_campus_details` phản ánh khác biệt nội dung/member giữa campus; khác campus hoặc lịch đơn thuần không tự làm request “mixed”.
- Thời lượng mỗi chuyến thăm tối thiểu 30 phút ở frontend, backend và DB.
- Frontend đã đặt caps được báo cáo là tối đa 10 campus/200 members và Excel import theo campus tối đa 5 MB.

---

## 5. Vì sao `visit_requests` vẫn còn nhiều cột V1

Đây là hành vi có chủ đích, không phải migration chưa chạy.

Mười cột global V1 vẫn được giữ tạm thời:

1. `delegation_name`
2. `visit_type`
3. `visit_type_other`
4. `purpose`
5. `working_content`
6. `working_language`
7. `transportation_note`
8. `media_consent_status`
9. `media_consent_note`
10. `note_to_fptu`

Với request V2:

- source of truth là `visit_instance_form_details`;
- các global fields chỉ là compatibility projection;
- nếu các campus đồng nhất, projection dùng common value;
- nếu mixed, projection dùng campus có `campus_id` nhỏ nhất để tương thích với code/constraint V1;
- V2 business/read/search không được lấy projection này làm source.

Chưa thể drop vì:

- feature flags vẫn mặc định OFF;
- frontend khi capability OFF/loading/error vẫn fallback V1;
- backend vẫn có dual-read và Class-C compatibility readers;
- create/edit/resubmit còn ghi compatibility projection;
- old clients/drafts và rollout production chưa được chứng minh đã chấm dứt.

Việc drop chỉ thuộc **Phase I contract cleanup**, sau khi đạt:

- zero legacy runtime reads;
- zero legacy runtime writes;
- mọi dữ liệu đã backfill/cutover;
- không còn old client/draft;
- export/rollback đã chứng minh;
- rollout/canary thành công.

Hiện Phase I readiness đang FAIL theo thiết kế. Không được xóa V1 để làm readiness giả xanh.

---

## 6. Kiến trúc database V2

Các bảng cốt lõi đã được thêm theo hướng additive:

1. `visit_instance_form_details`
2. `visit_instance_guest_members`
3. `visit_request_identity_changes`
4. `visit_request_identity_change_events`
5. `visit_instance_amendments`
6. `visit_instance_amendment_changes`
7. `visit_instance_form_revision_history`
8. `visit_request_revision_history`
9. `visit_request_pending_forms`

### 6.1. Các guard quan trọng

- Một `visit_instance_form_details` cho mỗi instance.
- Composite FK/unique chống member link cross-request.
- Unique pending identity change theo request/relation.
- Unique pending amendment theo instance.
- Minimum duration 30 phút ở DB.
- Guard chống OTHER rỗng và dữ liệu normalized không hợp lệ.
- Request/instance/detail có row-version dùng cho optimistic concurrency.

### 6.2. SQL migration package

Package nền tảng gồm:

- `01_preflight_readiness.sql`: kiểm tra blocker trước migration;
- `02_up_additive.sql`: bảng/cột/index/check/trigger additive;
- `03_backfill.sql`: V1 → V2 detail/member/revision/access state;
- `04_verify.sql`: presence, counts, orphan/cross-request/checksum;
- `05_rollback_down.sql`: guarded rollback, từ chối khi không thể khôi phục an toàn;
- `06_up_identity_claim_tokens.sql`: token purposes cho initial claim/identity;
- `07_up...`: transfer state support;
- `08_up_pending_v2_forms.sql`: snapshot bind giữa initiate và verify;
- `09_up...`: operational contact organization/email nullable đúng rule optional.

Tên file 07/09 phải lấy từ source thực tế; tài liệu này chỉ ghi vai trò vì tên đầy đủ không được lặp nhất quán trong báo cáo.

### 6.3. SQL verification đã thực hiện

Phase H đã drill:

- fresh import từ master đã fix;
- upgrade từ pre-V2 baseline;
- backfill và `V01–V15 = 0`;
- idempotent rerun với snapshot counts không đổi;
- rollback refusal khi có mixed/V2-only data và không partial drop;
- clean rollback khi an toàn;
- fresh-vs-upgrade schema byte-identical;
- constraints/indexes quan trọng đúng;
- disposable DB được drop sau evidence.

Master V11 ở các run mới nhất được báo cáo có 76 tables. Không suy con số này cho môi trường khác mà không kiểm tra.

---

## 7. Các workflow nghiệp vụ V2

## 7.1. Create V2 authenticated

- Endpoint create V2 nhận request-level data và `campusVisits[]` đầy đủ.
- Ghi request với `form_schema_version=2`, scope, mixed flag và fingerprint versioned.
- Tạo N campus instances, form details, member links và baseline revision rows trong transaction.
- Idempotency theo `submissionId`; concurrent duplicate trả winner.
- Audit `VISIT_REQUEST_CREATED_V2`.
- Notification Staff Leader/HO gửi sau commit, first-create-only.
- Rollback hoặc replay không gửi lặp.
- Limitation được ghi rõ: notification best-effort, chưa có outbox.

## 7.2. Public create V2 và OTP binding

Luồng cuối cùng:

1. `POST /api/v2/visit-requests/initiate` nhận **full V2 form**.
2. Backend chạy validator V2, không dùng rule V1 3 giờ/1 support.
3. Backend mint OTP và lưu canonical pending snapshot trong `visit_request_pending_forms`.
4. `POST /api/v2/visit-requests/verify` xác minh OTP.
5. Verify tạo request từ snapshot đã bind, không tin form mới do client gửi lại.

Stable errors:

- `PER_CAMPUS_V2_PENDING_NOT_FOUND`
- `PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH`

Việc binding đóng lỗ hổng thay campus/member/contact/time/content giữa initiate và verify.

Replay idempotent không consume OTP lần nữa và chỉ provision registrant. Primary contact khác registrant vẫn ở `PENDING_CONFIRMATION/INITIAL_CLAIM`.

## 7.3. Initial primary-contact claim

- Nếu contact email trùng registrant, relation có thể ACTIVE ngay theo rule.
- Nếu khác, tạo invitation 72 giờ.
- Token single-use; chỉ lưu hash.
- Public GET landing trả dữ liệu tối thiểu, email masked, không mutation.
- Accept/decline yêu cầu authenticated Google VISITOR với normalized login email trùng email mời.
- Chỉ POST accept mới link account và cấp quyền; mở link/login không tự claim.
- Accept khóa claim `FOR UPDATE`, link user, mark ACTIVE, burn token và ghi masked audit/event.
- Campus decisions không bị thay đổi.
- Resend vô hiệu token cũ, restart 72 giờ, cap 5.
- Replace pending contact hỗ trợ sửa email gõ nhầm.
- Khi ACTIVE, resend/replace pending bị từ chối.

## 7.4. Transfer primary contact

- Active owner initiate transfer tới contact mới.
- Invitation 24 giờ.
- Owner cũ vẫn ACTIVE cho tới khi contact mới accept.
- Accept thực hiện swap relation, không xóa history/account cũ.
- Có initiate/landing/accept/decline/resend/cancel.
- Wrong account/email không accept được.

Identity OTP fallback cho non-Google từng được ghi là deferred Product decision; không nhầm với OTP xác minh public registrant create V2 đã triển khai.

## 7.5. Cancel 3A

Registrant được cancel request khi:

- contact status là `PENDING_CONFIRMATION` của `INITIAL_CLAIM`;
- request đang ở trạng thái vốn cho phép cancel;
- còn qua guard 24 giờ;
- chưa campus nào started;
- có reason.

Cancel đóng claim/token cùng transaction và ghi event riêng. Khi contact ACTIVE, ngoại lệ của registrant kết thúc. Transfer pending không được xem như initial pending vì owner cũ vẫn ACTIVE.

## 7.6. Expiry và redaction jobs

- Initial claim hết 72 giờ → EXPIRED; request không bị hủy.
- Transfer hết 24 giờ → EXPIRED; owner cũ giữ nguyên.
- EXPIRED/DECLINED/CANCELLED/SUPERSEDED giữ full data 90 ngày.
- Sau 90 ngày redact full email/token refs/pending snapshot; giữ masked email, type/status/actor/timestamps.
- Jobs batchable, idempotent và có event `IDENTITY_CHANGE_REDACTED`.

## 7.7. Pending edit

- Chỉ lifecycle phù hợp trước approval.
- Explicit concurrency ở request và từng instance rowVersion.
- Conflict trả 409 ổn định.
- Change detection đảm bảo sửa A không ghi B.
- Add campus recheck availability, route leader và tạo baseline revision.
- Remove campus chỉ khi WAITING và không có downstream data; cleanup orphan member.
- Legacy shared member dùng copy-on-write.
- Registrant/partner/two account-bound emails immutable theo contract.
- Recompute scope, mixed flag, fingerprint và projection.
- Audit field-level có correlation ID.
- Editor là registrant hoặc ACTIVE contact.

## 7.8. Resubmit

- Chỉ khi toàn bộ campus REJECTED.
- Campus set cố định.
- Giữ nguyên `visitInstanceId`.
- Snapshot decisions cũ vào audit/history trước khi clear.
- Không xóa lịch sử.
- Parent về PENDING trước theo three-phase flush để trigger đúng.
- Route current leader.
- Tăng `resubmission_count`.
- `SELECT … FOR UPDATE` bảo đảm concurrent chỉ một winner, loser 409.

## 7.9. Safe edit và amendment sau approval

Backend là nguồn phân loại duy nhất. Frontend chỉ hiển thị dự đoán UX.

### Safe/correction — apply ngay

- Registrant full name/org/job title/phone.
- Primary-contact name/org/phone.
- Transportation note.
- Note to FPTU.
- Media note/consent.

Apply ngay, tăng revision phù hợp, field-level audit và notify actor liên quan.

### Privacy urgent

`media_consent_status → DECLINED`:

- apply ngay cả dưới 24 giờ;
- notification HIGH/URGENT;
- không chờ amendment approval.

### Approval-sensitive — amendment theo campus

- Delegation name.
- Visit type/other.
- Purpose.
- Working content.
- Guest/support list.
- Working language.
- Operational contact có tác động điều phối.
- Logistics-impacting requirements.
- Thay lịch campus đã duyệt.

Approved snapshot cũ vẫn active cho tới khi current Staff Leader đúng campus approve.

### Structural

- Add campus → instance mới chờ duyệt.
- Remove approved campus → cancel flow.
- Đổi campus → cancel cũ + add mới.
- Đổi lịch approved campus → amendment.

### Amendment invariants

- Một pending amendment mỗi instance.
- Reason bắt buộc.
- Base form/approval revisions và rowVersion được kiểm tra.
- Approval state và amendment state độc lập.
- Pending amendment không reset campus approval hoặc sibling.
- Reject/withdraw/expire giữ active snapshot.
- Approve apply đúng target campus, tăng revision và update history/calendar/logistics signals.
- Dưới 24 giờ không tạo self-service amendment mới, trừ privacy urgent.

Stable errors gồm:

- `AMENDMENT_ALREADY_PENDING`
- `AMENDMENT_NOT_EDITABLE`
- `AMENDMENT_BASE_REVISION_CONFLICT`
- `AMENDMENT_APPROVER_SCOPE_FORBIDDEN`
- `AMENDMENT_WINDOW_EXPIRED`
- concurrency code tương ứng.

---

## 8. Authorization và `allowedActions`

Frontend không được suy mutation permission từ role/status.

- Backend read model trả request-level và per-instance `allowedActions`.
- Frontend dùng typed action constants để hiện/ẩn nút.
- Relation chỉ được dùng cho identity panel khi phù hợp.
- Mọi command handler vẫn re-authorize; không tin frontend.

Ma trận đã được integration test:

- Pending owner → EDIT + SAFE_EDIT theo rule, chưa có amendment submit.
- Assigned owner → SUBMIT_AMENDMENT.
- Pending amendment → requester WITHDRAW; không submit lần hai.
- Current campus leader → APPROVE/REJECT đúng campus.
- Leader campus khác không quyết định sibling.
- HO → VIEW only.
- Rejected → RESUBMIT, không EDIT thường.

---

## 9. Search V2 và `matchedContexts`

Nguyên tắc bảo mật:

1. Xác định authorized request/instance scope.
2. Áp keyword trên dữ liệu đã scope.
3. Count/order/page không được chịu ảnh hưởng bởi campus ẩn.
4. Response group thành một parent request.
5. Chỉ trả authorized `matchedContexts`.

DTO đã triển khai:

```text
SearchMatchContextDto {
  Scope: REQUEST | CAMPUS,
  VisitInstanceId?,
  CampusId?,
  CampusName?,
  MatchedFields[]
}
```

Stable field codes đã báo cáo:

- `REQUEST_CODE`
- `REGISTRANT_ORGANIZATION`
- `PARTNER`
- `PRIMARY_CONTACT`
- `DELEGATION_NAME`
- `CAMPUS`
- `HOST`

Không trả raw value/snippet. Guest/support names không search mặc định.

`ViewGuestDelegationListQueryHandler` có hai paths:

- instance-level cho actor scoped theo instance;
- request-level cho owner/HO/registrant có visibility rộng hơn.

Cả hai đã thực hiện SQL `scope → keyword → count → order → pagination`. `VisitSearchMatchContextBuilder` chỉ enrichment sau page trên các campus đã authorize của row, nên không thể thay đổi hit/count/order hoặc truy cập hidden campus.

Frontend `SearchMatchContexts` render ví dụ:

- “Khớp tại: TP.HCM — Mục đích chuyến thăm”;
- “Khớp tại: Thông tin chung — Mã yêu cầu”.

Consumer audit kết luận:

- `VisitRequestManagement` là surface dùng DTO này và có keyword UX; đã render.
- Các search account/audit/security/gallery/news… là entity khác → N/A.
- Invitations attending tab dùng DTO/query riêng; đã scope-before-keyword từ Phase F.

Security tests đã chứng minh hidden-only keyword không tạo hit/count, shared token trả một parent với authorized contexts, request-level match không bị gán cho first campus và guest name không tạo result.

---

## 10. API surface V2 chính

Các routes được báo cáo/định nghĩa trong chương trình:

```text
POST /api/v2/visit-requests
POST /api/v2/visit-requests/initiate
POST /api/v2/visit-requests/verify
GET  /api/v2/visit-requests/{id}
PUT  /api/v2/visit-requests/{id}/pending-edit
POST /api/v2/visit-requests/{id}/resubmit
PATCH /api/v2/visit-requests/{id}/safe-details

POST /api/v2/visit-requests/{id}/instances/{instanceId}/amendments
GET  /api/v2/visit-requests/{id}/instances/{instanceId}/amendments/active
POST /api/v2/visit-requests/{id}/instances/{instanceId}/amendments/{amendmentId}/withdraw
POST /api/v2/visit-instances/{instanceId}/amendments/{amendmentId}/approve
POST /api/v2/visit-instances/{instanceId}/amendments/{amendmentId}/reject
GET  /api/v2/visit-requests/{id}/history

GET  /api/public/visit-contact-claims/{token}
POST /api/v2/visit-contact-claims/{token}/accept
POST /api/v2/visit-contact-claims/{token}/decline
```

Resend/replace/transfer/cancel identity endpoints cũng đã triển khai; agent phải lấy exact path từ controllers/API client thay vì dựa vào tài liệu tóm tắt.

---

## 11. Feature flags và rollout behavior

Hai flags:

- `PerCampusFormV2`
- `PerCampusFormV2Write`

Không có section bật chúng trong appsettings; default OFF.

Behavior:

- Write OFF → V2 write endpoints trả 404/inert theo convention.
- Write ON nhưng read OFF → reject `PER_CAMPUS_V2_READ_REQUIRED`.
- Cả hai ON → V2 hoạt động.
- Frontend capability resolving:
  - enabled → route V2;
  - OFF/loading/error → fail-safe V1;
  - CTA được giữ ổn định, tránh flicker.

Entry points đã cutover:

- Hero CTA/Final CTA → `/visit-registration/v2` khi capability ON.
- Dashboard create → `/visit/create-v2` khi ON.
- `/dashboard/visit/create` trở thành version-aware redirect, không còn dead prototype.
- V1 code được giữ để rollback/capability OFF.

Không có production flag flip, deployment hoặc canary trong các phiên này.

---

## 12. Frontend V2 đã triển khai

### 12.1. Form create

- React form schema `campusVisits[]`.
- Stable client keys.
- `useFieldArray` semantics.
- Accordion đóng không unregister field.
- Deep-copy campus/member data.
- Copy/apply-all có confirm.
- Excel import per campus.
- Draft V3 key riêng; migration V1 → V2 không overwrite draft V2 mới hơn.
- Public V2 dùng initiate V2 + OTP verify snapshot binding.
- Authenticated create dùng V2 API trực tiếp.

### 12.2. Read/detail

- Shared `CampusVisitDetailCard`.
- `VisitRequestV2DetailView` hiển thị request-level data một lần và cards theo campus.
- Mixed/uniform badges đúng scope.
- HO read-only.
- Identity panel, Amendment panel và History timeline dùng viewer context/allowedActions.
- Route `/dashboard/visit/v2/:id`.

### 12.3. Edit/resubmit

- Routes `/dashboard/visit/v2/:id/edit` và `/resubmit`.
- Reuse schema/CampusVisitCard; không tạo form model thứ ba.
- Hydrate resolved form với request/instance rowVersions.
- Stable 409 → message + reload.
- V1 `FORM_VERSION_UPGRADE_REQUIRED` fallback vẫn là defense-in-depth.

### 12.4. Post-submit summary

Summary render từ immutable submitted snapshot:

- request code;
- registrant;
- primary contact + claim state;
- partner;
- aggregate status;
- campus count;
- mixed/uniform badge;
- một card đầy đủ cho mỗi campus.

Instance status map bằng campus code/ID/instance, không positional/first-campus.

### 12.5. Version-aware shared modal

`SubmittedVisitRequestDetailModal` được sửa centrally:

- `form_schema_version=2` → render `VisitRequestV2DetailView` kể cả uniform;
- missing version → V1 fallback;
- không dùng mixed/campus-count làm discriminator;
- 6 components/7 invocation sites đã audit và report được sửa số đếm.

### 12.6. Safe edit/amendment UI

- Mọi mutation button gate bằng backend `allowedActions`.
- Safe-edit modal apply immediate fields.
- Amendment submit hiển thị active vs proposed.
- Member editor add/edit/remove guest/support với stable keys/deep-clone.
- Reason required.
- Leader approve/reject, requester withdraw.
- History không hiển thị raw audit IP/UA.

---

## 13. Real-stack test infrastructure

Kiến trúc:

```text
real Chromium
→ real React/Vite
→ real published .NET API
→ real disposable MySQL
```

Không mock network response.

### 13.1. Testing email sink

`FileSinkEmailService`:

- chỉ đăng ký trong Testing;
- yêu cầu explicit enable + path;
- thiếu path → fail closed;
- không log OTP/token;
- inbox ở temp directory và cleanup sau run.

### 13.2. Fail-closed E2E authentication

Không promote header-trusting `TestAuthHandler` cũ.

Scheme mới trong `E2ETestAuthentication.cs` chỉ bật khi đủ bốn gates:

1. environment = Testing;
2. `PEMS_E2E_TEST_AUTH_ENABLED=true`;
3. secret không blank;
4. profiles file hợp lệ.

Browser chỉ gửi opaque profile key + run-scoped secret. Role/user/campus/department/session được resolve từ server-side profile file được tạo từ actual disposable seed IDs.

- Secret `randomBytes(32)` mỗi run.
- Constant-time compare qua SHA-256 + `FixedTimeEquals`.
- Unknown/missing profile fail closed.
- Dev/Prod không đăng ký scheme.
- Spoof role/campus headers bị ignore.
- Active `user_sessions` row được seed và SessionId claim được emit để real `SessionValidationMiddleware` chấp nhận actor.

### 13.3. Harness bugs đã fix

- stale V10 → fixed V11 master;
- `fileURLToPath` cho repo path có space;
- temp `BaseOutputPath` tránh bin lock với dev server của người dùng;
- shell argument quoting;
- cleanup junction/temp/process an toàn.

---

## 14. Real-stack A–H và Full Browser UI E2E

Kết quả báo cáo cuối:

| Journey | Kết quả | Nội dung chính |
|---|---:|---|
| A | PASS | Public V2 create + real OTP sink |
| B | PASS | Authenticated HO dashboard, `/auth/me` 200 |
| C | PASS | Fail-closed auth + server-side identity |
| D | PASS | Owner mở mixed V2 detail, hai campus cards |
| E | PASS | Pending edit target-only + sibling no-op |
| F | PASS | Safe edit/member amendment lifecycle |
| G | PASS | Wrong-campus denial/current-campus gate |
| H | PASS | Scope-safe search matchedContexts |

Nhóm này được giữ nguyên là tám journey API-level/real-host `8/8`, không mock network.

### 14.1. Full-DOM promotion Slice 6c

Slice 6c bổ sung chín full-DOM workflows độc lập và tất cả đều PASS:

| Workflow | Bằng chứng chính |
|---|---|
| Pending edit | Sửa HN trên form thật; HN đổi + rowVersion tăng; HCM sibling byte/no-op |
| Resubmit | Reject hai campus làm precondition; form resubmit thật đưa request về PENDING, giữ campus/instance IDs, tăng resubmissionCount |
| Safe edit | Modal thật đổi transportation note và apply ngay, không tạo amendment |
| Member amendment | Empty reason bị disable; thêm guest, submit; active snapshot chưa đổi trước approval; sibling nguyên |
| Approve | HN leader click duyệt và áp dụng target-only |
| Reject | Fixture riêng; confirm bị disable khi thiếu reason; REJECTED không đổi active snapshot |
| Wrong-campus | HCM leader không thấy HN card/action; direct defense-in-depth call bị 403, HN leader qua campus gate |
| Withdraw | Requester click rút; amendment WITHDRAWN, snapshot nguyên, có thể đề xuất lại |
| Search no-leak | HCM-only keyword không làm request xuất hiện với HN leader; HN context không rò HCM; owner thấy HCM match |

Mỗi workflow thao tác trên browser DOM thật, `waitForResponse` đúng endpoint và kiểm chứng state qua read API/DB. Sibling-campus isolation được assert xuyên suốt. API-level A–H `8` và full-DOM `9` không gộp/double-count; tổng real-stack là `17/17`.

Identity claim/transfer chưa được thêm vào DOM matrix; integration layer đã cover. Đây vẫn là optional increment trừ khi H2/Product yêu cầu browser coverage.

### 14.2. Merge-overlap audit trong Slice 6c

Auto-merge Dev `64c83a59` tạo ba điểm giao:

1. Duplicate `VisitExpense*` DbSet declarations trong bốn UnitTests harnesses làm compile fail; đã sửa bằng commit `9c263a6c`.
2. Hai photo-upload unit tests fail sau thay đổi `FileValidationPolicy.cs`; được xác định là regression có sẵn từ Dev merge, ngoài Slice 6c và chưa sửa.
3. Dev thêm guest-name search, xung đột với invariant/test Slice 5B “guest/support names không được search mặc định”. Scope-before-keyword và PII-free matchedContexts vẫn an toàn, nhưng product/security semantics cần team chọn: chấp nhận guest search và cập nhật test, hoặc revert Dev clause.

Không assertion nào bị nới để làm test xanh.

---

## 15. Các lỗi quan trọng được test phát hiện

### 15.1. Optional operational-contact fields

Real-stack Journey A phát hiện `operational_contact_organization/email` optional ở validator/frontend nhưng DB NOT NULL, gây 500.

Fix:

- DB columns nullable;
- entity `string?`;
- create/edit normalize blank → NULL;
- read normalize NULL → empty UI string;
- master + migration 09 + regression tests.

### 15.2. Wrong rowVersion trong V2 read model

Journey F phát hiện V2 read model trả rowVersion của `visit_instance_form_details`, trong khi pending-edit/safe-edit/amendment kiểm tra rowVersion của `visit_request_campuses`.

Campus approval tăng instance rowVersion nhưng không tăng form-detail rowVersion, dẫn tới spurious 409 `VISIT_FORM_CONCURRENCY_CONFLICT` dù người dùng vừa reload.

Fix `4893c98d`:

- read model trả campus-instance rowVersion;
- thêm integration regression;
- full Integration tăng lên `400/400`.

Đây là ví dụ vì sao cần full UI/read-model/write-contract E2E, không chỉ unit/mocked tests.

---

## 16. Lịch sử triển khai và commits quan trọng

### 16.1. Backend/SQL core

| Commit | Phase | Nội dung |
|---|---|---|
| `836041f0` | B-1 | ExportDeptLeaderInvoice V2-safe, consumer 7/7 |
| `0f67eff8` | B-2 | Create V2 service, write flag, fingerprint, DTOs |
| `4dd1c1d4` | B-2 | Create command + POST V2 + idempotency |
| `0923a05b` | B-2.5 | Structural validator, notifier, public verify V2 |
| `3f4b37cf` | C-1 | Pending edit V2 + concurrency/change detection |
| `54c9996f` | C-2 | Resubmit V2, history/locking/reroute |
| `7ff70224` | D | Initial claim, cancel-3A, expiry/redaction |
| `375ccb4f` | D-4 | Transfer 24h workflow |
| `9635d66b` | E | Safe edit, amendments, history, expiry |
| `3353f7bd` | F | ~35 Class-C list/search/report/export/email surfaces V2-safe |

### 16.2. Frontend G/H

| Commit | Nội dung |
|---|---|
| `f9aa43f0` | G-1 API client, claim/transfer pages, identity/amendment/history components |
| `0cec2972` | Forward fix routes + untrack plan docs |
| `875c45d1` | G-2 per-campus form, draft migration, Excel, Vitest foundation |
| `b0f1dca4` | G-3 per-campus detail/mixed workflows |
| `01cddedd` | G-4A public V2 OTP initiate + bound pending snapshot |
| `ee7a056a` | G-4B pending edit/resubmit frontend |
| `96de2e3b` | H-1 SQL drill + master drift fix |
| `9f22cc24` | H-2 regression/E2E infra/matrix |
| `acdb9283` | H-3 observability + rollout docs |
| `c6af2f15` | Optional operational contact nullable fix |
| `e42836a3` | H-4 real-stack Journey A infrastructure |

### 16.3. Frontend cutover và completion slices

| Commit | Nội dung |
|---|---|
| `3e7d4d5d` | Capability + default entry-point cutover |
| `fa7849e6` | Version-aware management detail/edit/resubmit routing |
| `2cd948f8` | Full per-campus post-submit summary |
| `a14f1c99` | Slices 1–3 report |
| `7895be2d` | Restore UnitTests VisitExpense DbSets |
| `603abd46` | Backend allowedActions read model |
| `e30ad6a2` | Safe-edit/amendment frontend workflows |
| `89516d56` | S0 + Slice 4 report |
| `32f9ba25` | Member-list amendment frontend + copy-on-write IT |
| `213a9b3c` | Shared detail modal version-aware |
| `8a01f481` | Slice 4.1/5A report |
| `3b9af03a` | Scope-safe V2 search matchedContexts |
| `26d92bd3` | Slice 5B report + count correction |
| `dc9ddb90` | Fail-closed E2E auth scheme |
| `886df2c9` | Slice 5B.1/6a report |
| `edd1a8b3` | Authenticated real-stack A/B/C foundation |
| `5839663b` | A/B/C report |
| `4893c98d` | Fix campus-instance rowVersion read model |
| `09cdfa58` | Real-stack D–H |
| `5b943b1a` | Mark real-host A–H complete |
| `9c263a6c` | De-duplicate VisitExpense DbSet declarations sau Dev merge |
| `7c2e27d9` | Promote V2 mutation/search workflows thành full browser E2E |
| `5a44ebdd` | Ghi nhận Full Browser UI coverage và merge-overlap audit |
| `f4549b23` | Restore canonical guest-search scope; loại guest/job title/organization clause được Dev thêm |
| `c1ebe1fc` | Restore VisitRequestPhoto policy 5 MB + image magic-byte validation |
| `a5610e2f` | Add Phase I candidate preflight/UP/verify/DOWN + README |

Ngoài ra có các auto-merge commits từ Dev như `ba4ea97d` và `64c83a59`; agent phải kiểm tra ancestry thực tế thay vì dựa duy nhất vào bảng này.

---

## 17. Test evidence mới nhất

| Suite | Kết quả gần nhất được báo cáo |
|---|---:|
| Unit | `530/530` |
| Architecture | last run `14/14` |
| Full IntegrationTests | không rerun sau merge; last clean `400/400` |
| Targeted V2 IT | `45/45` |
| E2E auth guards | last run `4/4` |
| Vitest | last run `99`; NOT RUN sau R1/R2/Phase I draft |
| Real-stack API-level A–H | last run `8/8`; NOT RUN sau R1 |
| Real-stack Full DOM | last run `9/9`; NOT RUN sau R1 |
| Real-stack tổng | last run `17/17`; NOT RUN sau R1 |
| Browser-contract mocked-network | last explicitly reported `78` |
| TypeScript | `0` errors |
| Vite production build | pass |

Các con số là snapshot theo báo cáo, không phải cam kết cho HEAD mới. Agent phải chạy lại sau mutation.

### 17.1. Test hygiene

- Full IT sạch gần nhất chạy trên fresh disposable `pems_it_regression`; không rerun sau Dev merge do local DB/test-harness constraints đã nêu.
- Real-stack chạy `pems_e2e_realstack`.
- DBs được drop sau run.
- API/Vite test processes bị kill; không đụng dev server của người dùng.
- appsettings.Testing.json được backup byte-exact và trap restore về `database=pems_test`.
- `pems_db` và `pems_test` không mutation.
- `pems_pr3_test` chỉ dùng sanctioned transactional tests và được verify không leak V2/test rows.
- Junction/temp publish/SQL/inbox/profile/log được cleanup.

### 17.2. Known environment limitations

- `npm ci` từng fail do Windows native-file lock/lightningcss EPERM.
- Reproducible fallback đã dùng: `npm install --legacy-peer-deps`.
- Không được tuyên bố `npm ci` pass khi dùng fallback.
- PowerShell classifier đôi lúc unavailable; Bash trap + absolute path được dùng cho appsettings restore, PowerShell cho junction khi có.
- `pems_test` không tồn tại trên máy ở Slice 6c; 25 V2 IT files hardcode `pems_pr3_test`, trong khi DB này là protected và schema 71 tables đã stale so với master 76 tables. Không được recreate protected DB hoặc mass-edit test source chỉ để ép full suite chạy.
- Hai photo-upload Unit failures và guest-search IT conflict đã được sửa; tuy nhiên không được gọi toàn bộ regression green cho checkpoint `a5610e2f` vì full Integration, frontend và real-stack chưa rerun.
- Bốn Phase I disposable drills và schema diffs đều `NOT RUN` do local MySQL drill infrastructure chưa sẵn sàng.

---

## 18. Observability và security

Source chưa có metrics framework chuyên dụng. Quyết định hiện tại:

- structured `ILogger` với stable error codes;
- audit logs có correlation ID;
- không log PII/OTP/token/raw keyword;
- middleware log `ConflictException`/`BusinessRuleException` theo code;
- metrics rollout hiện suy ra từ log/audit;
- ngưỡng canary số cụ thể vẫn chờ Product.

Runbook đã ghi:

- frontend/backend flags OFF ban đầu;
- internal canary;
- exit criteria;
- production rollback bằng flags OFF, không phải DOWN migration.

---

## 19. Những phần đã DONE và chưa DONE

## 19.1. DONE

- Additive SQL V2 + backfill/verify/rollback drills.
- V1/V2 dual-read compatibility.
- V2 create authenticated/public OTP snapshot binding.
- Pending edit và resubmit.
- Initial claim, transfer, resend/replace, cancel-3A.
- Expiry/redaction jobs.
- Safe edit, privacy urgent, amendments, revision/history.
- Class-C consumers V2-safe.
- Scope-safe search + matchedContexts.
- Frontend create/read/edit/resubmit/identity/amendment/history/search.
- Capability-based entry-point cutover.
- Version-aware shared detail/modal.
- Backend allowedActions-driven UI.
- Fail-closed real-stack auth infrastructure.
- Real-host A–H `8/8`.
- Full Browser UI mutation/search workflows `9/9`; tổng real-stack `17/17`.
- Slice 6 DoD hoàn tất.
- Canonical guest-search exclusion đã được khôi phục; Unit/targeted V2 trở lại `530/530` và `45/45`.
- Photo upload `VisitRequestPhoto` trở lại strict 5 MB + image magic-byte validation.
- Phase I candidate files đã được tạo dưới `docs/database/scripts/phase_1_candidate/`.

## 19.2. Chưa DONE

### A. Phase I guarded contract-drop prep

Candidate draft đã có:

- `01_preflight.sql`;
- `02_guarded_up.sql`;
- `03_verify.sql`;
- `04_down_restore.sql`;
- `README.md`.

Nhưng Phase I chưa đạt DoD vì:

- báo cáo zero-unclassified chưa có occurrence counts/file-symbol map đầy đủ;
- candidate chưa được audit độc lập sau khi viết;
- `pems_i_fresh`, `pems_i_upgrade`, `pems_i_refusal`, `pems_i_rollback` đều NOT RUN;
- fresh-vs-upgrade và pre-UP-vs-post-DOWN schema diff NOT RUN;
- chưa có evidence default-deny/refusal không partial DDL;
- frontend/real-stack/full Integration chưa rerun sau R1/R2/candidate commits.

### B. Production rollout

Chưa:

- bật flags staging/canary;
- quan sát thực tế;
- xác nhận zero old client/draft;
- loại bỏ legacy runtime reads/writes;
- chạy contract drop trên DB thật.

Identity claim/transfer real-stack browser journeys là optional increment vì integration coverage đã có, trừ khi H2/Product yêu cầu.

---

## 20. Phase I — trạng thái và thiết kế mong muốn

## 20.1. Readiness hiện tại

| Gate | Trạng thái |
|---|---|
| Zero legacy runtime reads | FAIL |
| Zero legacy runtime writes | FAIL |
| All entry points production cutover | PARTIAL |
| Flags ON/old fallback retired | FAIL |
| Full DOM E2E | PASS (`9/9`, tổng real-stack `17/17`) |
| Backfill/SQL drill nền tảng | PASS |
| Real-host A–H | PASS (`8/8`) |
| Current targeted baseline | PASS (`530/530`, `45/45`) |
| Current full regression gates | INCOMPLETE: full Integration/frontend/real-stack NOT RUN sau latest commits |
| Phase I candidate files | DRAFTED |
| Phase I disposable drills/schema diff | NOT RUN |

Kết luận hiện tại: **NOT READY FOR EXECUTION**.

## 20.2. Candidate package đã draft, cần audit và drill

1. Hoàn tất audit mọi reference của 10 legacy fields với zero-unclassified counts và file/symbol evidence.
2. Phân loại runtime read/write, compatibility, migration-only, test-only và blocker.
3. Review `01_preflight.sql` để chứng minh read-only và đủ guards.
4. Review `02_guarded_up.sql` default-deny:
   - explicit confirmation;
   - kiểm tra mọi precondition trước DDL đầu tiên vì MySQL DDL implicit commit;
   - refuse V1 rows, missing detail, orphan/cross-request và schema drift;
   - chỉ drop đúng 10 fields + actual dependent index/check;
   - không wire vào startup migration.
5. Review `03_verify.sql` với postconditions đầy đủ.
6. Review `04_down_restore.sql`: re-add exact definitions và rebuild compatibility projection deterministic từ smallest campus.
7. Drill trên:
   - `pems_i_fresh`;
   - `pems_i_upgrade`;
   - `pems_i_refusal`;
   - `pems_i_rollback`.

Không chạy trên `pems_db`, `pems_test`, `pems_pr3_test`, staging hoặc production.

Kết luận Phase I hợp lệ dự kiến:

> Guarded contract-drop candidate prepared/tested on disposable databases; execution NOT READY while V1 fallback and legacy runtime references remain. No real database was modified.

---

## 21. Kế hoạch chính xác cho phiên tiếp theo

### Slice tiếp theo: Phase I candidate audit + disposable drills

1. Preflight Git/remote/auto-sync và audit merge descendants.
2. Review diff/content của cả năm Phase I candidate files; không tin chúng chỉ vì đã commit.
3. Hoàn tất zero-unclassified map cho đúng 10 legacy global fields, gồm occurrence counts và file/symbol/caller evidence.
4. Chứng minh readiness hiện tại; dự kiến vẫn `NOT READY FOR EXECUTION` vì flags OFF + V1 fallback + readers/writers + persisted V1 rows còn tồn tại.
5. Xác minh preflight read-only và guarded UP kiểm tra tất cả preconditions trước DDL đầu tiên.
6. Chuẩn bị local/disposable MySQL infrastructure mà không chạm protected DB.
7. Drill chỉ trên `pems_i_fresh`, `pems_i_upgrade`, `pems_i_refusal`, `pems_i_rollback`.
8. So sánh fresh-vs-upgrade và pre-UP-vs-post-DOWN; thu evidence refusal không partial-drop.
9. Rerun Unit/Architecture/targeted/full Integration khi an toàn, Vitest/tsc/build/browser-contract và real-stack `17/17` vì R1 ảnh hưởng search behavior.
10. Cleanup disposables/temp, update reports và commit theo logical slice; không push/merge/PR.

### Sau đó

- Final comprehensive audit.
- Production rollout là bước vận hành riêng, cần Product/authority và monitoring.

Ước lượng từ checkpoint hiện tại:

- R1/R2 reconciliation đã hoàn tất;
- 1–2 phiên Phase I audit/review + disposable drills;
- có thể 1 phiên final audit/fix;
- production canary/drop cần thêm các phiên vận hành riêng.

---

## 22. Definition of Done cuối cùng

Implementation chỉ được gọi hoàn tất khi:

- frontend create/edit/read/identity/amendment/search đầy đủ;
- full DOM journeys cần thiết xanh;
- Unit/Architecture/Integration/Vitest/build/browser/real-stack xanh;
- no hidden-campus leak;
- allowedActions và backend authorization đồng nhất;
- SQL fresh/upgrade/backfill/verify/rollback đã chứng minh;
- Phase I candidate guarded/tested trên disposable;
- không có temp/secret/test data leak;
- reports phản ánh đúng limitation;
- Git sạch ngoài bốn untracked plan/handoff docs.

Production chỉ được gọi chuyển hẳn sang V2 khi thêm các điều kiện vận hành:

- flags được rollout có kiểm soát;
- canary/metrics đạt exit criteria;
- không còn old client/draft;
- zero legacy runtime reads/writes;
- backup/restore/export được xác nhận;
- guarded contract drop được operator chạy có chủ đích;
- post-migration verification xanh.

Không đồng nhất “code complete behind flags” với “production đã xóa V1”.

---

## 23. Checklist bắt đầu nhanh cho Agent mới

Trước khi làm việc:

- [ ] Đọc `FINAL_IMPLEMENTATION_REPORT.md` và `IMPLEMENTATION_PROGRESS.md`.
- [ ] Đọc H2/H3/H4 docs và canonical docs “sau hop 13-07”.
- [ ] Kiểm tra branch/HEAD/upstream/ahead-behind.
- [ ] Xác minh commit `a5610e2f` hoặc forward descendants, gồm pushed merge `64c83a59`.
- [ ] Kiểm tra auto-push/auto-merge.
- [ ] Giữ bốn plan/handoff docs untracked.
- [ ] Không rewrite pushed history.
- [ ] Không kết nối/mutation protected DB.
- [ ] Không bật production flags.
- [ ] Không hồi quy canonical guest-search exclusion hoặc strict VisitRequestPhoto policy.
- [ ] Không coi candidate SQL là tested trước khi đủ bốn disposable drills/schema diffs.
- [ ] Phase I chỉ chuẩn bị/test candidate trên disposable DB; không execute trên DB thật.
- [ ] Commit bằng Tcanh12, không AI attribution.

Điểm resume ngắn gọn:

> R1/R2 đã hoàn tất và candidate SQL đã được draft. Tiếp theo phải audit độc lập candidate + hoàn tất zero-unclassified map, rồi chạy đủ `pems_i_fresh/upgrade/refusal/rollback`, schema diffs và các regression gates còn thiếu. Execution vẫn NOT READY khi V1 fallback, persisted V1 data và legacy readers/writers còn tồn tại.
