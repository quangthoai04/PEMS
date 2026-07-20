# Phase I Guarded Contract-Drop Candidate

## Trạng thái

`IN PROGRESS — candidate hardened; refusal/upgrade/rollback drills PASSED on disposable
databases; fresh-target drill NOT RUN; contract-drop NOT READY FOR EXECUTION.`

Chưa READY vì **10 runtime blocker sites vẫn còn sống ở HEAD** (xem
`docs/ChangeSauHopChiQUyen/sauhop_13-07/PHASE_I_AUDIT_REPORT.md` §3) và **backfill v1→v2 trên dữ
liệu đã lưu chưa chạy / chưa được chứng minh**. Không đặt dấu ✅ COMPLETE cạnh trạng thái này.

## Phạm vi database — EXACT ALLOWLIST (không dùng prefix)

Chỉ bốn tên sau, so khớp **chính xác** (không phải `pems_i_%`, vì prefix match sẽ cho phép một DB
ngoài ý muốn như `pems_i_anything`):

- `pems_i_fresh`
- `pems_i_upgrade`
- `pems_i_refusal`
- `pems_i_rollback`

**TUYỆT ĐỐI KHÔNG CHẠY TRÊN `pems_db`, `pems_test`, `pems_pr3_test`** hay bất kỳ database thật nào.
Rollback ở production là **tắt feature flags**, không phải chạy DOWN destructive.

## Nguyên tắc thiết kế: read-only gate TRƯỚC, DDL payload SAU

- `run_migration.ps1 -Action Up` **luôn** chạy `01_preflight.sql` trước; nếu verdict không phải
  `PHASE1_PREFLIGHT_RESULT: PASS` hoặc mysql exit code khác 0 thì payload **không được chạy**
  (exit 1, zero mutation).
- `02_guarded_up.sql` / `04_down_restore.sql` **tự từ chối** nếu thiếu cờ enable
  (`@ENABLE_PHASE_1_DROP` + `@PHASE1_PREFLIGHT_OK`, hoặc `@ENABLE_PHASE_1_RESTORE`) hoặc DB không
  nằm trong allowlist — kể cả khi ai đó chạy thẳng `mysql < 02_guarded_up.sql`.
- MySQL DDL **auto-commit**: mọi guard phải nằm trước lệnh `ALTER` đầu tiên; không có "rollback
  transaction" cho phần DDL.
- Runner không in `PASSED` trước khi các gate thực sự pass, và luôn chạy `03_verify.sql` sau UP/DOWN.

## Các file

| File | Vai trò |
|---|---|
| `01_preflight.sql` | **Read-only gate**. Exact allowlist, version check bằng số (không so chuỗi), exact 10 column definitions, 3 dependent indexes, FULLTEXT có `delegation_name`, resolve CHECK `visit_type` **theo expression + assert duy nhất**, data readiness (fsv=2, detail/instance, orphan, projection parity), runtime-blocker acknowledgement. Kết thúc bằng `PHASE1_PREFLIGHT_RESULT: PASS|FAIL`. |
| `02_guarded_up.sql` | Payload destructive. Guard fail-closed, drop đúng CHECK đã resolve (**không dùng `LIMIT 1`**), rebuild FULLTEXT bỏ `delegation_name`, drop 2 secondary indexes, drop 10 cột. |
| `03_verify.sql` | **Read-only verify** hai chế độ (`@PHASE1_VERIFY_MODE = 'UP' \| 'DOWN'`). Kết thúc bằng `PHASE1_VERIFY_RESULT: PASS\|FAIL`. |
| `04_down_restore.sql` | Restore. Thêm lại 10 cột đúng định nghĩa **và đúng ordinal position** (`AFTER`), backfill từ detail v2, **fail closed nếu backfill không đủ** (không bịa `'N/A'`), khôi phục indexes + CHECK + FULLTEXT. |
| `run_migration.ps1` | Runner fail-closed (ASCII-only cho PowerShell 5.1). |
| `generate_fresh_target.ps1`, `00_fresh_target.sql` | **CHƯA TIN CẬY** — generator dùng blind regex rewrite, artifact chưa từng import/verify. Xem §9 của audit report. |

## Cách chạy (disposable only)

```powershell
$env:MYSQL_BIN = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
$env:MYSQL_PASSWORD = '<password>'   # hoặc dùng -MysqlPassword

.\run_migration.ps1 -DbName pems_i_upgrade  -Action Preflight
.\run_migration.ps1 -DbName pems_i_upgrade  -Action Up -OverrideBlockers
.\run_migration.ps1 -DbName pems_i_upgrade  -Action Down
.\run_migration.ps1 -DbName pems_i_fresh    -Action Verify -VerifyMode UP
```

`-OverrideBlockers` chỉ là **acknowledgement cho drill trên disposable** rằng runtime V1
dependencies vẫn tồn tại. Nó **không bao giờ** là bằng chứng production đã sẵn sàng.

## Kết quả drill đã chạy thật

| Drill | Database | Kết quả |
|---|---|---|
| Refusal | `pems_i_refusal` | **PASS** — preflight FAIL → payload không chạy, exit 1, fingerprint không đổi (47 cols / 21 indexes / 7 CHECKs) |
| Upgrade | `pems_i_upgrade` | **PASS** — 12/12 gate PASS, drop đúng `visit_requests_chk_7`, verify(UP) PASS |
| Rollback | `pems_i_upgrade` | **PASS** — verify(DOWN) PASS, schema FP `4b6b715e…` và data FP `aa157ae8…` **trùng khớp trước UP** |
| Fresh target | — | **NOT RUN** (artifact chưa tin cậy) |

## 10 legacy fields trong phạm vi

`delegation_name`, `visit_type`, `visit_type_other`, `purpose`, `working_content`,
`working_language`, `transportation_note`, `media_consent_status`, `media_consent_note`,
`note_to_fptu`. (Các trường operational-contact **không** thuộc phạm vi.)
