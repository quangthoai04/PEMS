/**
 * Toạ độ tham chiếu (lat/lng) theo tên quốc gia tiếng Việt — dữ liệu địa lý tĩnh,
 * dùng để đặt ghim quốc gia đối tác thật (GET /public/partners/countries) lên quả cầu 3D.
 * Quốc gia không có trong bảng này sẽ bị bỏ qua an toàn (xem resolveCountryCoordinates).
 */

export interface CountryCoordinate {
  lat: number;
  lng: number;
}

const COUNTRY_COORDINATES: Record<string, CountryCoordinate> = {
  'việt nam': { lat: 14.0583, lng: 108.2772 },
  'hàn quốc': { lat: 35.9078, lng: 127.7669 },
  'nhật bản': { lat: 36.2048, lng: 138.2529 },
  'trung quốc': { lat: 35.8617, lng: 104.1954 },
  'đài loan': { lat: 23.6978, lng: 120.9605 },
  'hồng kông': { lat: 22.3193, lng: 114.1694 },
  'singapore': { lat: 1.3521, lng: 103.8198 },
  'thái lan': { lat: 15.8700, lng: 100.9925 },
  'malaysia': { lat: 4.2105, lng: 101.9758 },
  'indonesia': { lat: -0.7893, lng: 113.9213 },
  'philippines': { lat: 12.8797, lng: 121.7740 },
  'campuchia': { lat: 12.5657, lng: 104.9910 },
  'lào': { lat: 19.8563, lng: 102.4955 },
  'myanmar': { lat: 21.9162, lng: 95.9560 },
  'ấn độ': { lat: 20.5937, lng: 78.9629 },
  'úc': { lat: -25.2744, lng: 133.7751 },
  'new zealand': { lat: -40.9006, lng: 174.8860 },
  'hoa kỳ': { lat: 37.0902, lng: -95.7129 },
  'mỹ': { lat: 37.0902, lng: -95.7129 },
  'canada': { lat: 56.1304, lng: -106.3468 },
  'mexico': { lat: 23.6345, lng: -102.5528 },
  'brazil': { lat: -14.2350, lng: -51.9253 },
  'chile': { lat: -35.6751, lng: -71.5430 },
  'argentina': { lat: -38.4161, lng: -63.6167 },
  'anh': { lat: 55.3781, lng: -3.4360 },
  'vương quốc anh': { lat: 55.3781, lng: -3.4360 },
  'pháp': { lat: 46.2276, lng: 2.2137 },
  'đức': { lat: 51.1657, lng: 10.4515 },
  'ý': { lat: 41.8719, lng: 12.5674 },
  'tây ban nha': { lat: 40.4637, lng: -3.7492 },
  'bồ đào nha': { lat: 39.3999, lng: -8.2245 },
  'hà lan': { lat: 52.1326, lng: 5.2913 },
  'bỉ': { lat: 50.5039, lng: 4.4699 },
  'thuỵ sĩ': { lat: 46.8182, lng: 8.2275 },
  'thụy sĩ': { lat: 46.8182, lng: 8.2275 },
  'thuỵ điển': { lat: 60.1282, lng: 18.6435 },
  'thụy điển': { lat: 60.1282, lng: 18.6435 },
  'na uy': { lat: 60.4720, lng: 8.4689 },
  'đan mạch': { lat: 56.2639, lng: 9.5018 },
  'phần lan': { lat: 61.9241, lng: 25.7482 },
  'ba lan': { lat: 51.9194, lng: 19.1451 },
  'nga': { lat: 61.5240, lng: 105.3188 },
  'thổ nhĩ kỳ': { lat: 38.9637, lng: 35.2433 },
  'các tiểu vương quốc ả rập thống nhất': { lat: 23.4241, lng: 53.8478 },
  'ả rập xê út': { lat: 23.8859, lng: 45.0792 },
  'israel': { lat: 31.0461, lng: 34.8516 },
  'nam phi': { lat: -30.5595, lng: 22.9375 },
  'ai cập': { lat: 26.8206, lng: 30.8025 },
};

function normalize(input: string): string {
  return input
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[đĐ]/g, 'd')
    .trim()
    .toLowerCase()
    .replace(/\s+/g, ' ');
}

// Pre-normalize the lookup table's keys once for fast, diacritics-tolerant matching.
const NORMALIZED_TABLE = new Map<string, CountryCoordinate>(
  Object.entries(COUNTRY_COORDINATES).map(([name, coord]) => [normalize(name), coord]),
);

/** Returns coordinates for a country name, or null if unknown (caller should skip the pin). */
export function resolveCountryCoordinates(countryName: string): CountryCoordinate | null {
  return NORMALIZED_TABLE.get(normalize(countryName)) ?? null;
}
