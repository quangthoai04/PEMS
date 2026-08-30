# AUDIT — "Đối tác trong hệ thống" label trước khi triển khai trên các màn xem đơn

Ngày audit: 2026-08-30. Phạm vi: read-only source trace, KHÔNG sửa code/DB/UI trong bước này.

---

## A. Executive Summary

**Kết luận: NOT READY — có thể làm được, nhưng cần bổ sung data contract (backend) trước khi chạm frontend, và scope phải hẹp lại theo từng loại đối tượng (KHÔNG đồng nhất 1 label cho cả 4 nhóm).**

Lý do chính:

1. **Dữ liệu "đã chọn Partner" tồn tại nhưng KHÔNG đồng nhất giữa 4 nhóm đối tượng.** Registrant và Guest/External-Support đều có cột `*PartnerId` thật trên entity. Operational Contact thì **hoàn toàn không có cột Partner nào** — chỉ có đường vòng 2 hop (Contact → GuestMember → Partner) qua quan hệ NP-03 (`OperationalContactGuestMemberId`), và đường vòng này **chưa từng được bất kỳ API đọc nào hiện có join tới** (§C, §G).
2. **3 trong số các API xem đơn phổ biến nhất (VisitProcess detail, Process Summary, Contribution) đang ÂM THẦM LÀM RỚT field `OrganizationPartnerId`** ngay trong tầng mapping DTO nội bộ — dữ liệu đã được `VisitFormReadService` resolve sẵn, chỉ là 3 handler tự viết `MapRow` riêng và quên copy field này (§D, §J). Đây là bug thật, không phải thiếu dữ liệu.
3. **API DTO đầy đủ nhất (`GetEditableVisitRequestDetail` — có cả `PartnerId/PartnerName/PartnerIsActive/PartnerProfileStatus`) là DEAD CODE** — route đã bị khoá trả `410 Gone`, không controller nào gọi handler này qua HTTP (§C, §F).
4. **`OrganizationPartnerId != null` một mình KHÔNG đủ để kết luận "đã chọn Partner trong hệ thống" theo đúng nghĩa "quan hệ đã được xác nhận"** — vì đường ghi qua Amendment-approval KHÔNG gọi `GuestPartnerLinkResolver`, nên có thể có `OrganizationPartnerId` non-null mà không có `VisitGuestPartnerLink.MatchStatus=CONFIRMED` tương ứng (§B, §H).
5. **Đã có 1 quyết định thiết kế frontend rất quan trọng, đã ghi rõ trong code, PHẢI tôn trọng:** badge mạnh (pill xanh "Đã chọn đối tác có sẵn") CHỈ dành cho Partner cấp request (registrant); badge cấp guest-member phải nhẹ hơn (chỉ text "✓ Có trong hệ thống") vì tổ chức của từng guest có thể KHÁC với Partner của cả request — dùng chung 1 badge mạnh cho tất cả sẽ khiến người xem hiểu lầm tất cả cùng một Partner (`OrganizationCombobox.tsx` — trích trong §I).

→ Việc triển khai khả thi với nỗ lực nhỏ-vừa, nhưng phải đi qua 3 bước: (1) vá lỗ hổng DTO-mapping đang có, (2) quyết định chính thức model cho Operational Contact (hiện chưa có), (3) mới chạm frontend theo từng nhóm màn hình riêng biệt.

---

## B. Current Data Model

```
Registrant (VisitRequest, request-level)
   VisitRequest.PartnerId (nullable FK → partners, SET NULL)
        ↕ set together, tại write-time, với RegistrantOrganization
          (server GHI ĐÈ RegistrantOrganization = Partner.Name khi PartnerId set)
   → Partner

Guest / External Support (VisitGuestMember, member_type = GUEST | EXTERNAL_SUPPORT — CÙNG 1 bảng/entity)
   VisitGuestMember.OrganizationPartnerId (nullable FK → partners, SET NULL)
        — độc lập với —
   VisitGuestMember.Organization (free-text snapshot, NOT NULL, không bao giờ bị ghi đè lại)
   → Partner

   Song song, có 1 bảng xác nhận riêng:
   VisitGuestPartnerLink (visit_guest_partner_links)
        .GuestMemberId (nullable) hoặc .MinuteParticipantId (nullable) — ít nhất 1 trong 2
        .PartnerId (NOT NULL, FK → partners, ON DELETE RESTRICT)
        .MatchSource: AUTO_NAME | AUTO_EMAIL_DOMAIN | MANUAL | CREATED_FROM_GUEST | BUSINESS_CARD_OCR
        .MatchStatus: SUGGESTED | CONFIRMED | REJECTED
   → Partner (qua bảng link, KHÔNG phải qua VisitGuestMember)

External Support: KHÔNG phải entity riêng — dùng chung VisitGuestMember, phân biệt bằng MemberType.
   (đã xác nhận qua VisitFormV2Constants.cs — lỗi OPERATIONAL_CONTACT_MEMBER_NOT_ELIGIBLE mô tả
   "role belongs to the delegation's own side (GUEST / EXTERNAL_SUPPORT)")

Operational Contact (VisitInstanceFormDetail, PER-CAMPUS, 1:1 với VisitRequestCampus)
   OperationalContactOrganization (nullable free-text SNAPSHOT) — KHÔNG có cột Partner trực tiếp
   OperationalContactGuestMemberId (nullable FK → VisitGuestMember, SET NULL — quan hệ NP-03)
        ↓ (đường vòng DUY NHẤT, 2 hop, CHƯA API đọc nào join)
        VisitGuestMember.OrganizationPartnerId
        → Partner
```

