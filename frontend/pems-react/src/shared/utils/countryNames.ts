/**
 * Quy ước tên quốc gia của PEMS — nguồn duy nhất cho mọi chỗ cần đổi tên <-> mã ISO.
 *
 * `partners.country` lưu tên tiếng Việt NGẮN thông dụng ("Hoa Kỳ", "Nhật Bản", "Úc"...),
 * khớp với bảng toạ độ quả cầu (countryCoordinates.ts) và cờ (countryFlag.ts).
 * vi.json của i18n-iso-countries lại dùng tên trang trọng dài cho một số nước
 * ("Hợp chủng quốc Hoa Kỳ", "Cộng hòa Dân chủ nhân dân Lào") nên cần lớp override.
 * Visit request lưu tên tiếng Anh — hàm tra mã nhận cả hai để không tách một quốc gia
 * thành nhiều nhóm khi dữ liệu trộn vi/en.
 */
import countries from 'i18n-iso-countries';
import enLocale from 'i18n-iso-countries/langs/en.json';
import viLocale from 'i18n-iso-countries/langs/vi.json';

countries.registerLocale(enLocale);
countries.registerLocale(viLocale);

/** Tên vi ngắn cho các nước mà vi.json chỉ có tên dài/không khớp quy ước dữ liệu. */
export const VI_COUNTRY_NAME_OVERRIDES: Record<string, string> = {
  US: 'Hoa Kỳ',
  LA: 'Lào',
  MY: 'Malaysia',
  KP: 'Triều Tiên',
  SA: 'Ả Rập Xê Út',
};

/** Viết tắt/biến thể hay gặp trong dữ liệu thật → alpha-2. */
const EXTRA_NAME_ALIASES: Record<string, string> = {
  'mỹ': 'US',
  'hoa kỳ': 'US',
  'usa': 'US',
  'anh': 'GB',
  'uk': 'GB',
  'vương quốc anh': 'GB',
  'nga': 'RU',
  'hàn quốc': 'KR',
  'triều tiên': 'KP',
  'lào': 'LA',
  'uae': 'AE',
  'quần đảo cayman': 'KY',
};

let viNamesCache: Record<string, string> | null = null;
/** Mã alpha-2 -> tên tiếng Việt ngắn (ưu tiên alias của lib, rồi override của PEMS). */
export function getViCountryNames(): Record<string, string> {
  if (!viNamesCache) {
    viNamesCache = {
      ...(countries.getNames('vi', { select: 'alias' }) as Record<string, string>),
      ...VI_COUNTRY_NAME_OVERRIDES,
    };
  }
  return viNamesCache;
}

let enNamesCache: Record<string, string> | null = null;
/** Mã alpha-2 -> tên tiếng Anh official (quy ước lưu của visit request). */
export function getEnCountryNames(): Record<string, string> {
  if (!enNamesCache) {
    enNamesCache = countries.getNames('en', { select: 'official' }) as Record<string, string>;
  }
  return enNamesCache;
}

let nameToCodeCache: Map<string, string> | null = null;
function nameToCodeMap(): Map<string, string> {
  if (!nameToCodeCache) {
    const map = new Map<string, string>();
    for (const lang of ['en', 'vi'] as const) {
      const all = countries.getNames(lang, { select: 'all' }) as Record<string, string | string[]>;
      for (const [code, names] of Object.entries(all)) {
        for (const name of Array.isArray(names) ? names : [names]) {
          map.set(name.trim().toLowerCase(), code);
        }
      }
    }
    for (const [code, name] of Object.entries(VI_COUNTRY_NAME_OVERRIDES)) {
      map.set(name.toLowerCase(), code);
    }
    for (const [alias, code] of Object.entries(EXTRA_NAME_ALIASES)) {
      map.set(alias, code);
    }
    nameToCodeCache = map;
  }
  return nameToCodeCache;
}

/** Tên quốc gia (vi/en/alias, không phân biệt hoa thường) -> alpha-2, null nếu không nhận ra. */
export function countryNameToAlpha2(name: string): string | null {
  const trimmed = name.trim();
  if (!trimmed) return null;
  return (
    nameToCodeMap().get(trimmed.toLowerCase()) ??
    countries.getAlpha2Code(trimmed, 'vi') ??
    countries.getAlpha2Code(trimmed, 'en') ??
    null
  );
}
