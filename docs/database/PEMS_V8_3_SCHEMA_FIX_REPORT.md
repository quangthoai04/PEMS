# PEMS v8.3 Schema Fix Report

## 1. Vấn đề

File `DATABASE_SCHEMA` trước đó bị thiếu nhiều phần vì được cập nhật theo kiểu tài liệu thay đổi/summary từ v8/v8.2, không được dựng lại đầy đủ từ SQL full mới nhất. Vì vậy một số phần table detail, trigger/view, rule hủy sau duyệt và các cột mới không được phản ánh đầy đủ.

## 2. Cách sửa

Đã dựng lại file schema đầy đủ từ nguồn chuẩn:

```text
pems_full_sql_42tables_final_v8_3_cancel_after_approval_full_create.sql
```

File mới:

```text
DATABASE_SCHEMA_FULL_UPDATED_V8_3_CANCEL_AFTER_APPROVAL.md
```

## 3. Logic v8.3 đã phản ánh

- `visit_requests.status` chỉ lưu trạng thái đơn: `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED`.
- `CANCELLED` chỉ dùng sau khi đơn đã `APPROVED`.
- Trước khi duyệt, nếu khách muốn hủy/không đi nữa, HO hoặc Staff Leader dùng luồng `REJECTED` và ghi lý do trong `decision_note`.
- Sau khi duyệt, nếu khách tự vào hệ thống thì dùng UC-136 `SELF_SERVICE`.
- Sau khi duyệt, nếu khách không vào hệ thống nhưng xác nhận với host qua kênh ngoài thì host dùng UC-136 `EXTERNAL_CONFIRMATION`.
- Không còn cột `external_confirmation_note`; mọi ghi chú xác nhận ngoài hệ thống ghi vào `cancellation_reason`.
- Không có `actual_start_at` / `actual_end_at` trong `visit_request_campuses`.
- UC-136 thuộc `Delegation Reception Management`.

## 4. Kiểm tra tĩnh

| Check | Result |
|---|---:|
| Base tables parsed | 42 |
| Views parsed | 6 |
| Triggers parsed | 19 |
| `external_confirmation_note` occurrences | 0 |
| `UC-136.CANCEL_VISIT_REQUEST` occurrences | 8 |
| `actual_start_at` / `actual_end_at` columns | 0 |

## 5. Lưu ý

Môi trường hiện tại không có MySQL server nên chỉ kiểm tra tĩnh bằng parser/text. Cần chạy file SQL full trên MySQL database rỗng để kiểm tra runtime syntax/constraint/trigger.
