# TASK — PEMS AUTH/SECURITY DATABASE SCHEMA CLEANUP WITHOUT BREAKING RUNTIME

Bạn đang làm việc trên project **PEMS**.

## 0. MỤC TIÊU

Thực hiện **cleanup thật** cho nhóm bảng Người dùng + Xác thực dựa trên **2 file audit đã chốt đính kèm** và **code hiện tại trên nhánh Dev**, với mục tiêu:

1. Xóa các cột/ENUM/index/FK/trigger legacy hoặc dư thừa đã được chốt.
2. Sửa toàn bộ backend/frontend/tests/config/constants liên quan để code **không còn tham chiếu schema cũ**.
3. Tạo **SQL update/migration có thể chạy trên database hiện tại**.
4. Cập nhật **SQL master/canonical `PEMS_FULL_VS_31_07_NEW...sql`** để fresh import tạo ra schema mới ngay từ đầu.
5. Đảm bảo sau thay đổi các flow đang hoạt động **không bị hỏng**: credentials login DEV/test, Google SSO, refresh token, logout, session validation/revoke, forgot/reset password OTP, Visit Request V2 OTP, Admin Login Logs, Admin Session Management, Admin Security Monitoring, campus disable/revoke session, các security-policy flows.

**Không chỉ sửa SQL. Phải sửa đồng bộ code + SQL + tests + DTO/API/UI nếu các field bị xóa đang được map/return/filter/display.**

---

# 1. SOURCE OF TRUTH VÀ NGUYÊN TẮC

Đọc kỹ trước khi sửa:

- File audit chốt số 1: nhóm `otp_tokens`, `login_logs`, `security_events`.
- File audit chốt số 2: nhóm `users`, `user_auth_providers`, `user_sessions` và FEID cleanup.
- Code hiện tại trên branch `Dev`.
- SQL master hiện tại `PEMS_FULL_VS_31_07_NEW*.sql`.

Thứ tự ưu tiên:

1. **Hai file audit đính kèm xác định TARGET SCHEMA đã chốt.**
2. **Code Dev xác định tất cả chỗ phải sửa để đạt target mà không phá runtime.**
3. SQL master hiện tại là baseline để tạo migration chính xác.

Không dùng global text match kiểu cùng tên field ở entity khác để kết luận usage. Chỉ tính usage khi xác định được đúng entity/query/table/module.

Nếu gặp mục chỉ được ghi là `candidate`, `có thể xóa`, `nếu cần` mà **không nằm trong danh sách bắt buộc bên dưới**, không tự ý mở rộng scope. Báo cáo riêng, không drop.

---

# 2. PHẠM VI THAY ĐỔI BẮT BUỘC

## 2.1 `users`

### DROP bắt buộc

- `fe_id`
- `email_verified_at`
- `first_login_at`

### Đồng bộ code

- Xóa property/mapping/entity/config tương ứng.
- Xóa mọi assignment/read/test liên quan.
- Xóa `uq_users_fe_id` hoặc index/constraint nào phụ thuộc `fe_id`.
- Google SSO không còn ghi `EmailVerifiedAt`.
- Credentials/SSO không còn ghi `FirstLoginAt`.
- Giữ `last_login_at`; dùng `last_login_at IS NULL` khi cần xác định user chưa từng login.

### Không được xóa

- `password_hash`
- `failed_login_count`
- `locked_until`
- `last_login_at`
- `created_by`
- `updated_by`
- `primary_campus_id`
- `roles.status` không nằm trong cleanup bắt buộc của task này.

---

## 2.2 XÓA FEID END-TO-END

PEMS hiện tại không có FEID product flow thực tế. Xóa FEID đồng bộ, không chỉ xóa ENUM DB.

Tìm và loại bỏ nếu tồn tại:

- `/api/auth/feid` hoặc endpoint FEID tương đương.
- `LoginViaFeid/*`.
- `IFeidIdentityVerifier` / `FeidIdentityVerifier`.
- `ProviderTypes.FeId`.
- `AllowFeid`, FEID auth options/config.
- FEID-specific validation/errors/messages/tests/docs/seeds.
- Text UI/backend kiểu `SSO/FEID`, `Please use SSO/FEID`, v.v. sửa cho đúng Google/password hiện tại.
- Mọi FEID branch trong sync/provider/account code.

