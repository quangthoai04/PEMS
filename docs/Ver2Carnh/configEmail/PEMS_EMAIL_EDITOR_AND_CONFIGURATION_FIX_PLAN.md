# PEMS — Email Template Editor & Configuration Fix Plan

## 1. Mục tiêu

Khắc phục các lỗi hiện tại của màn hình **Quản lý email / Chỉnh sửa mẫu email** trên nhánh `Dev`, tập trung vào:

- Chèn biến đúng vị trí con trỏ và đúng field đang thao tác (`Subject` / `Body`).
- Không bị sai target khi chuyển qua lại giữa Subject và Body.
- Có thể thao tác hợp lý với nhiều biến liên tiếp.
- Sửa UX và tính ổn định khi chèn/chỉnh sửa bảng.
- Đảm bảo table / variable / system block không làm hỏng HTML khi lưu, preview hoặc gửi email.
- Rà soát các lỗi liên quan trực tiếp đến cấu hình template email, nhưng **không mở rộng scope sang refactor architecture hoặc thay API/DB nếu không cần**.

> Ưu tiên: sửa ít nhất có thể, giữ `EmailRichTextEditor` dùng chung, giữ contract/backend hiện tại và bổ sung test hồi quy.

---

## 2. Baseline cần kiểm tra trước khi sửa

### Branch

```text
Dev
```

Baseline đã kiểm tra tại thời điểm lập kế hoạch:

```text
26a382d2d4a169fc4192edf762f6fcbf4eaa7455
```

### Các file trọng tâm

```text
frontend/pems-react/src/pages/dashboard/emails/TemplateManagement.tsx

frontend/pems-react/src/features/emails/components/EmailRichTextEditor.tsx
frontend/pems-react/src/features/emails/components/EmailTableDialog.tsx

frontend/pems-react/src/features/emails/utils/emailEditorVariableChips.ts
frontend/pems-react/src/features/emails/utils/emailEditorTable.ts
frontend/pems-react/src/features/emails/utils/emailEditorSystemNodes.ts
frontend/pems-react/src/features/emails/utils/emailHtmlCanonicalizer.ts

frontend/pems-react/src/features/emails/__tests__/EmailRichTextEditor.test.tsx
frontend/pems-react/src/features/emails/__tests__/TemplateManagementEditorUx.test.tsx
frontend/pems-react/src/features/emails/__tests__/TemplateManagement.test.tsx
```

Nếu trong lúc triển khai phát hiện shared editor còn được dùng tại compose/send modal, kiểm tra thêm:

```text
frontend/pems-react/src/features/emails/components/EmailComposeModal.tsx
```

Không thay đổi backend trước khi có bằng chứng lỗi nằm ở backend.

---

# 3. Lỗi A — Chèn biến sai field: đang đặt caret trong Body nhưng biến nhảy lên Subject

## Root cause

`TemplateManagement.tsx` đang giữ trạng thái target:

```ts
const lastInsertTarget = useRef<Record<EditorLanguage, InsertTarget | null>>({
  VI: null,
  EN: null,
});
```

Khi Subject được focus/select:

```ts
lastInsertTarget.current[language] = 'subject';
```

Nhưng sau khi chuyển sang shared `EmailRichTextEditor`, Body chỉ tự lưu caret trong `lastRange`.

`TemplateManagement` **không còn nhận được tín hiệu khi Body được focus/select lại**.

Vì vậy flow lỗi:

```text
Click Subject
→ lastInsertTarget = subject

Click Body
→ editor biết caret Body
→ TemplateManagement vẫn giữ subject

Click biến sidebar
→ insertVariable() đọc subject
→ biến nhảy vào Subject
```

## Cách sửa

Không đưa logic caret Body trở lại `TemplateManagement`.

Giữ nguyên nguyên tắc:

- `EmailRichTextEditor` quản lý vị trí caret bên trong Body.
- `TemplateManagement` chỉ cần biết field hiện tại là `subject` hay `body`.

