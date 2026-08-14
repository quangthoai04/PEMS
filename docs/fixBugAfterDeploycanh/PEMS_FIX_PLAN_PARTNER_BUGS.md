# PEMS — Kế hoạch triển khai sửa các bug Partner module

## 1. Thông tin tài liệu

- Repository: `quangthoai04/PEMS`
- Nhánh được kiểm tra: `Dev`
- Commit cơ sở: `bd460b6229ae2fecec3969ecaa476dad258bd7f2`
- Phạm vi: lựa chọn đối tác trên form đăng ký, matching, liên kết thành viên–đối tác, trạng thái hồ sơ, liên hệ đối tác, biên bản và kiểm tra scope.
- Không thuộc phạm vi: các lỗi tìm kiếm đoàn khách, validation quốc gia, workflow T-6/readonly và theme; các lỗi đó nằm trong kế hoạch non-partner riêng.

---

## 2. Mục tiêu

Đợt sửa Partner module cần bảo đảm:

1. Khi người dùng chọn một Partner trong form, hệ thống lưu đúng `PartnerId`, không chỉ lưu tên hiển thị.
2. Biên bản nhận biết được Partner của từng thành viên mà không bắt người dùng tạo/liên kết lại.
3. Không thể liên kết với hồ sơ Partner đã bị từ chối.
4. Các thành viên cùng một tổ chức trong cùng visit được dùng chung PartnerId khi khớp chính xác.
5. Candidate do matcher sinh ra không bị nhầm với một quan hệ đã xác nhận.
6. Khi Partner đã được xác định, UI chuyển sang quản lý contact thay vì tiếp tục đề nghị tạo Partner.
7. Không thể liên kết guest/minute participant nằm ngoài đúng visit instance.
8. Public Visitor và người dùng nội bộ sử dụng đúng phạm vi tìm kiếm Partner.

---

## 3. Các quyết định kiến trúc cần chốt trước khi code

### 3.1. Quan hệ thật và gợi ý là hai khái niệm khác nhau

Chốt:

```text
PartnerMatcher candidate
= kết quả tính tạm thời, không phải quan hệ nghiệp vụ

VisitGuestPartnerLink
= quan hệ thành viên–Partner đã được xác nhận
```

Do đó, luồng mới không tạo `VisitGuestPartnerLink` chỉ vì matcher tìm được một candidate.

```text
Matcher chạy
→ trả candidate
→ người dùng xác nhận hoặc hệ thống có stable PartnerId
→ mới tạo VisitGuestPartnerLink CONFIRMED
```

Nếu cần ghi nhớ một candidate đã bị người dùng bỏ qua, lưu quyết định riêng `DISMISSED`; không dùng một active partner link để biểu diễn việc “không liên kết”.

### 3.2. Stable identity của tổ chức ở cấp từng thành viên

Mỗi guest member cần có:

```text
organization             string snapshot
organization_partner_id  nullable FK
```

Trong đó:

- `organization` giữ nội dung hiển thị/audit lịch sử.
- `organization_partner_id` cho biết người dùng thực sự đã chọn Partner nào.
- `organization_partner_id = null` nghĩa là tổ chức nhập tay hoặc chưa được xác định.

Không suy luận nguồn lựa chọn bằng cách so sánh tên hiển thị.

### 3.3. Không gán Partner cấp đơn cho toàn bộ thành viên

Một đoàn khách có thể gồm nhiều tổ chức. `VisitRequest.PartnerId` không đồng nghĩa mọi thành viên thuộc Partner đó.

Chỉ liên kết một thành viên khi:

1. Thành viên có `organization_partner_id`; hoặc
2. Thành viên khớp chính xác tổ chức đã được xác nhận trong cùng visit; hoặc
3. Người dùng xác nhận candidate do matcher đưa ra.

### 3.4. Partner `REJECTED` không được liên kết

Partner bị từ chối vẫn cần xuất hiện trong duplicate/matching result để ngăn tạo hồ sơ trùng. Tuy nhiên:

```text
canView = có thể xem hồ sơ/lý do
canLink = false
```

Hành động hợp lệ là chỉnh sửa và gửi duyệt lại, không phải liên kết hoặc tạo một Partner cùng tên.

### 3.5. Public search và internal search phải tách policy

Public Visitor chỉ được tìm:

```text
CooperationStatus = ACTIVE
ProfileStatus     = APPROVED
Visibility        = PUBLIC
```

Staff/Staff Leader đăng nhập cần internal option endpoint theo campus scope, có thể thấy:

```text
APPROVED + INTERNAL/PUBLIC trong phạm vi cho phép
PENDING_APPROVAL cùng cơ sở nếu business cho phép liên kết trong lúc chờ duyệt
```

Không dùng chung public endpoint cho mọi actor.

---

## 4. Danh sách bug

