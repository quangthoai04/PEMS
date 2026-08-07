/**
 * The table editor (V4 §7.3, §16.4, §17).
 *
 * <b>Why a dialog and not in-place editing.</b> A table in the email editor is one atomic node — see
 * `emailEditorTable.ts` for the measurements that forced that. Atomic means Quill will not let a caret
 * inside it, so the cells need somewhere else to be edited. A dialog also makes the structural operations
 * (add a column, drop a row, promote the first row to a heading) explicit and reversible, rather than
 * something that happens as a side effect of where a cursor was.
 *
 * <b>What it deliberately does not offer.</b> No raw HTML field and no free-text CSS. A cell is plain
 * text plus variable placeholders; borders, padding and the rest come from the generated markup, and the
 * only geometry choices are the fixed alignment and width presets. Everything here is checked again on
 * the backend — this layer exists so the author sees the result while editing, not so the backend can
 * trust it.
 */
import React, { useCallback, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import {
  type EmailTableAlign, type EmailTableModel, type EmailTableWidth,
  TABLE_MAX_COLS, TABLE_MAX_ROWS, TABLE_MIN_COLS, TABLE_MIN_ROWS, TABLE_WIDTH_PRESETS,
  addTableColumn, addTableRow, removeTableColumn, removeTableRow,
} from '../utils/emailEditorTable';
import type { EmailEditorVariable } from './EmailRichTextEditor';

export interface EmailTableDialogProps {
  /** The table being edited. A fresh model for "insert", the parsed one for "edit". */
  initial: EmailTableModel;
  /** Offered in the cell variable picker. Empty or absent hides it (COMPOSE mode has none). */
  variables?: EmailEditorVariable[];
  onCancel: () => void;
  onApply: (model: EmailTableModel) => void;
}

const ALIGNMENTS: { value: EmailTableAlign; label: string }[] = [
  { value: 'left', label: 'Trái' },
  { value: 'center', label: 'Giữa' },
  { value: 'right', label: 'Phải' },
];

const WIDTH_LABELS: Record<EmailTableWidth, string> = {
  '100%': 'Toàn bộ chiều rộng (100%)',
  '75%': 'Ba phần tư (75%)',
  '50%': 'Một nửa (50%)',
  auto: 'Tự động theo nội dung',
};

export function EmailTableDialog({ initial, variables, onCancel, onApply }: EmailTableDialogProps) {
  const [model, setModel] = useState<EmailTableModel>(initial);

  /**
   * Which cell a variable would go into, and where in it.
   *
   * Kept here for the same reason the editor keeps `lastRange`: choosing from the picker blurs the cell,
   * so by the time the handler runs nothing is focused. Without this the token could only ever be
   * appended to something, and "appended to whichever cell was last rendered" is not a position anybody
   * asked for.
   */
  const focused = useRef<{ row: number; col: number; start: number; end: number } | null>(null);

  /**
   * The same fact as `focused`, in a form the markup can read.
   *
   * The ref cannot drive the UI — nothing re-renders when it changes — and without something that does,
   * the picker looked usable with no cell chosen and then did nothing at all when it was used. A control
   * that silently declines is worse than one that is plainly not available yet: the author repeats the
   * action, decides the variable is broken, and types the placeholder by hand.
   */
  const [activeCell, setActiveCell] = useState<{ row: number; col: number } | null>(null);

  const rowCount = model.rows.length;
  const colCount = model.rows[0]?.length ?? 0;

  const setCell = useCallback((row: number, col: number, text: string) => {
    setModel((m) => ({
      ...m,
      rows: m.rows.map((r, ri) => (ri !== row
        ? r
        : r.map((cell, ci) => (ci !== col ? cell : { ...cell, text })))),
    }));
  }, []);

  const insertVariable = useCallback((name: string) => {
    const at = focused.current;
    if (!at) return;

    setModel((m) => {
      const cell = m.rows[at.row]?.[at.col];
      if (!cell) return m;

      // Replaces a selected run, exactly as the body editor does — the alternative is a token dropped
      // next to the text somebody had highlighted to get rid of.
      const text = `${cell.text.slice(0, at.start)}{{${name}}}${cell.text.slice(at.end)}`;
      return {
        ...m,
        rows: m.rows.map((r, ri) => (ri !== at.row
          ? r
          : r.map((c, ci) => (ci !== at.col ? c : { ...c, text })))),
      };
    });

    const caret = at.start + name.length + 4;
    focused.current = { ...at, start: caret, end: caret };
  }, []);

  const remember = useCallback((row: number, col: number) => (
    e: React.SyntheticEvent<HTMLTextAreaElement>,
  ) => {
    const el = e.currentTarget;
    focused.current = {
      row, col, start: el.selectionStart ?? el.value.length, end: el.selectionEnd ?? el.value.length,
    };
    // Same cell, same object: this runs on every keystroke, and a fresh object each time would re-render
    // the whole grid mid-word for no visible difference.
    setActiveCell((prev) => (prev && prev.row === row && prev.col === col ? prev : { row, col }));
  }, []);

  const preview = useMemo(
    () => `${rowCount} hàng × ${colCount} cột`,
    [rowCount, colCount],
  );

  /**
   * Whether there is a cell for a variable to go into — asked of the MODEL, not only of the ref.
   *
   * A cell that was focused and then deleted (its row dropped, its column removed) leaves the ref
   * pointing at a position the table no longer has, and the picker would offer to insert into it.
   */
  const cellIsLive = !!activeCell && !!model.rows[activeCell.row]?.[activeCell.col];

  return (
    <div
      className="fixed inset-0 z-[300] flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Chỉnh sửa bảng"
    >
      <div className="flex max-h-full w-full max-w-3xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-3">
          <div>
            <h3 className="text-sm font-bold text-gray-800">Chỉnh sửa bảng</h3>
            <p className="mt-0.5 text-xs text-gray-500">{preview}</p>
          </div>
          <button
            type="button"
            onClick={onCancel}
            title="Đóng"
            aria-label="Đóng"
            className="rounded-lg p-1.5 text-gray-400 hover:bg-gray-50 hover:text-gray-600"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
          {/* ── Structure ── */}
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setModel(addTableRow)}
              disabled={rowCount >= TABLE_MAX_ROWS}
              className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs text-gray-700 hover:bg-gray-50 disabled:opacity-40"
            >
              <Plus className="h-3.5 w-3.5" /> Thêm hàng
            </button>
            <button
              type="button"
              onClick={() => setModel(addTableColumn)}
              disabled={colCount >= TABLE_MAX_COLS}
              className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs text-gray-700 hover:bg-gray-50 disabled:opacity-40"
            >
              <Plus className="h-3.5 w-3.5" /> Thêm cột
            </button>

            <label className="ml-2 inline-flex items-center gap-1.5 text-xs text-gray-700">
              <input
                type="checkbox"
                checked={model.headerRow}
                onChange={(e) => setModel((m) => ({ ...m, headerRow: e.target.checked }))}
              />
              Hàng đầu là tiêu đề
            </label>

            <label className="ml-auto inline-flex items-center gap-1.5 text-xs text-gray-700">
              Căn lề
              <select
                aria-label="Căn lề bảng"
                className="h-8 rounded-lg border border-gray-200 px-1.5 text-xs outline-none focus:border-[#004c91]"
                value={model.align}
                onChange={(e) => setModel((m) => ({ ...m, align: e.target.value as EmailTableAlign }))}
              >
                {ALIGNMENTS.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
              </select>
            </label>

            <label className="inline-flex items-center gap-1.5 text-xs text-gray-700">
              Độ rộng
              <select
                aria-label="Độ rộng bảng"
                className="h-8 rounded-lg border border-gray-200 px-1.5 text-xs outline-none focus:border-[#004c91]"
                value={model.width}
                onChange={(e) => setModel((m) => ({ ...m, width: e.target.value as EmailTableWidth }))}
              >
                {TABLE_WIDTH_PRESETS.map((w) => <option key={w} value={w}>{WIDTH_LABELS[w]}</option>)}
              </select>
            </label>
          </div>

          {/* ── Cells ── */}
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-xs">
              <tbody>
                {model.rows.map((row, r) => (
                  // The index IS the identity here: rows have no id, and reordering is not offered.
                  // eslint-disable-next-line react/no-array-index-key
                  <tr key={`r${r}`}>
                    {row.map((cell, c) => (
                      // eslint-disable-next-line react/no-array-index-key
                      <td key={`c${c}`} className="border border-gray-200 p-1 align-top">
                        <textarea
                          rows={2}
                          aria-label={`Ô hàng ${r + 1} cột ${c + 1}`}
                          className={`w-full min-w-[7rem] resize-y rounded-md border border-transparent px-1.5 py-1 text-xs outline-none focus:border-[#004c91]
                            ${model.headerRow && r === 0 ? 'bg-gray-50 font-semibold' : ''}`}
                          value={cell.text}
                          onChange={(e) => { setCell(r, c, e.target.value); remember(r, c)(e); }}
                          onFocus={remember(r, c)}
                          onSelect={remember(r, c)}
                          onKeyUp={remember(r, c)}
                        />
                      </td>
                    ))}
                    <td className="w-8 p-1 align-top">
                      <button
                        type="button"
                        onClick={() => setModel((m) => removeTableRow(m, r))}
                        disabled={rowCount <= TABLE_MIN_ROWS}
                        title={`Xóa hàng ${r + 1}`}
                        aria-label={`Xóa hàng ${r + 1}`}
                        className="rounded-md p-1 text-gray-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30 disabled:hover:bg-transparent"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </td>
                  </tr>
                ))}
                <tr>
                  {Array.from({ length: colCount }, (_, c) => (
                    // eslint-disable-next-line react/no-array-index-key
                    <td key={`d${c}`} className="p-1 text-center">
                      <button
                        type="button"
                        onClick={() => setModel((m) => removeTableColumn(m, c))}
                        disabled={colCount <= TABLE_MIN_COLS}
                        title={`Xóa cột ${c + 1}`}
                        aria-label={`Xóa cột ${c + 1}`}
                        className="rounded-md p-1 text-gray-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-30 disabled:hover:bg-transparent"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </td>
                  ))}
                  <td />
                </tr>
              </tbody>
            </table>
          </div>

          {variables && variables.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <label className="text-xs text-gray-600" htmlFor="pems-table-variable">
                Chèn biến vào ô đang chọn
              </label>
              <select
                id="pems-table-variable"
                aria-label="Chèn biến vào ô đang chọn"
                // Not merely ignored when there is no cell to insert into — said. The token has to go
                // somewhere, and "wherever was rendered last" is not a position anybody asked for.
                disabled={!cellIsLive}
                title={cellIsLive ? undefined : 'Chọn một ô trước'}
                className="h-8 max-w-xs rounded-lg border border-gray-200 px-1.5 text-xs outline-none focus:border-[#004c91] disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-400"
                value=""
                onChange={(e) => {
                  if (e.target.value) insertVariable(e.target.value);
                  e.target.value = '';
                }}
              >
                <option value="">{cellIsLive ? 'Chọn biến…' : 'Chọn một ô trước'}</option>
                {variables.map((v) => (
                  <option key={v.name} value={v.name}>{`${v.label} — {{${v.name}}}`}</option>
                ))}
              </select>
              {cellIsLive && (
                <span className="text-[11px] text-gray-400" data-testid="table-variable-target">
                  {`Ô hàng ${activeCell!.row + 1} cột ${activeCell!.col + 1}`}
                </span>
              )}
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-3">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-gray-600 hover:bg-gray-50"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={() => onApply(model)}
            data-testid="table-dialog-apply"
            className="rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#013565]"
          >
            Áp dụng
          </button>
        </div>
      </div>
    </div>
  );
}
