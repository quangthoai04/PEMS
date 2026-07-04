import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  BusinessCardOcrJob,
  ConfirmBusinessCardContactRequest,
  ConfirmBusinessCardContactResponse,
  ScanBusinessCardContext,
} from '../types/businessCardOcr.types';

/**
 * Business Card OCR API — the frontend only ever uploads the image to OUR backend;
 * the backend calls Google Document AI with the stored credential.
 */
export const businessCardOcrApi = {
  async scan(file: File, context: ScanBusinessCardContext = {}): Promise<BusinessCardOcrJob> {
    const form = new FormData();
    form.append('file', file);
    if (context.visitInstanceId) form.append('visitInstanceId', String(context.visitInstanceId));
    if (context.guestMemberId) form.append('guestMemberId', String(context.guestMemberId));
    if (context.minuteParticipantId)
      form.append('minuteParticipantId', String(context.minuteParticipantId));
    if (context.partnerId) form.append('partnerId', String(context.partnerId));

    const idempotencyKey =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2)}`;

    const { data } = await httpClient.post<BusinessCardOcrJob>(
      API_ENDPOINTS.businessCardOcr.scan,
      form,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
          'Idempotency-Key': idempotencyKey,
        },
        timeout: 120_000,
      },
    );
    return data;
  },

  async getJob(ocrJobId: number | string): Promise<BusinessCardOcrJob> {
    const { data } = await httpClient.get<BusinessCardOcrJob>(
      API_ENDPOINTS.businessCardOcr.job(ocrJobId),
    );
    return data;
  },

  async confirmContact(
    ocrJobId: number | string,
    payload: ConfirmBusinessCardContactRequest,
  ): Promise<ConfirmBusinessCardContactResponse> {
    const { data } = await httpClient.post<ConfirmBusinessCardContactResponse>(
      API_ENDPOINTS.businessCardOcr.confirmContact(ocrJobId),
      payload,
    );
    return data;
  },

  async discard(ocrJobId: number | string) {
    const { data } = await httpClient.post(API_ENDPOINTS.businessCardOcr.discard(ocrJobId));
    return data as { ocrJobId: number; status: string };
  },
};