| ID | Bug | Mức độ | Nguyên nhân gốc |
|---|---|---:|---|
| PART-01 | Chọn Partner hệ thống ở dòng thành viên nhưng `PartnerId` bị mất | P1 | `OrganizationCombobox` chỉ lưu `displayName` |
| PART-02 | Đơn đã có Partner nhưng biên bản vẫn hiện “Tạo/liên kết” | P1 | Partner cấp đơn và link cấp từng người chưa được bridge |
| PART-03 | Authenticated Staff/Staff Leader chỉ tìm thấy Partner public | P1 | Form nội bộ đang dùng public partner options endpoint |
| PART-04 | Partner `REJECTED` vẫn được đề xuất và vẫn liên kết được | P1 | Matcher trả mọi status; `CanView` bị dùng như `CanLink` |
| PART-05 | Profile status, candidate và link status bị trình bày lẫn nhau | P1 | Một UI đang trộn ba state machine khác nhau |
| PART-06 | Các thành viên cùng `ABC` không tự liên kết cùng Partner | P1 | Không có resolver/propagation service theo stable identity/exact match |
| PART-07 | Partner đã xác định nhưng UI vẫn hiện “Tạo đối tác” | P1 | Action chưa chuyển từ partner resolution sang contact management |
| PART-08 | Có thể liên kết `GuestMemberId` ngoài đúng visit | **P0** | Validation có nhánh chỉ kiểm tra guest tồn tại ở bất kỳ đâu |

---

# 5. Kế hoạch sửa chi tiết

## PART-01 — Chọn Partner hệ thống nhưng mất `PartnerId`

### Hiện tượng

Ở danh sách visitors/support members, người dùng chọn một đơn vị trong dropdown hệ thống. UI có thể báo đã chọn tổ chức có sẵn, nhưng khi submit chỉ gửi tên đơn vị.

Sang biên bản, dòng thành viên vẫn hiển thị:

```text
Chưa liên kết
Tạo / liên kết
```

### Nguyên nhân

Public partner search trả:

```ts
{
  partnerId,
  name,
  shortName,
  displayName
}
```

Nhưng `OrganizationCombobox` chuyển kết quả thành:

```ts
{
  value: displayName,
  label: displayName
}
```

`partnerId` bị bỏ. Biến `pickedFromList` chỉ tồn tại trong state của component, không nằm trong form payload hoặc database.

### Thay đổi data model

Thêm cột nullable:

```text
visit_guest_members.organization_partner_id
```

Đề xuất FK:

```text
organization_partner_id → partners.partner_id
```

Giữ `organization` là snapshot. Khi Partner bị đổi tên về sau, lịch sử đơn vẫn hiển thị nội dung người đăng ký đã gửi tại thời điểm tạo.

Nếu hệ thống có hard delete Partner, cân nhắc `ON DELETE SET NULL`; nếu Partner không hard delete, dùng policy FK phù hợp với convention hiện tại của repository.

### Thay đổi frontend schema

```ts
interface PersonForm {
  fullName: string;
  jobTitle: string;
  organization: string;
  organizationPartnerId: number | null;
  nationality: string;
}
```

Khi chọn option:

```ts
onChange({
  organization: option.displayName,
  organizationPartnerId: option.partnerId,
});
```

Khi gõ/sửa text sau khi chọn:

```ts
onChange({
  organization: typedValue,
  organizationPartnerId: null,
});
```

### Thay đổi backend

Create/edit DTO phải nhận `OrganizationPartnerId` nullable.

Nếu ID khác null, backend phải:

1. Load Partner.
2. Kiểm tra Partner được phép lựa chọn theo actor/audience policy.
3. Không tin tên `organization` do client gửi để xác minh identity.
4. Lưu `OrganizationPartnerId` và snapshot hiển thị.
5. Tạo confirmed guest-partner link idempotently.

### Files chính

```text
frontend/pems-react/src/features/visit-request/components/shared/
  OrganizationCombobox.tsx
  PartnerOrgCombobox.tsx

frontend/pems-react/src/features/visit-request/components/v2/
  CampusVisitCard.tsx

frontend/pems-react/src/features/visit-request/schema/
  visitRequestV2.schema.ts

frontend/pems-react/src/features/visit-request/utils/
  visitRequestV2Form.ts
  visitRequestV2DraftStorage.ts

backend/PEMS.Domain/Entities/Delegations/
  VisitGuestMember.cs

backend/PEMS.Infrastructure/Services/
  VisitRequestV2CreateService.cs
  VisitRequestV2EditService.cs
```

### Acceptance criteria

- Chọn Partner hệ thống lưu đúng ID của option.
- Edit/draft/reset form không làm mất ID.
- Sửa text sau khi chọn clear ID ngay.
- Backend từ chối ID không tồn tại hoặc ngoài scope.
- Sang biên bản không còn nút “Tạo/liên kết” nếu member đã có PartnerId.
- Snapshot organization vẫn hiển thị đúng kể cả khi Partner đổi tên sau này.

---

## PART-02 — Partner cấp đơn và Partner cấp thành viên không được bridge

### Hiện tượng

Đơn có:

```text
VisitRequest.PartnerId != null
```

nhưng biên bản chỉ đọc các dòng:

```text
VisitGuestPartnerLink
```

Do đó đơn đã chọn Partner hệ thống nhưng các thành viên chưa có link vẫn bị coi là “Chưa liên kết”.

### Business rule

Không tạo một link cho mọi guest bằng `VisitRequest.PartnerId`.

Thực hiện theo từng người:

```text
Member có OrganizationPartnerId
→ liên kết Partner đó

Member không có OrganizationPartnerId
→ chưa xác định; matcher có thể gợi ý sau
```

Nếu request-level PartnerId trùng với OrganizationPartnerId của member thì dùng cùng PartnerId. Nếu organization khác, không ép theo Partner của request.

### Cách sửa

Trong transaction tạo/chỉnh sửa visit:

1. Persist guests và `OrganizationPartnerId`.
2. Resolve các guest–instance link.
3. Upsert `VisitGuestPartnerLink` cho member có stable PartnerId.
4. `MatchSource = REGISTRATION_SELECTED`.
5. Quan hệ mới là `CONFIRMED`.

Không phụ thuộc việc người dùng đã mở trang biên bản hay chưa.

### Acceptance criteria

- Member chọn Partner A được liên kết A ngay sau create/edit.
- Member khác trong cùng đoàn chọn Partner B được liên kết B.
- Request-level Partner A không ghi đè member thuộc Partner B.
- Reopen/edit request giữ nguyên mapping.
- Đồng bộ biên bản nhiều lần không tạo link trùng.

---

## PART-03 — Staff/Staff Leader đang dùng public partner search

### Hiện tượng

Search options hiện chỉ trả Partner thỏa đồng thời:

```text
CooperationStatus = ACTIVE
ProfileStatus     = APPROVED
Visibility        = PUBLIC
```

Policy này đúng cho anonymous/Public Visitor, nhưng authenticated Staff/Staff Leader cũng đang gọi cùng endpoint nên không thấy Partner nội bộ hợp lệ.

### Hệ quả

Staff/Staff Leader không chọn được:

- Partner `APPROVED + INTERNAL` đúng scope.
- Partner `PENDING_APPROVAL` cùng cơ sở nếu nghiệp vụ cho phép tiếp tục bổ sung member/contact trong lúc chờ duyệt.
- Partner hợp lệ trong nội bộ nhưng chưa công khai ngoài website.

Người dùng có thể phải nhập tay lại một tổ chức đã tồn tại, làm mất ID và tạo thêm matching work ở biên bản.

### Cách sửa

Giữ public endpoint hiện tại cho public form:

```text
GET /public/partners/options
```

Thêm hoặc sử dụng internal endpoint có authentication:

```text
GET /partners/options
```

Internal query phải áp dụng:

- Role/campus scope.
- Profile status được phép link.
- Visibility `INTERNAL/PUBLIC` theo quyền.
- Không trả `REJECTED` như option có thể chọn.
- Có thể trả `PENDING_APPROVAL` cùng campus với label rõ ràng nếu policy cho phép.

Frontend chọn endpoint theo actor:

```text
Anonymous/Public Visitor → public options
Authenticated Staff      → internal options
Authenticated Leader     → internal options
```

### UI option nội bộ

Option cần trả thêm:

```ts
{
  partnerId: number;
  displayName: string;
  profileStatus: 'PENDING_APPROVAL' | 'APPROVED';
  ownerCampusId: number;
  ownerCampusName: string;
}
```

Nếu Partner đang chờ duyệt, dropdown hiển thị:

```text
ABC University
Hồ sơ chờ duyệt · FPT University Hà Nội
```

### Acceptance criteria

- Public Visitor chỉ thấy `ACTIVE + APPROVED + PUBLIC`.
- Staff/Leader thấy Partner nội bộ đúng scope.
- Actor ngoài scope không thể lấy option chỉ bằng gọi API trực tiếp.
- Partner `REJECTED` không xuất hiện như selectable option.
- Pending Partner nếu được cho phép phải có trạng thái hiển thị rõ, không giả như đã duyệt.

---

## PART-04 — Partner `REJECTED` vẫn được gợi ý và liên kết

### Hiện tượng

Modal có thể hiện:

```text
Khớp cao
Từ chối
Andes University...
[Liên kết]
```

Khi bấm liên kết, hệ thống tạo một confirmed relation trỏ tới Partner profile đang `REJECTED`.

### Nguyên nhân

1. `PartnerMatcher` tìm trên Partner mà không loại hoặc phân loại riêng `REJECTED`.
2. Match query đặt `candidate.CanLink` bằng `PartnerAccess.CanViewPartner`.
3. Staff/Leader cùng owner campus có thể xem mọi profile status của campus.
4. Create/update link handler cũng chỉ kiểm tra `CanViewPartner`, chưa có profile-status guard dành cho linking.

### Cách sửa backend

Tạo policy riêng:

```csharp
PartnerAccess.CanLinkPartner(currentUser, partner)
```

Quy tắc đề xuất:

| Profile status | Link policy |
|---|---|
| `APPROVED` | Cho link nếu đúng visibility/scope |
| `PENDING_APPROVAL` | Chỉ cho link cùng campus nếu business đã chốt |
| `DRAFT` | Không link |
| `REJECTED` | Không link |

Create/update link handler phải kiểm tra `CanLinkPartner` và trả stable error code, ví dụ:

```text
PARTNER_REJECTED_CANNOT_LINK
PARTNER_NOT_LINKABLE
```

### Cách sửa matcher/UI

