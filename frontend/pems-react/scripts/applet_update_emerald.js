import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const filePath = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitProcess.tsx');
let code = fs.readFileSync(filePath, 'utf8');

code = code.replace(
  /<CheckCircle className="w-4 h-4 text-emerald-600" \/>/g,
  '<CheckCircle className="w-4 h-4 text-[#004c91]" />'
);

fs.writeFileSync(filePath, code, 'utf8');
console.log('done checkcircle replacement');