### `EmailRichTextEditor.tsx`

Bổ sung callback tối thiểu:

```ts
onActive?: () => void;
```

Hoặc tên tương đương rõ nghĩa:

```ts
onEditorActivated?: () => void;
```

Gọi callback khi:

- editor nhận focus;
- `onChangeSelection` nhận `range != null`.

Ví dụ:

```ts
onChangeSelection={(range) => {
  if (range) {
    lastRange.current = {
      index: range.index,
      length: range.length,
    };

    onEditorActivated?.();
  }

  setActive(editor()?.getFormat() ?? {});
}}
```

Có thể thêm `onFocus` tại wrapper/editor nếu ReactQuill hỗ trợ ổn định, nhưng `range != null` phải là nguồn chính.

### `TemplateManagement.tsx`

Truyền:

```tsx
<EmailRichTextEditor
  ...
  onEditorActivated={() => {
    lastInsertTarget.current[language] = 'body';
  }}
/>
```

Không lưu caret Body tại parent.

Không dùng `document.activeElement` để xác định target.

## Test hồi quy bắt buộc

Thêm case hiện tại đang thiếu:

```text
1. Focus Subject.
2. Đặt caret Subject.
3. Focus Body.
4. Đặt caret Body.
5. Click variable ở sidebar.
6. Variable phải vào Body.
7. Subject không thay đổi.
```

Thêm chiều ngược lại:

```text
Body → Subject → click variable → chỉ Subject thay đổi.
```

Kiểm tra riêng `VI` và `EN`.

---

# 4. Lỗi B — Không thể click/gõ giữa hai biến liên tiếp

## Root cause

Variable hiện được render bằng custom Quill `Embed`:

```ts
class VariableChip extends Embed
```

và:

```ts
contenteditable="false"
```

Đây là lựa chọn đúng để tránh người dùng sửa dở placeholder:

```text
{{senderName}}
```

thành giá trị không hợp lệ.

Nhưng hai embed liên tiếp:

```text
[VARIABLE A][VARIABLE B]
```

không có text position ổn định nằm giữa hai node để browser/Quill đặt caret.

## Yêu cầu giữ nguyên

Không đổi variable về raw editable text.

Không cho phép sửa một phần placeholder.

Serialized output vẫn phải là:

```text
{{variableName}}
```

## Cách sửa ưu tiên

Bổ sung **editor-only caret boundary** giữa các inline atomic variable.

Mục tiêu editor DOM:

```text
[chip A][caret position][chip B]
```

Stored HTML vẫn phải là:

```text
{{A}}{{B}}
```

hoặc:

```text
{{A}} text {{B}}
```

tùy người dùng nhập.

### Nguyên tắc

Caret helper:

- chỉ tồn tại trong editor;
- không được gửi về backend;
- không xuất hiện trong preview;
- không được tính là content thực;
- không gây dirty state nếu chỉ do editor tự normalize;
- không phá `chipsToVariables()` / `variablesToChips()`.

Có thể dùng:

- zero-width text node có kiểm soát;
- hoặc inline blot riêng dành cho caret boundary.

Ưu tiên giải pháp đơn giản nhất tương thích Quill 2 hiện tại.

### Không làm

Không chèn space thật tự động vào stored template.

Không chèn `&nbsp;` làm giải pháp mặc định.

Không đổi chip thành `contenteditable=true`.

## Test bắt buộc

```text
Variable A + Variable B
→ click giữa
→ gõ "/"
→ output stored = {{A}}/{{B}}
```

```text
Variable A + Variable B
→ click giữa
→ Space
→ output stored có đúng 1 khoảng trắng thật.
```

```text
Backspace/Delete gần boundary
→ không làm hỏng một nửa placeholder.
```

```text
Save → reload
→ variable vẫn là chip
→ text giữa hai variable vẫn còn.
```

---

# 5. Lỗi C — Chèn bảng rồi không thể nhập trực tiếp trong Body

