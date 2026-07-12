# LƯU Ý RÀ SOÁT INTEGRATION TEST UC-102 — UPDATE DEPARTMENT

## 1. Kết luận chung

File `UpdateDepartmentApiTests.cs` có phạm vi kiểm thử khá rộng và đã bao phủ nhiều nhánh quan trọng của UC-102:

- Phân quyền theo role
- Cập nhật tên Department thành công
- Giữ nguyên các field không thuộc phạm vi cập nhật
- Kiểm tra audit
- No-op khi tên không thay đổi
- Validation đầu vào
- Department không tồn tại
- Campus scope
- Không cho sửa Department loại `IC`
- Duplicate trong cùng campus
- Loại chính bản ghi đang sửa khỏi kiểm tra duplicate
- Chuẩn hóa khoảng trắng trước khi lưu

Tuy nhiên, trước khi coi bộ test là hoàn chỉnh và an toàn để chạy trên `pems_test`, cần lưu ý các rủi ro và khoảng trống kiểm thử dưới đây.

---

## 2. Rủi ro mức nghiêm trọng

### 2.1. Test `IcDepartment_DoesNotModify` có thể tác động vào dữ liệu seed thật

Test này đang lấy một Department loại `IC` đã tồn tại thật trong database và gửi request đổi tên.

Rủi ro xuất hiện khi backend có lỗi và cho phép update ngoài dự kiến:

- Department `IC` seed thật có thể bị đổi tên
- Tên mới có thể mang prefix test
- Cleanup theo prefix sau test có thể xóa nhầm Department `IC` seed thật
- Dữ liệu nền của `pems_test` có thể bị phá hỏng
- Các test khác phụ thuộc vào IC Department có thể fail dây chuyền

Đây là rủi ro an toàn database nghiêm trọng nhất trong file.

---

### 2.2. Authorization test có thể pass đúng status nhưng sai nguyên nhân

Các authorization test đang kỳ vọng `403 Forbidden`.

Tuy nhiên Department dùng cho test có thể thuộc campus khác với campus của user test, hoặc client test có thể thiếu đủ campus context.

Khi đó có thể xảy ra:

```text
Role guard bị lỗi và không chặn
→ request tiếp tục vào handler
→ campus scope hoặc thiếu context trả 403
→ test vẫn pass
```

Như vậy tên test nói rằng actor bị chặn do sai role, nhưng thực tế test có thể pass do sai campus hoặc thiếu dữ liệu context.

Đây là dạng pass giả theo nguyên nhân.

---

## 3. Lưu ý về authorization tests

### 3.1. Chưa chứng minh Department không bị thay đổi

Các test như:

- `Anonymous_Forbidden`
- `Staff_Forbidden`
- `DepartmentLead_Forbidden`
- `Department_Forbidden`
- `Student_Forbidden`
- `Ho_Forbidden`
- `Admin_Forbidden`
- `Visitor_Forbidden`

hiện chủ yếu kiểm tra HTTP status.

Điều đó chưa đủ để chứng minh:

- Name không bị đổi
- Audit không bị cập nhật
- Các field khác không bị thay đổi
- Không có partial update trước khi response lỗi được trả về

Prefix cleanup chỉ giúp dọn dữ liệu nếu có lỗi, nhưng không chứng minh không có side effect.

---

### 3.2. `CreateClientAsAsync` có thể thiếu context quan trọng

Client của các role khác Staff Leader có thể chưa mang đủ:

- `PrimaryCampusId`
- `DepartmentId`

Nếu role guard bị lỗi, handler có thể vẫn bị chặn ở bước scope/context và trả `403`.

Điều này làm giảm khả năng test xác định chính xác lỗi authorization.

---

### 3.3. Chỉ kiểm tra HTTP status chưa phân biệt được error source

Nhiều nhánh khác nhau có thể cùng trả `403`.

Do đó test có thể không phân biệt được:

- Sai role
- Sai campus
- Thiếu context
- Scope mismatch
- Session/user setup không hợp lệ

---

