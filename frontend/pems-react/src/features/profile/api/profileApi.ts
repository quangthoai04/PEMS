import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { UpdateProfileRequest, UploadAvatarResponse, ViewProfileResponse } from '../types/profile.types';

/** Avatar upload rules — mirrored on the backend (size + type are revalidated server-side). */
export const MAX_AVATAR_SIZE = 2 * 1024 * 1024; // 2 MB
export const ALLOWED_AVATAR_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** Returns a Vietnamese error message when the file is not a valid avatar, or null when it is. */
export function validateAvatarFile(file: File): string | null {
  if (!ALLOWED_AVATAR_TYPES.includes(file.type as (typeof ALLOWED_AVATAR_TYPES)[number])) {
    return 'Ảnh đại diện chỉ hỗ trợ JPG, PNG hoặc WEBP.';
  }
  if (file.size > MAX_AVATAR_SIZE) {
    return 'Ảnh đại diện không được vượt quá 2MB.';
  }
  return null;
}

/**
 * Self-service profile API. Backed by ProfilesController.
 * UC-14 view my profile, UC-15 update my profile + avatar. The backend resolves the user
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

  /** UC-15 — upload a new avatar (multipart/form-data, field "avatar"). */
  async uploadAvatar(file: File): Promise<UploadAvatarResponse> {
    const formData = new FormData();
    formData.append('avatar', file);

    const { data } = await httpClient.put<UploadAvatarResponse>(API_ENDPOINTS.profile.avatar, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },
};
