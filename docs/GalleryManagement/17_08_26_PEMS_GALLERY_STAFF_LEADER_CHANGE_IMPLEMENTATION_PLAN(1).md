# PEMS — Detailed Implementation Plan for Staff Leader Gallery Changes

**Target repository:** `quangthoai04/PEMS`  
**Reference branch for codebase study:** `Dev`  
**Branch to modify:** **the branch currently checked out when the AI Agent starts the task**  
**Module:** Staff Leader → VisitFPTU Gallery / Gallery Location Management  
**Primary role:** `STAFF_LEADER`  
**Purpose of this document:** Provide an AI coding agent with a precise implementation plan for three requested Gallery changes, including required behavior, affected layers, implementation rules, regression risks, and acceptance criteria.

## Branch Policy — IMPORTANT

The AI Agent must follow this branch rule exactly:

1. **Read/study the PEMS codebase on branch `Dev`** to understand the latest intended architecture, business rules, Gallery implementation, authorization model, translation flow, file-storage behavior, and test patterns.
2. **Do NOT implement changes directly on `Dev` unless `Dev` is already the current working branch.**
3. The actual code modifications must be made on **the branch that is currently checked out when the task begins**.
4. Before making any change, record the current branch:

```bash
git branch --show-current
```

5. Use `Dev` only as a reference baseline. Inspect it without changing the working branch where possible, for example:

```bash
git show Dev:path/to/file
git diff Dev...HEAD -- path/to/relevant/file
```

or use the repository/GitHub tools to read files from `Dev`.

6. **Do not automatically checkout, reset, merge, rebase, or cherry-pick `Dev` into the current branch.**
7. If the current branch differs from `Dev`, compare the relevant Gallery files first. The implementation must be adapted to the **current branch's actual code**, while using `Dev` to understand the intended/latest system behavior.
8. If a file/path/class exists on `Dev` but has been renamed, moved, or changed on the current branch, do not blindly recreate the `Dev` structure. Locate the current-branch equivalent and implement there.
9. If there is a conflict between:
   - assumptions in this document,
   - the `Dev` reference implementation,
   - and the current branch's actual code,

   then:
   - preserve the requested business behavior in this document,
   - integrate it into the current branch's real architecture,
   - and explicitly report the difference.
10. All commits/edits/tests for this task must remain on the **current branch**.

In short:

```text
READ / UNDERSTAND FROM: Dev
IMPLEMENT / MODIFY ON: current checked-out branch
```

---

# 1. Scope of Change

Implement the following three changes in the current PEMS Gallery module:

1. **Increase the upload size limit for Gallery Item media images**
   - Current Gallery Item images are limited to 5 MB.
   - Real Gallery photos are commonly larger than 5 MB.
   - Increase the limit for Gallery Item / Visit Delegation images to **20 MB per image**.
   - Do **not** unintentionally increase the limit for Gallery Location cover images or unrelated image purposes.

2. **Add permanent user-facing “Delete Gallery Item” functionality**
   - Current Gallery Item management supports only `PUBLISHED` / `HIDDEN`.
   - Add a separate **Delete** action.
   - Delete must use **soft delete**, not physical database deletion.
   - Deleted items must disappear from normal management and public Gallery results.
   - Do not immediately remove Google Drive binary files during the normal delete action.

3. **Change Area rename behavior in Gallery Location Management**
   - Current data model allows multiple `GalleryLocation` records to reference one shared `GalleryArea`.
   - Current edit handler updates the shared `GalleryArea` row directly, therefore renaming an Area from one location changes the Area name for every sibling location.
   - New rule:
     - **Changing the Area name from one location must affect only that location.**
     - Other locations that previously shared the same Area must keep the old Area name.
     - **If only the Area cover video changes and the Area name does not change, keep the current shared behavior:** the new video still applies to every location belonging to that Area.
     - If both Area name and Area video change together, the edited location must move to a newly separated Area and the new video belongs only to that new Area.

---

# 2. Current Architecture and Important Existing Behavior

## 2.1 Gallery structure

Current core relationship:

```text
Campus
└── GalleryArea
    ├── GalleryLocation
    │   └── GalleryItem
    │       ├── GalleryItemContent
    │       └── GalleryItemMedia
    └── GalleryLocation
```

Important implications:

- `GalleryArea` owns:
  - `AreaId`
  - `CampusId`
  - `AreaName`
  - `AreaNameEn`
  - `AreaKey`
  - `CoverFileId`
  - status / display order
  - translation metadata

- `GalleryLocation` owns:
  - `LocationId`
  - `AreaId`
  - `LocationName`
  - `LocationNameEn`
  - `LocationKey`
  - `CoverFileId`
  - status / display order
  - translation metadata

Therefore, multiple locations sharing the same `AreaId` necessarily share the same Area name and Area cover video.

Relevant current entities:

```text
backend/PEMS.Domain/Entities/Galleries/GalleryArea.cs
backend/PEMS.Domain/Entities/Galleries/GalleryLocation.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItem.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItemMedia.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItemContent.cs
```

---

## 2.2 Current Staff Leader Gallery APIs

Primary controller:

```text
backend/PEMS.Api/Controllers/GalleriesController.cs
```

Current Gallery Item management includes:

```text
GET  /api/Galleries/viewgalleryitemlist
GET  /api/Galleries/searchgalleryitems
GET  /api/Galleries/viewgalleryitemdetails
POST /api/Galleries/addgalleryitem
POST /api/Galleries/updategalleryitem
POST /api/Galleries/changegalleryitemstatus
```

Current Area / Location management includes:

```text
GET  /api/Galleries/viewgallerylocationlist
POST /api/Galleries/creategallerylocation
POST /api/Galleries/updategallerylocation
POST /api/Galleries/changegallerylocationstatus
```

The controller is already protected by:

```text
[Authorize]
[RoleAuthorize(EffectiveRole.StaffLeader)]
```

Handlers also enforce Staff Leader campus scope using the current user rather than trusting campus data from the client.

Any new delete action must follow the same authorization pattern and must not trust client-provided campus scope.

---

# 3. Change 1 — Increase Gallery Item Image Size Limit

## 3.1 Requested behavior

Gallery Item images uploaded through:

```text
Staff Leader
→ Quản lý Gallery
→ + Tải lên Media
→ Chọn ảnh
```

must support image files up to:

```text
20 MB per image
```

Supported formats remain:

```text
.jpg
.jpeg
.png
.webp
```

Maximum media count remains:

```text
20 total media per Gallery Item
```

Video upload rule remains unchanged:

```text
Video files are NOT uploaded from the local machine for Gallery Items.
Videos are added through YouTube URLs.
```

Do not alter the Area cover video rule:

```text
MP4
≤ 100 MB
≤ 120 seconds on frontend validation
```

Do not alter Gallery Location cover images unless separately requested.

---

## 3.2 Current frontend problem

Current shared frontend validator:

```text
frontend/pems-react/src/shared/utils/fileValidation.ts
```

contains a `GALLERY_IMAGE` rule with:

```text
maxSizeBytes = 5 * MB
```

`GalleryUpsertModal.tsx` validates Gallery Item media through that shared purpose.

Therefore an image above 5 MB is rejected before upload.

Current UI also explicitly says approximately:

```text
JPG/PNG/WEBP ≤5MB
```

That helper text must be updated.

---

## 3.3 Current backend problem

Backend validator:

```text
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
```

currently groups these image purposes under the 5 MB image rule:

```text
GalleryAreaCover
GalleryLocationCover
GalleryItemImage
GalleryDelegationImage
...
```

This means changing only frontend validation is insufficient.

Backend must also accept the new Gallery Item image size.

---

## 3.4 Required implementation

### Frontend

Prefer introducing a dedicated frontend validation purpose instead of globally increasing all Gallery image sizes.

Recommended change:

```ts
type FilePurpose =
  | ...
  | 'GALLERY_ITEM_IMAGE'
  | 'GALLERY_LOCATION_COVER'
  ...
```

Rules:

```text
GALLERY_ITEM_IMAGE        → 20 MB
GALLERY_LOCATION_COVER    → 5 MB
```

Then modify Gallery Item upload UI:

```text
frontend/pems-react/src/pages/dashboard/gallery/GalleryUpsertModal.tsx
```

Replace the current validation call:

```text
validateFile(file, 'GALLERY_IMAGE')
```

with a Gallery Item-specific validation purpose.

Update all displayed helper/error text from:

```text
≤5MB
```

to:

```text
≤20MB
```

for Gallery Item upload only.

Do not accidentally change Location cover text/rules.

### Backend

Modify:

```text
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
```

Split the current grouped image rule.

Required rule:

```text
FilePurpose.GalleryItemImage
FilePurpose.GalleryDelegationImage
    → MaxSizeBytes = 20 MB
    → image/jpeg
    → image/png
    → image/webp
    → .jpg/.jpeg/.png/.webp
    → RequireImageMagicBytes = true
```

Keep these at the old limit unless another requirement says otherwise:

```text
GalleryAreaCover
GalleryLocationCover
VisitRequestPhoto
PartnerLogo
PartnerCover
NewsImage
...
```

### Controller request limit

Current `GalleriesController` already allows a much larger multipart request body (1 GB headroom), so no increase should be needed there unless the actual current code changes before implementation.

The AI agent must verify this before modifying request limits.

---

## 3.5 Required acceptance cases

### Should pass

```text
8 MB JPG Gallery Item image
12 MB PNG Gallery Item image
19.9 MB WEBP Gallery Item image
multiple valid Gallery Item images with total media count ≤ 20
```

### Should fail

```text
>20 MB Gallery Item image
SVG
GIF
PDF
MP4 uploaded through the image picker
more than 20 total Gallery media sources
```

### Regression checks

```text
Gallery Location cover image still uses its intended smaller limit.
Area video remains MP4 ≤100 MB and frontend duration ≤120s.
News images are not unintentionally increased to 20 MB.
Visit photos are not unintentionally increased.
```

---

# 4. Change 2 — Add Delete Gallery Item

## 4.1 Requested behavior

Current Gallery Item management supports:

```text
PUBLISHED
HIDDEN
```

Add an explicit:

```text
DELETE
```

user action.

Delete is not the same as Hide.

### Hide

```text
PUBLISHED → HIDDEN
```

Properties:

- record still exists
- Staff Leader can still find/manage it
- Staff Leader can publish it again
- public Gallery does not show it

### Delete

Properties:

- item disappears from normal Staff Leader Gallery list
- item disappears from public Gallery
- item can no longer be edited
- item can no longer be hidden/published
- item should not appear in normal search/filter/counts
- retain enough database history for audit
- use soft delete

---

# 5. Soft Delete Design

## 5.1 Existing entity support

`GalleryItem` already has fields similar to:

```text
DeletedAt
DeletedBy
```

`GalleryItemMedia` also already has:

```text
DeletedAt
DeletedBy
```

Therefore the implementation should use the existing soft-delete design rather than adding a second status such as:

```text
DELETED
```

unless the repository has changed and now requires another policy.

Do not hard-delete the Gallery Item row.

---

## 5.2 Recommended backend command