**Kết luận bắt buộc:** Operational Contact **không có** khái niệm Partner của riêng nó trong schema hôm nay. Bất kỳ badge nào cho Contact chỉ có thể đúng nếu nó thực sự là 1 thành viên đoàn (`OperationalContactGuestMemberId` != null) VÀ member đó có `OrganizationPartnerId` != null — và ngay cả vậy, đây vẫn là suy luận gián tiếp (Contact tổ chức = tổ chức của member được liên kết), không phải Contact "tự chọn" Partner.

---

## C. Current Write Flow

### C.1 Registrant

```
UI: PartnerOrgCombobox.tsx (partnerSelectionMode: EXISTING_PARTNER|NEW_ORGANIZATION + partnerId)
→ API payload: V2CreatePayload.partnerId / SafeEditPayload.registrant.partnerId
→ Create: VisitRequestV2CreateService.cs:225  → PartnerId = form.PartnerId
→ Safe Edit: VisitSafeEditService.cs:132-134  → request.PartnerId = ...
→ Validate (cả 2 nơi): Partner phải CooperationStatus=ACTIVE && ProfileStatus=APPROVED
   (VisitRequestV2CreateService.cs:154-160; VisitSafeEditService.cs:97-105,
    qua GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync)
→ Server GHI ĐÈ RegistrantOrganization = Partner.Name/ShortName (cùng transaction)
→ DB: visit_requests.partner_id
```
Không có path nào khác set `VisitRequest.PartnerId` — không auto-derive, không fuzzy-match.

### C.2 Guest / External Support

```
UI: OrganizationCombobox trong CampusVisitCard.tsx (field organizationPartnerId, sibling của organization)
    — gõ đè text đã chọn → tự clear id (contract ghi rõ trong code, PART-01)
→ API payload: VisitorDto.OrganizationPartnerId / SupportTeamMemberDto.OrganizationPartnerId
→ Ghi trực tiếp (KHÔNG derive) tại 3 nơi:
   - Create:            VisitRequestV2CreateService.cs:350,362
   - Pending-edit (COW): VisitRequestV2EditOps.cs:82,93 (StageReplaceMembers)
   - Amendment-approve:  VisitAmendmentService.cs:412-413 (cũng qua StageReplaceMembers)
→ KHÔNG bị chạm bởi Schedule-only resubmit (cố ý — tránh bug cũ xoá OrganizationPartnerId)
→ Validate (mọi path): ACTIVE + APPROVED + PUBLIC (GuestOrganizationPartnerPolicy — CHẶT HƠN
   registrant, thêm điều kiện PUBLIC)
→ DB: visit_guest_members.organization_partner_id

Song song — GuestPartnerLinkResolver.ResolveForRequestAsync ghi visit_guest_partner_links:
   - Gọi tại: Create (VisitRequestV2CreateService.cs:569),
              Pending-edit (VisitRequestV2EditService.cs:1169)
   - KHÔNG gọi tại: Amendment-approval, Schedule-only resubmit
   ⚠️ → member do amendment tạo ra CÓ THỂ có OrganizationPartnerId non-null nhưng KHÔNG có
      link CONFIRMED tương ứng được tạo bởi chính hành động đó.
```

### C.3 Operational Contact

```
UI: OperationalContact org field trong CampusVisitCard.tsx dùng CÙNG component OrganizationCombobox
    nhưng KHÔNG truyền prop partnerId — có comment code xác nhận đây là chủ ý:
    "the stored value is still plain text — this contact is a snapshot and the schema
     has no relation to a partner record" (CampusVisitCard.tsx ~2515-2518)
→ Quan hệ NP-03 (OperationalContactGuestMemberId) được set qua 1 module dùng chung
  OperationalContactLink.cs, gọi từ 7 write path khác nhau (Create, Safe-Edit link/unlink,
  Pending-edit COW, Resubmit, Amendment submit/approve) — MỌI path đều yêu cầu "continuity
  proof" (member đang link phải xuất hiện đúng 1 lần trong dữ liệu mới) để tránh "disguised
  repoint".
→ Replace / Transfer (đổi đầu mối qua link xác nhận) → LUÔN LUÔN set
  OperationalContactGuestMemberId = null (vì người mới chỉ có email, chưa chắc là member nào
  trong đoàn) — invitation chỉ mang Organization dạng TEXT trong JSON snapshot
  (PendingSnapshotJson), KHÔNG có PartnerId.
→ Update-profile (sửa metadata) KHÔNG bao giờ chạm FK này.
```

---

## D. Current Read Flow

