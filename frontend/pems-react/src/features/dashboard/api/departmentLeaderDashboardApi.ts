import httpClient from '../../../shared/api/httpClient';

export type DepartmentLeaderQuickTask = {
  logisticsItemId: number;
  visitInstanceId: number;
  visitRequestId: number;
  delegationName: string;
  taskTitle: string;
  dueAt: string | null;
  status: string;
  assignedToUserId: number | null;
  assignedToName: string | null;
};

export type DepartmentLeaderUpcomingSchedule = {
  visitInstanceId: number;
  visitRequestId: number;
  delegationName: string;
  organizationName: string | null;
  plannedStartAt: string;
  plannedEndAt: string;
  campusName: string;
  location: string | null;
  status: string;
};

export type DepartmentLeaderDashboardSummary = {
  serverNow: string;
  pendingAssignmentCount: number;
  upcomingDelegationCount: number;
  processingDelegationCount: number;
  activePersonnelCount: number;
  quickTasks: DepartmentLeaderQuickTask[];
  upcomingSchedules: DepartmentLeaderUpcomingSchedule[];
};

export const departmentLeaderDashboardApi = {
  getSummary: async (): Promise<DepartmentLeaderDashboardSummary> => {
    const { data } = await httpClient.get('/dashboard/department-leader/summary');
    return data;
  },
};