Create:

```text
backend/PEMS.Application/Galleries/Commands/DeleteGalleryItem/
```

Suggested files:

```text
DeleteGalleryItemCommand.cs
DeleteGalleryItemCommandHandler.cs
```

Suggested request:

```text
galleryItemId
```

No campus ID should be accepted from the frontend for authorization.

---

## 5.3 Handler flow

Required behavior:

```text
1. Resolve authenticated Staff Leader campus with StaffLeaderGalleryScope.
2. Load Gallery Item.
3. Include/resolve its Location → Area → Campus.
4. Ensure the item belongs to the Staff Leader's campus.
5. Reject if item does not exist.
6. Reject or idempotently handle if already deleted.
7. Set GalleryItem.DeletedAt = now.
8. Set GalleryItem.DeletedBy = actorId.
9. Soft-delete related GalleryItemMedia:
      DeletedAt = now
      DeletedBy = actorId
   if this matches current repository convention.
10. Write AuditLog.
11. SaveChanges.
12. Return success DTO/message.
```

Recommended audit action:

```text
DELETE_GALLERY_ITEM
```

Recommended audit data:

```json
{
  "galleryItemId": 123,
  "title": "Campus Experience 2026",
  "locationId": 10,
  "areaId": 3,
  "deletedAt": "...",
  "deletedBy": 456
}
```

Do not expose unnecessary private details.

---

# 6. Do Not Physically Delete Google Drive Files During Normal Delete

Normal delete should not immediately call Google Drive delete.

Reason:

- database uses soft delete
- audit/history should remain valid
- Drive is outside the MySQL transaction
- deleting Drive first can leave broken DB references
- deleting DB first and Drive second can leave inconsistent state if Drive fails
- existing project already has best-effort compensation logic for orphan files

Recommended lifecycle:

```text
User deletes Gallery Item
    ↓
Soft delete database rows
    ↓
Public and management queries exclude deleted rows
    ↓
Binary files remain in Google Drive
    ↓
Optional future retention/purge process may physically remove old files
```

If product later requires restore, keeping the binary makes restore feasible.

---

# 7. New Delete API

To remain consistent with the current controller's verb-route style, recommended endpoint:

```http
POST /api/Galleries/deletegalleryitem
```

Possible body:

```json
{
  "galleryItemId": 123
}
```

Alternative REST DELETE is acceptable only if the repository is being systematically migrated to REST-style endpoints.

Do not introduce a one-off inconsistent style without reason.

Add controller action to:

```text
backend/PEMS.Api/Controllers/GalleriesController.cs
```

The controller-level Staff Leader authorization already exists and must remain.

The handler must still enforce campus scope.

---

# 8. Query Changes Required for Soft Delete

All management Gallery Item queries must exclude deleted items by default.

Review at least:

```text
ViewGalleryItemList
SearchGalleryItems
ViewGalleryItemDetails
GetGalleryFilterOptions if counts derive from items
GalleryDetailBuilder
```

Expected condition:

```text
GalleryItem.DeletedAt == null
```

For detail endpoints, requesting a deleted item should normally return the same not-found/business-not-found behavior used for inaccessible records.

Public Gallery queries must continue to exclude:

```text
GalleryItem.DeletedAt != null
GalleryItemMedia.DeletedAt != null
```

Audit all public query handlers, including likely files under:

```text
backend/PEMS.Application/Galleries/Public/
```

Do not assume every public query already filters correctly—verify all of them.

---

# 9. Frontend Delete UI

Primary page:

```text
frontend/pems-react/src/pages/dashboard/gallery/GalleryManagementStaffLeader.tsx
```

Likely API client:

```text
frontend/pems-react/src/features/gallery-management/api/galleryManagementApi.ts
```

Likely types:

```text
frontend/pems-react/src/features/gallery-management/types/galleryManagement.types.ts
```

Add Delete action to Gallery Item row actions.

Recommended actions:

```text
View
Edit
Hide / Show
Delete
```

Use the existing design language:

- Trash icon for destructive delete
- red/destructive semantic style
- do not use X icon for permanent delete

---

# 10. Delete Confirmation UX

Do not immediately delete on icon click.

Show a confirmation modal.

Recommended copy:

```text
Xóa nội dung Gallery?

Nội dung này sẽ không còn xuất hiện trong Quản lý Gallery và VisitFPTU.
Xóa khác với Ẩn nội dung và không thể bật lại bằng nút Hiện/Ẩn.

[Hủy] [Xóa]
```

If restore is definitely not exposed to users, optionally state:

```text
Bạn sẽ không thể khôi phục nội dung này từ giao diện hiện tại.
```

On success:

```text
Xóa nội dung Gallery thành công.
```

Then refresh list or remove row optimistically.

On failure:

- retain row
- show backend error
- do not silently hide the record

---

# 11. Change 3 — Area Rename Must Affect Only the Current Location

## 11.1 Existing problem

Current handler:

```text
backend/PEMS.Application/Galleries/Commands/UpdateGalleryLocation/UpdateGalleryLocationCommandHandler.cs
```

loads:

```text
location
location.Area
```

and updates the current `GalleryArea` in place.

Current conceptual behavior:

```text
AreaId = 2
AreaName = "Tòa B"

Location 101 → AreaId 2
Location 102 → AreaId 2
Location 103 → AreaId 2
```

If the handler changes:

```text
AreaName = "Tòa Beta"
```

all three locations display `Tòa Beta`.

That behavior is structurally correct for the old shared-area design, but it no longer matches the new requirement.

---

# 12. New Area Rename Rule

New required behavior:

Initial:

```text
Tòa B
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

Edit `Sảnh 1`:

```text
Tên khu vực:
Tòa B → Tòa Beta
```

Expected result:

```text
Tòa Beta
└── Sảnh 1

Tòa B
├── Tầng 1
└── Đồi Blockchain
```

Important:

This is not logically possible by simply updating one field on `GalleryLocation`, because the Area name is stored on `GalleryArea`.

The correct implementation is to **separate the edited location into another Area when needed**.

---

# 13. Required Pattern — Copy-on-Write Area Split

Use a copy-on-write strategy.

## Case A — Area name unchanged

Example:

```text
Tòa B
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

User edits only:

```text
Area cover video
B.mp4 → B-new.mp4
```

Expected:

```text
Tòa B — B-new.mp4
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

Implementation:

```text
location.AreaId remains unchanged
currentArea.CoverFileId is replaced
```

Therefore every location sharing that Area gets the new video.

This preserves the original rule exactly.

---

## Case B — Area name changes and Area has multiple Locations

Initial:

```text
AreaId 2
Tòa B
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

User edits `Sảnh 1`:

```text
Tòa B → Tòa Beta
```

Expected backend behavior:

```text
1. DO NOT mutate AreaId 2's AreaName.
2. Create new GalleryArea, e.g. AreaId 8:
      CampusId = same campus
      AreaName = "Tòa Beta"
      AreaKey = normalized key
      status = appropriate copied/default state
      display order = appropriate copied/generated value
      translation fields = calculated for new name
      CoverFileId = inherited or new uploaded video
3. Set:
      Sảnh 1.AreaId = 8
4. Keep:
      Tầng 1.AreaId = 2
      Đồi Blockchain.AreaId = 2
```

Result:

```text
AreaId 8: Tòa Beta
└── Sảnh 1

AreaId 2: Tòa B
├── Tầng 1
└── Đồi Blockchain
```

---

## Case C — Area name changes but current Area has only one Location

Initial:

```text
Tòa C
└── Sảnh chính
```

User changes:

```text
Tòa C → Tòa Gamma
```

There is no sibling location to protect.

Recommended behavior:

```text
Update existing Area directly.
Do not create an unnecessary duplicate Area.
```

Pseudo-rule:

```text
if areaNameChanged:
    siblingCount = count locations using currentAreaId

    if siblingCount > 1:
        split/create new area
        move current location
    else:
        rename current area directly
```

---

# 14. Video Rules During Rename

This must be implemented explicitly.

## 14.1 Rename + no new video

Initial:

```text
Tòa B — B.mp4
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

Edit `Sảnh 1`:

```text
Tòa B → Tòa Beta
No new video selected
```

Expected:

```text
Tòa Beta — B.mp4
└── Sảnh 1

Tòa B — B.mp4
├── Tầng 1
└── Đồi Blockchain
```

Implementation:

```text
newArea.CoverFileId = oldArea.CoverFileId
```

Do not duplicate the Drive file.

Both Area rows may reference the same existing file record unless the persistence constraints prohibit it.

The AI agent must verify the current FK model before coding.

---

## 14.2 Rename + upload new video

Initial:

```text
Tòa B — B.mp4
├── Sảnh 1
├── Tầng 1
└── Đồi Blockchain
```

Edit `Sảnh 1`:

```text
Tòa B → Tòa Beta
Upload beta.mp4
```

Expected:

```text
Tòa Beta — beta.mp4
└── Sảnh 1

Tòa B — B.mp4
├── Tầng 1
└── Đồi Blockchain
```

Important:

Do not replace the old shared Area's cover because the new video belongs to the newly separated Area.

---

# 15. Video-only Update Rule

If:

```text
areaNameChanged == false
AND
request.AreaCoverVideo != null
```

then retain existing logic:

```text
update currentArea.CoverFileId
```

All locations sharing the Area will see the new video.

This rule is explicitly required and must not be changed accidentally while implementing the split behavior.

---

# 16. Area Name Duplicate Rules

Current code includes area key uniqueness checks.

Do not silently break uniqueness while creating a new Area.

Recommended behavior:

If user attempts to rename a location's Area to an Area name that already exists in the same campus:

```text
Reject with a clear business error.
```

Example:

Existing:

```text
Tòa Alpha
Tòa Beta
Tòa B
```

User edits `Tòa B / Sảnh 1`:

```text
Tòa B → Tòa Beta
```

Recommended response:

```text
Khu vực "Tòa Beta" đã tồn tại. Vui lòng chọn tên khác.
```

Why reject instead of auto-merging:

- auto-merging raises ambiguity about the cover video
- auto-merging changes grouping semantics
- user did not explicitly ask to move this location to an existing Area
- future “Move Location to Area” should be a separate explicit operation if needed

Retain existing key normalization rules.

---

# 17. Translation Rules During Area Split

The Gallery module has bilingual VI/EN translation behavior and stale-preview handling.

Area split must preserve those guarantees.

When Area name changes:

```text
new Area's VI name = edited AreaName
new Area's EN name / translation metadata must be produced using the current existing translation flow
```

Must continue supporting:

```text
AUTO_PREVIEW
MANUAL
AUTO_ON_SAVE
translation source hash
STALE detection
FAILED warning behavior
```

Do not simply copy `AreaNameEn` from the old Area after the VI name changes.

If VI changes and the frontend submitted a valid preview/manual EN, reuse it.

If no usable EN is provided, use the current backend translate-on-save logic.

If only the video changes and Area name is unchanged:

```text
do not call translation provider
do not modify translation metadata
```

---

# 18. Location Translation Rule

Location editing behavior remains unchanged.

If:

```text
LocationName changes
```

apply the existing Location translation flow.

The Area split must not accidentally cause the location translation to be skipped or duplicated.

Where possible:

```text
Area translation + Location translation provider requests
```

should retain the existing batched translation behavior.

---

# 19. Transaction Requirement for Area Split

Area split must execute atomically.

Required database changes may include:

```text
INSERT GalleryArea
UPDATE GalleryLocation.AreaId
UPDATE GalleryLocation fields
INSERT AuditLog(s)
```

All database modifications must be inside one transaction where supported by the current handler pattern.

Failure must not leave:

```text
new Area inserted
but Location still linked to old Area
```

or:

```text
Location moved
but new Area incomplete
```

If a new Area cover video was uploaded to Google Drive before the DB transaction fails, use the existing compensation strategy to clean the newly uploaded file.

Do not delete the old cover on rollback/failure.

---

# 20. Suggested UpdateGalleryLocation Algorithm

Recommended conceptual algorithm:

```text
Handle(request):