Sau cleanup không còn production code path hoặc DB enum value `FEID`.

---

## 2.3 `user_auth_providers`

### TARGET COLUMNS

Chỉ còn:

- `auth_provider_id`
- `user_id`
- `provider_type`
- `provider_subject`
- `linked_at`

### DROP bắt buộc

- `provider_email`
- `is_enabled`
- `last_used_at`

### `provider_type`

Đổi từ:

```sql
ENUM('LOCAL_PASSWORD','GOOGLE_SSO','FEID')
```

thành:

```sql
ENUM('LOCAL_PASSWORD','GOOGLE_SSO')
```

### Giữ

- `PRIMARY KEY (auth_provider_id)`
- `uq_user_auth_provider_type (user_id, provider_type)`
- `uq_auth_provider_subject (provider_type, provider_subject)`
- `fk_auth_providers_user`

### DROP index

- `idx_auth_provider_email`
- `idx_auth_provider_type_email_enabled`

### Trigger

Giữ nhưng sửa:

- `trg_auth_providers_validate_bi`
- `trg_auth_providers_validate_bu`

Target logic:

```sql
IF NEW.provider_type = 'GOOGLE_SSO'
   AND (NEW.provider_subject IS NULL OR TRIM(NEW.provider_subject) = '')
THEN
    SIGNAL SQLSTATE '45000'
    SET MESSAGE_TEXT = 'GOOGLE_SSO provider_subject is required';
END IF;
```

`LOCAL_PASSWORD` được phép có `provider_subject = NULL`.

### Đồng bộ code rất quan trọng

- Xóa toàn bộ `ProviderEmail` assignment/read.
- Xóa toàn bộ `LastUsedAt` assignment/read.
- Xóa `IsEnabled` property và tất cả `if (!provider.IsEnabled)` / set true.
- **Không được vô tình thay đổi semantics credentials login:** nếu code hiện tại cho phép password login khi `password_hash` hợp lệ dù local provider row chưa tồn tại, cleanup này không được tự ý biến provider row thành requirement mới trừ khi có business rule rõ ràng.
- Google vẫn phải bind và kiểm tra `provider_subject` (`sub`) như hiện tại.
- Google provider thiếu thì vẫn link theo behavior hiện tại; subject mismatch vẫn phải reject.

---

## 2.4 `user_sessions`

### DROP bắt buộc

- `selected_campus_id`
- `refresh_expires_at`
- `refresh_revoked_at`

### Giữ

- `session_id`
- `user_id`
- `login_portal`
- `auth_provider_id`
- `refresh_token_hash`
- `ip_address`
- `user_agent`
- `created_at`
- `expires_at`
- `revoked_at`
- `revoked_by`
- `revoked_reason`

### Runtime consolidation

Sau cleanup:

- Refresh validity dùng `expires_at`.
- Session/refresh revoke dùng `revoked_at`.
- Không còn logic mirror:
  - `refresh_expires_at = expires_at`
  - `refresh_revoked_at = revoked_at`
- Refresh lookup phải dùng `refresh_token_hash + revoked_at + expires_at`.

### Constraints/index

Giữ:

- PK `session_id`
- `uq_sessions_refresh_hash`
- `idx_sessions_user_active`
- `idx_sessions_expires_at`
- `idx_sessions_revoked_at`
- `fk_sessions_user`
- `fk_sessions_auth_provider`
- `fk_sessions_revoked_by`

Xóa:

- `fk_sessions_selected_campus`
- `idx_sessions_refresh_active`

Sửa/xóa:

- `idx_sessions_portal_campus`: bỏ `selected_campus_id`; chỉ tạo replacement index portal nếu query thực tế/EXPLAIN cho thấy cần.
- `trg_sessions_validate_bi`: bỏ toàn bộ logic selected campus, chỉ giữ validation portal-role nếu trigger vẫn có giá trị.

