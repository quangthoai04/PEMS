/**
 * CitySelect — ô nhập thành phố có gợi ý theo quốc gia đã chọn (dùng ở form đối tác).
 * Dữ liệu thành phố lấy từ `country-state-city`, dynamic import để tách chunk —
 * chỉ tải khi người dùng đã chọn quốc gia. Vẫn cho nhập tự do khi không có gợi ý.
 */
import React, { useEffect, useMemo, useState } from 'react';
import CreatableSelect from 'react-select/creatable';
import type { SingleValue } from 'react-select';
import {
  buildSelectStyles,
  type CountryOption,
} from '../../visit-request/components/shared/CountrySelect';
import { countryNameToAlpha2 } from '../../../shared/utils/countryNames';

type CityOption = CountryOption;

const cityCache = new Map<string, CityOption[]>();

// Giới hạn số option render để tránh lag với quốc gia có hàng nghìn thành phố.
const MAX_VISIBLE_OPTIONS = 100;

// Bỏ dấu tiếng Việt (và diacritics nói chung) để gõ "can tho" vẫn khớp "Cần Thơ" —
// dữ liệu country-state-city lưu tên thành phố VN có dấu.
const stripDiacritics = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();

interface CitySelectProps {
  /** Tên quốc gia (tiếng Anh) đang chọn — nguồn để lọc gợi ý thành phố. */
  country: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  hasError?: boolean;
  disabled?: boolean;
}

const VIETNAM_PROVINCES: CityOption[] = [
  'An Giang', 'Bắc Ninh', 'Cao Bằng', 'Cà Mau', 'TP. Cần Thơ', 'TP. Đà Nẵng',
  'Đắk Lắk', 'TP. Đồng Nai', 'Đồng Tháp', 'Điện Biên', 'Gia Lai', 'TP. Hà Nội',
  'Hà Tĩnh', 'TP. Hải Phòng', 'TP. Hồ Chí Minh', 'TP. Huế', 'Hưng Yên', 'Khánh Hòa',
  'Lai Châu', 'Lâm Đồng', 'Lạng Sơn', 'Lào Cai', 'Nghệ An', 'Ninh Bình',
  'Phú Thọ', 'Quảng Bình', 'Quảng Ngãi', 'Quảng Ninh', 'Quảng Trị', 'Sơn La',
  'Tây Ninh', 'Thái Nguyên', 'Thanh Hóa', 'Tuyên Quang', 'Vĩnh Long'
].sort((a, b) => a.localeCompare(b, 'vi')).map(name => ({ value: name, label: name }));

export const CitySelect: React.FC<CitySelectProps> = ({
  country,
  value,
  onChange,
  placeholder,
  hasError,
  disabled,
}) => {
  const [options, setOptions] = useState<CityOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [inputValue, setInputValue] = useState('');

  useEffect(() => {
    const trimmed = country.trim();
    if (!trimmed) {
      setOptions([]);
      return;
    }
    const iso2 = countryNameToAlpha2(trimmed);
    if (!iso2) {
      setOptions([]);
      return;
    }
    if (iso2 === 'VN') {
      setOptions(VIETNAM_PROVINCES);
      setLoading(false);
      return;
    }
    const cached = cityCache.get(iso2);
    if (cached) {
      setOptions(cached);
      return;
    }
    let cancelled = false;
    setLoading(true);
    (async () => {
      try {
        const { City } = await import('country-state-city');
        const cities = City.getCitiesOfCountry(iso2) ?? [];
        const names = Array.from(new Set(cities.map((c) => c.name))).sort((a, b) =>
          a.localeCompare(b)
        );
        const opts = names.map((name) => ({ value: name, label: name }));
        cityCache.set(iso2, opts);
        if (!cancelled) setOptions(opts);
      } catch {
        if (!cancelled) setOptions([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [country]);

  const selectedOption = useMemo(() => {
    const normalized = value?.trim();
    if (!normalized) return null;
    return (
      options.find((o) => o.value.trim().toLowerCase() === normalized.toLowerCase()) ?? {
        value: normalized,
        label: normalized,
      }
    );
  }, [value, options]);

  const visibleOptions = useMemo(() => {
    const query = stripDiacritics(inputValue.trim());
    const filtered = query
      ? options.filter((o) => stripDiacritics(o.label).includes(query))
      : options;
    return filtered.slice(0, MAX_VISIBLE_OPTIONS);
  }, [options, inputValue]);

  const styles = useMemo(() => buildSelectStyles(hasError), [hasError]);

  return (
    <CreatableSelect<CityOption>
      options={visibleOptions}
      value={selectedOption}
      onChange={(opt: SingleValue<CityOption>) => onChange(opt?.value ?? '')}
      onCreateOption={(input) => onChange(input)}
      inputValue={inputValue}
      onInputChange={(input) => setInputValue(input)}
      placeholder={placeholder ?? 'Chọn hoặc nhập thành phố...'}
      styles={styles}
      isClearable
      isDisabled={disabled}
      isLoading={loading}
      isSearchable
      menuPortalTarget={document.body}
      menuPosition="fixed"
      formatCreateLabel={(input) => `Sử dụng "${input}"`}
      loadingMessage={() => 'Đang tải danh sách thành phố...'}
      noOptionsMessage={() =>
        country.trim()
          ? 'Không có gợi ý — bạn có thể nhập tự do'
          : 'Chọn quốc gia để xem gợi ý thành phố'
      }
      // Đã tự lọc + cắt bớt ở visibleOptions, bỏ qua bộ lọc mặc định của react-select.
      filterOption={() => true}
    />
  );
};
