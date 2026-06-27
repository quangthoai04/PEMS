import { useCallback, useEffect, useState } from 'react';
import { profileApi } from '../api/profileApi';
import type { UpdateProfileRequest, ViewProfileResponse } from '../types/profile.types';

interface UseProfileResult {
  profile: ViewProfileResponse | null;
  loading: boolean;
  error: unknown;
  refetch: () => Promise<void>;
  /** Updates allowed fields; on success refreshes local state and returns the new profile. */
  update: (payload: UpdateProfileRequest) => Promise<ViewProfileResponse>;
  /** Patches avatarUrl locally after an upload (avoids a full refetch flash). */
  applyAvatar: (avatarUrl: string) => void;
}

/** UC-14 + UC-15 — loads the current user's profile and exposes an update action. */
export function useProfile(): UseProfileResult {
  const [profile, setProfile] = useState<ViewProfileResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const refetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await profileApi.getMyProfile();
      setProfile(data);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refetch();
  }, [refetch]);

  const update = useCallback(async (payload: UpdateProfileRequest) => {
    const updated = await profileApi.updateMyProfile(payload);
    setProfile(updated);
    return updated;
  }, []);

  const applyAvatar = useCallback((avatarUrl: string) => {
    setProfile((prev) => (prev ? { ...prev, avatarUrl } : prev));
  }, []);

  return { profile, loading, error, refetch, update, applyAvatar };
}