Không tự ý drop `idx_sessions_ip_time` trong task này nếu chưa chứng minh bằng query thực tế/EXPLAIN; chỉ report nếu dư.

---

## 2.5 `otp_tokens`

### DROP bắt buộc

- `token_type`
- `last_attempt_at`
- `human_verified_at`
- `resend_count`

### Đồng bộ code

- Xóa `OtpToken.TokenType`.
- Xóa `OtpTokenTypes.OtpCode` nếu constant chỉ tồn tại để set field đã drop.
- Xóa `MAGIC_LINK` khỏi schema/constants/tests/docs.
- Xóa mọi write `LastAttemptAt`, `HumanVerifiedAt`, `ResendCount`.
- Cooldown/retry phải tiếp tục dựa trên:
  - `attempt_count`
  - `next_attempt_allowed_at`
  - `max_attempts`
  - `human_verification_required_at`
  - `invalidated_at`
- Resend/rate limit tiếp tục đếm row theo `email + purpose + created_at`/`issue_reason`, không phụ thuộc `resend_count`.

### Giữ

- `otp_token_id`
- `user_id`
- `email`
- `purpose`
- `token_hash`
- `challenge_token_hash`
- `submission_id`
- `issue_reason`
- `expires_at`
- `used_at`
- `attempt_count`
- `next_attempt_allowed_at`
- `human_verification_required_at`
- `invalidated_at`
- `invalidation_reason`
- `max_attempts`
- `ip_address`
- `user_agent`
- `created_at`

### `purpose`

Giữ đúng 2 value đang dùng:

```sql
ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')
```

### Index/constraint

Giữ:

- PK
- `uq_otp_tokens_hash`
- `uq_otp_challenge_token_hash`
- `idx_otp_email_purpose_time (email,purpose,created_at)`
- `fk_otp_tokens_user`

Drop bắt buộc:

- `idx_otp_submission`
- `idx_otp_email_purpose_active_v2`
- `idx_otp_issue_limit`
- `idx_otp_user_purpose_active`

`idx_otp_email_purpose_active` và `idx_otp_ip_time` không tự ý drop nếu chưa xác minh chính xác query plan/consumer; report riêng nếu redundant.

---

## 2.6 `login_logs`

### DROP bắt buộc

- `session_id`
- `selected_campus_id`

### `provider_type`

Giữ field nhưng chỉ còn:

```sql
ENUM('LOCAL_PASSWORD','GOOGLE_SSO')
```

Xóa `FEID`.

### Giữ

- `login_log_id`
- `user_id`
- `email`
- `login_portal`
- `provider_type`
- `status`
- `failure_reason`
- `ip_address`
- `user_agent`
- `created_at`

### `status`

Giữ đủ:

```sql
ENUM('SUCCESS','FAILED','BLOCKED')
```

Không xóa value nào.

### Đồng bộ backend/frontend

- `SecurityAuditService.WriteLoginLogAsync` bỏ parameter `selectedCampusId` và `sessionId` nếu không còn dùng cho login log.
- Sửa toàn bộ caller credentials/Google tương ứng.
- Xóa property `LoginLog.SelectedCampusId`, `LoginLog.SessionId`.
- Xóa DTO/API/frontend fields/filter/display liên quan 2 field này.
- Admin Login Logs vẫn phải hoạt động với email/status/portal/provider/IP/date.

### FK/index

Xóa:

- `fk_login_logs_campus`
- `idx_login_logs_portal_campus`

Giữ:

- `fk_login_logs_user`
- `idx_login_logs_provider_time`

Tối ưu index theo query thật:

- Ưu tiên `idx_login_logs_created_status (created_at,status)` cho dashboard/activity nếu EXPLAIN xác nhận có lợi.
- `idx_login_logs_user_time`, `idx_login_logs_email_status_time`, `idx_login_logs_ip_status_time`: chỉ drop khi đã xác nhận không có exact query phụ thuộc và không gây regression performance. Nếu drop, ghi rõ bằng chứng query/EXPLAIN trong report.

---

## 2.7 `security_events`

### TARGET COLUMNS

Giữ:

- `security_event_id`
- `user_id`
- `email_snapshot`
- `event_type`
- `result`
- `failure_reason_code`
- `login_portal`
- `ip_address`
- `user_agent`
- `detail_text`
- `created_at`

### DROP bắt buộc

- `severity`
- `selected_campus_id`
- `provider_type`
- `session_id`

### Vì drop `severity`

Phải sửa đồng bộ backend/frontend:

- Xóa Severity khỏi entity/DTO/query/filter/UI.
- Xóa Security Overview/Dashboard logic đếm `LOW/MEDIUM/HIGH/CRITICAL`.
- Xóa High/Critical cards/filter nếu UI hiện có.
- Không tạo metric thay thế tùy ý nếu chưa có business requirement; giữ Security Monitoring dựa trên event type/result/portal/IP/date.

### Vì drop `provider_type`

- Xóa provider filter/column khỏi Security Monitoring/API/DTO.
- Provider vẫn được audit đầy đủ ở `login_logs`; không duplicate ở `security_events`.

### Vì drop `selected_campus_id`

- Xóa FK/index/property/parameter tương ứng khỏi security event.
- Các event campus-specific đang có `campusId` trong `detail_text` tiếp tục giữ context đó.
- Không đổi sang cột `campus_id` mới trong task này.

### Vì drop `session_id`

- Xóa FK/index/property/DTO/API field.
- Không cố gắn session id mới vào producer; target là remove field.

### `event_type`

Target fresh schema chỉ giữ các value có producer production hiện tại:

```sql
ENUM('SSO_LOGIN','SESSION_REVOKED','SECURITY_POLICY_CHECK')
```

Xóa legacy/dead values khỏi constants/tests/docs/code nếu không còn producer:

- `PORTAL_VALIDATION`
- `CAMPUS_VALIDATION`
- `VISITOR_AUTO_PROVISION`
- `SESSION_CREATED`
- `SESSION_EXPIRED`
- `TOKEN_REFRESH`

### `result`

Giữ đủ:

```sql
ENUM('SUCCESS','FAILED','BLOCKED')
```

### `failure_reason_code`

Ưu tiên chuyển từ ENUM cứng sang:

```sql
VARCHAR(80) NULL
```

Lý do: đây là machine-readable security code có thể mở rộng; chuyển sang VARCHAR cũng bảo toàn historical rows an toàn hơn việc ép một enum nhỏ.

Code constants chỉ nên còn các code production thực sự đang phát sinh, tối thiểu:

- `ACCOUNT_NOT_FOUND`
- `ACCOUNT_DISABLED`
- `SSO_PROVIDER_ERROR`
- `INVALID_SSO_CLAIMS`
- `VISITOR_AUTO_PROVISION_DISABLED`

Các code legacy không còn producer phải xóa khỏi constants/switch/tests nếu thực sự không dùng:

- `PORTAL_MISMATCH`
- `CAMPUS_MISMATCH`
- `ROLE_MISMATCH`
- `SESSION_EXPIRED`
- `TOKEN_REVOKED`
- `SUSPICIOUS_IP`
- `UNKNOWN`

Không xóa historical row chỉ vì chứa code cũ. Migration phải bảo toàn dữ liệu.

### Index/FK

Giữ:

- PK
- `fk_security_events_user`
- `idx_security_type_result_time` nếu query hiện tại vẫn match.

Drop bắt buộc do column bị xóa:

- `idx_security_portal_campus_time`
- `idx_security_severity_time`
- `idx_security_session_time`
- `fk_security_events_selected_campus`
- `fk_security_events_session`

Các index `idx_security_user_time`, `idx_security_email_time`, `idx_security_failure_reason_time`, `idx_security_ip_time` chỉ drop khi đã xác minh query shape/EXPLAIN và ghi bằng chứng. Không được xóa bừa chỉ vì field vẫn còn nhưng index có vẻ ít dùng.

---

# 3. THỨ TỰ TRIỂN KHAI BẮT BUỘC ĐỂ KHÔNG BREAK EF/RUNTIME

Không được DROP DB trước rồi để Entity Framework vẫn SELECT column đã xóa.

Thực hiện theo thứ tự an toàn:

## Phase A — Preflight / inventory