## 4. Lưu ý về happy path

### 4.1. `StaffLeader_ValidPayload_UpdatesDepartment`

Test này đã kiểm tra update thành công, nhưng cần lưu ý tên test và mô tả nghiệp vụ khẳng định:

```text
Chỉ Name được thay đổi
```

Trong khi phạm vi assertion hiện tại có thể chưa bao phủ đầy đủ toàn bộ field cần giữ nguyên như:

- `HeadUserId`
- `CreatedAt`
- `CreatedBy`

Nếu các field này bị thay đổi ngoài ý muốn mà test không kiểm tra, test vẫn có thể pass.

---

### 4.2. `StaffLeader_Update_KeepsStatus`

Test này tập trung vào việc giữ nguyên status.

Rủi ro là:

```text
API trả 200
Status vẫn đúng
Nhưng Name không hề được update
```

Khi đó test vẫn có thể pass mặc dù hành vi update chính không xảy ra.

---

### 4.3. `StaffLeader_ValidPayload_UpdatesAudit`

Test đã kiểm tra:

- `UpdatedBy`
- `UpdatedAt`

Nhưng cần lưu ý phần create audit cũng phải được giữ nguyên:

- `CreatedAt`
- `CreatedBy`

Nếu create audit bị thay đổi ngoài ý muốn mà không được assert, test có thể không phát hiện.

---

## 5. Lưu ý về no-op

### 5.1. `StaffLeader_NoChange_KeepsRecordUnchanged`

Test hiện có thể chỉ gửi lại đúng tên đang lưu.

Điều này chỉ chứng minh:

```text
Tên input giống byte-for-byte tên trong DB
→ no-op
```

Nó chưa chắc đã chứng minh đầy đủ nghiệp vụ:

```text
Trim + collapse whitespace
→ sau normalize mới bằng tên hiện tại
→ no-op
```

Nếu handler thực hiện sai thứ tự normalize/no-op, test hiện tại có thể không phát hiện.

---

## 6. Lưu ý về validation

### 6.1. Thiếu case tên dài quá giới hạn

File hiện đã có các case:

- `DepartmentId_Zero_BadRequest`
- `EmptyName_DoesNotModify`
- `WhitespaceName_DoesNotModify`

Nhưng còn thiếu case kiểm tra tên dài hơn giới hạn tối đa.

Nếu validator hoặc pipeline API bỏ lọt tên quá dài, bộ Integration Test hiện tại có thể không phát hiện.

---

### 6.2. Validation test phải thật sự chứng minh không modify

Các test validation có tên `DoesNotModify` cần đảm bảo toàn bộ record không đổi, bao gồm:

- CampusId
- Name
- DepartmentType
- HeadUserId
- Status
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy

Đây là điểm bộ test hiện tại đã có nền tảng tốt nhờ `DepartmentSnapshot`, nhưng cần áp dụng nhất quán cho mọi nhánh lỗi.

---

## 7. Lưu ý về Department không tồn tại

### 7.1. ID giả có thể không hoàn toàn deterministic

Nếu dùng một ID cố định rất lớn như:

```text
999999999
```

thì xác suất tồn tại là rất nhỏ, nhưng không bằng 0 trong một database dùng lâu dài.

Nếu ID đó vô tình tồn tại, test sẽ không còn kiểm tra đúng nhánh `NotFound`.

---

## 8. Lưu ý về campus scope

### 8.1. `StaffLeader_OtherCampus_Forbidden`

Đây là test quan trọng để chứng minh Staff Leader chỉ được sửa Department trong campus của mình.

Cần lưu ý test phải đảm bảo:

- User thật sự là Staff Leader hợp lệ
- Department thật sự thuộc campus khác
- Không có lý do lỗi nào khác xảy ra trước campus-scope check
- Bản ghi không bị thay đổi
- Audit không bị thay đổi

Nếu setup không deterministic, test có thể pass sai nguyên nhân.

---

## 9. Lưu ý về Department loại IC

### 9.1. Không chỉ là business rule mà còn là vấn đề test isolation