1. Resolve Staff Leader campus + actor + current Vietnam time.

2. Normalize:
      requestedAreaName
      requestedLocationName

3. Load location in current Staff Leader campus.
   Include current Area.

4. Capture:
      oldArea
      oldAreaName
      oldAreaCoverFileId
      oldLocationName
      oldLocationCoverFileId

5. Detect:
      areaNameChanged
      locationNameChanged
      areaCoverChanged
      locationCoverChanged

6. Run duplicate checks before uploads.

7. Resolve bilingual translation payloads / stale checks.

8. Translate changed names if needed using existing coordinator.

9. Upload any new requested files.

10. Begin DB write logic.

11. If areaNameChanged:
      count locations referencing oldArea.AreaId

      if count > 1:
          create newArea
          newArea.CampusId = oldArea.CampusId
          newArea.AreaName = requestedAreaName
          newArea.AreaKey = requestedAreaKey
          newArea.CoverFileId =
              new uploaded area video if provided
              else oldArea.CoverFileId

          apply Area translation to newArea

          set audit timestamps/actor

          db.GalleryAreas.Add(newArea)
          SaveChanges if needed to obtain AreaId
          OR rely on tracked generated key if provider supports it

          location.AreaId = newArea.AreaId

          DO NOT mutate oldArea name
          DO NOT mutate oldArea cover

      else:
          rename oldArea directly
          if new Area video supplied:
              replace oldArea cover

12. Else: // areaName unchanged
      if new Area video supplied:
          replace oldArea cover
          // intentionally shared by every sibling location

13. Apply location name / location cover updates normally.

14. Add audit logs.

15. SaveChanges / commit.

16. After success:
      clean old replaced files only according to existing repository lifecycle rules.
      Never remove inherited/shared old Area cover if another Area still references it.

17. If failure:
      rollback DB
      compensate only newly uploaded files
```

---

# 21. Critical File Lifecycle Warning for Shared Area Covers

After implementing Area split, the same old `CoverFileId` may be referenced by both:

```text
oldArea
newArea
```

if the user renames the Area but does not upload a new video.

Therefore any existing code that deletes the previous Area cover after successful update must be audited.

Do not physically delete an old cover file when it is still referenced by another `GalleryArea`.

Before deleting any previous Gallery Area cover file, verify reference count:

```text
GalleryAreas.Count(a => a.CoverFileId == oldCoverFileId)
```

and also check any other entity type that may legally reference that file if the storage model permits cross-entity reuse.

Safer rule:

```text
Only delete old Drive/file record when no active database reference remains.
```

This is mandatory to avoid breaking another Area after split.

---

# 22. Audit Log Changes for Area Split

Current update flow records changes similar to:

```text
UPDATE_GALLERY_AREA
UPDATE_GALLERY_LOCATION
```

When an Area is split due to a location-specific rename, audit should clearly show what happened.

Recommended actions:

```text
CREATE_GALLERY_AREA_FROM_LOCATION_EDIT
MOVE_GALLERY_LOCATION_TO_NEW_AREA
```

or use existing project naming conventions if an equivalent exists.

Suggested audit payload:

```json
{
  "locationId": 15,
  "oldAreaId": 2,
  "oldAreaName": "Tòa B",
  "newAreaId": 8,
  "newAreaName": "Tòa Beta",
  "areaVideoChanged": true
}
```

If old Area is renamed directly because it only has one Location:

```text
UPDATE_GALLERY_AREA
```

remains appropriate.

If only video changed:

```text
UPDATE_GALLERY_AREA
```

with cover old/new IDs remains appropriate.

---

# 23. UI Changes for Gallery Location Edit Modal

Current warning is approximately:

```text
Thay đổi tên hoặc video đại diện khu vực sẽ áp dụng cho tất cả vị trí thuộc khu vực này.
```

This is no longer correct.

Replace with wording that describes the two different rules.

Recommended:

```text
Đổi tên khu vực chỉ áp dụng cho vị trí đang chỉnh sửa.
Nếu chỉ thay video đại diện mà không đổi tên khu vực, video mới sẽ áp dụng cho tất cả vị trí thuộc khu vực hiện tại.
```

Optional extra clarification when both fields change:

```text
Nếu đổi tên khu vực, vị trí hiện tại sẽ được tách sang khu vực mới. Video mới (nếu chọn) sẽ thuộc khu vực mới đó.
```

Do not imply that Location cover images are shared; they remain per-location.

---

# 24. Public VisitFPTU Impact

Current public hierarchy concept:

```text
Campus
→ Area
→ Location
→ Gallery Item
```

After a split:

```text
Tòa B
├── Tầng 1
└── Đồi Blockchain