1. Xác nhận branch/HEAD.
2. Xác định chính xác SQL master authoritative.
3. Search entity-qualified cho tất cả field/enum/index/trigger bị xóa.
4. Lập bảng dependency:
   - Entity/property
   - DbContext/config
   - Repository/query
   - Command/handler/service
   - API DTO/controller
   - frontend types/API/filter/UI
   - test/seed/docs
   - SQL index/FK/trigger
5. Không sửa gì ngoài scope nếu không cần để compile/runtime.

## Phase B — Code compatibility cleanup

Sửa code để **không còn cần các cột sắp drop**:

- remove properties/mappings
- remove parameters/assignments/reads
- simplify services/handlers
- remove dead enum/constants/routes
- update DTO/frontend
- update tests

Sau Phase B phải build/typecheck được với target model.

## Phase C — SQL migration/update script

Tạo một script executable, ví dụ:

`docs/database/scripts/auth_schema_cleanup/PEMS_AUTH_SCHEMA_CLEANUP_UP.sql`

Script phải có các section rõ ràng:

1. **PRECHECK**
2. **DROP dependent FK/index/trigger**
3. **ALTER/DROP columns**
4. **ALTER ENUM/type**
5. **RECREATE changed trigger/index**
6. **VERIFY**

Nếu MySQL không cho alter enum vì đang có legacy value, script **không được silently DELETE/convert historical data**. Phải:

- kiểm tra count trước;
- với FEID/provider legacy: fail-fast bằng precheck nếu còn row và report cách xử lý;
- với `security_events.failure_reason_code`: dùng `VARCHAR(80)` để bảo toàn history;
- với audit log enum lịch sử khác, không tự rewrite row.

Drop FK/index trước khi drop column.

## Phase D — Update SQL master

Cập nhật trực tiếp `PEMS_FULL_VS_31_07_NEW*.sql` để fresh import phản ánh **target schema cuối**, bao gồm:

- CREATE TABLE columns
- ENUM values
- indexes
- foreign keys
- triggers
- comments
- seeds nếu liên quan

Không được chỉ tạo migration mà quên master DDL.

## Phase E — Verification/gates

Chạy ít nhất:

### Static search gate

Không còn production references tới:

```text
users.fe_id
users.email_verified_at
users.first_login_at
ProviderEmail
IsEnabled (UserAuthProvider)
LastUsedAt (UserAuthProvider)
user_sessions.selected_campus_id
refresh_expires_at
refresh_revoked_at
otp_tokens.token_type
last_attempt_at
human_verified_at
resend_count
login_logs.selected_campus_id
login_logs.session_id
security_events.severity
security_events.selected_campus_id
security_events.provider_type
security_events.session_id
FEID
MAGIC_LINK
```

Lưu ý tránh false positive từ entity khác cùng tên.

### Backend gates

- restore/build
- unit tests
- architecture tests nếu có
- integration tests liên quan

### Frontend gates

- lint
- typecheck
- build
- unit tests affected admin/auth pages

### Database gates

1. Fresh import updated master SQL vào DB sạch.
2. Apply migration lên bản copy/schema clone của DB hiện tại.
3. Verify INFORMATION_SCHEMA:
   - dropped columns thực sự biến mất
   - enum/type đúng
   - FK/index/trigger đúng
4. Không có orphan FK hoặc invalid trigger.

### Runtime smoke flows bắt buộc

1. Credentials login success.
2. Credentials login wrong password.
3. Lockout/BLOCKED path.
4. Google SSO success.
5. Google subject mismatch/reject.
6. Google auto-provision visitor nếu flow hiện tại cho phép.
7. Refresh access token.
8. Logout + session revoke.
9. Middleware/session validation active/expired/revoked.
10. Admin Session Management list/filter.
11. Forgot Password OTP create/verify/reset.
12. Visit Request V2 OTP initiate/resend/recovery/verify.
13. Admin Login Logs list/filter: keyword/status/portal/provider/IP/date.
14. Admin Login Activity/dashboard login success/failed counts.
15. Admin Security Monitoring list/filter còn lại: keyword/result/event type/portal/IP/date.
16. Campus disable flow vẫn revoke session và ghi AuditLog + SecurityEvent.
17. Replace Staff Leader LOCKED flow nếu hiện tại có security event.