`IC` Department là dữ liệu nền quan trọng của campus.

Bất kỳ test nào trực tiếp dùng seed thật đều phải được xem xét ở góc độ:

- Có thay đổi seed thật hay không
- Có thể restore được không
- Cleanup có thể xóa nhầm không
- Có ảnh hưởng user đang tham chiếu Department đó không
- Có ảnh hưởng trigger/FK không

---

## 10. Lưu ý về duplicate

### 10.1. `DuplicateNameSameCampus_DoesNotModify`

Test này cần chứng minh đầy đủ:

- Duplicate chỉ trong cùng campus
- So sánh không phân biệt hoa/thường
- Có normalize tên trước khi so sánh
- Không tạo partial update
- Record ban đầu không đổi
- Record bị update cũng không đổi

Nếu chỉ kiểm tra status code hoặc chỉ kiểm tra Name, vẫn có thể bỏ sót side effect.

---

### 10.2. Chưa có chiều ngược lại khác campus

Bộ test hiện có thể chưa chứng minh rằng:

```text
Cùng tên nhưng ở campus khác
→ không bị coi là duplicate
```

Điều này quan trọng nếu business rule thật là unique theo:

```text
(campus_id, name)
```

---

### 10.3. `SameNameSelf_UpdatesRecord`

Test này nhằm chứng minh duplicate query loại chính record đang sửa.

Cần lưu ý phân biệt:

```text
Tên giống chính mình
```

và:

```text
Tên trùng record khác
```

Nếu test không kiểm tra đủ response/audit, một số lỗi update vẫn có thể lọt qua.

---

## 11. Lưu ý về normalization

### 11.1. `Name_TrimmedAndCollapsedBeforeSave`

Test này có giá trị vì kiểm tra:

- Trim đầu/cuối
- Collapse khoảng trắng nội bộ

Cần lưu ý assertion phải dựa vào dữ liệu thực sự lưu trong DB, không chỉ response.

Ngoài ra cần đảm bảo normalization không vô tình làm thay đổi:

- CampusId
- DepartmentType
- HeadUserId
- Status
- CreatedAt
- CreatedBy

---

## 12. Lưu ý về response deserialization

Một số test có thể dùng:

```csharp
body!
```

mà chưa có:

```csharp
Assert.NotNull(body);
```

Dấu `!` chỉ tắt cảnh báo compiler, không chứng minh response body thực sự deserialize thành công.

Nếu body null, thông báo lỗi test có thể khó hiểu hoặc che mất nguyên nhân thực tế.

---

## 13. Lưu ý về audit

### 13.1. Update audit

Khi update thật:

```text
UpdatedBy phải đúng actor
UpdatedAt phải được set hợp lệ
```

### 13.2. Create audit

Khi update:

```text
CreatedBy không được đổi
CreatedAt không được đổi
```

### 13.3. Failed request và no-op

Trong các nhánh:

- Forbidden
- BadRequest
- NotFound
- Conflict
- No-op

audit update không được thay đổi ngoài ý muốn.

---

## 14. Lưu ý về cleanup

`DisposeAsync()` hiện cleanup theo prefix Department test.

Cần lưu ý các trường hợp sau:

- Department bị update sang tên không còn prefix
- Test lỗi trước khi record được restore
- Test dùng seed thật
- Department có user/head tham chiếu
- Session test tích tụ
- User test tích tụ
- Cleanup xóa nhầm dữ liệu nền
- Cleanup vi phạm FK
- Prefix overlap với test class khác

Rủi ro cleanup của UC-102 cao hơn UC-101 vì test update có thể làm record thay đổi tên, không chỉ tạo mới.

---

## 15. Lưu ý về test isolation

Bộ test dùng chung `pems_test` cần đảm bảo:

- Tắt parallelization
- Prefix riêng cho UC-102
- Không đụng seed thật ngoài kiểm soát
- Không phụ thuộc thứ tự chạy test
- Không phụ thuộc dữ liệu do test khác tạo
- Mỗi test tự seed đủ dữ liệu cần thiết
- Dữ liệu sau test được cleanup hoặc giữ nguyên đúng snapshot