Matcher vẫn có thể trả Partner `REJECTED` trong `blockedCandidates` để ngăn tạo trùng. Candidate cần có:

```ts
{
  canLink: false,
  blockedReason: 'PARTNER_REJECTED',
  recommendedAction: 'RESUBMIT'
}
```

UI hiển thị:

```text
Hồ sơ đối tác đã bị từ chối
[Xem lý do] [Chỉnh sửa và gửi duyệt lại]
```

Không hiển thị:

```text
[Liên kết]
[Vẫn tạo mới]
```

### Lifecycle hồ sơ bị từ chối

Một tổ chức bị từ chối không được tạo lại thành record cùng tên. Phải có luồng:

```text
REJECTED
→ Edit
→ Resubmit
→ PENDING_APPROVAL
→ APPROVED hoặc REJECTED
```

Duplicate-name guard vẫn giữ nguyên để bảo đảm “một tổ chức, một hồ sơ”.

### Files chính

```text
backend/PEMS.Application/Partners/Common/
  PartnerAccess.cs
  PartnerMatcher.cs

backend/PEMS.Application/Partners/Queries/MatchPartner/
  MatchPartnerQueryHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Commands/CreateOrUpdateVisitGuestPartnerLink/
  CreateOrUpdateVisitGuestPartnerLinkCommandHandler.cs

frontend/pems-react/src/features/partners/components/
  CreatePartnerFromParticipantModal.tsx
  ParticipantPartnerCell.tsx
```

### Acceptance criteria

- Partner `REJECTED` không có link action.
- Gọi link API trực tiếp với rejected Partner bị từ chối.
- UI cho xem lý do và resubmit nếu actor có quyền.
- Không tạo Partner mới cùng normalized name để né rejected profile.
- Approved Partner cùng scope vẫn link bình thường.

---

## PART-05 — Candidate, profile status và relationship status bị trộn

### Ba khái niệm hiện có

#### Candidate matching

```text
Tên/alias/email-domain giống bao nhiêu phần trăm?
```

Candidate là kết quả tính tạm thời, ví dụ:

```text
Khớp cao · 95% · Khớp alias
```

#### Partner profile status

```text
DRAFT
PENDING_APPROVAL
APPROVED
REJECTED
```

Trả lời câu hỏi: hồ sơ tổ chức đã được duyệt chưa?

#### Confirmed relationship

Trả lời câu hỏi: thành viên này đã được xác nhận thuộc Partner nào?

### Vấn đề hiện tại

`VisitGuestPartnerLink.MatchStatus` có:

```text
SUGGESTED
CONFIRMED
REJECTED
```

Trong đó `REJECTED` mang nghĩa người dùng bỏ qua gợi ý, nhưng `Partner.ProfileStatus=REJECTED` lại mang nghĩa Staff Leader từ chối hồ sơ Partner.

Hai nghĩa khác nhau nhưng dùng cùng từ, khiến UI dễ hiển thị sai.

### Thiết kế khuyến nghị

Luồng mới:

```text
Matcher candidate
→ không tạo link trong DB

Người dùng xác nhận / có stable OrganizationPartnerId
→ tạo VisitGuestPartnerLink CONFIRMED

Người dùng không chọn candidate
→ không tạo active link
```

Nếu product yêu cầu ghi nhớ việc bỏ qua để candidate không xuất hiện lại, tạo decision record:

```text
visit_guest_partner_suggestion_decisions
```

Gợi ý fields:

```text
decision_id
visit_request_id
visit_instance_id
guest_member_id / minute_participant_id
partner_id
decision = DISMISSED
match_source
confidence_score
decided_by
decided_at
```

Không bắt buộc tạo bảng này nếu chấp nhận matcher tính lại candidate khi modal được mở lại.

### Tương thích dữ liệu hiện có

Trước migration phải thống kê:

```text
COUNT links GROUP BY match_status
COUNT confirmed links pointing to rejected profiles
COUNT suggested/rejected links per target
```

Hướng xử lý:

- `CONFIRMED` hợp lệ → giữ lại.
- `SUGGESTED` → re-evaluate hoặc bỏ để matcher tính lại; không coi là quan hệ thật.
- Link `REJECTED` mang nghĩa bỏ qua → chuyển thành dismissed decision nếu cần lưu lịch sử.
- Confirmed link trỏ tới Partner profile `REJECTED` → đưa vào danh sách remediation/manual review, không tự sửa im lặng.

### UI sau sửa

Confirmed link tới Partner pending:

```text
[Đã liên kết]
ABC University
[Hồ sơ chờ duyệt]
```

Candidate chưa xác nhận:

```text
[Khớp cao]
ABC University
[Xem chi tiết] [Xác nhận liên kết]
```

Partner rejected:

```text
[Hồ sơ bị từ chối]
ABC University
[Xem lý do] [Gửi duyệt lại]
```

### Acceptance criteria

- Candidate không xuất hiện như active link.
- `Partner.ProfileStatus` và trạng thái relationship được hiển thị riêng.
- “Bỏ qua gợi ý” không thay đổi hồ sơ Partner.
- Không có API dismiss có thể làm thay đổi confirmed link.
- Hủy một confirmed link phải là use case/API riêng, có confirmation và audit.

