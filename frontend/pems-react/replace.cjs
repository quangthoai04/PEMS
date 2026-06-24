const fs = require('fs');
const file = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx';
let content = fs.readFileSync(file, 'utf8');

const s1 = `                                   onClick={() => {
                                     setAssignedPerson(staff.name);
                                     setShowAssignDropdown(false);
                                   }}`;
const r1 = `                                   onClick={async () => {
                                     try {
                                       if (activePopoverEvent?.rawId) {
                                         await departmentReceptionTasksApi.assignAssignee(activePopoverEvent.rawId, staff.id);
                                         toast.success('Phân công thành công');
                                         setAssignedPerson(staff.name);
                                         setShowAssignDropdown(false);
                                         await fetchCalendarEvents();
                                       }
                                     } catch(e) { console.error(e); toast.error('Phân công th?t b?i'); }
                                   }}`;

const s2 = `                                       onClick={() => {
                                         setAssignedPerson(staff.name);
                                         setShowAssignDropdown(false);
                                       }}`;
const r2 = `                                       onClick={async () => {
                                         try {
                                           if (selectedTask?.rawId) {
                                             await departmentReceptionTasksApi.assignAssignee(selectedTask.rawId, staff.id);
                                             toast.success('Phân công thành công');
                                             setAssignedPerson(staff.name);
                                             setShowAssignDropdown(false);
                                             await fetchCalendarEvents();
                                           }
                                         } catch(e) { console.error(e); toast.error('Phân công th?t b?i'); }
                                       }}`;

content = content.replace(s1, r1);
content = content.replace(s2, r2);

fs.writeFileSync(file, content);
console.log('Done!');
