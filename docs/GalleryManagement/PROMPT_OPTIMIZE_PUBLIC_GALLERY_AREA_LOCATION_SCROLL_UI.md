# PROMPT / ĐẶC TẢ CODE — Tối ưu UI Public VisitFPTU Gallery cho Area Sidebar và Location Panel

> Tài liệu này dùng cho AI Agent đọc và sửa frontend Public VisitFPTU Gallery.
>
> Mục tiêu:
>
> 1. Hạ nút mở/đóng Area Sidebar (`>` / `<`) xuống vị trí chính giữa chiều cao Gallery.
> 2. Tách nút này khỏi nút `Trở về`, tránh hai nút nằm sát hoặc chồng nhau.
> 3. Giữ Area Sidebar có thể cuộn đầy đủ khi danh sách Area dài.
> 4. Sửa triệt để Location Panel để Area cuối có 20 Location vẫn xem và cuộn tới Location cuối.
> 5. Giữ nguyên API, database, YouTube embed và toàn bộ nghiệp vụ Gallery hiện tại.
>
> AI Agent phải đọc source frontend thật trước khi sửa. Không mock data. Không sinh file rác. Không refactor ngoài phạm vi.

---

# 0. Vấn đề hiện tại

Sau lần cập nhật scroll trước:

```text
- Area Sidebar đã có thể cuộn.
- Tuy nhiên nút mở/đóng Sidebar đang nằm quá cao.
- Nút mũi tên nằm sát nút Trở về, làm giao diện chật và thiếu cân đối.
- Khi chọn Area cuối danh sách, Location Panel vẫn mở từ vị trí quá thấp.
- Location Panel có scrollbar nhưng container bị giới hạn chiều cao.
- Vì vậy không thể cuộn tới toàn bộ Location của Area cuối.
```

Nguyên nhân chính:

```text
1. Nút mở/đóng Sidebar đang neo theo top của viewport hoặc cùng vùng với nút Trở về.
2. Location Panel vẫn phụ thuộc vào vị trí row Area đang active.
3. Location Panel có thể đang render bên trong Area row hoặc tính top theo offset/index.
4. Chỉ thêm overflow-y-auto không đủ nếu chiều cao panel ban đầu đã bị cắt.
```

---

# 1. Kết quả UI cần đạt

Bố cục desktop mục tiêu:

```text
┌──────────────────────────────────────────────────────────────┐
│ Public Header                                                │
├──────────────────────────────────────────────────────────────┤
│ [Trở về]                                                     │
│                                                              │
│ >  ┌───────────────┐ ┌────────────────────┐                  │
│    │ Area Sidebar  │ │ Location Panel     │ Gallery Viewer   │
│    │ scroll riêng  │ │ scroll riêng       │                  │
│    │               │ │                    │                  │
│    └───────────────┘ └────────────────────┘                  │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

Yêu cầu:

```text
- Nút Trở về nằm phía trên.
- Nút > hoặc < nằm chính giữa chiều cao Gallery.
- Area Sidebar có scroll dọc riêng.
- Location Panel có scroll dọc riêng.
- Location Panel không phụ thuộc vị trí Area row.
- Chọn Area đầu, giữa hoặc cuối thì Location Panel vẫn xuất hiện cùng một vị trí.
- Có thể cuộn tới toàn bộ Area và toàn bộ Location.
```

---

# 2. Không được thay đổi

Không sửa:

```text
- Database.
- Public API.
- Gallery API.
- Quan hệ Area → Location → Gallery Item.
- item_type.
- media_kind.
- YouTube embed.
- Upload file.
- Active state Area/Location.
- Campus selection.
- Area Showcase.
- Location Showcase.
- Routing/query parameter.
- Trạng thái PUBLISHED/HIDDEN.
- RBAC.
```

Đây chỉ là task tối ưu layout và overflow frontend.

---

# 3. Gallery Shell

Toàn bộ Public Gallery phải nằm trong viewport bên dưới header.

```css
.public-gallery-shell {
  position: relative;
  height: calc(100dvh - var(--public-header-height, 94px));
  min-height: 0;
  overflow: hidden;
}
```

Yêu cầu:

```text
- Không để body dài xuống theo số Area hoặc Location.
- Không để Gallery Viewer tạo page scroll dọc.
- Các panel con phải dùng chiều cao dựa trên Gallery Shell.
```

Ưu tiên `100dvh` thay vì chỉ `100vh` để hoạt động tốt hơn trên mobile browser.

---

# 4. Nút Trở về

Nút `Trở về` giữ vị trí riêng phía trên bên trái.

```css
.gallery-back-button {
  position: absolute;
  top: 24px;
  left: 32px;
  z-index: 45;
}
```

Yêu cầu:

```text
- Không đặt chung flex row với nút mở/đóng Sidebar.
- Không để nút mũi tên bám theo vị trí nút Trở về.
- Nút Trở về luôn click được.
```

---

# 5. Nút mở/đóng Area Sidebar

## 5.1. Vị trí mới

Nút `>` / `<` phải được căn giữa theo chiều dọc của Gallery Shell.

```css
.gallery-area-toggle {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  z-index: 40;
}
```

Không dùng:

```text
top: 16px
top: 24px
top theo nút Trở về
top theo Area row
```

## 5.2. Khi Sidebar đóng

```css
.gallery-area-toggle--closed {
  left: 0;
}
```

Hành vi:

```text
- Hiển thị icon >.
- Nằm ở chính giữa mép trái Gallery.
- Không sát nút Trở về.
- Click mở Area Sidebar.
```

## 5.3. Khi Sidebar mở

```css
.gallery-area-toggle--opened {
  left: calc(
    var(--area-sidebar-left)
    + var(--area-sidebar-width)
    - 4px
  );
}
```

Hành vi:

```text
- Icon đổi thành <.
- Nút nằm ở mép phải Area Sidebar.
- Vẫn giữ chính giữa chiều cao.
- Click đóng Area Sidebar.
```

## 5.4. Tailwind concept

```tsx
className={cn(
  'absolute top-1/2 z-40 flex h-14 w-9 -translate-y-1/2',
  'items-center justify-center rounded-r-2xl',
  'bg-black/45 text-white backdrop-blur-md',
  'transition-[left,background-color] duration-300',
  'hover:bg-black/65',
  areaSidebarOpen
    ? 'left-[276px]'
    : 'left-0'
)}
```

Giá trị `left-[276px]` phải được thay bằng kích thước thật của Sidebar hiện tại.

---

# 6. Area Sidebar

## 6.1. Sidebar container

Area Sidebar phải có `top` và `bottom` cố định trong Gallery Shell.

```css
.gallery-area-sidebar {
  position: absolute;
  top: 16px;
  bottom: 16px;
  left: 28px;
  z-index: 30;

  width: 264px;
  min-height: 0;

  display: flex;
  flex-direction: column;

  overflow: hidden;
  border-radius: 20px;
}
```

Không để chiều cao Sidebar phụ thuộc số lượng Area.

## 6.2. Area list

```css
.gallery-area-list {
  flex: 1;
  min-height: 0;

  overflow-y: auto;
  overflow-x: hidden;

  overscroll-behavior: contain;
  scrollbar-gutter: stable;
  touch-action: pan-y;
}
```

Điểm bắt buộc:

```text
- Parent phải là flex column.
- List phải có flex: 1.
- List phải có min-height: 0.
- Chỉ list được scroll.
- Sidebar container phải overflow: hidden.
```

## 6.3. Hành vi

```text
- Có thể cuộn từ Area đầu tới Area cuối.
- Area cuối hiển thị đầy đủ.
- Area cuối click được.
- Area active vẫn highlight màu cam.
- Scroll Area không cuộn Location.
- Scroll Area không cuộn Gallery Viewer.
```

---

# 7. Location Panel — sửa triệt để

## 7.1. Nguyên nhân lỗi

Location Panel hiện nhiều khả năng đang dùng một trong các cách sai:

```text
- Render bên trong Area row active.
- top = offsetTop của Area row.
- top = selectedAreaIndex × rowHeight.
- top = 100%.
- translateY theo Area row.
- Đặt trong container Area có overflow.
```

Khi chọn Area cuối:

```text
Area cuối nằm gần đáy
→ Location Panel bắt đầu gần đáy
→ Container còn rất ít chiều cao
→ Dù có scrollbar vẫn không xem hết Location
```

## 7.2. Cấu trúc đúng

Location Panel phải là sibling trực tiếp của Area Sidebar.

```tsx
<div className="public-gallery-shell">
  <AreaSidebar />

  {selectedArea && (
    <LocationPanel />
  )}

  <GalleryViewer />