## Hiện trạng

Table hiện là `BlockEmbed` atomic:

```ts
class EmailTable extends BlockEmbed
```

và wrapper:

```html
contenteditable="false"
```

Điều này là chủ đích để Quill không phá:

- row/column;
- border;
- padding;
- inline CSS cần cho email;
- `role="presentation"`.

Do đó **không được coi việc không gõ trực tiếp trong cell là bug kỹ thuật của Quill**.

Vấn đề thực tế là UX chưa đủ rõ và flow chỉnh bảng còn khó sử dụng.

## Quyết định triển khai

Trong đợt fix này:

**Giữ table atomic.**

Không chuyển sang native Quill table nếu chưa chứng minh được round-trip email-safe.

### Cần cải thiện

#### A. Sau khi tạo bảng

Sau `Áp dụng`:

- bảng được selected;
- caret được đặt sau bảng;
- nút `Chỉnh sửa bảng` active đúng;
- user có thể gõ text ngay sau bảng.

#### B. Click bảng

Single click:

```text
select table
```

Double click:

```text
open EmailTableDialog
```

Thêm visual selected state rõ ràng.

Ví dụ:

```css
.pems-email-table[data-selected="true"]
```

hoặc state/class tương đương.

Không làm selected style đi vào serialized HTML.

#### C. Khả năng chỉnh bảng

Dialog phải cho phép:

- sửa text từng cell;
- thêm hàng;
- thêm cột;
- xóa hàng;
- xóa cột;
- bật/tắt header row;
- alignment;
- width preset;
- insert variable vào cell.

Các hành vi hiện có phải giữ nguyên.

#### D. Sau khi edit

Sau `Áp dụng`:

- thay đúng table đang edit;
- caret ra sau table;
- selected state reset/giữ hợp lý;
- không edit nhầm table nếu document thay đổi.

---

# 6. Lỗi D — Table dialog có thể khó nhập / mất focus / selection

## Rà soát `EmailTableDialog.tsx`

Các cell đang dùng:

```tsx
<textarea ... />
```

Đây là đúng.

Cần kiểm tra các case sau ngoài happy path.

### Case 1 — Click textarea phải nhập được ngay

Không có overlay nào được phép chặn pointer event.

Kiểm tra:

- modal overlay;
- table wrapper;
- sticky action bar;
- z-index;
- parent `pointer-events`;
- focus trap nếu có.

### Case 2 — Thêm hàng/cột không làm mất text đang nhập

Các row/cell hiện dùng index làm key.

Do không support reorder, index key vẫn có thể chấp nhận.

Nhưng cần test:

```text
nhập A ở row 2 col 2
→ Add row
→ A vẫn nằm row 2 col 2
```

và:

```text
nhập A
→ Add column
→ A không mất
```

### Case 3 — Insert variable trong cell

Sau khi chọn variable:

- insert đúng cell đang active;
- insert đúng selection/caret;
- caret state cập nhật về sau token;
- không append nhầm cell khác.

### Case 4 — User click picker khi chưa chọn cell

Hiện tại `focused.current == null` thì no-op.

Cần UI rõ ràng:

- disable select nếu chưa chọn cell;
- hoặc hiển thị placeholder: `Chọn một ô trước`.

Không silently no-op.

---

# 7. Lỗi E — Chèn variable liên tiếp bị dính không có separator

Hiện tại:

```ts
q.insertEmbed(index, 'pemsVariable', ...)
q.setSelection(index + 1, 0)
```

Nếu user click nhiều biến liên tục, kết quả có thể là:

```text
{{senderPhone}}{{senderPhone}}
```

Đây có thể là dữ liệu hợp lệ, nên **không tự thêm whitespace vào stored content**.

Nhưng editor phải cho phép:

- đặt caret giữa hai chip;
- gõ dấu cách / dấu `/` / `-` / text;
- di chuyển bằng arrow keys qua chip ổn định.

