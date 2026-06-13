const fs = require('fs');
const file = 'src/pages/dashboard/visit/VisitProcess.tsx';
let content = fs.readFileSync(file, 'utf8');

content = content.replace(/border border-gray-200 focus:border-\[#004c91\]/g, 'border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors');
content = content.replace(/border border-gray-200 text-sm/g, 'border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none');

fs.writeFileSync(file, content);
