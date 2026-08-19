import httpClient, { type PemsRequestConfig } from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AuthResponse,
  CampusOption,
  ChangePasswordRequest,
  MessageResponse,

  ResetPasswordRequest,
  UserProfileResponse,
} from '../types/authentication.types';

export const authenticationApi = {
  // ── Login (portal/campus are resolved server-side from the account) ──

  async login(email: string, password: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.login, { email, password });
    return data;
  },

  async loginWithGoogle(idToken: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.google, { idToken });
    return data;
  },

  // ── Session management ──────────────────────────────────────────────

  async logout(refreshToken?: string | null): Promise<MessageResponse> {
    // Timeout ngắn: nếu backend đang chậm, người dùng vẫn được đăng xuất cục bộ
    // nhanh thay vì chờ vô thời hạn (AuthContext.logout() vẫn clear session dù lỗi).
    const { data } = await httpClient.post<MessageResponse>(
      API_ENDPOINTS.auth.logout,
      { refreshToken: refreshToken ?? null },
      { timeout: 5000 },
    );
    return data;
  },

  async getMe(config?: PemsRequestConfig): Promise<UserProfileResponse> {
    const { data } = await httpClient.get<UserProfileResponse>(API_ENDPOINTS.auth.me, config);
    return data;
  },


  // ── Password management ─────────────────────────────────────────────

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

  // ── Campus ──────────────────────────────────────────────────────────

  async getActiveCampuses(): Promise<CampusOption[]> {
    const { data } = await httpClient.get<CampusOption[]>(API_ENDPOINTS.campuses.active);
    return data;
  },
};
