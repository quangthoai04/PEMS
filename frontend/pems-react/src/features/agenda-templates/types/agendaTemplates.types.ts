// Agenda template module types (v10 4-table design).
// visit_type mirrors visit_requests.visit_type; template items use relative offset + duration.

export type VisitType =
  | 'CAMPUS_TOUR'
  | 'MEETING'
  | 'WORKSHOP'
  | 'SIGNING_CEREMONY'
  | 'EXCHANGE'
  | 'OTHER';

export const VISIT_TYPES: VisitType[] = [
  'CAMPUS_TOUR',
  'MEETING',
  'WORKSHOP',
  'SIGNING_CEREMONY',
  'EXCHANGE',
  'OTHER',
];

export const VISIT_TYPE_LABELS: Record<VisitType, string> = {
  CAMPUS_TOUR: 'Tham quan cơ sở (Campus Tour)',
  MEETING: 'Họp / Trao đổi',
  WORKSHOP: 'Workshop / Chuyên đề',
  SIGNING_CEREMONY: 'Lễ ký kết',
  EXCHANGE: 'Giao lưu',
  OTHER: 'Khác',
};

export type AgendaTemplateStatus = 'ACTIVE' | 'INACTIVE';

/** Template timeline item submitted by the client (create/update). */
export interface AgendaTemplateItemInput {
  displayOrder: number;
  startOffsetMinutes: number;
  durationMinutes: number;
  title: string;
  description?: string | null;
  location?: string | null;
  responsibleRoleLabel?: string | null;
}

/** Template timeline item returned by the API (detail / default / setup). */
export interface AgendaTemplateItemDto {
  agendaTemplateItemId: number;
  displayOrder: number;
  startOffsetMinutes: number;
  durationMinutes: number;
  title: string;
  description?: string | null;
  location?: string | null;
  responsibleRoleLabel?: string | null;
}

/** Template header + items (detail / default lookup / setup preview). */
export interface AgendaTemplateDto {
  agendaTemplateId: number;
  campusId?: number | null;
  campusScopeKey: string;
  visitType: VisitType;
  name: string;
  description?: string | null;
  status: AgendaTemplateStatus;
  items: AgendaTemplateItemDto[];
}

/** Lightweight list / dropdown row. */
export interface AgendaTemplateSummary {
  agendaTemplateId: number;
  campusId?: number | null;
  campusScopeKey: string;
  visitType: VisitType;
  name: string;
  description?: string | null;
  status: AgendaTemplateStatus;
  itemCount: number;
  isDefault: boolean;
}

export interface AgendaTemplateListResponse {
  templates: AgendaTemplateSummary[];
  total: number;
}

export interface AgendaTemplateDetailDto extends AgendaTemplateDto {
  isDeleted: boolean;
  isDefault: boolean;
}

export interface CreateAgendaTemplatePayload {
  campusId?: number | null;
  visitType: VisitType;
  name: string;
  description?: string | null;
  status: AgendaTemplateStatus;
  items: AgendaTemplateItemInput[];
}

export interface UpdateAgendaTemplatePayload extends CreateAgendaTemplatePayload {}

export interface AgendaTemplateDefaultRow {
  agendaTemplateDefaultId: number;
  campusId?: number | null;
  campusScopeKey: string;
  visitType: VisitType;
  agendaTemplateId: number;
  templateName: string;
  templateStatus: AgendaTemplateStatus;
  templateDeleted: boolean;
}

export interface SetAgendaTemplateDefaultPayload {
  campusId?: number | null;
  visitType: VisitType;
  agendaTemplateId: number;
}

export interface ResolveDefaultResponse {
  resolved: boolean;
  resolvedScope?: 'CAMPUS' | 'GLOBAL' | null;
  template?: AgendaTemplateDto | null;
}

/** A concrete visit_agendas row (absolute datetime). */
export interface AgendaRowView {
  agendaId: number;
  sequenceOrder: number;
  title: string;
  startTime: string;
  endTime?: string | null;
  location?: string | null;
  description?: string | null;
  sourceTemplateItemId?: number | null;
}

/** A selectable template in the setup dropdown — a summary plus its embedded items, so the host
 * can preview it without a second (management-gated) detail call. */
export interface AgendaSetupTemplateOption extends AgendaTemplateSummary {
  items: AgendaTemplateItemDto[];
}

export interface AgendaSetupForInstance {
  visitInstanceId: number;
  visitRequestId: number;
  campusId: number;
  visitType: VisitType;
  plannedStartAt: string;
  plannedEndAt: string;
  relation: 'HOST' | 'STAFF_LEADER' | 'HO' | string;
  canApply: boolean;
  defaultTemplateId?: number | null;
  defaultScope?: 'CAMPUS' | 'GLOBAL' | null;
  hasExistingAgenda: boolean;
  selectableTemplates: AgendaSetupTemplateOption[];
  currentAgenda: AgendaRowView[];
}

export interface ApplyAgendaTemplatePayload {
  visitInstanceId: number;
  agendaTemplateId: number;
  replaceExisting: boolean;
}

export interface ApplyAgendaTemplateResponse {
  visitInstanceId: number;
  agendaTemplateId: number;
  count: number;
  requestVisitType: VisitType;
  templateVisitType: VisitType;
  visitTypeMismatch: boolean;
  items: AgendaRowView[];
  message: string;
}
