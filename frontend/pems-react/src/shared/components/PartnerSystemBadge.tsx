import React from 'react';
import { useTranslation } from 'react-i18next';
import { Check } from 'lucide-react';

export type PartnerBadgeStrength = 'strong' | 'light';

interface Props {
  /**
   * 'strong' — the pill badge, reserved for the REQUEST-level partner (registrant). 'light' — a plain
   * text note, for member-level organizations (guest/support/operational contact), which may belong
   * to a different body than the request's own partner and must never look like the same claim (see
   * the identical distinction already made in OrganizationCombobox vs PartnerOrgCombobox on the write
   * side).
   */
  strength: PartnerBadgeStrength;
  /**
   * Already-translated wording override. Every caller except Operational Contact omits this and gets
   * the shared default ("Đã chọn đối tác có sẵn" / "✓ Có trong hệ thống") — Operational Contact passes
   * its own ("✓ Tổ chức đã có trong hệ thống") because its claim is narrower (an organization relation
   * via a linked delegation member, never "the contact picked a partner"). Taking a pre-translated
   * string rather than a key keeps this component free of any particular i18n namespace.
   */
  label?: string;
  className?: string;
  'data-testid'?: string;
}

/**
 * The read-only counterpart of the badges PartnerOrgCombobox / OrganizationCombobox show while
 * editing — same default wording, same two-tier visual weight, so a viewer who saw the pick while
 * filling in the form sees the identical signal while reading it back. Callers render this ONLY when
 * the corresponding partnerId / organizationPartnerId / isOrganizationInSystem is true; this component
 * never decides that itself and never looks anything up.
 *
 * The light variant is a `<span>` with `display:block` (not a `<p>`) precisely so it stays valid
 * nested inside any of its callers' containers — some are `<span>`/`<dd>`/`<td>`, and a `<p>` is not a
 * legal child of an inline `<span>`. `block` keeps it on its own line either way, which is also what
 * keeps it from ever overlapping the organization text it follows.
 */
export const PartnerSystemBadge: React.FC<Props> = ({ strength, label, className = '', ...rest }) => {
  const { t } = useTranslation(['visitRequest']);
  const defaultText = strength === 'strong'
    ? t('visitRequest:select.partnerSelected')
    : t('visitRequest:select.orgKnown');
  const text = label ?? defaultText;

  if (strength === 'strong') {
    return (
      <div
        data-testid={rest['data-testid']}
        className={`inline-flex items-center gap-1.5 rounded-md border border-green-100 bg-green-50 px-2 py-1 text-xs font-semibold text-green-700 ${className}`}
      >
        <Check className="h-3 w-3 shrink-0" /> {text}
      </div>
    );
  }

  return (
    <span
      data-testid={rest['data-testid']}
      className={`mt-0.5 block text-xs font-normal text-green-700 ${className}`}
    >
      {text}
    </span>
  );
};
