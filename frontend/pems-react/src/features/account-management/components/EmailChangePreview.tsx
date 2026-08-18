import { ArrowDown } from 'lucide-react';

/**
 * The "from → to" block both email-change confirmations show.
 *
 * Shared so the two dialogs cannot drift apart visually, and because this is the part the operator
 * actually checks before committing: the old address is muted and the new one carries the brand
 * colour, so a mistyped correction stands out instead of hiding inside a sentence.
 */
export function EmailChangePreview({ oldEmail, newEmail }: { oldEmail: string; newEmail: string }) {
  return (
    <div>
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-2.5">
        <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Email hiện tại</p>
        <p className="mt-0.5 break-all text-[15px] font-normal text-gray-500">{oldEmail || '—'}</p>
      </div>

      <div className="flex justify-center py-1.5" aria-hidden="true">
        <ArrowDown className="h-4 w-4 text-gray-300" />
      </div>

      <div className="rounded-xl border border-[#004c91]/25 bg-[#004c91]/[0.04] px-4 py-2.5">
        <p className="text-[11px] font-bold uppercase tracking-wider text-[#004c91]/70">Email mới</p>
        <p className="mt-0.5 break-all text-[15px] font-normal text-[#004c91]">{newEmail}</p>
      </div>
    </div>
  );
}
