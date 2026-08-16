import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { UpdateProfileRequest, UploadAvatarResponse, ViewProfileResponse } from '../types/profile.types';

/** Avatar upload constraints — kept in sync with the backend handler (2MB, jpeg/png/webp). */
export const MAX_AVATAR_SIZE = 2 * 1024 * 1024;
export const ALLOWED_AVATAR_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

export type AvatarValidationError = 'INVALID_TYPE' | 'TOO_LARGE';

/** Client-side avatar validation. Returns a stable error code (for the caller to localize via
 * profile:avatar.invalidType/tooLarge), or null when valid. */
export function validateAvatarFile(file: File): AvatarValidationError | null {
  if (!(ALLOWED_AVATAR_TYPES as readonly string[]).includes(file.type)) {
    return 'INVALID_TYPE';
  }
  if (file.size > MAX_AVATAR_SIZE) {
    return 'TOO_LARGE';
  }
  return null;
}

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

  /** UC-15 — upload a new avatar (multipart). Returns the new file id + proxy URL. */
  async uploadAvatar(file: File): Promise<UploadAvatarResponse> {
    const formData = new FormData();
    formData.append('avatar', file);
    const { data } = await httpClient.put<UploadAvatarResponse>(API_ENDPOINTS.profile.avatar, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
};