</div>
```

Không được dùng:

```tsx
<AreaRow>
  {isActive && <LocationPanel />}
</AreaRow>
```

Không được đặt Location Panel bên trong `.gallery-area-list`.

---

# 8. Vị trí Location Panel

Location Panel phải luôn nằm cùng một vị trí bất kể Area nào active.

```css
.gallery-location-panel {
  position: absolute;
  top: 16px;
  bottom: 16px;
  left: 304px;
  z-index: 31;

  width: 340px;
  min-height: 0;

  display: flex;
  flex-direction: column;

  overflow: hidden;
  border-radius: 20px;
}
```

Cách tính `left`:

```text
Area sidebar left
+ Area sidebar width
+ khoảng cách giữa hai panel
```

Ví dụ:

```text
28px + 264px + 12px = 304px
```

Không hard-code bừa nếu source hiện tại dùng kích thước khác.

Có thể dùng CSS variables:

```css
:root {
  --area-sidebar-left: 28px;
  --area-sidebar-width: 264px;
  --gallery-panel-gap: 12px;
}

.gallery-location-panel {
  left: calc(
    var(--area-sidebar-left)
    + var(--area-sidebar-width)
    + var(--gallery-panel-gap)
  );
}
```

---

# 9. Location list

Location Panel phải dùng flex column.

Nếu có header:

```tsx
<div className="gallery-location-panel">
  <div className="shrink-0">
    {/* Location panel header */}
  </div>

  <div
    ref={locationListRef}
    className="gallery-location-list"
  >
    {/* Location items */}
  </div>
</div>
```

CSS:

```css
.gallery-location-list {
  flex: 1;
  min-height: 0;

  overflow-y: auto;
  overflow-x: hidden;

  overscroll-behavior: contain;
  scrollbar-gutter: stable;
  touch-action: pan-y;
}
```

Yêu cầu:

```text
- Area có 20 Location vẫn xem được đầy đủ.
- Có thể cuộn tới Location 20.
- Location cuối click được.
- Scroll Location không cuộn Area.
- Location Panel không bị cắt bởi Area list.
```

---

# 10. Loại bỏ các class gây xung đột

Tìm và loại bỏ hoặc sửa các class/style tương tự:

```text
max-h-[300px]
max-h-[400px]
max-h-[420px]
h-fit
h-auto
bottom-auto
top-full
translate-y-*
overflow-visible
top theo active row
top theo selectedAreaIndex
top theo offsetTop
```

Cần kiểm tra cả:

```text
- inline style.
- Tailwind class.
- CSS module.
- styled component.
- calculated style object.
```

Không giữ một `max-height` nhỏ hơn chiều cao thực của Gallery Shell.

---

# 11. Reset scroll Location khi đổi Area

Khi `selectedAreaId` thay đổi:

```tsx
const locationListRef = useRef<HTMLDivElement>(null);

useEffect(() => {
  locationListRef.current?.scrollTo({
    top: 0,
    behavior: 'auto',
  });
}, [selectedAreaId]);
```

Hành vi:

```text
- Chọn Area mới thì Location list bắt đầu từ đầu.
- Không giữ scrollTop của Area trước.
- Nếu route chỉ định Location cụ thể, sau khi render xong thì đưa Location đó vào view.
```

---

# 12. Auto-scroll item active

## 12.1. Cách đơn giản

```tsx
useEffect(() => {
  activeAreaRef.current?.scrollIntoView({
    block: 'nearest',
    behavior: 'smooth',
  });
}, [selectedAreaId]);

