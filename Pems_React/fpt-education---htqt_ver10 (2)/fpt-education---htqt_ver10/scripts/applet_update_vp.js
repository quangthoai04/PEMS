import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const filePath = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitProcess.tsx');
let code = fs.readFileSync(filePath, 'utf8');

// Replacements
code = code.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.electricCar.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedElectricCarDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {setupDetails.electricCar.confirmed 
                                    ? (getLeaderName(selectedElectricCarDept) || "Chưa chọn")
                                    : "Chưa xác nhận"
                                  }
                                </span>`
);

code = code.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.driver.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedDriverDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {setupDetails.driver.confirmed 
                                    ? (getLeaderName(selectedDriverDept) || "Chưa chọn")
                                    : "Chưa xác nhận"
                                  }
                                </span>`
);

code = code.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.meetingRoom.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedRoomDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {setupDetails.meetingRoom.confirmed 
                                    ? (getLeaderName(selectedRoomDept) || "Chưa chọn")
                                    : "Chưa xác nhận"
                                  }
                                </span>`
);

code = code.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {setupDetails.teabreak.confirmed 
                                    ? \`Người cho mượn: \${getLeaderName(selectedTeabreakDept) || "Chưa chọn bộ phận"}\`
                                    : "Người cho mượn: Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {setupDetails.teabreak.confirmed 
                                    ? (getLeaderName(selectedTeabreakDept) || "Chưa chọn")
                                    : "Chưa xác nhận"
                                  }
                                </span>`
);

code = code.replaceAll(
  `className="font-bold text-emerald-900 bg-emerald-100/50 px-2.5 py-1 rounded-lg border border-emerald-200">
                                  {item.confirmed 
                                    ? "Đã xác nhận request"
                                    : "Chưa xác nhận request"
                                  }
                                </span>`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm">
                                  {item.confirmed ? "Đã xác nhận" : "Chưa xác nhận"}
                                </span>`
);

// CSS Classes
code = code.replaceAll(
  `className="font-bold text-[#004c91] bg-blue-50 px-2.5 py-1 rounded-lg border border-blue-100"`,
  `className="font-bold text-[#004c91] bg-white px-2 py-0.5 rounded border border-blue-200 text-xs shadow-sm"`
);
code = code.replaceAll(
  `className="text-gray-600 flex items-center gap-1.5 font-medium"`,
  `className="text-gray-600 flex items-center gap-1.5 font-medium text-xs"`
);
code = code.replaceAll(
  `className="flex items-center justify-between p-4 bg-emerald-50/40 hover:bg-emerald-100/30 border border-emerald-200 rounded-xl transition-all cursor-pointer shadow-sm"`,
  `className="flex items-center justify-between p-2.5 bg-blue-50/30 hover:bg-blue-100/20 border border-blue-100 rounded-xl transition-all cursor-pointer shadow-sm relative"`
);
code = code.replaceAll(
  `<CheckCircle className="w-5 h-5 text-emerald-600 shrink-0" />`,
  `<CheckCircle className="w-4 h-4 text-[#004c91] shrink-0" />`
);

fs.writeFileSync(filePath, code, 'utf8');
console.log('Update Complete');
