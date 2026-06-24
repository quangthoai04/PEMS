const fs = require('fs');
const file = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx';
let content = fs.readFileSync(file, 'utf8');

content = content.replace(/onClick=\{\(\) => \{\s*setAssignedPerson\(staff\.name\);\s*setShowAssignDropdown\(false\);\s*\}\}/g, `onClick={async () => {
                                     try {
                                       if (activePopoverEvent?.rawId) {
                                         await departmentReceptionTasksApi.assignAssignee(activePopoverEvent.rawId, staff.id);
                                         toast.success('Phân công thành công');
                                         setAssignedPerson(staff.name);
                                         setShowAssignDropdown(false);
                                         await fetchCalendarEvents();
                                       }
                                     } catch(e) { console.error(e); toast.error('Phân công th?t b?i'); }
                                   }}`);

fs.writeFileSync(file, content);
console.log('Done!');
