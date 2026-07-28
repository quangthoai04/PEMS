---
type: merge-audit
feature: department-reception-tasks
status: final
updated: 2026-07-29
links:
  - docs/merge-audit/2026-07-28_P0_HANDLER_REVIEW.md
  - docs/merge-audit/2026-07-29_LATEST_DEV_DELTA_REVIEW.md
---

# Logistics assign — HTTP 500 root cause

> Điều tra sự cố `POST /api/department-reception-tasks/{id}/assign` trả về **500 INTERNAL_SERVER_ERROR**
> trong lúc dựng real-stack journey LG-02…LG-05 của nhánh `merge/dev-into-canh-iter1-final-closure`.

## 1. Triệu chứng

Journey LG-02 gọi endpoint assign với dữ liệu hợp lệ, nhận về:

```
HTTP 500
{ "errorCode": "INTERNAL_SERVER_ERROR", "message": "Đã xảy ra lỗi hệ thống", "traceId": "…" }
```

Response không nói được điều gì. Với 500 + thông điệp chung, không thể phân biệt **hệ thống hỏng** với
**hệ thống từ chối đúng luật** — và đó chính là nội dung của phát hiện này.

## 2. Cách lấy bằng chứng (không suy đoán)

Harness real-stack trước đó chạy backend với `stdio: 'ignore'`, nên stack trace thật của server bị nuốt.
Đã thêm biến `PEMS_E2E_API_LOG` vào `scripts/run-realstack-e2e.mjs` để ghi toàn bộ stdout/stderr của
tiến trình API ra file, rồi chạy lại journey.

Log server cho ra **nguyên nhân trực tiếp**, không phải phỏng đoán:

```
System.Exception: Nhân sự Department Staff Cơ sở vật chất HN đã bị trùng lịch làm việc…   (×3)
System.Exception: Không thể phân công khi nhiệm vụ đang ở trạng thái: ASSIGNED
```

## 3. Nguyên nhân gốc

Cả hai exception trên là **từ chối nghiệp vụ đúng đắn**, không phải lỗi hệ thống:

- "người này đã bận trong khung giờ đó" là một **câu trả lời**, không phải một sự cố;
- "nhiệm vụ đang ở trạng thái ASSIGNED" là ràng buộc vòng đời, cũng là một câu trả lời.

Vấn đề nằm ở **cách phát tín hiệu**. `AssignRequestAssigneeCommand` và `ProposeRequestChangeCommand` báo
mọi từ chối nghiệp vụ bằng `throw new Exception(...)` trần. `ExceptionHandlingMiddleware` chỉ có thể coi
`Exception` trần là lỗi chưa xử lý, nên:

| Tầng | Hành vi |
|---|---|
| Handler | `throw new Exception("… đã bị trùng lịch …")` |
| Middleware | không khớp `ConflictException` / `ValidationException` / `NotFoundException` / `AuthBusinessException` → nhánh mặc định |
| HTTP | `500` + `INTERNAL_SERVER_ERROR` + message bị thay bằng "Đã xảy ra lỗi hệ thống" |

Hai hệ quả, và cái thứ hai nghiêm trọng hơn:

1. **Người dùng bị báo sai.** Họ được cho biết server hỏng, trong khi server đã từ chối đúng. Nước đi tiếp
   theo của điều phối viên lẽ ra chỉ đơn giản là chọn người khác.
2. **Lỗi thật và từ chối thường lệ trông y hệt nhau trong log.** Không thể tìm cái này bằng cách theo dõi
   cái kia. Một `NullReferenceException` thật sự trong chính handler đó sẽ chìm lẫn giữa hàng loạt 500 do
   trùng lịch — và ngược lại.

**Đây không phải lỗi do merge.** Đã kiểm tra: 5 commit mới của `origin/Dev` (baseline `d732e651` → HEAD
`1a0f9c53`) không chạm vào thư mục `DepartmentReceptionTasks`. Khiếm khuyết có sẵn từ trước, và chỉ lộ ra
vì đây là lần đầu có real-stack journey đi qua đường này.

## 4. Khắc phục

Thêm `LogisticsTaskErrorCodes` (12 mã ổn định) và chuyển **toàn bộ 14** `throw new Exception` trần trong
hai handler sang exception có kiểu, mỗi kiểu ứng với đúng ý nghĩa nghiệp vụ:

| Ý nghĩa | Kiểu | HTTP |
|---|---|---|
| Trạng thái không cho phép / trùng lịch / đã ký bàn giao / đang chờ phản hồi | `ConflictException` | 409 |
| Thiếu lý do, số lượng hoặc khung giờ không hợp lệ | `ValidationException` | 400 |
| Ngoài phạm vi phòng ban, không phải người được phân công | `AuthBusinessException` | 403 |
| Không tìm thấy yêu cầu | `NotFoundException` | 404 |

**Không nới lỏng bất kỳ luật nào.** Tập hợp các trường hợp bị từ chối trước và sau là như nhau; chỉ có
cách phát tín hiệu thay đổi. Sau thay đổi, cả hai file còn **0** `Exception` trần.

6 unit test hiện có phải cập nhật vì `Assert.ThrowsAsync<Exception>` trong xUnit khớp **đúng kiểu**, không
khớp kiểu con. Các test này được **siết chặt**, không nới: nay khẳng định cả kiểu cụ thể **và** mã lỗi ổn
định, thay vì chỉ "có ném exception nào đó".

## 5. Khiếm khuyết thứ hai — nằm ở chính bộ test

Trong lúc điều tra phát hiện thêm: các journey ban đầu dùng **chung một khung giờ sử dụng** cho mọi hạng
mục, nên cùng một nhân sự được seed thật sự **bị trùng lịch**. Nghĩa là một phần các 500 kia là do test
dựng sai dữ liệu, không phải do sản phẩm.

Đã sửa cả hai phía: mỗi hạng mục logistics nay có ngày riêng (`nextDay()` neo trên
`globalThis.__pemsLogisticsDay`), nên trạng thái trùng lịch chỉ xuất hiện ở đúng journey cố ý kiểm tra nó.

## 6. Vì sao khiếm khuyết này sống lâu đến vậy

Đáng ghi lại: assertion kiểu `expect(status).toBeGreaterThanOrEqual(400)` **thoả mãn hoàn hảo** với một
HTTP 500. Một bộ test chỉ hỏi "có bị từ chối không" sẽ mãi mãi xanh trên khiếm khuyết này.

Vì vậy helper `expectRefusal` trong `departmentRealstackHelpers.ts` bắt buộc **status chính xác** và, khi
có, **mã lỗi ổn định** — chính điều tách bạch "server đã từ chối đúng" khỏi "server đổ".

## 7. Rủi ro còn lại

**Sáu command anh em khác trong `DepartmentReceptionTasks` vẫn ném `Exception` trần** — cùng loại khiếm
khuyết, chưa có real-stack journey đi qua. Không sửa trong vòng này vì mỗi cái cần bộ test riêng, và sửa
mù không có test là đúng kiểu thay đổi mà chính báo cáo này khuyên chống lại. Đã ghi vào mục rủi ro của PR.