Test:

```text
insert A
insert B
ArrowLeft
type " / "
→ {{A}} / {{B}}
```

---

# 8. Lỗi F — Target variable giữa VI / EN

`lastInsertTarget` và Subject selection đang tách theo language.

Phải giữ behavior này.

Cần test đầy đủ:

```text
VI Subject caret
→ chuyển EN
→ Body EN
→ insert variable
→ không sử dụng caret/target VI.
```

```text
EN Subject
→ VI Body
→ insert
→ Body VI.
```

Khi đổi language:

- editor remount theo `key={bodyField}` hiện tại vẫn giữ;
- không chia sẻ `lastRange` giữa VI và EN;
- không overwrite body ngôn ngữ còn lại.

Không bỏ `key={bodyField}` nếu chưa có replacement tương đương.

---

# 9. Rà soát Subject variable

Subject vẫn là plain input.

Phải giữ:

```ts
selectionStart
selectionEnd
```

và replace selection đúng.

### Kiểm tra thêm

Nếu variable có:

```text
forbiddenInSubject = true
```

thì sidebar không được chèn vào Subject một cách mù quáng.

Nếu contract hiện đã validate sau khi chèn, ưu tiên thêm guard UX sớm:

```text
Target = Subject
Variable forbiddenInSubject
→ không insert
→ hiển thị thông báo
```

Backend vẫn là authority.

Không thay contract API.

---

# 10. Rà soát variable sidebar

Hiện có hai đường insert variable:

1. Picker trong `EmailRichTextEditor`.
2. Sidebar `3. Biến của mẫu này` trong `TemplateManagement`.

Cả hai phải cho kết quả giống nhau.

### Quy tắc

- Editor picker dùng caret Body.
- Sidebar dùng target `Subject` / `Body`.
- Nếu target Body, delegate cho editor handle.
- Không tự sửa HTML Body tại parent khi live editor sẵn sàng.
- Fallback khi editor chưa mounted chỉ dùng cho tình huống thực sự cần thiết.

### Fallback cần xem lại

Hiện fallback prepend variable vào đầu Body khi editor không ready.

Đây phù hợp hơn append cuối, nhưng user runtime bình thường không nên rơi vào branch này.

Thêm diagnostic/test để đảm bảo:

```text
editor mounted bình thường
→ sidebar luôn gọi editor.insertVariable()
```

---

# 11. Rà soát dirty state sau các fix

Không để việc bổ sung caret helper làm template báo:

```text
Có thay đổi chưa lưu
```

khi user chưa sửa nội dung.

Các editor-only node phải bị loại bỏ trước canonical comparison.

### Cases

```text
Open template
→ dirty = false.
```

```text
Click variable rồi Undo về trạng thái ban đầu
→ dirty = false nếu HTML semantic bằng baseline.
```

```text
Click table
→ open dialog
→ Apply không đổi gì
→ dirty = false.
```

```text
Caret boundary tự sinh
→ dirty = false.
```

```text
Switch VI ↔ EN
→ dirty = false.
```

---

# 12. Rà soát table serialization

Đảm bảo cycle:

```text
stored HTML
→ tablesToNodes()
→ Quill
→ nodesToTables()
→ stored HTML
```

không làm mất:

- `<table>`;
- `<tbody>`;
- `<tr>`;
- `<th>` / `<td>` theo model hiện tại;
- width;
- align;
- margin;
- border;
- padding;
- inline styles;
- variables trong cell.

### Đặc biệt

Không để editor-only selected marker được serialize.

Không để `contenteditable="false"` đi vào stored HTML.

Không để wrapper `.pems-email-table` đi vào stored HTML.

---

# 13. Rà soát variable serialization

Cycle:

```text
{{senderName}}
→ variablesToChips()
→ Quill chip
→ chipsToVariables()
→ {{senderName}}
```

phải giữ nguyên.

Sau khi thêm caret boundary:

```text
{{A}}{{B}}
```

không được thành:

```text
{{A}}​{{B}}
```

với zero-width char lưu thật trong DB.

Kiểm tra và strip chính xác editor-only character/node trong:

```text
chipsToVariables()
```

Không strip text thật của người dùng.

---

# 14. Rà soát System Action Block

Shared editor còn chứa:

```text
pemsSystemActionBlock
```

Đây cũng là atomic embed.

Sau khi sửa caret behavior cho variable/table, kiểm tra không vô tình phá action block.

### Test

```text
text
[action block]
text
```

- caret trước block;
- caret sau block;
- move block;
- save;
- reload;
- đúng 1 block;
- không duplicate;
- không mất vị trí.

Không cho fix variable tạo ra khả năng edit text bên trong action block.

---

# 15. Rà soát cấu hình email/template liên quan

Phạm vi này chỉ audit các lỗi có thể cùng nguồn với editor/config.

## 15.1 Contract variables

Sidebar chỉ hiển thị variable do template contract trả về.

Kiểm tra:

```text
variable được offer
→ save backend chấp nhận
→ renderer có value khi gửi.
```

Không hard-code thêm variable ở frontend.

## 15.2 Sender variables

Các biến:

```text
senderName
senderRole
senderEmail
senderPhone
senderDepartment
senderCampus
```

phải:

- chỉ xuất hiện ở template cho phép;
- insert được Body;
- insert Subject nếu contract cho phép;
- không duplicate do click nhiều lần ngoài ý muốn;
- preview có sample hợp lệ;
- runtime send resolve đúng.

Không đưa lại contact configuration cũ.

## 15.3 System blocks

Kiểm tra frontend không offer block mà backend contract không cho phép.

Các block bắt buộc:

- phải được phát hiện nếu thiếu;
- không được duplicate;
- UI phải chỉ ra đúng language/field lỗi.

## 15.4 Subject

Subject phải là plain text.

Không cho HTML/table/system block trong subject.

Variable bị forbidden trong subject phải được chặn.

## 15.5 Preview

Sau mỗi thay đổi Body:

```text
stored formData
→ sample substitution
→ system block substitution
→ sanitize
→ preview
```

Preview không được render editor-only:

```text
.pems-variable-chip
.pems-email-table wrapper
contenteditable
caret spacer
editor selection class
```

## 15.6 Save

Payload chỉ chứa canonical stored content.

Không chứa DOM editor.

Trước save kiểm tra:

```text
bodyVi
bodyEn
subjectVi
subjectEn
```

đều đúng contract.

Không đổi API payload nếu không cần.

## 15.7 Restore default

Sau restore:

- editor hiển thị đúng body mới;
- variable trở lại chip;
- table trở lại atomic table;
- dirty state = false;
- caret state cũ bị reset;
- Subject target cũ không được giữ sang template/restored document mới.

---

# 16. Reset selection state khi đổi template

Đây là lỗi liên quan cần fix cùng scope.

Khi mở template khác hoặc đóng editor:

reset:

```ts
subjectSelection.current = {
  VI: null,
  EN: null,
};

lastInsertTarget.current = {
  VI: null,
  EN: null,
};
```

Shared editor remount sẽ tự reset `lastRange`.

Không để caret của template trước được áp dụng vào template mới.

Tương tự sau `restore default`, nên đảm bảo selection cũ không trỏ vào index vượt quá document mới.

---

# 17. Rà soát table selection stale

`selectedTable` hiện giữ `HTMLElement`.

Nếu React/Quill replace DOM sau một controlled update, ref này có thể stale.

### Cần kiểm tra

Sau:

```text
select table
→ edit text ngoài table
→ controlled editor rerender
→ bấm Chỉnh sửa bảng
```

không được edit node đã detached.

Trước `editSelectedTable`:

```ts
if (!selectedTable?.isConnected) {
  setSelectedTable(null);
  return;
}
```

