const fs = require('fs');

const files = [
  'src/pages/dashboard/visit/VisitRequestDetail.tsx',
  'src/pages/dashboard/home/DashboardHome.tsx',
  'src/pages/dashboard/department-staff/useDeptStaffData.ts',
  'src/pages/dashboard/department-staff/StaffTasksTab.tsx',
  'src/pages/dashboard/departments/SharedDashboardView.tsx',
  'src/pages/dashboard/department-staff/DeptStaffDashboard.tsx',
  'src/pages/dashboard/departments/TaskDetail.tsx',
  'src/pages/dashboard/departments/TaskInvitationDetail.tsx',
  'src/pages/dashboard/department-staff/AssignedTaskList.tsx',
];

for (const file of files) {
  let content = fs.readFileSync(file, 'utf8');
  
  // replace now.getFullYear() -> now.getUTCFullYear()
  // if now is derived from toVietnamCalendarDate(new Date())!
  content = content.replace(/toVietnamCalendarDate\([^)]*\)(\s*\?\?\s*new Date\([^)]*\))?(!)?\.(getFullYear|getMonth|getDate|getHours|getMinutes)\(\)/g, (match, p1, p2, method) => {
    return match.replace(method, 'getUTC' + method.substring(3));
  });

  // also replace sd.getFullYear(), ed.getMonth() if sd, ed, start, end, s, e, now, t, today are UTC Dates
  // We can just find `.getFullYear`, `.getMonth`, `.getDate`, `.getHours`, `.getMinutes` and replace with `.getUTC...`
  // But wait, there are local dates as well. Let's just manually replace the variables known to be returned by toVietnamCalendarDate.
  const vars = ['now', 'todayObj', 'today', 'sd', 'ed', 'start', 'end', 's', 'e', 't'];
  
  for (const v of vars) {
    const regex = new RegExp(`\\b${v}\\.(getFullYear|getMonth|getDate|getHours|getMinutes|getDay)\\(\\)`, 'g');
    content = content.replace(regex, (match, method) => {
      // getDay -> getUTCDay
      let utcMethod = 'getUTC' + (method === 'getDay' ? 'Day' : method.substring(3));
      return `${v}.${utcMethod}()`;
    });
  }

  // exception in DeptStaffDashboard.tsx:
  // const [currentYear, setCurrentYear] = useState((toVietnamCalendarDate(new Date()) ?? new Date()).getFullYear());
  content = content.replace(/\(toVietnamCalendarDate\(new Date\(\)\) \?\? new Date\(\)\)\.getFullYear\(\)/g, "(toVietnamCalendarDate(new Date()) ?? new Date()).getUTCFullYear()");

  fs.writeFileSync(file, content, 'utf8');
}
console.log('Done');
