import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AuthResponse,
  CampusOption,
  ChangePasswordRequest,
  LoginPortal,
  MessageResponse,
  PermissionsResponse,
  ResetPasswordRequest,
  UserProfileResponse,
} from '../types/authentication.types';

export const authenticationApi = {
  // ── Portal-specific login methods ────────────────────────────────────

  async loginInternal(email: string, password: string, selectedCampusId: number): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.login, {
      email,
      password,
      loginPortal: 'INTERNAL' as LoginPortal,
      selectedCampusId,
    });
    return data;
  },

  async loginVisitor(email: string, password: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.login, {
      email,
      password,
      loginPortal: 'VISITOR' as LoginPortal,
    });
    return data;
  },

  async loginGoogleInternal(idToken: string, selectedCampusId: number): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.google, {
      idToken,
      loginPortal: 'INTERNAL' as LoginPortal,
      selectedCampusId,
    });
    return data;
  },

  async loginGoogleVisitor(idToken: string): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.google, {
      idToken,
      loginPortal: 'VISITOR' as LoginPortal,
    });
    return data;
  },

  // ── Generic (kept for AuthContext backward compat) ───────────────────

  async login(email: string, password: string, loginPortal: LoginPortal, selectedCampusId?: number | null): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.login, {
      email,
      password,
      loginPortal,
      selectedCampusId: loginPortal === 'INTERNAL' ? (selectedCampusId ?? null) : null,
    });
    return data;
  },

  async loginWithGoogle(idToken: string, loginPortal: LoginPortal, selectedCampusId?: number | null): Promise<AuthResponse> {
    const { data } = await httpClient.post<AuthResponse>(API_ENDPOINTS.auth.google, {
      idToken,
      loginPortal,
      selectedCampusId: loginPortal === 'INTERNAL' ? (selectedCampusId ?? null) : null,
    });
    return data;
  },

  // ── Session management ──────────────────────────────────────────────

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
