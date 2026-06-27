import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { UpdateProfileRequest, ViewProfileResponse } from '../types/profile.types';

/**
 * Self-service profile API. Backed by ProfilesController.
 * UC-14 view my profile, UC-15 update my profile. The backend resolves the user
 * from the JWT — no userId is ever sent from here.
 */
export const profileApi = {
  /** UC-14 — current user's own profile. */
  async getMyProfile(): Promise<ViewProfileResponse> {
    const { data } = await httpClient.get<ViewProfileResponse>(API_ENDPOINTS.profile.me);
    return data;
  },

  /** UC-15 — update allowed text fields; returns the refreshed profile. */
  async updateMyProfile(payload: UpdateProfileRequest): Promise<ViewProfileResponse> {
    const { data } = await httpClient.post<ViewProfileResponse>(API_ENDPOINTS.profile.update, payload);
    return data;
  },
};
