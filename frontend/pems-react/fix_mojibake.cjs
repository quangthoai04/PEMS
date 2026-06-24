const fs = require('fs');
const file = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx';
let buf = fs.readFileSync(file);
let text = buf.toString('utf8');
let fixedBuf = Buffer.from(text, 'latin1');
let fixedText = fixedBuf.toString('utf8');
fs.writeFileSync('d:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView_fixed.tsx', fixedText);
console.log("Done");
