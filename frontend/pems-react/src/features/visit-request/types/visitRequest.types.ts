export interface VisitorEntry {
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

export interface SupportTeamEntry {
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

export interface VisitSlot {
  campus: string;
  startDatetime: string;
  endDatetime: string;
}

export interface RegisterInfoData {
  fullName: string;
  organization: string;
  jobTitle: string;
  phone: string;
  email: string;
  nationality: string;
}

export interface ContactPointData {
  fullName: string;
  organization: string;
  phone: string;
  email: string;
}

export interface VisitRequestFormData {
  registerInfo: RegisterInfoData;
  delegationName: string;
  visitMode: 'single' | 'multiple';
  visitType: string;
  visitTypeOther?: string;
  visits: VisitSlot[];
  purpose: string;
  workingContent: string;
  visitors: VisitorEntry[];
  supportTeam: SupportTeamEntry[];
  contactPoint: ContactPointData;
  workingLanguage: 'EN' | 'VI';
  transportationType: string;
  transportationDetail?: string;
  mediaConsentStatus: 'AGREED' | 'DECLINED';
  mediaConsentNote?: string;
  partnerId?: number | null;
  notes: string;
}

export interface ExcelValidationError {
  row: number;
  column: string;
  message: string;
}

export interface ExcelValidationResult {
  valid: boolean;
  totalRows: number;
  errorRows: number;
  errors: ExcelValidationError[];
  data: VisitorEntry[];
}

export interface SupportTeamExcelValidationResult {
  valid: boolean;
  totalRows: number;
  errorRows: number;
  errors: ExcelValidationError[];
  data: SupportTeamEntry[];
}

export interface OrganizationOption {
  value: string;
  label: string;
}
