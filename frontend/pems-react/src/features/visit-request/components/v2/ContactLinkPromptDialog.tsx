import React from 'react';
import { useTranslation } from 'react-i18next';
import type { ContactLinkPrompt } from '../../hooks/useContactLinkPrompt';

/**
 * "Đầu mối này có phải chính thành viên đó không?" — asked before a form is submitted, whenever the
 * contact typed into a campus card matches exactly one member of its delegation on name, job title
 * AND organisation.
 *
 * <p>Unanswered, the request holds two records of one human: a member row with a guest_member_id and
 * a contact snapshot with no link at all. Downstream that is not a near-miss to be tidied up later —
 * it is two people, and the biên bản lists both.</p>
 *
 * <p>Lives in its own component because BOTH the create form and the edit screen have to ask it. The
 * edit screen did not, so the same typing produced a link on one screen and none on the other, and a
 * user had no way to tell which screen they were being judged by.</p>
 */
export const ContactLinkPromptDialog: React.FC<{
  prompt: ContactLinkPrompt;
  onSame: () => void;
  onDifferent: () => void;
  onReview: () => void;
}> = ({ prompt, onSame, onDifferent, onReview }) => {
  const { t } = useTranslation(['visitRequestV2']);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="v2-contact-link-title"
      data-testid="v2-contact-link-prompt"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
    >
      <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
        <h3 id="v2-contact-link-title" className="text-base font-extrabold text-slate-900">
          {t('visitRequestV2:contactLink.title')}
        </h3>
        <p className="mt-2 text-sm text-slate-600">
          {t('visitRequestV2:contactLink.body', {
            name: prompt.memberName,
            kind: t(prompt.memberKind === 'visitors'
              ? 'visitRequestV2:card.contactPickKindGuest'
              : 'visitRequestV2:card.contactPickKindSupport'),
          })}
        </p>
        <p className="mt-1 text-xs text-slate-500">{t('visitRequestV2:contactLink.consequence')}</p>
        <div className="mt-4 flex flex-wrap justify-end gap-2">
          <button
            type="button"
            data-testid="v2-contact-link-review"
            className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
            onClick={onReview}
          >
            {t('visitRequestV2:contactLink.review')}
          </button>
          <button
            type="button"
            data-testid="v2-contact-link-different"
            className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
            onClick={onDifferent}
          >
            {t('visitRequestV2:contactLink.different')}
          </button>
          <button
            type="button"
            data-testid="v2-contact-link-same"
            className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white"
            onClick={onSame}
          >
            {t('visitRequestV2:contactLink.same')}
          </button>
        </div>
      </div>
    </div>
  );
};
