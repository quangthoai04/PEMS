import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Loader2, Search, X } from 'lucide-react';
import { visitRequestApi, type PublicPartnerOptionDto } from '../../api/visitRequestApi';

/**
 * Free-solo tổ chức/đối tác combobox — MỘT ô input duy nhất:
 *  - Gõ tự do → text hợp lệ, partnerId = null (tổ chức nhập tay).
 *  - Gõ ≥ 2 ký tự → debounce 300ms rồi search partner ACTIVE/APPROVED; dropdown chỉ hiện theo keyword.
 *  - Chọn 1 partner → link partnerId + hiển thị badge "Đã chọn đối tác có sẵn".
 *  - Sửa lại text sau khi đã chọn → tự clear partnerId, quay về tổ chức nhập tay.
 *  - Input rỗng / < 2 ký tự → KHÔNG gọi API, KHÔNG hiện danh sách.
 * Không tự tạo hồ sơ partner mới — chỉ là tên tổ chức tự do khi partnerId = null.
 */

const MIN_SEARCH_LENGTH = 2;
const SEARCH_DEBOUNCE_MS = 300;

export interface PartnerOrgChange {
  organization: string;
  partnerId: number | null;
  mode: 'EXISTING_PARTNER' | 'NEW_ORGANIZATION';
}

interface PartnerOrgComboboxProps {
  /** Text tổ chức hiện tại (nguồn sự thật hiển thị trong input). */
  organization: string;
  /** partnerId đã link (null nếu tổ chức tự nhập). */
  partnerId: number | null;
  onChange: (next: PartnerOrgChange) => void;
  onBlur?: () => void;
  hasError?: boolean;
  placeholder?: string;
}

