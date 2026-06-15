import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const file = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitDuringTab.tsx');
let content = fs.readFileSync(file, 'utf8');

// 1. Add state
const statePattern = "const [isScanned, setIsScanned] = useState(false);";
const stateNew = "const [isScanned, setIsScanned] = useState(false);\n  const [matchedPartnerFromEmail, setMatchedPartnerFromEmail] = useState<string | null>(null);";
content = content.replace(statePattern, stateNew);

// 2. Set matched data when file is uploaded
const setScannedOld = `                             setScannedInfo({
                               name: "Nguyễn Văn Nhật",
                               title: "Trưởng phòng Phát triển nguồn nhân lực",
                               company: "Tập đoàn Công nghệ FPT",
                               phone: "0987654321",
                               email: "nhatnv@example.com",
                               website: "https://fpt.com.vn",
                               address: "Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội"
                             });`;

const setScannedNew = `                             setScannedInfo({
                               name: "Nguyễn Văn Nhật",
                               title: "Trưởng phòng Phát triển nguồn nhân lực",
                               company: "Tập đoàn Công nghệ FPT",
                               phone: "0987654321",
                               email: "nhatnv@example.com",
                               website: "https://fpt.com.vn",
                               address: "Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội"
                             });
                             setMatchedPartnerFromEmail("Tập đoàn Công nghệ FPT");`;
content = content.replace(setScannedOld, setScannedNew);

// 3. Clear data when clear image
const clearInfoOld = `                                  setScannedInfo({
                                    name: "",
                                    title: "",
                                    company: "",
                                    phone: "",
                                    email: "",
                                    website: "",
                                    address: ""
                                  });`;

const clearInfoNew = `                                  setScannedInfo({
                                    name: "",
                                    title: "",
                                    company: "",
                                    phone: "",
                                    email: "",
                                    website: "",
                                    address: ""
                                  });
                                  setMatchedPartnerFromEmail(null);`;

content = content.replace(clearInfoOld, clearInfoNew);

// 4. Render UI
const renderOld = `                        {/* New Address field */}
                        <div className="sm:col-span-2 space-y-1.5 font-sans">
                          <label className="flex items-center gap-1.5 text-xs font-bold text-gray-650"><MapPin className="w-3.5 h-3.5" /> Địa chỉ</label>
                          <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#004c91] outline-none" value={scannedInfo.address || ''} onChange={e => setScannedInfo({...scannedInfo, address: e.target.value})} />
                        </div>`;

const renderNew = `                        {/* New Address field */}
                        <div className="sm:col-span-2 space-y-1.5 font-sans">
                          <label className="flex items-center gap-1.5 text-xs font-bold text-gray-650"><MapPin className="w-3.5 h-3.5" /> Địa chỉ</label>
                          <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#004c91] outline-none" value={scannedInfo.address || ''} onChange={e => setScannedInfo({...scannedInfo, address: e.target.value})} />
                        </div>
                        
                        {/* Hiển thị partner trùng khớp theo DB */}
                        {matchedPartnerFromEmail && (
                          <div className="sm:col-span-2 p-3 bg-indigo-50 border border-indigo-200 text-indigo-700 rounded-xl text-xs font-bold flex items-center gap-2.5 font-sans shadow-sm mt-1">
                            <span className="flex items-center justify-center w-6 h-6 bg-indigo-600 rounded-full text-white shrink-0 shadow-sm shadow-indigo-200">
                              <CheckCircle2 className="w-4 h-4" />
                            </span>
                            <span>
                              Người liên hệ này đã thuộc danh sách của đối tác: <b className="text-indigo-900 border-b border-indigo-800 cursor-pointer hover:text-indigo-600 transition-colors">{matchedPartnerFromEmail}</b>
                            </span>
                          </div>
                        )}`;

content = content.replace(renderOld, renderNew);

fs.writeFileSync(file, content, 'utf8');
