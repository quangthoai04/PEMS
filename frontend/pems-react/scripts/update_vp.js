import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const filePath = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitProcess.tsx');
let code = fs.readFileSync(filePath, 'utf8');

const replacements = [
  // 1. Xe dien
  {
    find: `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.electricCar.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedElectricCarDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }`,
    replace: `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {setupDetails.electricCar.confirmed 
                                    ? (getLeaderName(selectedElectricCarDept) || "Chưa chọn")
                                    : "Chưa xác nhận request"
                                  }`
  },
  
  // 2. Nguoi lai
  {
    find: `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.driver.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedDriverDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }`,
    replace: `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {setupDetails.driver.confirmed 
                                    ? (getLeaderName(selectedDriverDept) || "Chưa chọn")
                                    : "Chưa xác nhận request"
                                  }`
  },

  // 3. Phong hop
  {
    find: `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.meetingRoom.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedRoomDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }`,
    replace: `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {setupDetails.meetingRoom.confirmed 
                                    ? (getLeaderName(selectedRoomDept) || "Chưa chọn")
                                    : "Chưa xác nhận request"
                                  }`
  },

  // 4. Teabreak
  {
    find: `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.teabreak.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedTeabreakDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }`,
    replace: `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {setupDetails.teabreak.confirmed 
                                    ? (getLeaderName(selectedTeabreakDept) || "Chưa chọn")
                                    : "Chưa xác nhận request"
                                  }`
  },

  // other requests
  {
    find: `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {item.confirmed 
                                    ? "Đã xác nhận request"
                                    : "Chưa xác nhận request"
                                  }`,
    replace: `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {item.confirmed ? "Đã xác nhận request" : "Chưa xác nhận request"}
                                ` // wait, this might have a closing brace left over.
  }
];

let newCode = code;

for(const rep of replacements) {
  newCode = newCode.replace(rep.find, rep.replace);
}

// Fix another formatting
newCode = newCode.replaceAll(
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {item.confirmed ? "Đã xác nhận request" : "Chưa xác nhận request"}
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {item.confirmed ? "Đã xác nhận request" : "Chưa xác nhận request"}
                                </span>`
);

newCode = newCode.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {item.confirmed 
                                    ? "Đã xác nhận request"
                                    : "Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-100 text-xs">
                                  {item.confirmed ? "Đã xác nhận request" : "Chưa xác nhận request"}
                                </span>`
);


newCode = newCode.replaceAll(
  `className="font-bold text-[#004c91] bg-blue-50 px-2.5 py-1 rounded-lg border border-blue-100"`,
  `className="font-bold text-[#004c91] bg-blue-50 px-2 py-0.5 rounded border border-blue-100 text-xs"`
);
newCode = newCode.replaceAll(
  `className="text-gray-600 flex items-center gap-1.5 font-medium"`,
  `className="text-gray-600 flex items-center gap-1.5 font-medium text-xs"`
);
newCode = newCode.replaceAll(
  `className="flex items-center justify-between p-4 bg-emerald-50/40 hover:bg-emerald-100/30 border border-emerald-200 rounded-xl transition-all cursor-pointer shadow-sm"`,
  `className="flex items-center justify-between p-3 sm:p-3 bg-blue-50/40 hover:bg-blue-100/30 border border-blue-200 rounded-xl transition-all cursor-pointer shadow-sm"`
);
newCode = newCode.replaceAll(
  `<CheckCircle className="w-5 h-5 text-emerald-600 shrink-0" />`,
  `<CheckCircle className="w-5 h-5 text-[#004c91] shrink-0" />`
);


fs.writeFileSync(filePath, newCode, 'utf8');
console.log('Done replacement');
