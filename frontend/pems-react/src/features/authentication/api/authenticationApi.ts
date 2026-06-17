import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AuthResponse,
  ChangePasswordRequest,
  LoginPortal,
  MessageResponse,
  PermissionsResponse,
  ResetPasswordRequest,
  UserProfileResponse,
} from '../types/authentication.types';

export const authenticationApi = {
  async login(email: string, password: string, loginPortal: LoginPortal): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.login, {
      email,
      password,
      loginPortal,
    });
    return data;
  },

  async loginWithGoogle(idToken: string, loginPortal: LoginPortal): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.google, {
      idToken,
      loginPortal,
    });
    return data;
  },

  async logout(refreshToken?: string | null): Promise<MessageResponse> {
    const { data } = await httpClient.post<MessageResponse>(API_ENDPOINTS.auth.logout, {
      refreshToken: refreshToken ?? null,
    });
    return data;
  },

  async getMe(): Promise<UserProfileResponse> {
    const { data } = await httpClient.get<UserProfileResponse>(API_ENDPOINTS.auth.me);
    return data;
  },

  async getPermissions(): Promise<PermissionsResponse> {
    const { data } = await httpClient.get<PermissionsResponse>(API_ENDPOINTS.auth.permissions);
    return data;
  },

  async forgotPassword(email: string): Promise<MessageResponse> {
    const { data } = await httpClient.post<MessageResponse>(API_ENDPOINTS.auth.forgotPassword, { email });
    return data;
  },

  async resetPassword(payload: ResetPasswordRequest): Promise<MessageResponse> {
    const { data } = await httpClient.post<MessageResponse>(API_ENDPOINTS.auth.resetPassword, payload);
    return data;
  },

  async changePassword(payload: ChangePasswordRequest): Promise<MessageResponse> {
    const { data } = await httpClient.post<MessageResponse>(API_ENDPOINTS.auth.changePassword, payload);
    return data;
  },
};
