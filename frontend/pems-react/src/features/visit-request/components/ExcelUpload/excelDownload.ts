import * as XLSX from 'xlsx';

const saveWorkbook = (wb: XLSX.WorkBook, filename: string) => {
  XLSX.writeFile(wb, filename);
};

const makeSheet = (rows: (string | number)[][], colWidths: number[]): XLSX.WorkSheet => {
  const ws = XLSX.utils.aoa_to_sheet(rows);
  ws['!cols'] = colWidths.map((wch) => ({ wch }));
  return ws;
};

export const downloadVisitorTemplate = () => {
  const header = ['STT', 'Họ và tên', 'Số HC/CMND', 'Email', 'Quốc tịch', 'Chức vụ'];
  const sample = [1, 'Nguyễn Văn A', 'P123456789', 'nguyenvana@example.com', 'Vietnam', 'Giám đốc'];
  const ws = makeSheet([header, sample], [6, 28, 18, 30, 18, 20]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Danh sách khách');
  saveWorkbook(wb, 'mau_danh_sach_khach.xlsx');
};

export const downloadSupportTeamTemplate = () => {
  const header = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
  const sample = [1, 'Trần Thị B', 'Trưởng nhóm', 'Công ty ABC', 'Vietnam'];
  const ws = makeSheet([header, sample], [6, 28, 20, 28, 18]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Team hỗ trợ');
  saveWorkbook(wb, 'mau_team_ho_tro.xlsx');
};
