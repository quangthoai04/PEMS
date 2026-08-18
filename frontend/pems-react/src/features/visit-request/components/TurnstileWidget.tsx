import React, { useEffect, useRef, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';

/**
 * Cloudflare Turnstile wrapper for the UC17 OTP recovery step.
 *
 * With VITE_TURNSTILE_SITE_KEY configured, the real widget is rendered and its token is
 * passed to `onToken` (the backend re-validates it server-side — the widget is never
 * trusted by itself). Without a site key (local dev / E2E), an EXPLICIT fallback button
 * produces the dev bypass token; the backend only accepts it outside Production and only
 * when it matches Turnstile:DevBypassToken exactly.
 */

const TURNSTILE_SCRIPT_SRC =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
const TURNSTILE_ACTION = 'pems_uc17_otp_recover';

declare global {
  interface Window {
    turnstile?: {
      render: (
        el: HTMLElement,
        options: {
          sitekey: string;
          action?: string;
          callback: (token: string) => void;
          'error-callback'?: () => void;
          'expired-callback'?: () => void;
        }
      ) => string;
      remove: (widgetId: string) => void;
    };
  }
}

const env = import.meta.env as unknown as Record<string, string | undefined>;
const SITE_KEY = env.VITE_TURNSTILE_SITE_KEY;
const DEV_BYPASS_TOKEN = env.VITE_TURNSTILE_DEV_BYPASS_TOKEN || 'PEMS_DEV_HUMAN_OK';

let scriptPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (window.turnstile) return Promise.resolve();
  if (!scriptPromise) {
    scriptPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = TURNSTILE_SCRIPT_SRC;
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => {
        scriptPromise = null;
        reject(new Error('turnstile script failed to load'));
      };
      document.head.appendChild(script);
    });
  }
  return scriptPromise;
}

interface Props {
  onToken: (token: string) => void;
  disabled?: boolean;
}

export const TurnstileWidget: React.FC<Props> = ({ onToken, disabled }) => {
  const { t } = useTranslation(['visitRequest']);
  const containerRef = useRef<HTMLDivElement>(null);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    if (!SITE_KEY) return;
    let widgetId: string | null = null;
    let cancelled = false;

    loadTurnstileScript()
      .then(() => {
        if (cancelled || !containerRef.current || !window.turnstile) return;
        widgetId = window.turnstile.render(containerRef.current, {
          sitekey: SITE_KEY,
          action: TURNSTILE_ACTION,
          callback: onToken,
          'error-callback': () => setLoadError(true),
        });
      })
      .catch(() => setLoadError(true));

    return () => {
      cancelled = true;
      if (widgetId && window.turnstile) window.turnstile.remove(widgetId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!SITE_KEY) {
    return (
      <button
        type="button"
        data-testid="turnstile-fallback"
        disabled={disabled}
        onClick={() => onToken(DEV_BYPASS_TOKEN)}
        className="inline-flex w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 px-4 py-3 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <ShieldCheck className="h-4 w-4 text-[#004c91]" />
        {t('visitRequest:otp.human.devVerify')}
      </button>
    );
  }

  return (
    <div className="flex flex-col items-center gap-2">
      <div ref={containerRef} data-testid="turnstile-widget" className="min-h-[65px]" />
      {loadError && (
        <p className="text-xs font-normal text-red-600">
          {t('visitRequest:otp.human.loadError')}
        </p>
      )}
    </div>
  );
};
