import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

// 1. Update DepartmentDetailDashboard.tsx
const detailPath = path.resolve(projectRoot, 'src/pages/dashboard/departments/DepartmentDetailDashboard.tsx');
let detailCode = fs.readFileSync(detailPath, 'utf8');

detailCode = detailCode.replace(
  /\{\!\(isStaffRole \|\| user\?\.role\?\.toUpperCase\(\) === 'DEPT'\) && \(/g,
  "{!(isStaffRole || user?.role?.toUpperCase() === 'DEPT' || isHO) && ("
);

// update colsPan
detailCode = detailCode.replace(
  /colSpan=\{\(isStaffRole \|\| user\?\.role\?\.toUpperCase\(\) === 'DEPT'\) \? 6 : 7\}/g,
  "colSpan={(isStaffRole || user?.role?.toUpperCase() === 'DEPT' || isHO) ? 6 : 7}"
);

fs.writeFileSync(detailPath, detailCode, 'utf8');

// 2. Update DepartmentManagement.tsx
const mgmtPath = path.resolve(projectRoot, 'src/pages/dashboard/departments/DepartmentManagement.tsx');
let mgmtCode = fs.readFileSync(mgmtPath, 'utf8');

// Remove header
mgmtCode = mgmtCode.replace(
  /\{isHO && <th className="p-4 w-\[14%\] font-bold text-center whitespace-nowrap">CHƯA CẤP TÀI KHOẢN<\/th>\}/g,
  ""
);

// Remove body cell
const regexBody = /\{isHO && \(\s*<td className="p-4 text-center font-medium whitespace-nowrap">[\s\S]*?<\/td>\s*\)\}/;
mgmtCode = mgmtCode.replace(regexBody, "");

// Colspan
mgmtCode = mgmtCode.replace(
  /colSpan=\{isHO \? 8 : 6\}/g,
  "colSpan={isHO ? 7 : 6}"
);

fs.writeFileSync(mgmtPath, mgmtCode, 'utf8');
console.log('Update done');
