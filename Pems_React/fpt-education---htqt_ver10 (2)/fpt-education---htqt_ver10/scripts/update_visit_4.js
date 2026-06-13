import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const file = path.resolve(projectRoot, 'src/pages/dashboard/visit/VisitDuringTab.tsx');
let content = fs.readFileSync(file, 'utf8');

// 1. Add state
const statePattern = "const [isScanned, setIsScanned] = useState(false);";
const stateNew = "const [isScanned, setIsScanned] = useState(false);\n  const [isGuideModalOpen, setIsGuideModalOpen] = useState(false);";
content = content.replace(statePattern, stateNew);

// 2. Change the inline guide to a button
const oldGuide = `                 <div className="mt-4 p-4 bg-amber-50 border border-amber-200 rounded-xl">
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
                 </div>`;

const newButton = `                 <button 
                    type="button"
                    onClick={() => setIsGuideModalOpen(true)}
                    className="mt-4 w-full p-2.5 bg-amber-50 hover:bg-amber-100 border border-amber-200 text-amber-800 rounded-xl text-xs font-bold flex items-center justify-between transition-colors outline-none cursor-pointer"
                 >
                    <span className="flex items-center gap-1.5"><FileText className="w-4 h-4" /> Hướng dẫn xử lý liên hệ trùng lặp</span>
                    <span className="bg-amber-200 text-amber-800 px-2 py-0.5 rounded-md text-[10px] uppercase">Xem chi tiết</span>
                 </button>`;

content = content.replace(oldGuide, newButton);

// 3. Add Modal exactly before the final </div> of the component return
const endDiv = `    </div>
  );
}`;

const modal = `      {/* Guide Modal */}
      {isGuideModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between bg-white relative z-10">
              <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                <FileText className="w-5 h-5 text-amber-500" /> 
                Hướng dẫn
              </h3>
              <button onClick={() => setIsGuideModalOpen(false)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 bg-amber-50/30">
              <p className="text-sm text-gray-800 leading-relaxed mb-5 font-medium">
                Nếu thông tin liên hệ của người đưa card visit đã thuộc 1 đối tác có trên hệ thống thì chia ra 2 TH:
              </p>
              <div className="space-y-4">
                <div className="bg-white p-4 rounded-xl border border-gray-100 shadow-sm relative overflow-hidden">
                  <div className="absolute top-0 left-0 w-1 h-full bg-blue-500"></div>
                  <h4 className="font-bold text-gray-900 mb-1.5 text-sm flex items-center gap-2">
                    <span className="bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded text-[10px] uppercase">TH1</span> 
                    Đơn vị công tác giống nhau
                  </h4>
                  <p className="text-sm text-gray-600">Ấn <b className="text-blue-600">Lưu thông tin</b> ⇒ Hệ thống sẽ tự động cập nhật thông tin của họ trong đơn vị đó.</p>
                </div>
                <div className="bg-white p-4 rounded-xl border border-gray-100 shadow-sm relative overflow-hidden">
                  <div className="absolute top-0 left-0 w-1 h-full bg-fuchsia-500"></div>
                  <h4 className="font-bold text-gray-900 mb-1.5 text-sm flex items-center gap-2">
                    <span className="bg-fuchsia-100 text-fuchsia-700 px-1.5 py-0.5 rounded text-[10px] uppercase">TH2</span> 
                    Đơn vị công tác đã thay đổi
                  </h4>
                  <p className="text-sm text-gray-600">Ấn <b className="text-blue-600">Lưu thông tin</b> ⇒ Hệ thống sẽ tự động xóa thông tin của họ trên đơn vị cũ và cập nhật thông tin của họ vào đơn vị mới.</p>
                </div>
              </div>
            </div>
            <div className="p-4 border-t border-gray-100 bg-gray-50 flex justify-end">
              <button 
                onClick={() => setIsGuideModalOpen(false)}
                className="px-6 py-2 bg-[#004c91] hover:bg-[#003366] text-white font-bold rounded-xl transition-colors outline-none cursor-pointer shadow-sm"
              >
                Đã hiểu
              </button>
            </div>
          </div>
        </div>
      )}
`;

content = content.replace(endDiv, modal + '\n' + endDiv);

fs.writeFileSync(file, content, 'utf8');
console.log('done');