Tòa Beta
└── Sảnh 1
```

Public VisitFPTU should naturally display two Areas.

Review public navigation queries to ensure they group dynamically from current FK relations and do not cache/assume old Area IDs.

Likely review:

```text
GetPublicCampusNavigationQueryHandler
GetPublicLocationShowcaseQueryHandler
GetPublicLocationGalleryItemQueryHandler
GetPublicGalleryItemDetailQueryHandler
PublicGalleryMediaAccess
```

No public redesign should be necessary if those queries correctly follow `GalleryLocation.AreaId`.

---

# 25. Management List Impact After Area Split

`ViewGalleryLocationList` should automatically display:

```text
AreaName from each Location's current Area relation
```

Verify it does not use an old cached area lookup after update.

After split, expected management rows:

```text
Tòa Beta | Sảnh 1
Tòa B    | Tầng 1
Tòa B    | Đồi Blockchain
```

No unrelated row should change.

---

# 26. Required Frontend Files to Review

At minimum review:

```text
frontend/pems-react/src/pages/dashboard/gallery/GalleryManagementStaffLeader.tsx
frontend/pems-react/src/pages/dashboard/gallery/GalleryUpsertModal.tsx
frontend/pems-react/src/pages/dashboard/gallery/LocationManagementStaffLeader.tsx
frontend/pems-react/src/pages/dashboard/gallery/LocationManagement.tsx
frontend/pems-react/src/pages/dashboard/gallery/locationModalShared.tsx
frontend/pems-react/src/features/gallery-management/api/galleryManagementApi.ts
frontend/pems-react/src/features/gallery-management/types/galleryManagement.types.ts
frontend/pems-react/src/shared/utils/fileValidation.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Search for all strings:

```text
5MB
≤5MB
GALLERY_IMAGE
changegalleryitemstatus
HIDDEN
PUBLISHED
updategallerylocation
Thay đổi tên hoặc video
```

Do not update only one duplicate helper string.

---

# 27. Required Backend Files to Review

At minimum review:

```text
backend/PEMS.Api/Controllers/GalleriesController.cs

backend/PEMS.Application/Common/Files/FileValidationPolicy.cs

backend/PEMS.Application/Galleries/Common/GalleryMediaClassifier.cs
backend/PEMS.Application/Galleries/Common/GalleryDetailBuilder.cs
backend/PEMS.Application/Galleries/Common/GalleryFileCleanup.cs
backend/PEMS.Application/Galleries/Common/GalleryLocationGuard.cs
backend/PEMS.Application/Galleries/Common/GalleryLocationWriteGuard.cs
backend/PEMS.Application/Galleries/Common/StaffLeaderGalleryScope.cs

backend/PEMS.Application/Galleries/Commands/AddGalleryItem/
backend/PEMS.Application/Galleries/Commands/UpdateGalleryItem/
backend/PEMS.Application/Galleries/Commands/ChangeGalleryItemStatus/
backend/PEMS.Application/Galleries/Commands/UpdateGalleryLocation/
backend/PEMS.Application/Galleries/Commands/CreateGalleryLocation/

backend/PEMS.Application/Galleries/Queries/ViewGalleryItemList/
backend/PEMS.Application/Galleries/Queries/SearchGalleryItems/
backend/PEMS.Application/Galleries/Queries/ViewGalleryItemDetails/
backend/PEMS.Application/Galleries/Queries/ViewGalleryLocationList/

backend/PEMS.Application/Galleries/Public/

backend/PEMS.Domain/Entities/Galleries/GalleryArea.cs
backend/PEMS.Domain/Entities/Galleries/GalleryLocation.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItem.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItemMedia.cs
```

Also inspect persistence mappings and DB schema for:

```text
gallery_areas
gallery_locations
gallery_items
gallery_item_media
files
audit_logs
```

before adding or changing constraints.

---

# 28. Database Changes

## Change 1 — image size

No schema change expected.

## Change 2 — Gallery Item delete

Likely no schema change because `GalleryItem` and `GalleryItemMedia` already have soft-delete columns.

Verify actual production/manual SQL schema contains:

```text
gallery_items.deleted_at
gallery_items.deleted_by
gallery_item_media.deleted_at
gallery_item_media.deleted_by
```

Do not rely only on C# entities.

If the DB schema lacks those columns, create a reviewed manual SQL patch because PEMS is database-first/manual SQL.

## Change 3 — Area split

No schema change should be required.

The current Area/Location relation already supports assigning a Location to a new Area via `AreaId`.

---

# 29. API Contract Changes

## New delete API

Add frontend endpoint mapping and API client method.

Suggested TypeScript:

```ts
export interface DeleteGalleryItemInput {
  galleryItemId: number;
}
```

Suggested API method:

```text
galleryManagementApi.deleteGalleryItem(...)
```

## Update Gallery Location

Existing request payload can remain conceptually the same:

```text
locationId
areaName
areaNameEn
areaTranslationOrigin
areaTranslationSourceHash
locationName
locationNameEn
locationTranslationOrigin
locationTranslationSourceHash
areaCoverVideo
locationCoverImage
```

The changed behavior should be implemented server-side.

Do not force frontend to create an Area manually.

---

# 30. Error Codes

Use or extend the Gallery-specific error code constants.

Recommended errors if equivalents do not already exist:

```text
GALLERY_ITEM_NOT_FOUND
GALLERY_ITEM_ALREADY_DELETED
GALLERY_AREA_NAME_DUPLICATE
GALLERY_DELETE_FAILED
```

Reuse existing codes when semantics already match.

Do not return generic 500 for normal business errors.

---

