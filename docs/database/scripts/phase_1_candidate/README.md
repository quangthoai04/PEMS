# Phase I Guarded Contract-Drop Candidate

## Mục đích
Bộ scripts này là **candidate** để chuẩn bị loại bỏ 10 trường dữ liệu legacy V1 khỏi bảng `visit_requests`.
Hiện tại, hệ thống đang ở trạng thái **NOT READY FOR EXECUTION** do vẫn còn dual-read và compatibility write. Bộ script này CHỈ ĐƯỢC CHẠY trên các disposable database có tiền tố `pems_i_` (như `pems_i_fresh`, `pems_i_upgrade`, `pems_i_rollback`) để drill test.
TUYỆT ĐỐI KHÔNG CHẠY TRÊN `pems_db`, `pems_test` hay `pems_pr3_test`.

## Các file scripts
1. `01_preflight.sql`: Kiểm tra DB environment (chỉ cho phép `pems_i_%`), kiểm tra schema.
2. `02_guarded_up.sql`: Default-deny script. Yêu cầu bật cờ `@ENABLE_PHASE_1_DROP = 1` để thực hiện drop 10 legacy columns.
3. `03_verify.sql`: Xác minh sau khi up, đảm bảo không có orphan data và 10 cột đã bị xoá.
4. `04_down_restore.sql`: Restore compatibility. Thêm lại 10 cột và tái tạo dữ liệu dựa trên projection từ detail V2.

## 10 Legacy Fields bị ảnh hưởng:
- delegation_name
- visit_type
- visit_type_other
- purpose
- working_content
- working_language
- transportation_note
- media_consent_status
- media_consent_note
- note_to_fptu
