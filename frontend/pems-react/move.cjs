const fs = require('fs');
const filePath = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/home/SharedDashboardView.tsx';
let content = fs.readFileSync(filePath, 'utf8');

const blockStart = '                  {/* Xem chi tiết đoàn đón khách */}';
const blockEndStr = '                  <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">';

const startIndex = content.indexOf(blockStart);
const endIndex = content.indexOf(blockEndStr);

if (startIndex === -1 || endIndex === -1) {
  console.log('Could not find block', { startIndex, endIndex });
  process.exit(1);
}

let extractedBlock = content.substring(startIndex, endIndex);
content = content.substring(0, startIndex) + content.substring(endIndex);

const targetAnchor = '<div className="p-6 md:p-8 space-y-4 overflow-y-auto max-h-[70vh] no-scrollbar bg-slate-50/50">';
const targetIndex = content.indexOf(targetAnchor);

if (targetIndex === -1) {
  console.log('Could not find target');
  process.exit(1);
}

// Ensure the button spans full width and has some spacing at bottom before other specific category UI starts.
const insertionStr = targetAnchor + '\n                ' + extractedBlock.trim() + '\n';

content = content.substring(0, targetIndex) + insertionStr + content.substring(targetIndex + targetAnchor.length);

fs.writeFileSync(filePath, content, 'utf8');
console.log('Successfully moved the block.');
