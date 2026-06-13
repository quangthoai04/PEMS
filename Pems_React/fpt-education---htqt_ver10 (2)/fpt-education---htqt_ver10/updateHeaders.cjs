const fs = require('fs');
const file = 'src/pages/dashboard/visit/VisitProcess.tsx';
let content = fs.readFileSync(file, 'utf8');

// The headers have this structure:
// <h4 className="text-base font-bold text-gray-800 mb-3 flex items-center gap-2">
content = content.replace(/<h4 className="text-base font-bold text-gray-800 mb-3 flex items-center gap-2">/g, '<h4 className="text-base font-bold text-[#004c91] mb-3 flex items-center gap-2">');

fs.writeFileSync(file, content);
