import { useEffect, useState } from 'react';
import { profileApi } from '../../features/profile/api/profileApi';
import { useAuthenticatedImage } from './useAuthenticatedImage';

/** Fired (on window) after a successful avatar upload so every avatar on screen refreshes. */
export const AVATAR_UPDATED_EVENT = 'pems:avatar-updated';

const USER_KEYS = ['currentUser', 'pems_user'];

function readStoredAvatarUrl(): string | null {
  for (const key of USER_KEYS) {
    try {
      const raw = localStorage.getItem(key);
      if (raw) {
        const obj = JSON.parse(raw);
        if (obj?.avatarUrl) return obj.avatarUrl as string;
      }
    } catch {
      /* ignore malformed local state */
    }
  }
  return null;
}

function cacheAvatarUrl(avatarUrl: string) {
  for (const key of USER_KEYS) {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) continue;
      const obj = JSON.parse(raw);
      obj.avatarUrl = avatarUrl;
      localStorage.setItem(key, JSON.stringify(obj));
    } catch {
      /* ignore malformed local state */
    }
  }
}

/**
 * Resolves the logged-in user's avatar as an authenticated blob object URL — shared by the
 * dashboard Sidebar and the site Header so both stay in sync with the Profile page.
 *
 * It reads the avatar path cached in local user state; if none is cached yet (e.g. right after
 * login), it fetches the profile once to learn it and caches it. It re-reads on the
 * `pems:avatar-updated` event so a fresh upload reflects everywhere immediately. Returns null
 * when there is no logged-in session or no avatar set, so the caller falls back to the default image.
 */
export function useCurrentUserAvatar(): string | null {
  const [avatarUrl, setAvatarUrl] = useState<string | null>(readStoredAvatarUrl);

  useEffect(() => {
    let cancelled = false;
    const hasSession = !!localStorage.getItem('token');

    // Learn the avatar path once if it isn't cached but the user is logged in.
    if (!avatarUrl && hasSession) {
      profileApi
        .getMyProfile()
        .then((p) => {
          if (cancelled || !p?.avatarUrl) return;
          cacheAvatarUrl(p.avatarUrl);
          setAvatarUrl(p.avatarUrl);
        })
        .catch(() => {
          /* not logged in / no avatar — keep the default image */
        });
    }

    const onUpdated = () => setAvatarUrl(readStoredAvatarUrl());
    window.addEventListener(AVATAR_UPDATED_EVENT, onUpdated);
    return () => {
      cancelled = true;
      window.removeEventListener(AVATAR_UPDATED_EVENT, onUpdated);
    };
  }, [avatarUrl]);

  const { src } = useAuthenticatedImage(avatarUrl);
  return src;
}