---

## 16. Lưu ý về trạng thái hiện tại của authorization

Source hiện tại ghi nhận `DepartmentsController` không có `[Authorize]` hoặc `[RoleAuthorize]`.

Vì vậy anonymous request có thể đi tới Handler và bị chặn bằng `403`.

Cần hiểu đây là behavior hiện tại của source, không đồng nghĩa đây là kiến trúc authorization tối ưu.

Nếu sau này thêm `[Authorize]`, behavior anonymous có thể đổi từ:

```text
403 Forbidden
```

sang:

```text
401 Unauthorized
```

Khi đó test hiện tại có thể fail do contract thay đổi.

---

## 17. Danh sách test và điểm cần chú ý

| Test | Điểm cần lưu ý |
|---|---|
| `Anonymous_Forbidden` | Chỉ status chưa chứng minh record không đổi |
| `Staff_Forbidden` | Có thể pass do sai campus hoặc thiếu context |
| `DepartmentLead_Forbidden` | Có thể pass sai nguyên nhân |
| `Department_Forbidden` | Có thể pass sai nguyên nhân |
| `Student_Forbidden` | Cần phân biệt đúng nguyên nhân 403 |
| `Ho_Forbidden` | Chưa chứng minh không có side effect |
| `Admin_Forbidden` | Chưa chứng minh không có side effect |
| `Visitor_Forbidden` | Chưa chứng minh không có side effect |
| `StaffLeader_ValidPayload_UpdatesDepartment` | Cần bảo đảm chỉ Name thay đổi |
| `StaffLeader_Update_KeepsStatus` | Có thể pass dù Name không update |
| `StaffLeader_ValidPayload_UpdatesAudit` | Chưa chắc đã kiểm tra create audit giữ nguyên |
| `StaffLeader_NoChange_KeepsRecordUnchanged` | Chưa chắc chứng minh normalize trước no-op |
| `DepartmentId_Zero_BadRequest` | Cần bảo đảm không modify DB |
| `EmptyName_DoesNotModify` | Phải giữ nguyên toàn snapshot |
| `WhitespaceName_DoesNotModify` | Phải giữ nguyên toàn snapshot |
| `NonExistingDepartment_NotFound` | ID giả có thể chưa deterministic tuyệt đối |
| `StaffLeader_OtherCampus_Forbidden` | Cần bảo đảm fail đúng vì campus scope |
| `IcDepartment_DoesNotModify` | Rủi ro phá seed thật |
| `DuplicateNameSameCampus_DoesNotModify` | Cần kiểm tra không có partial update |
| `SameNameSelf_UpdatesRecord` | Cần phân biệt self-exclusion với duplicate thật |
| `Name_TrimmedAndCollapsedBeforeSave` | Cần kiểm tra cả DB và field không liên quan |
| `TooLongName_DoesNotModify` | Hiện còn thiếu |

---

## 18. Kết luận cuối

Những điểm cần ưu tiên lưu ý trước khi chạy Integration Test UC-102:

1. Test Department loại `IC` có thể làm hỏng dữ liệu seed thật.
2. Authorization test có thể pass đúng status nhưng sai nguyên nhân.
3. Authorization test chưa chắc đã chứng minh record hoàn toàn không đổi.
4. Một số happy-path test chưa chứng minh đầy đủ rằng chỉ `Name` được thay đổi.
5. Test giữ status có thể pass dù update tên không xảy ra.
6. Audit test cần bao phủ cả update audit và create audit.
7. No-op test chưa chắc đã kiểm tra normalize trước khi so sánh.
8. Thiếu test tên quá dài.
9. ID không tồn tại cần deterministic.
10. Cleanup theo prefix có rủi ro cao trong test update.
11. Các test lỗi phải kiểm tra toàn bộ snapshot, không chỉ status code.
12. Cần phân biệt behavior authorization hiện tại với security contract mong muốn.
