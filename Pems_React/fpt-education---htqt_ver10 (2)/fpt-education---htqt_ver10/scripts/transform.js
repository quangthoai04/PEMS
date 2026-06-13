import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const file = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitProcess.tsx');
let content = fs.readFileSync(file, 'utf8');

// The inputs are currently mostly defined by:
// className="w-full px-3 py-2 rounded-lg border border-gray-200 focus:border-[#004c91] outline-none text-sm"
// className="px-3 py-2 rounded-lg border border-gray-200 text-sm w-full sm:w-auto"
// ...

// Change to border-gray-300 only for elements inside section 2 Setup Detail, but it is easier to replace them all safely using replace method contextually.
// Actually, I can just replace `border border-gray-200 focus:border-[#004c91]` globally to `border border-gray-300 focus:border-[#004c91] hover:border-gray-400`.
content = content.replace(/border border-gray-200 focus:border-\[#004c91\]/g, 'border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors');
content = content.replace(/border border-gray-200 text-sm/g, 'border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none');

// also for disabled ones which might have border-gray-200 focus:border-[#004c91], they are covered above.

fs.writeFileSync(file, content);
