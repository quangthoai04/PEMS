# Gallery Bilingual Audio — Database Migration

Phần thay đổi DB đi kèm tính năng **Gallery song ngữ (VI/EN mô tả + audio thủ công)**,
thay thế cơ chế EverAI TTS cũ. Có **2 cách** để đưa thay đổi này vào DB tuỳ theo tình
trạng DB hiện tại của bạn.

---

## Cách 1 — Import mới hoàn toàn (fresh import)

Dùng khi bạn setup DB từ đầu hoặc muốn làm sạch.

- Chạy file full-schema: `../PEMS_FULL_V11_EXPENSE_COMPATIBILITY_FIXED_V3.sql`
- File này **đã được hợp nhất**: chứa cả phần Expense/Per-campus v2 (từ nhánh Dev)
  **và** phần Gallery song ngữ. Sau khi import xong bạn không cần chạy thêm gì trong
  thư mục này.
- ⚠️ File có `DROP DATABASE IF EXISTS pems_db;` ở đầu — nó sẽ **xoá sạch** DB `pems_db`
  hiện có. Chỉ dùng khi bạn thật sự muốn tạo lại từ đầu.

## Cách 2 — Nâng cấp tại chỗ (in-place upgrade)

Dùng khi bạn **đã có DB Dev đang chạy** (bản V11 cũ: còn bảng `gallery_item_tts_audios`
và cột `gallery_items.description`) và **không muốn mất dữ liệu** bằng cách import lại.

Chạy **lần lượt theo đúng thứ tự** trên DB hiện tại (`pems_db`):

| # | File | Tác dụng |
|---|------|----------|
| 1 | `2026_07_17_A_gallery_item_contents_additive.sql` | Tạo bảng `gallery_item_contents` (1:1 với `gallery_items`): `description_vi/en` + `audio_vi/en_file_id`. Thuần additive, không phá gì. |
| 2 | `2026_07_17_B_gallery_tts_cleanup.sql` | Xoá bảng `gallery_item_tts_audios`, xoá cột `gallery_items.description`, và **xoá các gallery item seed cũ không có nội dung song ngữ** (cascade media). |
| 3 | `2026_07_18_audit_schema_drift_patch.sql` | Thêm các cột audit (`correlation_id`, …) nếu DB import từ base cũ còn thiếu. Idempotent — chạy thừa cũng vô hại. |

Cả 3 script đều **idempotent** (tự kiểm tra `information_schema` trước khi ALTER),
nên chạy lại nhiều lần vẫn an toàn.

> ⚠️ Lưu ý về script #2: mọi gallery item **chưa có** bản ghi `gallery_item_contents`
> tương ứng sẽ bị xoá (kèm media). Đây là chủ đích — mỗi item giờ bắt buộc phải có
> mô tả + audio song ngữ. Nếu DB của bạn có item thật cần giữ, hãy tạo nội dung song
> ngữ cho chúng qua ứng dụng **trước khi** chạy script #2.

---

## Đã kiểm chứng

Cả hai cách đã được test và cho ra **schema giống hệt nhau** (đối chiếu từng cột của
`gallery_items` + `gallery_item_contents`, và toàn bộ 76 bảng trùng khớp):

- Cách 1: import full-schema V11 đã hợp nhất → sạch.
- Cách 2: import V11 gốc của Dev (còn tts + description) → chạy A → B → audit patch →
  hội tụ về đúng cùng một schema với Cách 1.