---

## PART-06 — Các thành viên cùng `ABC` không tự liên kết cùng Partner

### Hiện tượng

Người đầu tiên tạo Partner `ABC`:

```text
ProfileStatus = PENDING_APPROVAL
```

Hệ thống tạo contact và confirmed link cho người đầu tiên. Những người còn lại trong cùng visit cũng có organization `ABC` nhưng vẫn hiển thị “Tạo/liên kết”.

### Business rule

Sau một hành động rõ ràng xác nhận Partner cho một organization trong cùng visit, các member khác khớp chính xác organization đó được dùng chung PartnerId.

Không tự động lan truyền dựa trên fuzzy match.

### Tạo resolver dùng chung

Đề xuất service:

```text
GuestPartnerLinkResolver
```

Input:

```text
visitRequestId
visitInstanceId
guest/member target
organization snapshot
organizationPartnerId nullable
```

Thứ tự resolve:

1. Có `OrganizationPartnerId` hợp lệ → confirmed.
2. Trong cùng visit đã có confirmed mapping cho exact normalized organization → confirmed propagation.
3. Exact canonical name hoặc exact active alias → candidate mạnh.
4. Email domain match → candidate.
5. Fuzzy match → candidate tham khảo.
6. Không có candidate → cho phép tạo Partner mới.

### Quy tắc auto-confirm

Chỉ auto-confirm khi:

- Có stable PartnerId do người dùng chọn; hoặc
- Exact normalized organization đã được xác nhận trong chính visit; hoặc
- Exact alias được policy cho phép và không có ambiguity.

Fuzzy/near-name không được auto-confirm.

### Sau khi tạo Partner mới

Trong cùng transaction hoặc orchestration idempotent:

1. Tạo Partner một lần ở `PENDING_APPROVAL`.
2. Link người tạo.
3. Tìm member khác trong cùng visit có exact organization key.
4. Upsert confirmed link cùng PartnerId.
5. Upsert PartnerContact theo quy tắc chống trùng.
6. Refetch links để UI cập nhật toàn bộ dòng.

### Idempotency

Chạy resolver nhiều lần phải cho cùng kết quả:

```text
Không tạo duplicate Partner
Không tạo duplicate guest-partner link
Không tạo duplicate PartnerContact
```

Mỗi target chỉ có tối đa một active confirmed Partner link. Nếu đổi Partner, phải dùng use case “Thay đổi liên kết” có audit, không silently overwrite.

### Acceptance criteria

- Tạo `ABC` cho người đầu tiên chỉ tạo một Partner record.
- Người khác cùng exact organization trong cùng visit được link cùng PartnerId.
- Người organization gần giống chỉ nhận candidate, không tự link.
- Người tổ chức khác không bị ảnh hưởng.
- Resolver chạy lại không sinh dữ liệu trùng.
- Multi-campus chỉ propagate trong scope đã được business chốt; mặc định không vượt instance nếu membership khác nhau.

---

## PART-07 — UI vẫn hiện “Tạo đối tác” khi Partner đã được xác định

### Hiện tượng

Khi member đã có Partner hoặc candidate mạnh, UI vẫn tập trung vào hành động:

```text
Tạo / liên kết đối tác
```

Người dùng có thể hiểu rằng cần tạo một Partner mới cho từng thành viên.

### State-action matrix mới

| State của member | UI chính | Action |
|---|---|---|
| Có confirmed Partner link và contact | Tên Partner + profile status | `Cập nhật thông tin liên hệ` |
| Có confirmed Partner link, chưa có contact | Tên Partner + profile status | `Thêm thông tin liên hệ` |
| Có selectable candidate | Candidate + match reason | `Xác nhận liên kết` / `Không phải đối tác này` |
| Candidate là Partner `REJECTED` | Hồ sơ bị từ chối | `Xem lý do` / `Gửi duyệt lại` |
| Không có candidate | Chưa xác định Partner | `Tìm hoặc tạo đối tác` |

### Contact form

Khi Partner đã xác định, modal phải chuyển sang contact management và cho phép nhập:

```text
Họ tên
Chức vụ
Phòng ban
Email
Số điện thoại
Ghi chú
Đầu mối chính?
```

### Quy tắc upsert contact

1. Cùng PartnerId + cùng normalized email → cùng contact.
2. Nếu không có email: cùng PartnerId + exact full name + exact job title → cảnh báo xác nhận trước khi gộp.
3. Không gộp chỉ dựa trên tên.
4. Cập nhật contact không được làm thay đổi guest snapshot ngoài use case được thiết kế.

### Trạng thái hiển thị

Ví dụ member đã link Partner chờ duyệt:

```text
[Đã liên kết]
ABC University
[Hồ sơ chờ duyệt]
[Thêm thông tin liên hệ]
```

Không hiển thị “Tạo đối tác”.

### Files chính

```text
frontend/pems-react/src/features/partners/components/
  ParticipantPartnerCell.tsx
  CreatePartnerFromParticipantModal.tsx

frontend/pems-react/src/features/partners/api/
  partnersApi.ts

backend/PEMS.Application/Partners/VisitLinks/
backend/PEMS.Application/Partners/Contacts/
```

### Acceptance criteria

