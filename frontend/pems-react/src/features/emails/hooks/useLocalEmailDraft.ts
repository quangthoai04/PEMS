export type EmailRecipientInput = {
  email: string;
  name?: string | null;
};

export type LocalEmailDraft = {
  savedAt: string;
  expiresAt: string;
  templateId?: number | null;
  relatedType?: string | null;
  relatedId?: number | null;
  to: string; // we will store raw strings for simplicity in the form
  cc?: string;
  bcc?: string;
  subject: string;
  body: string;
};

const DRAFT_TTL_MS = 30 * 60 * 1000; // 30 mins

export function useLocalEmailDraft(userId: number | string) {
  const key = `pems_email_draft_${userId}`;

  const saveDraft = (draft: Omit<LocalEmailDraft, 'savedAt' | 'expiresAt'>) => {
    const now = new Date();
    const payload = {
      ...draft,
      savedAt: now.toISOString(),
      expiresAt: new Date(now.getTime() + DRAFT_TTL_MS).toISOString(),
    };
    localStorage.setItem(key, JSON.stringify(payload));
  };

  const getValidDraft = (): LocalEmailDraft | null => {
    const raw = localStorage.getItem(key);
    if (!raw) return null;

    try {
      const draft = JSON.parse(raw) as LocalEmailDraft;
      if (new Date(draft.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(key);
        return null;
      }
      return draft;
    } catch {
      localStorage.removeItem(key);
      return null;
    }
  };

  const clearDraft = () => {
    localStorage.removeItem(key);
  };

  return { saveDraft, getValidDraft, clearDraft };
}
