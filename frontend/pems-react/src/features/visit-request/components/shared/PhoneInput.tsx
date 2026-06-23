import React, { useState, useRef, useEffect, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { ChevronDown, Search } from 'lucide-react';
import { getCountries, getCountryCallingCode, parsePhoneNumber } from 'libphonenumber-js';
import countries from 'i18n-iso-countries';
import enLocale from 'i18n-iso-countries/langs/en.json';

countries.registerLocale(enLocale);

const flagEmoji = (code: string) =>
  code.toUpperCase().split('').map(c => String.fromCodePoint(c.charCodeAt(0) + 127397)).join('');

interface DialOption {
  code: string;
  dialCode: string;
  name: string;
  flag: string;
}

const DIAL_OPTIONS: DialOption[] = (() => {
  const names = countries.getNames('en', { select: 'official' });
  return getCountries()
    .map(c => ({
      code: c,
      dialCode: String(getCountryCallingCode(c)),
      name: names[c] || c,
      flag: flagEmoji(c),
    }))
    .sort((a, b) => a.name.localeCompare(b.name));
})();

const DEFAULT_COUNTRY = 'VN';

interface PhoneInputProps {
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  hasError?: boolean;
}

export const PhoneInput: React.FC<PhoneInputProps> = ({ value, onChange, onBlur, hasError }) => {
  const [selectedCode, setSelectedCode] = useState(DEFAULT_COUNTRY);
  const [localNumber, setLocalNumber] = useState('');
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [dropdownStyle, setDropdownStyle] = useState<React.CSSProperties>({});

  const buttonRef = useRef<HTMLButtonElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const prevValueRef = useRef('');

  // Sync from external value changes (e.g. checkbox "Tôi là đầu mối liên hệ")
  useEffect(() => {
    if (value === prevValueRef.current) return;
    prevValueRef.current = value;

    if (!value) {
      setLocalNumber('');
      return;
    }
    try {
      const parsed = parsePhoneNumber(value);
      if (parsed?.country && parsed.nationalNumber) {
        setSelectedCode(parsed.country);
        setLocalNumber(parsed.nationalNumber);
      }
    } catch {}
  }, [value]);

  const selectedOpt = useMemo(
    () => DIAL_OPTIONS.find(o => o.code === selectedCode) ?? DIAL_OPTIONS.find(o => o.code === DEFAULT_COUNTRY)!,
    [selectedCode]
  );

  const filtered = useMemo(() => {
    if (!search) return DIAL_OPTIONS;
    const q = search.toLowerCase();
    return DIAL_OPTIONS.filter(o => o.name.toLowerCase().includes(q) || o.dialCode.includes(q));
  }, [search]);

  const openDropdown = () => {
    if (buttonRef.current) {
      const r = buttonRef.current.getBoundingClientRect();
      setDropdownStyle({
        position: 'fixed',
        top: r.bottom + 4,
        left: r.left,
        width: 280,
        zIndex: 99999,
      });
    }
    setOpen(true);
  };

  // Focus search input when dropdown opens
  useEffect(() => {
    if (open) setTimeout(() => searchRef.current?.focus(), 50);
  }, [open]);

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as Node;
      const dropdownEl = document.getElementById('phone-dial-dropdown');
      if (buttonRef.current?.contains(target) || dropdownEl?.contains(target)) return;
      setOpen(false);
      setSearch('');
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  const selectCountry = (code: string) => {
    const opt = DIAL_OPTIONS.find(o => o.code === code)!;
    setSelectedCode(code);
    setOpen(false);
    setSearch('');
    onChange(localNumber ? `+${opt.dialCode}${localNumber.replace(/\s/g, '')}` : '');
  };

  const handleNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const num = e.target.value.replace(/[^\d\s\-()]/g, '');
    setLocalNumber(num);
    const clean = num.replace(/[\s\-()]/g, '');
    onChange(clean ? `+${selectedOpt.dialCode}${clean}` : '');
  };

  return (
    <>
      <div
        className={[
          'flex h-12 rounded-xl border bg-white overflow-hidden transition-colors shadow-sm',
          hasError
            ? 'border-red-400 focus-within:border-red-500 focus-within:ring-2 focus-within:ring-red-500/10'
            : 'border-slate-300 focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-[#004c91]/10',
        ].join(' ')}
      >
        {/* Country code button */}
        <button
          ref={buttonRef}
          type="button"
          onClick={openDropdown}
          className="flex h-full items-center gap-1.5 pl-4 pr-2 bg-slate-50 border-r border-slate-200 hover:bg-slate-100 transition-colors shrink-0"
        >
          <span className="text-base leading-none">{selectedOpt.flag}</span>
          <span className="text-sm font-semibold text-gray-700">+{selectedOpt.dialCode}</span>
          <ChevronDown
            className={`w-3.5 h-3.5 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`}
          />
        </button>

        {/* Local number input */}
        <input
          type="tel"
          value={localNumber}
          onChange={handleNumberChange}
          onBlur={onBlur}
          placeholder="912 345 678"
          className="flex-1 px-4 h-full bg-white outline-none text-sm font-semibold text-slate-800 min-w-0"
        />
      </div>

      {/* Dropdown — rendered via portal to avoid overflow clipping by modal */}
      {open &&
        createPortal(
          <div id="phone-dial-dropdown" style={dropdownStyle} className="bg-white border border-gray-200 rounded-xl shadow-2xl overflow-hidden">
            {/* Search */}
            <div className="p-2 border-b border-gray-100">
              <div className="flex items-center gap-2 px-2.5 py-1.5 bg-gray-50 rounded-lg">
                <Search className="w-3.5 h-3.5 text-gray-400 shrink-0" />
                <input
                  ref={searchRef}
                  type="text"
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  placeholder="Tìm quốc gia hoặc mã..."
                  className="flex-1 bg-transparent outline-none text-xs text-gray-700 placeholder:text-gray-400"
                />
              </div>
            </div>

            {/* List */}
            <div className="max-h-52 overflow-y-auto">
              {filtered.length === 0 ? (
                <p className="text-center text-xs text-gray-400 py-4">Không tìm thấy</p>
              ) : (
                filtered.map(opt => (
                  <button
                    key={opt.code}
                    type="button"
                    onClick={() => selectCountry(opt.code)}
                    className={[
                      'w-full flex items-center gap-3 px-4 py-2 text-left hover:bg-orange-50 transition-colors',
                      opt.code === selectedCode
                        ? 'bg-blue-50 text-[#004c91] font-semibold'
                        : 'text-gray-700',
                    ].join(' ')}
                  >
                    <span className="text-base leading-none w-5 shrink-0">{opt.flag}</span>
                    <span className="flex-1 truncate text-xs">{opt.name}</span>
                    <span className="text-xs font-bold text-gray-500 shrink-0">+{opt.dialCode}</span>
                  </button>
                ))
              )}
            </div>
          </div>,
          document.body
        )}
    </>
  );
};
