import type {
  AgendaTemplateDto,
  AgendaTemplateItemDto,
  AgendaTemplateItemInput,
} from '../types/agendaTemplates.types';
import { formatVietnamTime } from '../../../shared/utils/vietnamTime';

/** Minimal shape needed to scale one template item's relative timeline. */
export interface TemplateBoundaryInput {
  startOffsetMinutes: number;
  durationMinutes: number;
}

/** One item's [start, end) boundary after proportional scaling onto a real visit window. */
export interface ScaledAgendaBoundary {
  start: Date;
  end: Date;
}

/**
 * Helpers to map between the editor model and API payloads, and to preview the absolute clock
 * time of a template's items once applied to a real visit.
 *
 * `scaleTemplateItems` mirrors the backend's AgendaTemplateTimelineScaler ONE-FOR-ONE (same ratio
 * formula, same minute rounding, same last-item-pinned-to-plannedEnd rule) so the setup preview the
 * host sees is exactly what Apply will persist — never two formulas that can silently drift apart.
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

  /**
   * templateSpanMinutes = max(startOffsetMinutes + durationMinutes) across all items — the furthest
   * endpoint any item reaches, NOT sum(durationMinutes) (a template can have gaps/overlaps between
   * items). Mirrors AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes on the backend.
   */
  computeTemplateSpanMinutes(items: TemplateBoundaryInput[]): number {
    if (items.length === 0) return 0;
    return Math.max(...items.map((i) => i.startOffsetMinutes + i.durationMinutes));
  },

  /**
   * Proportionally scales every item's template-relative [start, end) onto the visit's real
   * [plannedStartIso, plannedEndIso] window, in the SAME order as `items`. Each boundary is computed
   * independently from plannedStart (never chained off a previous item's computed end), so per-item
   * minute rounding cannot accumulate into drift. The item(s) whose template-relative end equals the
   * template span are pinned exactly to plannedEnd rather than recomputed through the ratio.
   *
   * Returns [] when the inputs cannot produce a valid timeline (missing/invalid dates, an end not
   * after start, or an empty/zero-span template) — the caller shows its own "not previewable" state
   * rather than rendering a broken timeline.
   */
  scaleTemplateItems(
    plannedStartIso: string | null | undefined,
    plannedEndIso: string | null | undefined,
    items: TemplateBoundaryInput[],
  ): ScaledAgendaBoundary[] {
    if (!plannedStartIso || !plannedEndIso || items.length === 0) return [];
    const plannedStart = new Date(plannedStartIso);
    const plannedEnd = new Date(plannedEndIso);
    if (Number.isNaN(plannedStart.getTime()) || Number.isNaN(plannedEnd.getTime())) return [];

    const visitSpanMs = plannedEnd.getTime() - plannedStart.getTime();
    if (visitSpanMs <= 0) return [];

    const templateSpanMinutes = agendaTemplatesAdapter.computeTemplateSpanMinutes(items);
    if (templateSpanMinutes <= 0) return [];

    const scaleBoundary = (templateMinuteOffset: number): Date => {
      const ratio = templateMinuteOffset / templateSpanMinutes;
      const scaledMinutes = Math.round((visitSpanMs / 60_000) * ratio);
      return new Date(plannedStart.getTime() + scaledMinutes * 60_000);
    };

    return items.map((item) => {
      const templateEnd = item.startOffsetMinutes + item.durationMinutes;
      const start = scaleBoundary(item.startOffsetMinutes);
      let end = templateEnd >= templateSpanMinutes ? plannedEnd : scaleBoundary(templateEnd);
      if (end.getTime() <= start.getTime()) end = new Date(start.getTime() + 60_000);
      return { start, end };
    });
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
