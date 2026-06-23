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
  const header = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
  const sample = [1, 'Nguyễn Văn A', 'Giám đốc', 'Công ty XYZ', 'Vietnam'];
  const ws = makeSheet([header, sample], [6, 28, 20, 30, 18]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Danh sách khách');
  saveWorkbook(wb, 'visit-guests-template.xlsx');
};

export const downloadSupportTeamTemplate = () => {
  const header = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
  const sample = [1, 'Trần Thị B', 'Trưởng nhóm', 'Công ty ABC', 'Vietnam'];
  const ws = makeSheet([header, sample], [6, 28, 20, 28, 18]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Team hỗ trợ');
  saveWorkbook(wb, 'support-team-template.xlsx');
};