Hoặc resolve table qua blot/current document trước khi mở.

Không chuyển sang global table ID nếu chưa cần.

---

# 18. Rà soát index sau atomic embed

Các thao tác:

```ts
q.setSelection(index + 1, 0)
```

cần test với:

- variable embed;
- table block;
- action block;
- divider;
- image.

Mục tiêu:

```text
insert object
→ caret nằm ngay sau object
→ gõ tiếp không bị nhảy đầu document.
```

Không dùng `getSelection(true)` ở những nơi blur có thể biến range thành `0` ngoài ý muốn nếu đã có `lastRange`.

---

# 19. Rà soát toolbar làm mất caret

Toolbar button đang dùng:

```tsx
onMouseDown={(e) => e.preventDefault()}
```

để giữ selection.

Giữ behavior này.

Kiểm tra các control không dùng `TB`:

- font select;
- size select;
- color input;
- background input;
- variable dropdown;
- table dialog.

Các control này có thể blur editor.

Nếu thao tác cần caret, phải dùng `lastRange` đã nhớ, không dựa vào selection sau blur.

---

# 20. Test plan

## Unit / component

### `EmailRichTextEditor.test.tsx`

Bổ sung tối thiểu:

1. Variable tại remembered caret.
2. Variable replace selected text.
3. Hai variable liên tiếp có thể chèn text giữa.
4. Arrow navigation quanh variable.
5. Variable serialization không chứa editor-only caret helper.
6. Table insert.
7. Table edit.
8. Table click → selected.
9. Table double click → dialog.
10. Text gõ được ngay sau table.
11. Apply unchanged table không dirty.
12. Action block không bị ảnh hưởng.
13. Open editor không emit user change.
14. Switch controlled value không corrupt selection ngoài expected reset.

### `TemplateManagementEditorUx.test.tsx`

Bổ sung:

1. Subject → Body → sidebar variable → Body.
2. Body → Subject → sidebar variable → Subject.
3. VI Subject → EN Body.
4. EN Subject → VI Body.
5. Forbidden-in-subject variable.
6. Mở template mới reset target.
7. Restore reset caret/target.
8. Editor ready path không dùng fallback.
9. Dirty state không thay đổi do editor-only nodes.

### `EmailTableDialog`

Bổ sung test nếu chưa có:

1. typing cell;
2. add row giữ text;
3. add col giữ text;
4. remove row đúng row;
5. remove col đúng col;
6. variable picker disabled/no-op rõ ràng khi chưa focus cell;
7. variable inserted at selected range;
8. apply/cancel.

---

# 21. Manual QA

Dùng một template có cả:

- Subject variable;
- Body variable;
- table;
- sender variables.

Thực hiện tuần tự.

## Case A — Subject / Body target

```text
1. Click giữa Subject.
2. Add "Email người gửi".
3. Click giữa Body.
4. Add "Họ tên người gửi".
5. Kiểm tra không có biến nào vào sai field.
```

## Case B — Adjacent variables

```text
1. Add "Họ tên người gửi".
2. Add "Vai trò người gửi".
3. Click giữa hai chip.
4. Gõ " - ".
5. Save.
6. Reload.
```

Expected:

```text
{{senderName}} - {{senderRole}}
```

## Case C — Table

```text
1. Chèn bảng.
2. Nhập tất cả cell trong dialog.
3. Apply.
4. Double click bảng.
5. Sửa một cell.
6. Add row.
7. Add column.
8. Apply.
9. Gõ một đoạn text sau bảng.
10. Save.
11. Reload.
```

Không mất:

- cell;
- row;
- column;
- border;
- padding;
- variable.

## Case D — VI / EN

```text
VI Body edit
→ EN Subject edit
→ VI Body add variable
→ EN Body add variable
→ Save
→ Reload
```

Không cross-write.

## Case E — Preview

Preview phải giống content semantic đã sửa.

Không hiện:

```text
data-variable
pems-variable-chip
data-email-table
contenteditable
caret helper
```

