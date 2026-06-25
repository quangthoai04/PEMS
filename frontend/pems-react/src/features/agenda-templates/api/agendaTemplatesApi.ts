import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AgendaSetupForInstance,
  AgendaTemplateDefaultRow,
  AgendaTemplateDetailDto,
  AgendaTemplateListResponse,
  ApplyAgendaTemplatePayload,
  ApplyAgendaTemplateResponse,
  CreateAgendaTemplatePayload,
  ResolveDefaultResponse,
  SetAgendaTemplateDefaultPayload,
  UpdateAgendaTemplatePayload,
  VisitType,
} from '../types/agendaTemplates.types';

const ep = API_ENDPOINTS.agendaTemplates;

export interface ListTemplatesParams {
  campusId?: number | null;
  visitType?: VisitType;
  status?: 'ACTIVE' | 'INACTIVE';
  includeDeleted?: boolean;
}

export const agendaTemplatesApi = {
  // ── Template CRUD ──────────────────────────────────────────────────────
  async list(params: ListTemplatesParams = {}): Promise<AgendaTemplateListResponse> {
    const { data } = await httpClient.get<AgendaTemplateListResponse>(ep.list, { params });
    return data;
  },

  async detail(agendaTemplateId: number): Promise<AgendaTemplateDetailDto> {
    const { data } = await httpClient.get<AgendaTemplateDetailDto>(ep.detail(agendaTemplateId));
    return data;
  },

  async create(payload: CreateAgendaTemplatePayload): Promise<{ agendaTemplateId: number; itemCount: number; message: string }> {
    const { data } = await httpClient.post(ep.create, payload);
    return data;
  },

  async update(agendaTemplateId: number, payload: UpdateAgendaTemplatePayload): Promise<{ agendaTemplateId: number; itemCount: number; message: string }> {
    const { data } = await httpClient.put(ep.update(agendaTemplateId), payload);
    return data;
  },

  async remove(agendaTemplateId: number): Promise<{ agendaTemplateId: number; message: string }> {
    const { data } = await httpClient.delete(ep.delete(agendaTemplateId));
    return data;
  },

  // ── Default-template mapping ───────────────────────────────────────────
  async listDefaults(campusId?: number | null): Promise<{ defaults: AgendaTemplateDefaultRow[]; total: number }> {
    const { data } = await httpClient.get(ep.defaults, { params: campusId != null ? { campusId } : {} });
    return data;
  },

  async setDefault(payload: SetAgendaTemplateDefaultPayload): Promise<unknown> {
    const { data } = await httpClient.put(ep.setDefault, payload);
    return data;
  },

  async resolveDefault(visitType: VisitType, campusId?: number | null): Promise<ResolveDefaultResponse> {
    const params: Record<string, unknown> = { visitType };
    if (campusId != null) params.campusId = campusId;
    const { data } = await httpClient.get<ResolveDefaultResponse>(ep.resolveDefault, { params });
    return data;
  },

  // ── Per-instance setup / apply ─────────────────────────────────────────
  async getSetupForInstance(visitInstanceId: number): Promise<AgendaSetupForInstance> {
    const { data } = await httpClient.get<AgendaSetupForInstance>(ep.forInstance(visitInstanceId));
    return data;
  },

  async apply(payload: ApplyAgendaTemplatePayload): Promise<ApplyAgendaTemplateResponse> {
    const { data } = await httpClient.post<ApplyAgendaTemplateResponse>(ep.apply, payload);
    return data;
  },
};

export default agendaTemplatesApi;