# 31. Concurrency / Stale Update Safety

The AI agent must inspect current Gallery concurrency behavior.

For delete:

```text
Delete versus Edit
Delete versus Hide/Publish
```

must not produce silent inconsistent state.

At minimum:

- second delete should be safe/idempotent or a controlled business error
- editing a deleted item must fail
- publishing a deleted item must fail

For Area split:

If sibling count changes between read and save because another user edits locations concurrently, protect against inconsistent decisions where practical using the repository's current transaction/concurrency pattern.

Do not invent a radically new concurrency subsystem solely for this task.

---

# 32. Tests Required — Change 1

## Backend unit tests

Add tests for `FileValidationPolicy`:

```text
GalleryItemImage allows <=20 MB
GalleryDelegationImage allows <=20 MB
GalleryItemImage rejects >20 MB
GalleryLocationCover still rejects >5 MB
```

Also validate MIME/extensions remain unchanged.

## Frontend unit tests

Test:

```text
GALLERY_ITEM_IMAGE 20 MB passes
GALLERY_ITEM_IMAGE >20 MB fails
Location cover >5 MB still fails
```

Test GalleryUpsertModal helper text / rejection where practical.

---

# 33. Tests Required — Change 2

## Backend unit/integration tests

Cases:

```text
Staff Leader deletes own-campus Gallery Item → success
DeletedAt/DeletedBy set
related media soft-deleted if required
AuditLog written

Staff Leader attempts another campus item → forbidden/not found
Other role → forbidden at controller
Deleted item excluded from management list
Deleted item excluded from search
Deleted item detail inaccessible
Deleted item excluded from public navigation/gallery
Deleted item cannot be updated
Deleted item cannot change status
Second delete handled deterministically
```

## Frontend tests

Cases:

```text
Delete action appears for Staff Leader Gallery Item
click Delete opens confirmation
Cancel does not call API
Confirm calls delete API once
success removes/refreshes row
failure leaves row and shows error
```

---

# 34. Tests Required — Change 3

This change requires especially careful tests.

## Case 1 — shared Area, rename only

Initial:

```text
Area A
Location 1
Location 2
```

Edit Location 1:

```text
Area A → Area B
```

Assert:

```text
new Area B created
Location 1 → Area B
Location 2 → original Area A
original Area A name unchanged
```

## Case 2 — shared Area, video only

Edit Location 1:

```text
same Area name
new area video
```

Assert:

```text
no new Area created
both Location 1 and Location 2 still point to original Area
original Area cover updated
both see new video
```

## Case 3 — shared Area, rename + new video

Assert:

```text
new Area created
Location 1 moved
new Area gets new video
old Area keeps old video
Location 2 remains unchanged
```

## Case 4 — shared Area, rename without new video

Assert:

```text
new Area created
Location 1 moved
new Area inherits old cover file reference
old Area keeps same cover
```

## Case 5 — Area has one Location

Assert:

```text
Area renamed in place
no unnecessary new Area
```

## Case 6 — duplicate Area name

Assert:

```text
rename to existing campus Area name rejected
no file uploaded if duplicate detected before upload
no new Area
no Location move
```

## Case 7 — location name changes simultaneously

Assert both:

```text
Area split logic
Location name update/translation
```

work in one request.

## Case 8 — translation preview stale

Assert:

```text
no Area split occurs
no DB write
no Drive orphan
```

when translation preview is invalid/stale.

## Case 9 — DB failure after new video upload

Assert compensation:

```text
new Drive upload/file record cleaned up
old Area and old location relations preserved
```

---

# 35. Regression Test Matrix

After implementation run regression for:

```text
Create Gallery Item
Edit Gallery Item
Hide Gallery Item
Publish Gallery Item
Delete Gallery Item

Create Gallery Location in existing Area
Create new Area + first Location
Edit Area name
Edit Area cover video
Edit Location name
Edit Location cover
Change Location status

Public VisitFPTU navigation
Public Gallery Item detail
Public media/image streaming
Public audio streaming

Gallery translation preview
manual English fields
translation stale handling

Staff Leader campus authorization
```

---

# 36. UX Expected Final Behavior

## Gallery Item upload

Display:

```text
Chỉ ảnh (JPG/PNG/WEBP ≤20MB)
tối đa 20 media
video thêm qua YouTube
```

## Gallery Item row actions

Display:

```text
View
Edit
Hide/Show
Delete
```

## Gallery Location edit warning

Display:

```text
Đổi tên khu vực chỉ áp dụng cho vị trí đang chỉnh sửa.
Nếu chỉ thay video đại diện mà không đổi tên khu vực, video mới sẽ áp dụng cho tất cả vị trí thuộc khu vực hiện tại.
```

---

# 37. Non-Goals

Do not implement any of the following unless required by existing repository compatibility:

- Upload Gallery Item video files directly from machine.
- Increase every image type in the application to 20 MB.
- Hard delete Gallery Item database rows.
- Immediately delete Gallery Item binaries from Google Drive.
- Copy Area name fields into `gallery_locations`.
- Remove Area/Location relational model.
- Auto-merge a renamed location into another existing Area.
- Add a new “move location to existing area” feature.
- Change Location cover image sharing behavior.
- Change Area video sharing behavior when Area name is unchanged.
- Change Staff Leader campus authorization rules.
- Redesign the public VisitFPTU UI.

---

# 38. Implementation Priority / Recommended Order

**All implementation steps below are performed on the current checked-out branch. `Dev` is reference-only unless it is already the current branch.**

Implement in this order:

## Phase 1 — Tests and current-behavior verification