- Confirmed link không bao giờ hiển thị “Tạo đối tác”.
- Contact action đúng theo có/không có `PartnerContactId`.
- Thêm/cập nhật contact không tạo duplicate.
- Partner profile status được hiển thị riêng với relationship state.
- Đổi liên kết Partner là action riêng có confirmation và audit.

---

## PART-08 — Lỗi scope khi liên kết `GuestMemberId`

### Mức độ

```text
P0 — security/data integrity
```

### Hiện tượng trong code

Validation hiện có các nhánh kiểm tra guest thuộc instance/request, nhưng nhánh cuối chỉ cần:

```text
GuestMemberId tồn tại ở bất kỳ đâu
```

Nhánh này làm vô hiệu hóa các kiểm tra scope phía trước.

### Nguy cơ

- Liên kết guest của visit khác.
- Liên kết chéo campus/request.
- Làm sai Partner visit history.
- Có tính chất IDOR nếu người dùng đoán hoặc thu được ID.
- Có thể tạo link/contact sai chủ thể.

### Cách sửa

V2 chỉ cho phép nếu có quan hệ chính xác:

```text
VisitInstanceGuestMember.VisitInstanceId
== request.VisitInstanceId

VisitInstanceGuestMember.GuestMemberId
== request.GuestMemberId
```

Không giữ fallback:

```text
Any GuestMemberId exists
```

Nếu phải hỗ trợ legacy, fallback tối đa chỉ được trong cùng `VisitRequestId` và phải có comment/test giải thích dữ liệu legacy nào cần nó.

### Kiểm tra bổ sung

- `LinkId` thuộc đúng request và instance.
- `MinuteParticipantId` thuộc minutes của đúng instance.
- `PartnerContactId` thuộc đúng PartnerId.
- Actor có quyền quản lý đúng visit instance.
- Partner có trạng thái/scope được phép link.
- Không được dùng một link ID để đổi target sang guest khác.

### Files chính

```text
backend/PEMS.Application/Partners/VisitLinks/Commands/CreateOrUpdateVisitGuestPartnerLink/
  CreateOrUpdateVisitGuestPartnerLinkCommandHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Commands/RejectVisitGuestPartnerSuggestion/
  RejectVisitGuestPartnerSuggestionCommandHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Common/
  VisitLinkSupport.cs
```

### Acceptance criteria

- Guest của instance A không thể được link thông qua endpoint instance B.
- Guest cùng request nhưng không thuộc instance hiện tại bị từ chối theo policy V2.
- Minute participant ngoài instance bị từ chối.
- LinkId ngoài scope trả 404/403 theo convention, không tiết lộ dữ liệu.
- Không có cross-campus link nếu actor không có quyền.
- Audit log ghi đúng actor, instance, guest target và PartnerId.

---

# 6. API contract đề xuất

## 6.1. Partner option cho form

Public:

```http
GET /api/public/partners/options?keyword=abc&limit=20
```

Authenticated:

```http
GET /api/partners/options?keyword=abc&limit=20
```

Response:

```json
[
  {
    "partnerId": 123,
    "name": "ABC University",
    "shortName": "ABCU",
    "displayName": "ABC University (ABCU)",
    "profileStatus": "APPROVED",
    "ownerCampusId": 1,
    "ownerCampusName": "FPT University Hà Nội",
    "country": "Việt Nam",
    "city": "Hà Nội"
  }
]
```

## 6.2. Person payload

```json
{
  "fullName": "Daniel Kim",
  "jobTitle": "International Program Manager",
  "organization": "ABC University (ABCU)",
  "organizationPartnerId": 123,
  "nationality": "Hàn Quốc"
}
```

Nếu nhập tay:

```json
{
  "organization": "New Organization",
  "organizationPartnerId": null
}
```

## 6.3. Match candidate

```json
{
  "partnerId": 123,
  "name": "ABC University",
  "profileStatus": "APPROVED",
  "matchScore": 95,
  "matchReason": "Khớp theo tên gọi khác (alias)",
  "canLink": true,
  "blockedReason": null,
  "recommendedAction": "LINK"
}
```

Rejected candidate:

```json
{
  "partnerId": 124,
  "name": "ABC Institute",
  "profileStatus": "REJECTED",
  "matchScore": 92,
  "canLink": false,
  "blockedReason": "PARTNER_REJECTED",
  "recommendedAction": "RESUBMIT"
}
```

## 6.4. Confirm link

```http
POST /api/visit-instances/{visitInstanceId}/partner-links
```

```json
{
  "guestMemberId": 1001,
  "minuteParticipantId": null,
  "partnerId": 123,
  "partnerContactId": null,
  "matchSource": "REGISTRATION_SELECTED"
}
```

Backend luôn lưu quan hệ mới là `CONFIRMED`; client không được tự quyết định arbitrary match status.

---

# 7. Migration và xử lý dữ liệu hiện có

## 7.1. Migration schema

1. Thêm `visit_guest_members.organization_partner_id` nullable.
2. Thêm FK/index theo DB convention.
3. Nếu triển khai dismissed decisions, tạo bảng riêng sau khi product xác nhận cần ghi nhớ việc bỏ qua.
4. Rà soát constraint/index của `visit_guest_partner_links` để bảo đảm một target không có nhiều active confirmed link.