| # | API/Query | DTO | Registrant Partner? | Member Partner? | Contact Partner? | FE type khớp? |
|---|---|---|---|---|---|---|
| 1 | `GET /api/v2/visit-requests/{id}` (`VisitFormReadService.ResolveAsync` → `ResolvedVisitFormDto`) | `ResolvedVisitFormDto` | ✅ `PartnerId` (raw, chưa resolve tên) | ✅ `ResolvedMemberDto.OrganizationPartnerId` | ❌ không có field nào | ✅ khớp — `ResolvedVisitForm`/`ResolvedMember` trong `visitRequestV2Api.ts` |
| 2 | `GET api/visit-instances/{id}/partner-links` (`GetVisitGuestPartnerLinksQueryHandler`) | `VisitGuestPartnerLinkDto` | N/A (không phải registrant) | ✅ `PartnerId/PartnerName/MatchSource/MatchStatus/ConfidenceScore/PartnerContactId` | N/A | ✅ khớp — `VisitGuestPartnerLink` trong `partners.types.ts`, dùng bởi `ParticipantPartnerCell.tsx` (chỉ màn Minutes) |
| 3 | `GET api/delegations/viewguestdelegationlist` (list quản lý) | `VisitRequestManagementItemDto` | 🟡 chỉ `PartnerName` string (server resolve sẵn, fallback về RegistrantOrganization nếu null) | ❌ | ❌ | khớp field name nhưng **không có id/flag** để phân biệt "picked" vs trùng tên tình cờ |
| 4 | `GET .../process-detail` (`GetVisitProcessDetailQueryHandler`) | `VisitProcessDetailDto`/`...RequestSummaryDto`/`...GuestMemberDto` | ❌ | ❌ **(BUG — bị drop trong `MapRow` cục bộ, dù `VisitFormReadService` đã resolve sẵn field này)** | ❌ | khớp (2 bên đều thiếu — vì backend không gửi) |
| 5 | `.../summary` (Process Summary, có cả feedback đã verify) | `ProcessSummaryPageDto` (dùng lại DTO #4) | ❌ | ❌ **(cùng bug #4 — `MapRow` riêng, copy-paste gần giống hệt)** | ❌ | khớp (thiếu cả 2 bên) |
| 6 | `.../contribution` (Contribution page) | dùng lại `VisitProcessGuestMemberDto` | ❌ | ❌ **(cùng bug #4)** | ❌ | khớp (thiếu cả 2 bên) |
| 7 | `.../submitted-form-detail` (snapshot v1, dùng bởi màn pre-approval/approved/rejected) | `SubmittedVisitRequestFormDetailDto` | ❌ | ❌ **(bị drop trong `MapMemberRow`)** | ❌ | khớp (thiếu cả 2 bên) |
| 8 | `{id}/edit-detail` | `EditableVisitRequestDetailDto` — **DTO đầy đủ nhất**: `PartnerId/PartnerName/PartnerIsActive/PartnerProfileStatus` + member `OrganizationPartnerId` | ✅ (nếu gọi được) | ✅ (nếu gọi được) | ❌ | **DEAD — route trả 410 Gone, không controller nào gọi handler này** |
| 9 | Minutes read (`GetVisitInstanceMinutesQueryHandler`) | `MinuteParticipantDto` | N/A | ❌ (chỉ `OrganizationSnapshot` text) | N/A | badge Partner ở màn Minutes đến từ API #2 riêng, KHÔNG từ API này |

**Kết luận D:** Đường dữ liệu đầy đủ nhất, đang HOẠT ĐỘNG, là API #1 (`GetVisitRequestFormV2`). API #4/5/6/7 là nơi cần vá trước tiên vì lỗi nằm ở tầng mapping nội bộ, không phải thiếu dữ liệu gốc.

---

## E. Current UI Coverage

| Screen (file) | Person type | Partner data available từ API? | Label hiện có? | Thiếu gì |
|---|---|---|---|---|
| `VisitRequestV2DetailView.tsx` (registrant section) | Registrant | ✅ có (`data.partnerId`) | ❌ chưa render dù data đã có | Chỉ cần thêm UI — data sẵn sàng |
| `CampusVisitDetailCard.tsx` → `PersonListTable.tsx` | Guest/Support | ✅ có ở nguồn (`ResolvedMember.organizationPartnerId`) | ❌ | Bị drop khi build row cho `PersonListTable` (`PersonRow` type không có field) |
| `OperationalContactReadOnly.tsx` | Contact | ❌ (xem §C.3/§G — chưa build) | ❌ | Cần quyết định model trước (§H) |
| `SubmittedVisitRequestInfoPanel.tsx` (v1 legacy) | Cả 3 nhóm | ❌ (DTO #7 không có) | ❌ | Cần sửa backend #7 trước |
| `RequestInfoReadOnly.tsx` (dùng bởi `VisitProcess.tsx`, `VisitProcessSummaryPage.tsx`) | Cả 3 nhóm | ❌ (DTO #4/#5 bug) | ❌ | Cần vá bug backend #4/#5 trước |
| `VisitContributionPage.tsx` | Registrant + Contact | ❌ (DTO #6 bug) | ❌ | Cần vá bug backend #6 trước |
| `VisitorVisitDetailPage.tsx`, `VisitRequestDetail.tsx` | Cả 3 nhóm | ❌ | ⚠️ có label "Đối tác" nhưng đang hiển thị NHẦM `registrantOrganization` text (cosmetic reuse, KHÔNG phải tín hiệu Partner thật) — **cần lưu ý đây là bug liên quan, không phải feature đã có** | Cần vá bug backend trước + sửa nhầm lẫn label này (ngoài phạm vi audit, chỉ ghi nhận) |
| `VisitRequestManagement.tsx` (list) | Registrant (hàng list) | 🟡 chỉ có tên, không có id | ❌ | Cần thêm field định danh (id hoặc flag) ở API list mới làm đúng được |
| `ParticipantPartnerCell.tsx` (Minutes) | Guest/Support (qua participant) | ✅ (API #2, đầy đủ nhất) | ✅ **ĐÃ CÓ** badge multi-state (Nội bộ/Đã liên kết/Gợi ý/Chưa liên kết) | Đây là hệ thống KHÁC (post-hoc matching cho biên bản), tín hiệu có thể lệch với `OrganizationPartnerId` lúc đăng ký — không dùng trực tiếp cho mục tiêu label này |

---

## F. Permission Analysis

| Role | Registrant info | Guest/Support info | Contact info | Partner match data (API #2) |
|---|---|---|---|---|
| Admin | ❌ (bị từ chối tường minh ở MỌI API xem đơn — quy tắc PERMISSION_MATRIX §5.4 hiện có) | ❌ | ❌ | ❌ (SEC-18) |
| HO | ✅ toàn bộ, mọi request | ✅ toàn bộ | ✅ toàn bộ | ✅ toàn bộ |
| Staff Leader | ✅ (own `PrimaryCampusId`) | ✅ (own campus) | ✅ (own campus) | ✅ (own campus) |
| IC Staff | ✅ (host/attending) | ✅ (host/attending) | ✅ (host/attending) | ✅ (Host hoặc participant ACCEPTED/ASSIGNED) |
| Dept Leader / Dept Staff | ✅ (assignment cụ thể — logistics/agenda/participant) | ✅ (cùng scope) | ✅ (cùng scope) | ✅ (cùng scope) |
| Student | ✅ (assignment cụ thể) | ✅ (cùng scope) | ✅ (cùng scope) | ✅ (participant ACCEPTED/ASSIGNED) |
| Visitor (registrant/contact) | ✅ (chính mình) | ✅ (đơn của mình) | ✅ (campus mình là contact) | ⚠️ **KHÔNG có nhánh nào cho registrant/contact trong `GetVisitGuestPartnerLinksQueryHandler`** — Visitor tự nộp đơn KHÔNG xem được dữ liệu match/partner-link của chính đơn mình qua API này |

**Điểm cần quyết định:** API #2 (`GetVisitGuestPartnerLinks`) vốn được thiết kế là công cụ nội bộ cho FPTU (matching cho biên bản), không cho khách xem đơn của chính họ. Nếu label mới muốn hiển thị cho Visitor xem đơn của họ, KHÔNG được lấy nguồn từ API #2 — phải dùng API #1 (`GetVisitRequestFormV2`, đã có `OrganizationPartnerId`/`PartnerId` và registrant/contact đã có quyền đọc theo đúng scope).

**Nguyên tắc bắt buộc giữ nguyên:** Admin bị loại khỏi MỌI API xem đơn hiện tại — đây là chủ ý (PERMISSION_MATRIX), thay đổi cho label KHÔNG được vô tình mở quyền này cho Admin.

---

## G. Multi-campus Analysis

Quan hệ Partner của Guest/Support/Contact nằm ở **PERSON/MEMBER level** (cụ thể: trên `VisitGuestMember`, gắn với `VisitInstanceFormDetail` — tức cấp INSTANCE cho Operational Contact do quan hệ NP-03 nằm trên bảng per-campus), KHÔNG phải REQUEST level. Registrant Partner (`VisitRequest.PartnerId`) là REQUEST level, hoàn toàn độc lập — không có FK/trigger nào nối nó với bất kỳ Partner nào của từng campus.

**Join chain đúng, campus-scoped, cho Operational Contact:**
```
VisitInstanceFormDetail (PK = VisitInstanceId, 1:1 với VisitRequestCampus)
  .OperationalContactGuestMemberId → VisitGuestMember.GuestMemberId
                                        .OrganizationPartnerId → Partner
```
Đây là 1 hop FK duy nhất, tự nhiên đã campus-scoped vì nằm trên bảng 1:1-per-campus — **không cần** đi qua bảng join `VisitInstanceGuestMember`.

**Rủi ro hiển thị nhầm:** Một `VisitGuestMember` (member "legacy", chưa từng bị sửa) CÓ THỂ được chia sẻ bởi nhiều campus của cùng 1 request cho tới khi 1 trong các campus đó bị edit lần đầu (copy-on-write mới tách bản ghi). Trước khi tách:
- Campus A và Campus B CÓ THỂ hợp lệ cùng trỏ `OperationalContactGuestMemberId` về CÙNG 1 `VisitGuestMember` — đây **không phải lỗi**, phản ánh đúng thực tế 1 người phụ trách cả 2 campus.
- Sau khi 1 campus bị sửa, COW tách bản ghi và campus kia giữ nguyên bản gốc — cách ly tự động.

**Điều kiện BẮT BUỘC để không hiển thị nhầm campus:** mọi truy vấn đọc PHẢI lấy `OperationalContactGuestMemberId` từ đúng `VisitInstanceFormDetail` của campus đang xem, KHÔNG được suy luận "contact của campus X" bằng cách match tên/tổ chức qua nhiều campus, và KHÔNG được query `VisitGuestMember` chỉ theo `GuestMemberId` rồi giả định nó thuộc riêng 1 campus mà không xác nhận qua đúng cột FK campus đó.

---

## H. Data Contract Gap

```
Đã có (backend, đủ tin cậy có điều kiện):
  VisitRequest.PartnerId                       — registrant, request-level
  VisitGuestMember.OrganizationPartnerId        — guest/support, member-level
  VisitGuestPartnerLink.PartnerId + MatchStatus — xác nhận độc lập (mạnh hơn nhưng KHÔNG
                                                   phủ 100% trường hợp — xem gap Amendment)

Thiếu hoàn toàn (schema-level):
  Operational Contact KHÔNG có PartnerId trực tiếp — chỉ có đường vòng 2 hop, CHƯA API nào
  join. Đây KHÔNG phải lỗi mapping, mà là CHƯA THIẾT KẾ.

Thiếu ở tầng DTO-mapping (backend, SỬA ĐƯỢC không cần đổi schema):
  GetVisitProcessDetail / GetVisitInstanceSummary / GetVisitInstanceContribution đều
  drop OrganizationPartnerId dù dữ liệu đã resolve sẵn trong VisitFormReadService.
  GetSubmittedVisitRequestFormDetail cũng drop tương tự.

Thiếu để label ĐÁNG TIN CẬY (không chỉ "có id"):
  - Registrant: field hiện có (PartnerId) đã đủ tin cậy vì server re-validate
    ACTIVE+APPROVED tại mọi lần ghi và ép RegistrantOrganization = Partner.Name — KHÔNG cần
    thêm field.
  - Guest/Support: OrganizationPartnerId một mình KHÔNG đủ mạnh (gap Amendment ở §B/§C.2).
    Muốn label chính xác 100%, cần join thêm điều kiện
    VisitGuestPartnerLink.MatchStatus == CONFIRMED cho đúng GuestMemberId đó — HOẶC chấp
    nhận rủi ro đã biết (gap hẹp, chỉ xảy ra qua đường Amendment-approve) và ghi nhận rõ.
  - List screen (`ViewGuestDelegationList`): chỉ có `PartnerName` string, KHÔNG đủ để làm
    badge tin cậy (tên trùng không chứng minh được nguồn gốc) — cần thêm `PartnerId` hoặc 1
    boolean vào DTO.
  - Operational Contact: cần quyết định thiết kế trước khi code — xem Khuyến nghị.
```

---

## I. Recommended Implementation

**Chỉ đề xuất, KHÔNG thực hiện trong bước audit này.**

1. **Backend — vá lỗ hổng DTO-mapping có sẵn (rủi ro thấp nhất, giá trị cao nhất):**
   Copy `OrganizationPartnerId` trong `MapRow`/`MapMemberRow` của `GetVisitProcessDetailQueryHandler.cs`, `GetVisitInstanceSummaryQueryHandler.cs`, `GetVisitInstanceContributionQueryHandler.cs`, `GetSubmittedVisitRequestFormDetailQueryHandler.cs` — dữ liệu nguồn đã có sẵn trong `VisitFormMemberRow`, chỉ là bị bỏ sót khi map sang DTO.
2. **Backend — DTO changes:** Thêm `PartnerName` (resolve tên, không bắt frontend tự lookup) đi kèm mọi `PartnerId`/`OrganizationPartnerId` đã hoặc sẽ được trả — tránh vi phạm quy tắc "không suy nguồn gốc bằng cách so tên" (mục 20 của yêu cầu gốc) theo hướng ngược, tức nếu trả PartnerId thì trả kèm luôn tên chính thức từ Partner record, không dùng tên snapshot cũ.
3. **Backend — Guest/Support reliability:** cân nhắc thêm cờ phái sinh (không phải cột DB mới) kiểu `isConfirmedPartnerLink` tại tầng DTO cho các case cần độ tin cậy cao (vd biên bản), join `VisitGuestPartnerLink` theo `GuestMemberId` + `MatchStatus=CONFIRMED`. Ở các màn "xem đơn" thông thường, `OrganizationPartnerId != null` (đã qua validate ACTIVE+APPROVED+PUBLIC tại write-time) có thể đủ dùng — đây là quyết định trade-off giữa độ chính xác tuyệt đối và độ phức tạp, cần chọn tường minh, không mặc định.
4. **Backend — Operational Contact:** đây là quyết định nghiệp vụ, không phải kỹ thuật thuần — 2 lựa chọn:
   - (a) Xây API đọc mới join `OperationalContactGuestMemberId → VisitGuestMember.OrganizationPartnerId`, chỉ hiển thị badge khi quan hệ NP-03 tồn tại (chấp nhận: Contact vừa Replace/Transfer sẽ KHÔNG có badge cho tới khi được liên kết lại với 1 member — đúng với thực tế dữ liệu hiện có).
   - (b) Không làm badge cho Contact ở giai đoạn này, chỉ làm cho Registrant + Guest/Support — vì Contact chưa từng có khái niệm "tự chọn Partner" trong toàn bộ luồng ghi hiện tại.
5. **Backend — List screen:** thêm `PartnerId` (hoặc 1 boolean) vào `VisitRequestManagementItemDto` nếu muốn badge xuất hiện ở danh sách quản lý.
6. **Backend — permission:** nếu dùng API #2 (`GetVisitGuestPartnerLinks`) làm nguồn cho bất kỳ màn nào Visitor có thể xem, phải bổ sung nhánh registrant/operational-contact vào allow-list của handler đó — hiện KHÔNG có. Khuyến nghị: KHÔNG dùng API #2 làm nguồn chính cho label này; dùng API #1 (đã đúng scope cho mọi role liên quan).
7. **Frontend — types:** thêm field còn thiếu vào `PersonRow` (`PersonListTable.tsx`), `ResolvedOperationalContact`, `Submitted*`, `VisitProcess*` types tương ứng với backend đã bổ sung.
8. **Frontend — component:** dùng **2 mức badge khác nhau theo đúng tiền lệ đã có trong code** — pill mạnh (giống `PartnerOrgCombobox.tsx`) CHỈ cho cấp Registrant/request; badge nhẹ dạng text (giống `OrganizationCombobox.tsx` "✓ Có trong hệ thống") cho cấp Guest/Support/Contact — để không tạo cảm giác sai rằng tất cả cùng 1 Partner. Cân nhắc tạo 1 component dùng chung nhỏ (vd `PartnerBadge`) nhận prop `strength: 'strong'|'light'` thay vì copy-paste JSX vào từng màn — nhưng đây là quyết định kỹ thuật nhỏ, không phải business logic.
9. **i18n:** dùng lại pattern namespace hiện có; khoá gần nhất đã tồn tại `visitRequestV2.json` → `"partnerExisting": "Đối tác đã có trong hệ thống"` — có thể tái dùng trực tiếp hoặc làm biến thể ngắn hơn cho badge nhẹ.
10. **KHÔNG sửa (ngoài phạm vi):** `VisitRequestDetail.tsx`/`VisitorVisitDetailPage.tsx` đang có label "Đối tác" hiển thị nhầm `registrantOrganization` — đây là bug liên quan nhưng KHÁC nhiệm vụ (gắn nhầm text vào label sẵn có, không phải thiếu tín hiệu Partner) — ghi nhận riêng, không gộp vào thay đổi này.

---

## J. Files To Change

| File | Change | Reason | Risk |
|---|---|---|---|
| `backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs` | REQUIRED — copy `OrganizationPartnerId` trong `MapRow` | dữ liệu đã có sẵn, đang bị rớt | Thấp |
| `backend/PEMS.Application/Delegations/Queries/GetVisitInstanceSummary/GetVisitInstanceSummaryQueryHandler.cs` | REQUIRED — cùng fix | cùng lý do | Thấp |
| `backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs` | REQUIRED — cùng fix | cùng lý do | Thấp |
| `backend/PEMS.Application/Delegations/Queries/GetSubmittedVisitRequestFormDetail/GetSubmittedVisitRequestFormDetailQueryHandler.cs` | REQUIRED — cùng fix + thêm `PartnerId/PartnerName` request-level | v1 legacy vẫn đang phục vụ màn pre-approval/approved/rejected | Thấp-Trung (DTO v1, cần check consumer) |
| `backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs` + handler | OPTIONAL — thêm `PartnerId`/flag | chỉ cần nếu muốn badge ở list quản lý | Thấp |
| Operational-contact read path (mới, chưa tồn tại) | REQUIRED nếu chọn phương án (a) ở §I.4 | hiện chưa có API nào join Contact→Partner | Trung — cần thiết kế join mới, quyết định nghiệp vụ trước |
| `frontend/.../v2/CampusVisitDetailCard.tsx` + `shared/PersonListTable.tsx` | REQUIRED — truyền/khai báo `organizationPartnerId` | dữ liệu có ở `ResolvedMember` nhưng bị bỏ khi build row | Thấp |
| `frontend/.../v2/VisitRequestV2DetailView.tsx` | REQUIRED — render `data.partnerId` (đã có sẵn, chưa dùng) | quick win, không cần đổi backend | Thấp |
| `frontend/.../v2/OperationalContactReadOnly.tsx` | REQUIRED nếu chọn phương án (a) ở §I.4; DO NOT CHANGE nếu chọn (b) | phụ thuộc quyết định nghiệp vụ | Trung |
| `frontend/.../delegations/components/SubmittedVisitRequestInfoPanel.tsx`, `RequestInfoReadOnly.tsx`, `VisitContributionPage.tsx` | REQUIRED sau khi backend #4/#5/#6/#7 được vá | hiện types không có field | Thấp (chờ backend trước) |
| `frontend/.../visit-request/components/shared/OrganizationCombobox.tsx`, `PartnerOrgCombobox.tsx` | DO NOT CHANGE | đã đúng, đây là NGUỒN quyết định thiết kế 2-mức-badge cần noi theo, không phải nơi cần sửa | — |
| `backend/.../GetEditableVisitRequestDetail*` | DO NOT CHANGE (trong phạm vi này) | dead code — nếu muốn hồi sinh, đó là quyết định riêng ngoài scope label | — |
| `frontend/.../features/partners/components/ParticipantPartnerCell.tsx` | DO NOT CHANGE | hệ thống match riêng cho biên bản, tín hiệu khác, không phải nguồn cho label này | — |
| `VisitRequestDetail.tsx` / `VisitorVisitDetailPage.tsx` label "Đối tác" hiển thị sai | DO NOT CHANGE (trong phạm vi này) | bug liên quan nhưng khác nhiệm vụ — ghi nhận riêng | — |

---

## K. Regression Risks

- **Permission:** phải không được lấy dữ liệu Partner từ API #2 cho màn Visitor có thể xem (hiện API đó không cho registrant/contact) — nếu vô tình đổi allow-list của API #2 để "tiện" thì phá vỡ ranh giới nội bộ/khách đã có chủ đích (SEC-18).
- **Multi-campus:** phải lấy `OperationalContactGuestMemberId` đúng từ `VisitInstanceFormDetail` của ĐÚNG campus đang xem — không suy luận qua tên.
- **Old requests:** field `OrganizationPartnerId`/`PartnerId` có thể null với đơn cũ (trước khi tính năng Partner tồn tại) — phải render "không có label" một cách im lặng, không lỗi.
- **Partner inactive/rejected:** FK vẫn sống (SET NULL chỉ khi Partner bị XOÁ, mà Partner không bao giờ bị xoá cứng) — nên badge có thể hiển thị cho 1 Partner đã REJECTED/INACTIVE nếu không kiểm tra `ProfileStatus`/`CooperationStatus` tại thời điểm hiển thị. Cần quyết định: badge "đã chọn từ hệ thống" có nên vẫn hiện dù Partner hiện tại không còn active hay không (khuyến nghị: vẫn hiện, vì đây là fact lịch sử "đã chọn từ hệ thống lúc đó", không phải "Partner hiện đang active") — nhưng phải QUYẾT ĐỊNH rõ, không mặc định.
- **Null:** field không tồn tại ở DTO cũ (v1 legacy) — cần optional-safe ở frontend.
- **Notification deep-link:** các trang đích của deep-link (`VisitRequestDetail.tsx`, v.v.) nằm trong nhóm DTO còn thiếu field (§D #4-7) — phải vá backend trước khi các trang này có gì để hiển thị.
- **Amendment gap (đã nêu ở B/C.2):** không được dùng `VisitGuestPartnerLink.MatchStatus=CONFIRMED` làm ĐIỀU KIỆN BẮT BUỘC duy nhất để hiện badge cho Guest/Support, vì sẽ ẩn nhầm badge cho các member hợp lệ được tạo qua amendment (link resolver không chạy ở path đó).

---

## L. Test Plan (đề xuất, chưa viết code)

**Backend unit/integration:**
- `GetVisitProcessDetailQueryHandler`/`GetVisitInstanceSummaryQueryHandler`/`GetVisitInstanceContributionQueryHandler`: guest có `OrganizationPartnerId` → DTO trả đúng id (regression test cho bug đang có).
- Registrant có `PartnerId` set qua Create → DTO đọc lại đúng id + tên đã ép đồng bộ.
- Guest có `OrganizationPartnerId` qua Amendment-approve (không qua resolver) → vẫn đọc được id ở DTO (xác nhận field vẫn hiện dù không có `VisitGuestPartnerLink`).
- Partner bị Reject sau khi đã được chọn → request cũ đọc lại vẫn trả đúng `PartnerId` (FK sống), không lỗi.
- Multi-campus: member legacy chia sẻ 2 campus → cả 2 campus đọc đúng cùng 1 Partner (trước COW); sau khi 1 campus bị edit → 2 campus tách biệt, không lẫn.
- Permission: Visitor gọi API #1 cho đơn của chính họ → thấy `PartnerId`; gọi API #2 → bị từ chối (xác nhận hành vi hiện tại, để không vô tình "sửa" permission khi thêm field).

**Frontend component:**
- Badge KHÔNG hiện khi field null/undefined (đơn cũ).
- Badge cấp Registrant dùng style mạnh; cấp Guest/Support dùng style nhẹ (snapshot test phân biệt 2 style).
- List quản lý: không hiện badge nếu chỉ có `partnerName` mà không có id (tránh suy đoán qua tên).

**Test matrix (theo yêu cầu gốc mục 18):**

| Case | Partner system | Custom | Viewer | Expected |
|---|---|---|---|---|
| Registrant chọn Partner | ✓ | | Registrant | Badge mạnh hiện |
| Registrant custom | | ✓ | Registrant | Không hiện |
| Guest chọn Partner (qua create/edit) | ✓ | | Staff có quyền | Badge nhẹ hiện |
| Guest chọn Partner qua Amendment | ✓ | | Staff có quyền | Badge nhẹ vẫn hiện dù không có `VisitGuestPartnerLink` |
| Guest custom | | ✓ | Staff | Không hiện |
| External support chọn Partner | ✓ | | Viewer có quyền | Badge nhẹ hiện |
| Operational Contact | tuỳ quyết định §I.4 | | Viewer có quyền | Theo phương án đã chọn — nếu (b) thì KHÔNG hiện ở giai đoạn này |
| Multi-campus A/B, member legacy chia sẻ | ✓/✓ | | Staff Leader campus A | Chỉ thấy đúng scope campus A, không lẫn campus B |
| Partner inactive/rejected (đơn cũ) | ✓ (lịch sử) | | Viewer có quyền | Vẫn hiện badge (theo quyết định K), không crash, không mất dữ liệu |
| Đơn cũ trước khi có tính năng | null | null | Viewer có quyền | Không hiện badge, không lỗi |
| Unauthorized viewer | ✓ | | Không có quyền | Không leak — 403 như hiện tại, field Partner không bao giờ tới FE |
| Visitor xem đơn của chính mình | ✓ | | Registrant/Contact | Hiện đúng qua API #1 (không phải API #2) |

---

## M. Final Verdict

1. **Đã có đủ dữ liệu để biết "đã chọn Partner trong hệ thống" chưa?**
   Có, cho **Registrant** và **Guest/External-Support** (2 cột `PartnerId`/`OrganizationPartnerId` thật, có validate tại write-time). **Chưa** cho **Operational Contact** (không có cột Partner nào, chỉ có đường vòng chưa được xây).

2. **Nếu chưa, thiếu chính xác ở đâu?**
   - Operational Contact: thiếu hoàn toàn ở tầng schema/API — cần quyết định nghiệp vụ trước (§I.4), không phải chỉ thiếu code.
   - 4 API đọc (VisitProcess detail/summary/contribution, Submitted-form-detail): thiếu ở tầng DTO-mapping backend (bug rớt field, SỬA ĐƯỢC nhanh).
   - List quản lý: thiếu `PartnerId`/flag để làm badge tin cậy (hiện chỉ có tên).

3. **Có cần database migration không?**
   Không, cho Registrant/Guest/Support — cột đã tồn tại. **Có thể cần** nếu chọn xây badge trực tiếp (không qua đường vòng) cho Operational Contact — nhưng khuyến nghị KHÔNG migrate, dùng đường vòng NP-03 sẵn có (phương án (a) ở §I.4) để tránh đổi schema.

4. **Có cần thay đổi business logic không?**
   Không cần đổi logic ghi (write flow) hiện tại. Chỉ cần bổ sung ở tầng đọc (mapping DTO) và hiển thị. Ngoại lệ: nếu muốn độ tin cậy tuyệt đối cho Guest/Support (đóng gap Amendment ở §B), cần quyết định có bổ sung việc gọi `GuestPartnerLinkResolver` vào đường Amendment-approve hay không — đây LÀ thay đổi business logic, nằm NGOÀI phạm vi "chỉ thêm label", nên khuyến nghị KHÔNG làm trong đợt này, chỉ ghi nhận rủi ro đã biết.

5. **Có thể chỉ sửa backend DTO + frontend UI không?**
   Có, cho Registrant + Guest/Support + 4 API đang bug. Cho Operational Contact thì KHÔNG — cần quyết định nghiệp vụ trước khi biết "chỉ sửa DTO" có đủ hay không.

6. **Những màn hình nào chắc chắn phải update?**
   `VisitRequestV2DetailView.tsx` (quick win), `CampusVisitDetailCard.tsx`/`PersonListTable.tsx`, và sau khi backend được vá: `RequestInfoReadOnly.tsx` (dùng bởi `VisitProcess.tsx`, `VisitProcessSummaryPage.tsx`), `SubmittedVisitRequestInfoPanel.tsx`, `VisitContributionPage.tsx`.

7. **Những màn hình nào KHÔNG được update vì khác scope/quyền?**
   Bất kỳ màn nào dùng API #2 (`GetVisitGuestPartnerLinks`) cho Visitor xem đơn của chính họ — API đó không cấp quyền cho registrant/contact, không được "mở" quyền này chỉ để tiện lấy dữ liệu Partner. `ParticipantPartnerCell.tsx`/Minutes giữ nguyên (hệ thống khác). Không đổi hành vi loại trừ Admin khỏi mọi API xem đơn.

8. **Có nguy cơ hiển thị nhầm Partner giữa các campus/person không?**
   Có nguy cơ NẾU implementation tương lai suy luận "contact của campus X" bằng cách match tên/tổ chức thay vì đọc đúng FK `OperationalContactGuestMemberId` của đúng `VisitInstanceFormDetail` campus đó (§G). Không có nguy cơ nếu tuân thủ đúng join chain đã mô tả.

9. **Đề xuất implementation nhỏ nhất nhưng đủ chính xác là gì?**
   (1) Vá 4 chỗ backend đang rớt `OrganizationPartnerId`/`PartnerId` trong DTO-mapping (rủi ro thấp nhất, giá trị cao). (2) Render `data.partnerId` đã có sẵn ở `VisitRequestV2DetailView.tsx` và truyền `organizationPartnerId` qua `PersonListTable`. (3) Thêm 1 component badge dùng chung 2 mức mạnh/nhẹ theo đúng tiền lệ `PartnerOrgCombobox`/`OrganizationCombobox`. (4) TẠM HOÃN Operational Contact cho tới khi có quyết định nghiệp vụ rõ ràng — không đoán.

---

*Audit thực hiện qua 5 agent nghiên cứu song song, đọc trực tiếp source code + test suite hiện có. Mọi kết luận trích dẫn file:line cụ thể trong báo cáo gốc của từng agent (lưu trong lịch sử phiên làm việc). Không có thay đổi nào được thực hiện lên code/DB trong quá trình audit.*