export const PartnerOrgCombobox: React.FC<PartnerOrgComboboxProps> = ({
  organization,
  partnerId,
  onChange,
  onBlur,
  hasError,
  placeholder,
}) => {
  const { t } = useTranslation(['visitRequest']);
  const [text, setText] = useState(organization ?? '');
  const [options, setOptions] = useState<PublicPartnerOptionDto[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [highlight, setHighlight] = useState(-1);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const reqSeqRef = useRef(0);

  // Đồng bộ input khi giá trị đến từ bên ngoài (load edit-detail, form.reset…).
  useEffect(() => {
    setText(organization ?? '');
  }, [organization]);

  // Đóng dropdown khi click ra ngoài.
  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  const runSearch = useCallback((keyword: string) => {
    if (debounceRef.current) clearTimeout(debounceRef.current);

    const trimmed = keyword.trim();
    if (trimmed.length < MIN_SEARCH_LENGTH) {
      setOptions([]);
      setIsLoading(false);
      setHasSearched(false);
      setIsOpen(false);
      return;
    }

    setIsLoading(true);
    setIsOpen(true);
    const seq = ++reqSeqRef.current;
    debounceRef.current = setTimeout(async () => {
      try {
        const results = await visitRequestApi.searchOrganizations(trimmed);
        if (seq !== reqSeqRef.current) return; // bỏ kết quả cũ (race)
        setOptions(results);
      } catch {
        if (seq !== reqSeqRef.current) return;
        setOptions([]);
      } finally {
        if (seq === reqSeqRef.current) {
          setIsLoading(false);
          setHasSearched(true);
        }
      }
    }, SEARCH_DEBOUNCE_MS);
  }, []);

  const handleType = (value: string) => {
    setText(value);
    setHighlight(-1);
    // Gõ = tổ chức nhập tay: luôn clear link partner.
    onChange({ organization: value, partnerId: null, mode: 'NEW_ORGANIZATION' });
    runSearch(value);
  };

  const handleSelect = (opt: PublicPartnerOptionDto) => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    reqSeqRef.current++; // huỷ mọi search đang chờ
    setText(opt.displayName);
    setOptions([]);
    setHasSearched(false);
    setIsOpen(false);
    setHighlight(-1);
    onChange({ organization: opt.displayName, partnerId: opt.partnerId, mode: 'EXISTING_PARTNER' });
  };

  const handleClear = () => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    reqSeqRef.current++;
    setText('');
    setOptions([]);
    setHasSearched(false);
    setIsOpen(false);
    setHighlight(-1);
    onChange({ organization: '', partnerId: null, mode: 'NEW_ORGANIZATION' });
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!isOpen || options.length === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlight((h) => Math.min(h + 1, options.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlight((h) => Math.max(h - 1, 0));
    } else if (e.key === 'Enter') {
      if (highlight >= 0 && highlight < options.length) {
        e.preventDefault();
        handleSelect(options[highlight]);
      }
    } else if (e.key === 'Escape') {
      setIsOpen(false);
    }
  };

  const showDropdown = isOpen && text.trim().length >= MIN_SEARCH_LENGTH;
  const showNoResult = showDropdown && !isLoading && hasSearched && options.length === 0;

  const borderCls = hasError
    ? 'border-red-400 focus-within:border-red-400 focus-within:ring-red-100'
    : partnerId != null
      ? 'border-green-400 focus-within:border-[#004c91] focus-within:ring-[#004c91]/20'
      : 'border-gray-300 focus-within:border-[#004c91] focus-within:ring-[#004c91]/20';

  return (
    <div ref={containerRef} className="relative">
      <div
        className={`flex items-center gap-2 rounded-xl border bg-white px-3 transition-all focus-within:ring-2 ${borderCls}`}
      >
        <Search className="h-4 w-4 shrink-0 text-gray-400" />
        <input
          type="text"
          value={text}
          onChange={(e) => handleType(e.target.value)}
          onFocus={() => {
            if (text.trim().length >= MIN_SEARCH_LENGTH && options.length > 0) setIsOpen(true);
          }}
          onBlur={onBlur}
          onKeyDown={handleKeyDown}
          placeholder={placeholder ?? t('visitRequest:select.partnerComboPlaceholder')}
          className="w-full bg-transparent py-2.5 text-sm font-medium text-gray-900 outline-none placeholder:font-normal placeholder:text-gray-400"
          autoComplete="off"
        />
        {isLoading && <Loader2 className="h-4 w-4 shrink-0 animate-spin text-gray-400" />}
        {!isLoading && text && (
          <button
            type="button"
            onClick={handleClear}
            className="shrink-0 rounded-full p-1 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
            title={t('visitRequest:select.clear')}
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Badge trạng thái link partner */}
      {partnerId != null ? (
        <div className="mt-1.5 inline-flex items-center gap-1.5 rounded-md bg-green-50 px-2 py-1 text-xs font-semibold text-green-700 border border-green-100">
          <Check className="h-3 w-3" /> {t('visitRequest:select.partnerSelected')}
        </div>
      ) : (
        text.trim().length > 0 && (
          <p className="mt-1.5 text-xs text-gray-500">{t('visitRequest:select.partnerFreeText')}</p>
        )
      )}

      {showDropdown && (
        <div className="absolute z-[9999] mt-1 w-full overflow-hidden rounded-xl border border-gray-200 bg-white shadow-lg">
          {isLoading ? (
            <div className="flex items-center gap-2 px-4 py-3 text-sm text-gray-400">
              <Loader2 className="h-4 w-4 animate-spin" /> {t('visitRequest:select.searching')}
            </div>
          ) : showNoResult ? (
            <div className="px-4 py-3 text-sm text-gray-500">
              {t('visitRequest:select.partnerNoMatch')}
            </div>
          ) : (
            <ul className="max-h-60 overflow-y-auto py-1">
              {options.map((opt, idx) => (
                <li key={opt.partnerId}>
                  <button
                    type="button"
                    onMouseDown={(e) => {
                      e.preventDefault();
                      handleSelect(opt);
                    }}
                    onMouseEnter={() => setHighlight(idx)}
                    className={`flex w-full flex-col items-start px-4 py-2 text-left text-sm transition-colors ${
                      idx === highlight ? 'bg-[#f0f7ff] text-[#004c91]' : 'text-gray-800 hover:bg-gray-50'
                    }`}
                  >
                    <span className="font-medium">{opt.displayName}</span>
                    {(opt.country || opt.city) && (
                      <span className="text-xs text-gray-400">
                        {[opt.city, opt.country].filter(Boolean).join(', ')}
                      </span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
};