## 7.2. Audit trước backfill

Chạy report/read-only query thống kê:

```text
Guest members có organization trùng exact Partner name/alias
Guest members đã có confirmed link
Targets có nhiều link
Links trỏ tới rejected Partner profiles
Links theo match_status
Partner contacts có khả năng trùng email/name
```

## 7.3. Backfill an toàn

Chỉ backfill `OrganizationPartnerId` tự động khi:

- Member đã có confirmed link hợp lệ; hoặc
- Exact unique canonical/alias match và không có ambiguity, sau khi product chấp thuận.

Không backfill bằng fuzzy match.

Nếu nhiều Partner candidate khớp, đưa vào manual review report.

## 7.4. Remediation dữ liệu sai

Không tự động xóa/sửa im lặng các confirmed link trỏ tới rejected Partner. Tạo report gồm:

```text
link_id
visit_request_id
visit_instance_id
guest/minute target
partner_id
partner_name
profile_status
created_by
created_at
```

Staff Leader hoặc product owner quyết định unlink, resubmit Partner hoặc giữ lịch sử theo từng trường hợp.

---

# 8. Thứ tự triển khai

## Phase 0 — Security hotfix

1. Bỏ fallback `GuestMemberId exists anywhere`.
2. Enforce exact visit-instance target scope.
3. Tách `CanViewPartner` và `CanLinkPartner`.
4. Chặn link tới `REJECTED/DRAFT` Partner.
5. Thêm integration tests P0 trước khi triển khai các thay đổi schema lớn.

## Phase 1 — Persist Partner identity trên member

1. Migration `organization_partner_id`.
2. Cập nhật entity, DTO, validator, create/edit service.
3. Cập nhật form schema và draft/edit mapping.
4. Cập nhật combobox để giữ ID.
5. Backend validation cho ID và audience/scope.

## Phase 2 — Tách public/internal option search

1. Giữ public query với `ACTIVE + APPROVED + PUBLIC`.
2. Thêm internal option query có authorization.
3. Frontend chọn endpoint theo authentication/role.
4. Hiển thị pending status rõ ràng cho internal option nếu được phép.

## Phase 3 — Link resolver và propagation

1. Tạo `GuestPartnerLinkResolver` dùng chung.
2. Seed confirmed link từ `OrganizationPartnerId`.
3. Propagate exact organization mapping trong cùng scope.
4. Không auto-confirm fuzzy match.
5. Bảo đảm idempotency và concurrency safety.

## Phase 4 — UI và contact management

1. Sửa candidate card của rejected Partner.
2. Tách badge relationship/profile status.
3. Thay “Tạo đối tác” bằng action theo state matrix.
4. Thêm/cập nhật PartnerContact.
5. Thêm use case đổi/hủy confirmed link có confirmation và audit nếu nghiệp vụ cần.

## Phase 5 — Data audit/backfill

1. Thống kê dữ liệu hiện tại.
2. Backfill confirmed identity an toàn.
3. Lập manual-review report cho ambiguity và rejected-profile links.
4. Không dùng fuzzy match cho backfill.

## Phase 6 — Full regression

1. Backend unit/integration tests.
2. Frontend component tests.
3. E2E form → approval/minutes flow.
4. Authorization/IDOR tests.
5. Multi-campus and concurrency tests.

---

# 9. Test matrix bắt buộc

## 9.1. Form selection

```text
[ ] Public Visitor chỉ thấy ACTIVE + APPROVED + PUBLIC.
[ ] Staff thấy Partner internal đúng scope.
[ ] Staff không thấy/select Partner ngoài scope.
[ ] Chọn option lưu đúng OrganizationPartnerId.
[ ] Sửa text sau khi chọn clear ID.
[ ] Draft save/load giữ ID.
[ ] Edit request giữ ID nếu organization không đổi.
```

## 9.2. Profile status

```text
[ ] APPROVED đúng scope link thành công.
[ ] PENDING_APPROVAL cùng campus xử lý đúng policy đã chốt.
[ ] PENDING_APPROVAL ngoài campus bị từ chối.
[ ] DRAFT không link được.
[ ] REJECTED không link được ở UI và API.
[ ] Rejected candidate chỉ có xem lý do/resubmit.
```

## 9.3. Matching

```text
[ ] Stable PartnerId không cần matcher.
[ ] Exact name/alias sinh candidate đúng.
[ ] Email domain sinh candidate đúng.
[ ] Fuzzy match không auto-confirm.
[ ] Candidate không tạo active DB link trước khi xác nhận.
[ ] Bỏ qua candidate không thay đổi Partner profile.
```

## 9.4. Propagation

```text
[ ] Tạo ABC một lần chỉ có một Partner record.
[ ] Member khác cùng exact ABC trong visit được link cùng PartnerId.
[ ] Organization gần giống không tự link.
[ ] Organization khác không bị ảnh hưởng.
[ ] Resolver chạy lại không tạo duplicate link/contact.
[ ] Multi-campus propagation không vượt scope đã chốt.
```

## 9.5. Contact

