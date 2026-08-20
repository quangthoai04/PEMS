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

/** Viết tắt/biến thể hay gặp trong dữ liệu thật → alpha-2. Exported for the backend country-data
 * generator/parity check — see `buildCountryDataSnapshot` below. */
export const EXTRA_NAME_ALIASES: Record<string, string> = {
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
        // The alpha-2 code itself is always a valid input — the Patch 4 decision's own worked
        // example resolves "KR" directly ("Hàn Quốc" / "South Korea" / "KR" → resolve KR → persist
        // "Hàn Quốc"). `i18n-iso-countries`'s getAlpha2Code() does NOT accept a bare code as input
        // (it only looks up by name), so without this, typing "KR" into an Excel cell would resolve
        // on the backend but silently fail to canonicalize on the frontend — found while building the
        // H4-2 FE/BE parity guard.
        map.set(code.toLowerCase(), code);
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

/**
 * Mã alpha-2 -> mọi alias đã biết cho nó (đảo ngược `nameToCodeMap()`), TỪ CHÍNH cái map runtime
 * `countryNameToAlpha2` dùng để resolve — không phải một bản sao logic riêng. Đây là nguồn duy nhất
 * cho cả generator lẫn parity-check của backend `countries.json` (Patch 4 hardening H4-2): backend
 * không thể lệch khỏi runtime resolve của FE vì cả hai xuất phát từ cùng một hàm.
 */
export function getAliasesByAlpha2(): Record<string, string[]> {
  const byCode: Record<string, string[]> = {};
  for (const [alias, code] of nameToCodeMap().entries()) {
    (byCode[code] ??= []).push(alias);
  }
  return byCode;
}

export interface CountryDataSnapshotEntry {
  alpha2: string;
  vi: string;
  en: string;
  aliases: string[];
}

export interface CountryDataSnapshot {
  countries: CountryDataSnapshotEntry[];
}

/**
 * Deterministic, sorted snapshot of every country this module can resolve — the model both
 * `scripts/generate-country-data.ts` (writes `backend/PEMS.Domain/Data/countries.json`) and
 * `src/test/countryDataParity.test.ts` (fails CI when that file is stale) serialize. A code with no
 * Vietnamese short name is dropped rather than emitted with an empty one — defensive, should not
 * happen for any real i18n-iso-countries entry.
 */
export function buildCountryDataSnapshot(): CountryDataSnapshot {
  const aliasesByCode = getAliasesByAlpha2();
  const viNames = getViCountryNames();
  const enNames = getEnCountryNames();
  const countries = Object.keys(aliasesByCode)
    .filter((code) => !!viNames[code])
    .sort()
    .map((alpha2) => ({
      alpha2,
      vi: viNames[alpha2],
      en: enNames[alpha2] ?? '',
      aliases: [...new Set(aliasesByCode[alpha2])].sort(),
    }));
  return { countries };
}
