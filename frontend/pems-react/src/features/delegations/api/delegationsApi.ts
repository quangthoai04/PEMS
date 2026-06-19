import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CancelVisitRequestPayload,
  CancelVisitRequestResult,
} from '../types/delegations.types';

export const delegationsApi = {
  /**
   * UC-136: cancel an APPROVED visit request (Visitor self-cancel, or Staff Leader / HO).
   * Cancels every still-cancellable campus instance; the request becomes CANCELLED when
   * all its campuses are cancelled.
   */
  async cancelVisitRequest(
    visitRequestId: number | string,
    payload: CancelVisitRequestPayload
  ): Promise<CancelVisitRequestResult> {
    const { data } = await httpClient.post<CancelVisitRequestResult>(
      API_ENDPOINTS.delegations.cancel(visitRequestId),
      payload
    );
    return data;
  },

  /**
   * UC-136: the current Host cancels a single campus instance after the guest confirms
   * the cancellation through an external channel (record the details in the reason).
   */
  async cancelVisitRequestCampus(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    payload: CancelVisitRequestPayload
  ): Promise<CancelVisitRequestResult> {
    const { data } = await httpClient.post<CancelVisitRequestResult>(
      API_ENDPOINTS.delegations.cancelCampus(visitRequestId, visitInstanceId),
      payload
    );
    return data;
  },
};
