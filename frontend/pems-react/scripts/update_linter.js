import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const filePath = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitDuringTab.tsx');
let code = fs.readFileSync(filePath, 'utf8');

// 1. Let's do a reliable replace of the "Nguyễn Văn Nhật" scan info block
const search1 = `name: "Nguyễn Văn Nhật"`;
const idx1 = code.indexOf(search1);
if (idx1 !== -1) {
  // Let's replace the whole block by splitting lines
  const lines = code.split('\n');
  const targetIndex = lines.findIndex(l => l.includes('name: "Nguyễn Văn Nhật"'));
  if (targetIndex !== -1) {
    console.log("Found scan info load at line:", targetIndex + 1);
    // Find where the setScannedInfo ending ");" is:
    let endIdx = targetIndex;
    while (endIdx < lines.length && !lines[endIdx].includes('});')) {
      endIdx++;
    }
    // Let's splice the new properties right before the closing "});"
    lines.splice(endIdx, 0, 
      `                               website: "https://fpt.com.vn",`,
      `                               address: "Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội",`
    );
    code = lines.join('\n');
    console.log("Successfully appended website and address to scannedInfo loader!");
  }
} else {
  console.error("Scan load Nguyễn Văn Nhật not found!");
}

// 2. Let's do the same for the reset handler around line 918
const lines2 = code.split('\n');
const resetIndex = lines2.findIndex((l, index) => {
  return l.includes('setScannedInfo({') && 
         lines2[index + 1]?.includes('name: ""') && 
         lines2[index + 5]?.includes('email: ""');
});

if (resetIndex !== -1) {
  console.log("Found scan reset at line:", resetIndex + 1);
  // insert website, address mock fields
  lines2.splice(resetIndex + 6, 0, 
    '                                    website: "",',
    '                                    address: "",'
  );
  code = lines2.join('\n');
  console.log("Successfully appended website and address to scannedInfo empty reset!");
} else {
  console.error("Scan reset handler not found!");
}

fs.writeFileSync(filePath, code, 'utf8');
console.log("Linter fixes applied successfully!");
