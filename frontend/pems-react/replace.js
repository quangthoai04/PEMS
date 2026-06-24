
const fs = require('fs');
const path = require('path');
const p = path.resolve('d:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx');
let c = fs.readFileSync(p, 'utf8');
c = c.replace(/activePopoverEvent\.category === 'L?i m?i tham gia'/g, "activePopoverEvent.itemType === 'INVITATION');
c = c.replace(/activePopoverEvent\.category === 'Ðon yêu c?u mu?n d?'/g, "activePopoverEvent.itemType === 'REQUEST');
fs.writeFileSync(p, c);
console.log('done');