1. Read the relevant implementation on `Dev` as the reference baseline.
2. Record and inspect the current checked-out branch.
3. Compare the current branch against `Dev` for all Gallery files relevant to this task.
4. Implement only on the current checked-out branch.
5. Verify the current branch's actual DB/schema expectations.
6. Locate all Gallery-specific tests on the current branch and compare with `Dev` where useful.
7. Add failing tests for the three new requirements before code where practical.

## Phase 2 — Image size

1. Split frontend file purpose.
2. Update frontend Gallery Item validation to 20 MB.
3. Update UI text.
4. Split backend `FileValidationPolicy`.
5. Run upload tests.

## Phase 3 — Delete Gallery Item

1. Add command/handler.
2. Add controller endpoint.
3. Add API client/type.
4. Update management/public queries for soft delete.
5. Add UI action + confirm modal.
6. Add audit.
7. Test all delete-related flows.

## Phase 4 — Area split behavior

1. Refactor `UpdateGalleryLocationCommandHandler`.
2. Preserve existing translation logic.
3. Add sibling-count logic.
4. Add new Area creation path.
5. Preserve video-only shared update path.
6. Audit file cleanup/refcount behavior.
7. Update audit logs.
8. Update frontend warning text.
9. Test all 9 area cases.

## Phase 5 — Full regression

Run backend tests, frontend tests, build, and targeted system scenarios.

---

# 39. Required Build/Test Gate

The AI agent must not declare completion based only on code inspection.

Run the project's available quality gate, including as applicable:

```bash
dotnet test
```

and frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Run Gallery-specific tests directly if the full suite is too large during iteration, but finish with the repository's real final validation commands.

Do not ignore existing test failures. Distinguish:

```text
pre-existing failures
new failures introduced by this implementation
```

---

# 40. Definition of Done

This task is complete only when all conditions below are true.

## Change 1

- [ ] Gallery Item JPG/JPEG/PNG/WEBP supports up to 20 MB.
- [ ] >20 MB is rejected.
- [ ] Gallery Location cover limit is not unintentionally increased.
- [ ] Frontend helper text matches backend reality.
- [ ] Backend re-validates the limit.

## Change 2

- [ ] Staff Leader sees Delete action.
- [ ] Delete requires confirmation.
- [ ] Delete uses `DeletedAt` / `DeletedBy`.
- [ ] Deleted item disappears from management.
- [ ] Deleted item disappears from public VisitFPTU.
- [ ] Deleted item cannot be edited or republished.
- [ ] Delete writes audit data.
- [ ] Google Drive binary is not blindly removed during normal delete.
- [ ] Campus authorization remains enforced.

## Change 3

- [ ] Renaming a shared Area from one Location changes only that Location's effective Area.
- [ ] Sibling Locations keep the original Area.
- [ ] Shared Area is split only when needed.
- [ ] A single-location Area may be renamed in place.
- [ ] Video-only Area update still affects all sibling Locations.
- [ ] Rename + video creates/separates Area and applies new video only to the new Area.
- [ ] Rename without video inherits current Area video.
- [ ] Old shared video is not accidentally deleted while still referenced.
- [ ] Translation behavior remains correct.
- [ ] Audit clearly records split/move behavior.
- [ ] Public VisitFPTU displays the resulting Area hierarchy correctly.

---

# 41. Final Instruction to the AI Coding Agent

Before changing code:

1. Run `git branch --show-current` and record the branch that will receive the implementation.
2. Read the Gallery-related codebase on branch `Dev` to understand the latest intended PEMS behavior and architecture.
3. Compare the relevant `Dev` implementation with the same files/features on the current checked-out branch.
4. **Do not switch to `Dev` just to implement this task.**
5. **Do not modify `Dev` unless `Dev` is already the current checked-out branch.**
6. Make all code changes on the current checked-out branch.
7. Treat the current branch's actual runtime code and database contract as the integration source of truth, while using `Dev` as the reference baseline.
8. Do not blindly copy `Dev` files over the current branch. Adapt changes to the current branch's existing architecture and code.
9. Do not blindly follow outdated comments/docs if current code differs.
10. Preserve Staff Leader campus authorization.
11. Preserve existing Gallery translation semantics.
12. Preserve Google Drive compensation behavior.
13. Do not make unrelated refactors.
14. Do not perform automatic `checkout`, `reset`, `merge`, `rebase`, or `cherry-pick` operations involving `Dev` without explicit instruction.

After coding:

1. Report the current branch name on which the implementation was made.
2. Report every modified file.
3. Explain the reason for each modification.
4. Report any material difference found between `Dev` and the current branch.
5. Report any SQL patch required.
6. Report tests added/updated.
7. Provide exact build/test results.
8. Report any remaining risk or ambiguous business rule.
9. Do not claim success if tests/build were not executed.

---

# 42. Condensed Business Rule Summary

```text
RULE 1:
Gallery Item image:
JPG/JPEG/PNG/WEBP ≤20 MB
max 20 media/item
video by YouTube URL only

RULE 2:
HIDDEN ≠ DELETED

HIDDEN:
recoverable through publish/show

DELETED:
soft-deleted
not visible in normal management
not visible in public
not editable/publishable

RULE 3:
Rename Area from a Location:
affects only current Location

If shared Area:
create/split new Area
move current Location

If Area only has current Location:
rename Area directly

RULE 4:
Area video change WITHOUT Area rename:
still updates shared Area
therefore applies to all Locations in that Area

RULE 5:
Area rename + new video:
new Area receives new video
old Area and sibling Locations keep old video

RULE 6:
Area rename + no new video:
new Area inherits old Area's existing video reference
```

---

**End of implementation plan.**