```text
[ ] Confirmed Partner chưa có contact hiện “Thêm thông tin liên hệ”.
[ ] Có contact hiện “Cập nhật thông tin liên hệ”.
[ ] Cùng normalized email không tạo contact trùng.
[ ] Trùng tên nhưng khác email không tự gộp.
[ ] ContactId luôn thuộc đúng PartnerId.
```

## 9.6. Security/scope

```text
[ ] Guest instance A không link qua endpoint instance B.
[ ] Minute participant ngoài instance bị từ chối.
[ ] LinkId ngoài request/instance bị từ chối.
[ ] Actor không có quyền visit không link được.
[ ] Actor không có quyền Partner không link được.
[ ] Không thể đổi target của link bằng request update giả mạo.
```

---

# 10. Definition of Done

Chỉ coi Partner bugfix hoàn tất khi:

```text
[ ] Mỗi member đã chọn Partner hệ thống giữ được stable PartnerId.
[ ] Biên bản không yêu cầu tạo/liên kết lại khi PartnerId đã tồn tại.
[ ] Partner cấp đơn không bị gán mù cho mọi member.
[ ] Public và internal option search sử dụng đúng audience policy.
[ ] Partner REJECTED/DRAFT không link được bằng UI hoặc API.
[ ] Candidate matching không bị coi là confirmed relationship.
[ ] Confirmed link và Partner profile status được hiển thị riêng.
[ ] Tạo ABC một lần, exact ABC members trong scope dùng chung PartnerId.
[ ] Fuzzy match không tự động liên kết.
[ ] Khi Partner đã xác định, UI chuyển sang quản lý contact.
[ ] Không tạo duplicate Partner, link hoặc contact khi đồng bộ lặp lại.
[ ] Không có cross-visit/cross-campus GuestMemberId link.
[ ] Dữ liệu hiện có đã được audit; ambiguity/rejected links có remediation report.
[ ] Toàn bộ unit, integration, frontend và E2E tests liên quan đều pass.
```

### Lệnh kiểm tra cuối

Backend:

```bash
dotnet test
```

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu đã có Playwright/E2E:

```bash
npm run test:e2e
```

---

# 11. Danh sách files trọng tâm

## Frontend

```text
frontend/pems-react/src/features/visit-request/components/shared/
  OrganizationCombobox.tsx
  PartnerOrgCombobox.tsx
  PartnerAsyncSelect.tsx

frontend/pems-react/src/features/visit-request/components/v2/
  CampusVisitCard.tsx
  VisitRequestFormV2.tsx

frontend/pems-react/src/features/visit-request/schema/
  visitRequestV2.schema.ts

frontend/pems-react/src/features/visit-request/utils/
  visitRequestV2Form.ts
  visitRequestV2DraftStorage.ts

frontend/pems-react/src/features/partners/components/
  ParticipantPartnerCell.tsx
  CreatePartnerFromParticipantModal.tsx

frontend/pems-react/src/features/partners/api/
  partnersApi.ts

frontend/pems-react/src/features/partners/types/
  partners.types.ts
```

## Backend

```text
backend/PEMS.Domain/Entities/Delegations/
  VisitGuestMember.cs

backend/PEMS.Domain/Entities/Partners/
  VisitGuestPartnerLink.cs
  Partner.cs
  PartnerContact.cs

backend/PEMS.Application/Partners/Common/
  PartnerAccess.cs
  PartnerMatcher.cs
  PartnerNormalization.cs

backend/PEMS.Application/Partners/Queries/SearchPublicPartnerOptions/
  SearchPublicPartnerOptionsQueryHandler.cs

backend/PEMS.Application/Partners/Queries/MatchPartner/
  MatchPartnerQueryHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Queries/GetVisitGuestPartnerLinks/
  GetVisitGuestPartnerLinksQueryHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Commands/CreateOrUpdateVisitGuestPartnerLink/
  CreateOrUpdateVisitGuestPartnerLinkCommandHandler.cs

backend/PEMS.Application/Partners/VisitLinks/Commands/RejectVisitGuestPartnerSuggestion/
  RejectVisitGuestPartnerSuggestionCommandHandler.cs

backend/PEMS.Application/Partners/Commands/CreatePartnerFromGuest/
  CreatePartnerFromGuestCommandHandler.cs

backend/PEMS.Application/Partners/Commands/CreatePartner/
  CreatePartnerCommandHandler.cs

backend/PEMS.Infrastructure/Services/
  VisitRequestV2CreateService.cs
  VisitRequestV2EditService.cs
```

---

# 12. Kết luận triển khai

Không nên sửa riêng từng nút “Tạo/liên kết” hoặc chỉ tăng độ fuzzy của matcher. Phương án đúng phải giải quyết đồng thời ba nguyên nhân gốc:

```text
1. Lưu PartnerId ở cấp từng thành viên.
2. Chỉ lưu confirmed relationship như một link thật.
3. Dùng một resolver thống nhất từ form đăng ký tới biên bản.
```

Thứ tự ưu tiên bắt buộc:

```text
P0 scope/security
→ Partner status guard
→ Persist OrganizationPartnerId
→ Internal/public search split
→ Resolver/propagation
→ Contact UX
→ Data backfill/regression
```
