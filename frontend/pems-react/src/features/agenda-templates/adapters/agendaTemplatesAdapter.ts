import type {
  AgendaTemplateDto,
  AgendaTemplateItemDto,
  AgendaTemplateItemInput,
} from '../types/agendaTemplates.types';
import { formatVietnamTime } from '../../../shared/utils/vietnamTime';

/**
 * Helpers to map between the editor model and API payloads, and to preview the absolute clock
 * time of a template item relative to a base datetime (offset arithmetic mirrors the backend:
 * start = base + startOffsetMinutes, end = start + durationMinutes).
 */
export const agendaTemplatesAdapter = {
  /** "+0′", "+1h30′" style offset label. */
  formatOffset(minutes: number): string {
    const m = Math.max(0, Math.floor(minutes));
    const h = Math.floor(m / 60);
    const rem = m % 60;
    if (h === 0) return `+${rem}′`;
    return rem === 0 ? `+${h}h` : `+${h}h${rem}′`;
  },

  /** "15′", "1h30′" duration label. */
  formatDuration(minutes: number): string {
    const m = Math.max(0, Math.floor(minutes));
    const h = Math.floor(m / 60);
    const rem = m % 60;
    if (h === 0) return `${rem}′`;
    return rem === 0 ? `${h}h` : `${h}h${rem}′`;
  },

  /** Preview "HH:mm – HH:mm" given a base ISO datetime + the item's offset/duration. */
  previewRange(baseIso: string | null | undefined, offsetMinutes: number, durationMinutes: number): string {
    if (!baseIso) return '';
    const base = new Date(baseIso);
    if (Number.isNaN(base.getTime())) return '';
    const start = new Date(base.getTime() + offsetMinutes * 60_000);
    const end = new Date(start.getTime() + durationMinutes * 60_000);
    // Hiển thị theo giờ Việt Nam cố định, không phụ thuộc timezone browser.
    return `${formatVietnamTime(start)} – ${formatVietnamTime(end)}`;
  },

  /** Map API items (with id) down to the input shape used by the editor / create+update payloads. */
  toInputItems(items: AgendaTemplateItemDto[]): AgendaTemplateItemInput[] {
    return [...items]
      .sort((a, b) => a.startOffsetMinutes - b.startOffsetMinutes || a.displayOrder - b.displayOrder)
      .map((i, idx) => ({
        displayOrder: idx + 1,
        startOffsetMinutes: i.startOffsetMinutes,
        durationMinutes: i.durationMinutes,
        title: i.title,
        description: i.description ?? null,
        location: i.location ?? null,
        responsibleRoleLabel: i.responsibleRoleLabel ?? null,
      }));
  },

  /** Re-number displayOrder 1..n in array order (used before submitting an edited list). */
  renumber(items: AgendaTemplateItemInput[]): AgendaTemplateItemInput[] {
    return items.map((i, idx) => ({ ...i, displayOrder: idx + 1 }));
  },

  scopeLabel(template: Pick<AgendaTemplateDto, 'campusId' | 'campusScopeKey'>): string {
    return template.campusId == null ? 'Toàn hệ thống' : `Cơ sở #${template.campusId}`;
  },
};

export default agendaTemplatesAdapter;