useEffect(() => {
  activeLocationRef.current?.scrollIntoView({
    block: 'nearest',
    behavior: 'smooth',
  });
}, [selectedLocationId]);
```

## 12.2. Cách ưu tiên

Ưu tiên scroll riêng container để tránh browser scroll body.

```tsx
function ensureItemVisible(
  container: HTMLElement | null,
  item: HTMLElement | null,
) {
  if (!container || !item) return;

  const itemTop = item.offsetTop;
  const itemBottom = itemTop + item.offsetHeight;

  const visibleTop = container.scrollTop;
  const visibleBottom = visibleTop + container.clientHeight;

  if (itemTop < visibleTop) {
    container.scrollTo({
      top: itemTop,
      behavior: 'smooth',
    });
    return;
  }

  if (itemBottom > visibleBottom) {
    container.scrollTo({
      top: itemBottom - container.clientHeight,
      behavior: 'smooth',
    });
  }
}
```

Dùng cho:

```text
- Area active.
- Location active.
```

Không để `scrollIntoView` làm toàn bộ trang hoặc Gallery Viewer dịch chuyển.

---

# 13. Scrollbar

```css
.gallery-area-list,
.gallery-location-list {
  scrollbar-width: thin;
  scrollbar-color:
    rgba(255, 255, 255, 0.45)
    rgba(255, 255, 255, 0.08);
}

.gallery-area-list::-webkit-scrollbar,
.gallery-location-list::-webkit-scrollbar {
  width: 6px;
}

.gallery-area-list::-webkit-scrollbar-thumb,
.gallery-location-list::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.42);
  border-radius: 999px;
}

.gallery-area-list::-webkit-scrollbar-track,
.gallery-location-list::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.08);
}
```

Không ẩn scrollbar hoàn toàn trên desktop.

---

# 14. Z-index và pointer events

Z-index đề xuất:

```text
Gallery Viewer       z-0
Background overlay   z-10
Area Sidebar         z-30
Location Panel       z-31
Area Toggle          z-40
Back Button          z-45
Public Header        z-50 hoặc cao hơn
```

Kiểm tra:

```text
- Nút mũi tên click được.
- Nút Trở về click được.
- Area item click được.
- Location item click được.
- Overlay không chặn pointer.
- Panel không nằm sau Gallery Viewer.
```

Không dùng `pointer-events-none` trên container panel.

---

# 15. Responsive

## 15.1. Desktop

```text
- Area Sidebar và Location Panel nằm cạnh nhau.
- Hai list cuộn độc lập.
- Nút mũi tên nằm giữa chiều cao.
```

## 15.2. Tablet

```text
- Area Sidebar có thể overlay.
- Location Panel có thể overlay cạnh Area hoặc thay thế Area panel.
- Vẫn dùng top/bottom cố định.
- List vẫn overflow-y-auto.
```

## 15.3. Mobile

Luồng đề xuất:

```text
Bấm >
→ mở Area drawer.

Chọn Area
→ mở Location drawer hoặc chuyển sang bước Location.
```

Drawer:

```css
height: calc(100dvh - var(--public-header-height));
min-height: 0;
overflow: hidden;
```

List:

```css
flex: 1;
min-height: 0;
overflow-y: auto;
```

Không để body dài theo số item.

---

# 16. Hành vi mong muốn

## 16.1. Nút mở/đóng

```text
Given Area Sidebar đang đóng
When user xem Public Gallery
Then nút > nằm chính giữa mép trái Gallery
And không nằm sát nút Trở về.

When user click >
Then Area Sidebar mở
And icon đổi thành <
And nút nằm ở mép phải Area Sidebar
And vẫn giữ chính giữa chiều cao.
```

## 16.2. Area list

```text
Given campus có 24 Area
When Sidebar mở
Then user cuộn được từ Area đầu đến Area cuối
And Area cuối hiển thị đầy đủ
And Area cuối click được.
```

## 16.3. Location Panel

```text
Given Area cuối có 20 Location
When user chọn Area cuối
Then Location Panel xuất hiện ở vị trí cố định cạnh Area Sidebar
And panel không mở từ vị trí row Area cuối
And panel có chiều cao gần toàn bộ Gallery viewport
And user cuộn được tới Location 20.
```

## 16.4. Scroll độc lập

```text
When pointer nằm trên Area list
Then chỉ Area list cuộn.

When pointer nằm trên Location list
Then chỉ Location list cuộn.