---

# 22. Gates

Chạy tối thiểu:

```bash
cd frontend/pems-react

npm run typecheck
npm run build
npm run test
```

Nếu project dùng script test khác, dùng script hiện có trong `package.json`.

Ưu tiên chạy targeted trước:

```text
EmailRichTextEditor.test.tsx
TemplateManagementEditorUx.test.tsx
TemplateManagement.test.tsx
```

Sau đó full FE suite.

Nếu thay đổi serialization có khả năng ảnh hưởng preview/send parity, chạy thêm các email real-stack/E2E hiện có.

---

# 23. Definition of Done

Task chỉ hoàn thành khi đủ tất cả:

- [ ] Body focus thực sự đổi `lastInsertTarget` sang `body`.
- [ ] Subject focus đổi target sang `subject`.
- [ ] Không còn case Body → add variable → Subject.
- [ ] VI/EN không dùng chung selection.
- [ ] Hai variable liên tiếp có thể đặt caret/gõ text giữa.
- [ ] Placeholder vẫn atomic, không thể sửa dở.
- [ ] Không có editor-only spacer trong DB/payload/preview.
- [ ] Table vẫn email-safe.
- [ ] Table dialog nhập/sửa cell ổn định.
- [ ] Add/remove row/column không làm mất cell khác.
- [ ] Click/double-click table hoạt động rõ ràng.
- [ ] Có thể tiếp tục gõ text trước/sau table.
- [ ] Dirty state không bật khi chỉ mở template/table.
- [ ] Restore không để lại caret/target cũ.
- [ ] Preview không chứa editor-only markup.
- [ ] Save payload là canonical content.
- [ ] System action block không bị ảnh hưởng.
- [ ] Contract variable rules vẫn fail-closed.
- [ ] Targeted tests xanh.
- [ ] Full frontend tests/typecheck/build xanh hoặc ghi rõ baseline failure có sẵn.

---

# 24. Không làm trong scope này

Không:

- đổi database schema;
- tạo bảng DB mới;
- đổi email template API;
- thay renderer backend nếu chưa có bằng chứng;
- quay lại hai editor riêng;
- dùng Quill native table nếu chưa chứng minh email-safe;
- đổi variable chip thành editable raw text;
- hồi sinh contact configuration cũ;
- refactor toàn bộ email module;
- xử lý các feature email khác không liên quan tới editor/configuration.

Nếu phát hiện lỗi ngoài scope có thể làm sai email gửi thật, ghi riêng:

```text
BLOCKER / OUT-OF-SCOPE
```

kèm file + bằng chứng + ảnh hưởng, không tự mở rộng implementation.

---

# 25. Thứ tự triển khai đề xuất

1. Viết regression test cho **Subject → Body → variable** trước.
2. Fix active target contract giữa `TemplateManagement` và `EmailRichTextEditor`.
3. Bổ sung test VI/EN + reset selection.
4. Thiết kế và implement caret boundary cho adjacent variable chips.
5. Bổ sung serialization tests cho chip boundary.
6. Cải thiện table selection / double click / caret after table.
7. Rà soát `EmailTableDialog` focus và variable-in-cell.
8. Chạy dirty-state / preview / save round-trip tests.
9. Rà soát system action block regression.
10. Chạy targeted FE tests.
11. Chạy full FE typecheck/build/test.
12. Chỉ khi có lỗi parity với backend mới mở rộng sang backend.

---

## Kết quả mong đợi

Editor email sau fix phải có behavior nhất quán:

```text
User đặt caret ở đâu
→ insert/format áp dụng đúng ở đó.

Editor hiển thị chip/table dưới dạng object an toàn
→ nhưng user vẫn có các vị trí caret hợp lý để tiếp tục soạn.

Editor-only markup
→ không bao giờ đi vào DB, preview hoặc email gửi thật.

Save / preview / send
→ cùng một nội dung semantic.
```