---

# 4. DATA SAFETY

Trước mọi destructive migration:

- Backup schema/data hoặc tạo clone DB.
- PRECHECK phải report row counts cho các giá trị legacy cần loại bỏ.
- Không `DELETE FROM ...` chỉ để ALTER ENUM chạy được nếu chưa có quyết định business rõ.
- Không rewrite historical audit/security/login data chỉ để schema đẹp hơn.
- Với cột drop, chấp nhận mất dữ liệu cột đó vì đã được audit/chốt là dư; nhưng phải verify code không còn consumer trước khi drop.

---

# 5. KHÔNG ĐƯỢC LÀM

- Không đổi architecture ngoài phạm vi cleanup này.
- Không tự thêm FEID replacement.
- Không đổi credentials login DEV/test thành production-only Google trong task này.
- Không xóa `LOCAL_PASSWORD`.
- Không xóa `login_portal`.
- Không xóa `provider_subject`.
- Không xóa `linked_at`.
- Không xóa `revoked_by`/`revoked_reason`.
- Không xóa `otp_tokens.invalidation_reason`, `ip_address`, `user_agent` trong task này.
- Không xóa cả bảng `login_logs`, `audit_logs`, `security_events`.
- Không gộp 3 bảng audit vào một bảng.
- Không drop candidate indexes ngoài danh sách bắt buộc nếu chưa có evidence/EXPLAIN.
- Không để backend compile bằng cách comment/hard-code bỏ logic security.
- Không để frontend chỉ ẩn field trong khi API/entity vẫn lệch schema.

---

# 6. OUTPUT BẮT BUỘC

Khi hoàn tất, trả về:

## A. Change summary

Theo từng bảng:

```text
TABLE
- dropped columns
- altered enums/types
- dropped/added indexes
- dropped/changed FKs
- changed triggers
- code files updated
```

## B. Migration SQL

Đường dẫn file SQL update đã tạo.

Phải cho biết:

- có precheck gì
- có destructive changes gì
- migration có idempotent hay không
- yêu cầu backup gì

## C. Master SQL

Xác nhận exact path của `PEMS_FULL_VS_31_07_NEW*.sql` đã cập nhật.

## D. Removed code inventory

Liệt kê:

- FEID files/constants/options/routes removed
- removed entity properties
- removed DTO/frontend fields
- removed dead enum constants

## E. Verification

Bảng PASS/FAIL:

```text
Backend build
Backend unit
Backend integration
Frontend lint
Frontend typecheck
Frontend build
Frontend unit
Fresh DB import
Migration-on-clone
Auth smoke
OTP smoke
Admin login logs
Admin sessions
Admin security monitoring
Campus disable security consequence
```

Không ghi `PASS` nếu chưa chạy thật.

## F. Remaining debt

Chỉ liệt kê những mục chưa chốt hoặc chưa thể xóa an toàn, ví dụ candidate indexes. Không được trộn chúng với phần đã hoàn thành.

---

# 7. DEFINITION OF DONE

Task chỉ được coi là xong khi đồng thời đạt:

1. Code không còn reference tới column/enum đã drop.
2. Updated master SQL fresh import thành công.
3. Migration chạy thành công trên DB clone hiện tại.
4. EF/runtime không phát sinh `Unknown column`, FK/index/trigger error.
5. Credentials login DEV/test vẫn chạy.
6. Google SSO + provider_subject binding vẫn chạy.
7. Refresh/logout/session validation vẫn chạy.
8. OTP password + Visit V2 vẫn chạy.
9. Login Logs/Admin Sessions/Security Monitoring vẫn mở và query được sau khi bỏ field.
10. Không còn FEID production path/value.
11. Không có test/build gate mới fail do cleanup.
12. Có report cuối cùng nêu rõ file nào đã sửa và bằng chứng gate.

**Không dừng ở việc “SQL đã sửa”. Mục tiêu là code + schema + runtime đồng bộ hoàn toàn và không phá business logic hiện tại.**
