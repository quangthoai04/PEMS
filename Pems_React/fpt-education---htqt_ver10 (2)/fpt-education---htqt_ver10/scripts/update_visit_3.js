import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const file = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitDuringTab.tsx');
let content = fs.readFileSync(file, 'utf8');

// 1. Remove the old block (lines 997 to 1008 loosely)
const oldBlock = `                        {/* Hiển thị partner trùng khớp theo DB */}
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

content = content.replace(oldBlock, '');

// 2. Insert into left column
const targetTarget = `                  </div>

              </div>

                            {/* Form Section */}`;

const newBlock = `                  </div>
                  
                  {matchedPartnerFromEmail && (
                    <div className="p-3 bg-indigo-50 border border-indigo-200 text-indigo-700 rounded-xl text-xs font-bold flex items-start gap-2.5 font-sans shadow-sm">
                      <span className="flex items-center justify-center w-6 h-6 bg-indigo-600 rounded-full text-white shrink-0 shadow-sm shadow-indigo-200 mt-0.5">
                        <CheckCircle2 className="w-4 h-4" />
                      </span>
                      <span>
                        Người này đã thuộc vào danh sách liên hệ của đối tác: <b className="text-indigo-900 border-b border-indigo-800 cursor-pointer hover:text-indigo-600 transition-colors">{matchedPartnerFromEmail}</b>
                      </span>
                    </div>
                  )}

                  <div className="p-4 bg-amber-50 border border-amber-200 rounded-xl">
                    <h4 className="text-xs font-bold text-amber-800 flex items-center gap-1.5 mb-2">
                       <FileText className="w-4 h-4" /> Hướng dẫn
                    </h4>
                    <p className="text-xs text-amber-800 leading-relaxed mb-2 font-medium">
                      Nếu thông tin liên hệ của người đưa card visit đã thuộc 1 đối tác có trên hệ thống thì chia ra 2 TH:
                    </p>
                    <ul className="text-xs text-amber-700 space-y-1.5 pl-3 list-disc">
                      <li>
                         <b>TH1:</b> Đơn vị mà họ công tác vẫn giống nhau ⇒ ấn lưu thông tin ⇒ cập nhật thông tin của họ trong đơn vị đó
                      </li>
                      <li>
                         <b>TH2:</b> Đơn vị mà họ công tác đã thay đổi ⇒ ấn lưu thông tin ⇒ tự động xóa thông tin của họ trên đơn vị cũ ⇒ cập nhật thông tin của họ vào đơn vị mới
                      </li>
                    </ul>
                  </div>

              </div>

                            {/* Form Section */}`;

content = content.replace(targetTarget, newBlock);

fs.writeFileSync(file, content, 'utf8');
console.log("updated");
