import apiClient from '../shared/api/httpClient';

export interface VisitExpenseItem {
  expenseItemId: number;
  itemOrigin: 'REQUEST_ITEM' | 'MANUAL' | 'ADDITIONAL' | 'DAMAGE_LOSS' | 'OTHER';
  itemName: string;
  description: string | null;
  quantity: number;
  unitName: string | null;
  unitPrice: number;
  totalAmount: number;
  itemNote: string | null;
  displayOrder: number;
  rowVersion: number;
}

export interface VisitExpenseReport {
  expenseReportId: number;
  visitInstanceId: number;
  reportScope: 'GENERAL' | 'LOGISTICS';
  logisticsItemId: number | null;
  departmentId: number | null;
  status: 'DRAFT' | 'SAVED' | 'FINALIZED' | 'CANCELLED';
  reportNote: string | null;
  noExpense: boolean;
  currencyCode: string;
  rowVersion: number;
  createdAt: string;
  totalAmount: number;
  // Chỉ có trong summary (host view)
  departmentName?: string | null;
  logisticsItemTitle?: string | null;
  items: VisitExpenseItem[];
}

export interface VisitInstanceExpenseSummary {
  visitInstanceId: number;
  totalAmount: number;
  generalReport: VisitExpenseReport | null;
  logisticsReports: VisitExpenseReport[];
}

export interface SaveExpenseItemDto {
  expenseItemId?: number;
  itemOrigin: 'REQUEST_ITEM' | 'MANUAL' | 'ADDITIONAL' | 'DAMAGE_LOSS' | 'OTHER';
  itemName: string;
  description?: string | null;
  quantity: number;
  unitName?: string | null;
  unitPrice: number;
  itemNote?: string | null;
  displayOrder: number;
}

export interface SaveExpenseReportCommand {
  reportNote?: string | null;
  rowVersion: number;
  /** true = xác nhận "Không có chi phí" cho báo cáo này */
  noExpense?: boolean;
  items: SaveExpenseItemDto[];
}

export interface RemindExpenseReportsResult {
  remindedCount: number;
  recipients: string[];
}

const visitExpenseService = {
  getGeneralExpenseReport: async (visitInstanceId: number): Promise<VisitExpenseReport> => {
    const res = await apiClient.get<VisitExpenseReport>(`/VisitExpenses/general/${visitInstanceId}`);
    return res.data;
  },

  getLogisticsExpenseReport: async (logisticsItemId: number): Promise<VisitExpenseReport> => {
    const res = await apiClient.get<VisitExpenseReport>(`/VisitExpenses/logistics/${logisticsItemId}`);
    return res.data;
  },

  saveExpenseReport: async (expenseReportId: number, data: SaveExpenseReportCommand): Promise<VisitExpenseReport> => {
    const res = await apiClient.put<VisitExpenseReport>(`/VisitExpenses/${expenseReportId}`, data);
    return res.data;
  },

  getExpenseSummary: async (visitInstanceId: number): Promise<VisitInstanceExpenseSummary> => {
    const res = await apiClient.get<VisitInstanceExpenseSummary>(`/VisitExpenses/summary/${visitInstanceId}`);
    return res.data;
  },

  remindExpenseReports: async (visitInstanceId: number): Promise<RemindExpenseReportsResult> => {
    const res = await apiClient.post<RemindExpenseReportsResult>(`/VisitExpenses/remind/${visitInstanceId}`);
    return res.data;
  }
};

export default visitExpenseService;
