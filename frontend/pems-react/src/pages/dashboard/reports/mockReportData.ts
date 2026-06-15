/**
 * Module Mock Data
 * Dữ liệu giả lập cấu hình đồ thị mảng báo cáo thống kê chuyên sâu.
 */

export const REPORT_STATISTICS = {
  totalVisits: 124,
  totalVisitors: 4500,
  upcomingVisits: 12,
  averageRating: 4.8,
  ratingChange: "+0.2",
  visitsChange: "+15%",
  visitorsChange: "+20%",
  upcomingChange: "-2"
};

export const VISITS_OVER_TIME = [
  { name: 'Tháng 1', visits: 10, visitors: 300 },
  { name: 'Tháng 2', visits: 15, visitors: 450 },
  { name: 'Tháng 3', visits: 8, visitors: 200 },
  { name: 'Tháng 4', visits: 25, visitors: 800 },
  { name: 'Tháng 5', visits: 20, visitors: 600 },
  { name: 'Tháng 6', visits: 30, visitors: 1100 },
  { name: 'Tháng 7', visits: 16, visitors: 1050 },
];

export const VISITOR_TYPES = [
  { name: 'Trường THPT', value: 65, color: '#004c91' },
  { name: 'Đại học/Cao đẳng', value: 15, color: '#e85c0d' },
  { name: 'Sở Giáo dục', value: 10, color: '#f37021' },
  { name: 'Đối tác doanh nghiệp', value: 10, color: '#cbd5e1' },
];

export const TOP_RATED_VISITS = [
  { id: 1, name: 'Đoàn khách Đại học Monash', type: 'Đại học/Cao đẳng', date: '18/10/2026', rating: 4.8, visitors: 25 },
  { id: 2, name: 'Đoàn trường THPT Lê Lợi', type: 'Trường THPT', date: '19/10/2026', rating: 4.5, visitors: 120 },
  { id: 4, name: 'Phụ huynh trúng tuyển HN', type: 'Phụ huynh', date: '21/10/2026', rating: 5.0, visitors: 50 },
  { id: 5, name: 'Đoàn trường THPT FPT', type: 'Trường THPT', date: '22/10/2026', rating: 4.0, visitors: 200 },
  { id: 6, name: 'FU Cần Thơ', type: 'Đại học/Cao đẳng', date: '23/10/2026', rating: 5.0, visitors: 30 },
];

export const DEPT_LEADER_STATISTICS = {
  totalTasks: 145,
  tasksChange: "+12",
  totalHours: 320,
  hoursChange: "+45",
  totalPartners: 28,
  partnersChange: "+5",
  completionRate: "95%",
  completionChange: "+2%"
};

export const DEPT_TASKS_OVER_TIME = [
  { name: 'Tháng 1', assigned: 20, completed: 18 },
  { name: 'Tháng 2', assigned: 25, completed: 24 },
  { name: 'Tháng 3', assigned: 15, completed: 15 },
  { name: 'Tháng 4', assigned: 30, completed: 28 },
  { name: 'Tháng 5', assigned: 22, completed: 22 },
  { name: 'Tháng 6', assigned: 35, completed: 33 },
  { name: 'Tháng 7', assigned: 18, completed: 17 },
];

export const DEPT_TASKS_BY_TYPE = [
  { name: 'Khâu chuẩn bị', value: 30, color: '#004c91' },
  { name: 'Thiết kế media', value: 15, color: '#e85c0d' },
  { name: 'Tiếp đón trực tiếp', value: 40, color: '#f37021' },
  { name: 'Viết bài truyền thông', value: 15, color: '#cbd5e1' },
];

export const TOP_PERFORMING_MEMBERS = [
  { id: 1, name: 'Nguyễn Văn A', role: 'Nhân viên Tuyển sinh', tasksCompleted: 45, hoursSpent: 80, avatar: 'https://i.pravatar.cc/150?u=a042581f4e29026024d' },
  { id: 2, name: 'Trần Thị B', role: 'Nhân viên Truyền thông', tasksCompleted: 38, hoursSpent: 65, avatar: 'https://i.pravatar.cc/150?u=a042581f4e29026704d' },
  { id: 3, name: 'Lê Văn C', role: 'Nhân viên Hành chính', tasksCompleted: 32, hoursSpent: 50, avatar: 'https://i.pravatar.cc/150?u=a04258114e29026702d' },
  { id: 4, name: 'Phạm Thị D', role: 'Nhân viên Đối ngoại', tasksCompleted: 30, hoursSpent: 125, avatar: 'https://i.pravatar.cc/150?u=a048581f4e29026701d' },
];