When pointer nằm trên Gallery Viewer
Then Area và Location list không tự di chuyển.
```

---

# 17. Acceptance Criteria

## AC-UI-01 — Nút mũi tên

```text
Given Public Gallery đang hiển thị
Then nút > hoặc < nằm tại top 50% của Gallery Shell
And không chồng hoặc nằm sát nút Trở về.
```

## AC-UI-02 — Area dài

```text
Given campus có ít nhất 24 Area
When Sidebar mở
Then Area Sidebar không vượt viewport
And user có thể cuộn tới Area cuối.
```

## AC-UI-03 — Location dài

```text
Given Area cuối có 20 Location
When Area cuối được chọn
Then Location Panel vẫn nằm trọn trong viewport
And user có thể cuộn tới Location cuối.
```

## AC-UI-04 — Không phụ thuộc Area row

```text
Given lần lượt chọn Area đầu, giữa và cuối
Then Location Panel luôn giữ cùng top, bottom và left
And không dịch theo vị trí row Area active.
```

## AC-UI-05 — Reset Location scroll

```text
Given user đã cuộn sâu trong Location list của Area A
When chuyển sang Area B
Then Location list của Area B bắt đầu từ đầu
Unless route đang yêu cầu một Location cụ thể.
```

## AC-UI-06 — Scroll độc lập

```text
When cuộn Area list
Then Location list không thay đổi scrollTop.

When cuộn Location list
Then Area list không thay đổi scrollTop.
```

## AC-UI-07 — Giữ flow cũ

```text
Given UI đã được sửa
Then API, routing, active state, YouTube embed,
Area Showcase và Location Showcase vẫn hoạt động như trước.
```

---

# 18. Test bắt buộc

```text
1. Campus có 1 Area.
2. Campus có 24 Area.
3. Active Area đầu.
4. Active Area giữa.
5. Active Area cuối.
6. Area có 1 Location.
7. Area cuối có 20 Location.
8. Cuộn tới Location 20.
9. Click Location 20.
10. Mở/đóng Sidebar nhiều lần.
11. Nút Trở về và nút mũi tên không chồng nhau.
12. Scroll Area bằng mouse wheel.
13. Scroll Location bằng mouse wheel.
14. Scroll bằng trackpad.
15. Scroll bằng touch.
16. Laptop chiều cao thấp.
17. Browser zoom 125%.
18. Browser zoom 150%.
19. Desktop.
20. Tablet.
21. Mobile.
22. Reload route có selected Area.
23. Reload route có selected Location.
24. YouTube media vẫn render.
25. Uploaded video vẫn render.
```

---

# 19. Build và báo cáo

Sau khi sửa:

```bash
npm run build
```

Nếu project có test frontend:

```bash
npm test
```

Báo cáo phải có:

```text
1. File frontend đã sửa.
2. Component nào quản lý Gallery Shell.
3. Component nào quản lý Area Sidebar.
4. Component nào quản lý Location Panel.
5. Cách định vị nút > / <.
6. Cách tách Location Panel khỏi Area row.
7. Cách reset Location scroll.
8. Cách auto-scroll item active.
9. Build result.
10. Test result.
11. Phần chưa thể xác nhận runtime.
```

Không báo hoàn thành nếu chưa chạy build hoặc chưa nói rõ lý do không chạy được.

---

# 20. Definition of Done

```text
[ ] Nút Trở về giữ nguyên phía trên.
[ ] Nút > / < nằm chính giữa chiều cao Gallery Shell.
[ ] Nút mũi tên không sát hoặc chồng nút Trở về.
[ ] Nút > mở Area Sidebar.
[ ] Nút < đóng Area Sidebar.
[ ] Area Sidebar không vượt viewport.
[ ] Area list cuộn được tới Area cuối.
[ ] Location Panel là sibling của Area Sidebar.
[ ] Location Panel không render trong Area row.
[ ] Location Panel không tính top theo Area index/offset.
[ ] Location Panel dùng top và bottom cố định.
[ ] Location list cuộn được tới Location 20.
[ ] Scroll Area và Location độc lập.
[ ] Đổi Area reset Location scroll.
[ ] Active Area/Location tự vào vùng nhìn thấy.
[ ] Gallery Viewer không bị page scroll.
[ ] Nút và item vẫn click được.
[ ] Responsive không vỡ.
[ ] API và database không thay đổi.
[ ] YouTube embed vẫn hoạt động.
[ ] npm run build thành công.
[ ] Không mock data.
[ ] Không sinh file rác.
```
