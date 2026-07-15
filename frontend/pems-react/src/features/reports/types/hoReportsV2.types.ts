/** Types cho báo cáo hệ thống 3 phần của HO (GET /reports/ho-report-v2). */

export type HoV2Preset = 'THIS_MONTH' | 'THIS_QUARTER' | 'THIS_YEAR' | 'CUSTOM';
export type HoV2Granularity = 'YEAR' | 'MONTH' | 'WEEK' | 'DAY' | 'HOUR';

export interface HoV2Filters {
  preset: HoV2Preset;
  fromDate: string;
  toDate: string;
}

export interface HoV2CampusInfo {
  campusId: number;
  name: string;
}

export interface HoV2TrendPoint {
  month: string;
  monthLabel: string;
  /** Số đoàn theo campus trong bucket — key là campusId dạng string. */
  byCampus: Record<string, number>;
}

export interface HoV2CampusRow {
  campusId: number;
  name: string;
  totalVisits: number;
  totalPartners: number;
  feedbackAverage: number | null;
  feedbackCount: number;
}

export interface HoV2Overview {
  campusCount: number;
  totalVisits: number;
  totalGuests: number;
  totalPartners: number;
  multiCampusRequests: number;
  singleCampusRequests: number;
  completed: number;
  cancelled: number;
  rejected: number;
  feedbackAverage: number | null;
  feedbackCount: number;
  trendGranularity: HoV2Granularity;
  campuses: HoV2CampusInfo[];
  trend: HoV2TrendPoint[];
  campusRows: HoV2CampusRow[];
}

export interface HoV2PartnerTrendPoint {
  month: string;
  monthLabel: string;
  visitsWithPartner: number;
  newPartners: number;
  cumulativePartners: number;
}

export interface HoV2PartnerRow {
  partnerId: number;
  name: string;
  partnerType: string;
  country: string | null;
  visitCount: number;
  feedbackAverage: number | null;
  feedbackCount: number;
}

export interface HoV2Partners {
  trendGranularity: HoV2Granularity;
  trend: HoV2PartnerTrendPoint[];
  rows: HoV2PartnerRow[];
}

export interface HoReportV2 {
  generatedAt: string;
  preset: HoV2Preset;
  fromDate: string;
  toDate: string;
  overview: HoV2Overview;
  partners: HoV2Partners;
}
