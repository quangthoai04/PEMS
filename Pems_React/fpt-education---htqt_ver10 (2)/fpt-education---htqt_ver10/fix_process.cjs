const fs = require('fs');

let content = fs.readFileSync('src/pages/dashboard/visit/VisitProcess.tsx', 'utf8');

// 1. Add state
const stateToAdd = `
  const [selectedTourGuide, setSelectedTourGuide] = useState('');
  const [addedTourGuides, setAddedTourGuides] = useState<string[]>([]);
  const [sentRequests, setSentRequests] = useState<Record<string, boolean>>({});

  const handleSendRequest = (type: string) => {
    setSentRequests(prev => ({ ...prev, [type]: true }));
  };
`;
content = content.replace(/(const \[selectedOtherDept, setSelectedOtherDept\] = useState\(''\);)/, '$1\n' + stateToAdd);

// 2. Modify renderLeaderInfo
const oldRenderLeaderInfo = `  const renderLeaderInfo = (dept: string) => {
    if (!dept) return null;
    let name = "Leader " + dept;
    let phone = "0987.xxx.xxx";
    if (dept === "Tuyển sinh") {
      name = "Nguyễn Văn Tuấn";
    } else if (dept === 'Hành chính') {
      name = "Trần Thị Bích";
    } else if (dept === 'Các bộ môn liên quan') {
      name = "Lê Văn Cường";
    }

    return (
      <div className="mt-3 p-3 bg-yellow-50/80 border border-yellow-200 rounded-xl flex items-center gap-3 animate-in fade-in slide-in-from-top-2">
        <div className="w-10 h-10 rounded-full bg-yellow-500 text-white flex items-center justify-center font-bold">
          {name.charAt(0)}
        </div>
        <div>
          <div className="text-sm font-bold text-yellow-900">{name} <span className="text-xs font-medium text-yellow-700 bg-white px-2 py-0.5 rounded-full border border-yellow-200 ml-2">Trưởng phòng</span></div>
          <div className="text-xs font-medium text-yellow-700/80 flex items-center gap-1 mt-0.5"><Phone className="w-3 h-3" /> {phone}</div>
        </div>
        <button className="ml-auto px-3 py-1.5 bg-yellow-100 text-yellow-700 text-xs font-bold rounded-lg hover:bg-yellow-200 transition-colors outline-none shadow-sm">
          Gửi yêu cầu
        </button>
      </div>
    );
  };`;

const newRenderLeaderInfo = `  const renderLeaderInfo = (dept: string, type: string) => {
    if (!dept) return null;
    let name = "Leader " + dept;
    if (dept === "Tuyển sinh") {
      name = "Nguyễn Văn Tuấn";
    } else if (dept === 'Hành chính') {
      name = "Trần Thị Bích";
    } else if (dept === 'Các bộ môn liên quan') {
      name = "Lê Văn Cường";
    }

    const isSent = sentRequests[type];

    return (
      <div className="mt-3 p-3 bg-yellow-50/80 border border-yellow-200 rounded-xl flex items-center gap-3 animate-in fade-in slide-in-from-top-2">
        <div className="w-10 h-10 rounded-full bg-yellow-500 text-white flex items-center justify-center font-bold text-lg">
          {name.charAt(0)}
        </div>
        <div className="flex-1">
          <div className="text-sm font-bold text-yellow-900 flex items-center gap-2 flex-wrap">
            {name} 
            <span className="text-[11px] font-bold uppercase tracking-wider text-yellow-700 bg-white px-2 py-0.5 rounded-md border border-yellow-200 shadow-sm">Trưởng phòng</span>
          </div>
          {isSent && (
            <div className="text-[13px] font-bold text-[#10b981] flex items-center gap-1 mt-1 animate-in fade-in slide-in-from-bottom-1"><CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi yêu cầu tới "{name}"</div>
          )}
        </div>
        {!isSent ? (
          <button 
            type="button"
            onClick={() => handleSendRequest(type)}
            className="ml-auto px-4 py-2 bg-yellow-100 text-yellow-800 text-xs font-bold rounded-xl hover:bg-yellow-200 transition-all active:scale-[0.98] outline-none shadow-sm flex items-center gap-1"
          >
            Gửi yêu cầu
          </button>
        ) : (
          <button
            type="button"
            className="ml-auto px-4 py-2 bg-[#10b981]/10 text-[#10b981] text-xs font-bold rounded-xl hover:bg-[#10b981]/20 transition-all active:scale-[0.98] outline-none shadow-sm flex items-center gap-1 border border-[#10b981]/20"
          >
            Xem <ArrowRight className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
    );
  };`;

content = content.replace(oldRenderLeaderInfo, newRenderLeaderInfo);
content = content.replace(/\{renderLeaderInfo\(selectedElectricCarDept\)\}/g, '{renderLeaderInfo(selectedElectricCarDept, "electricCar")}');
content = content.replace(/\{renderLeaderInfo\(selectedDriverDept\)\}/g, '{renderLeaderInfo(selectedDriverDept, "driver")}');
content = content.replace(/\{renderLeaderInfo\(selectedRoomDept\)\}/g, '{renderLeaderInfo(selectedRoomDept, "room")}');
content = content.replace(/\{renderLeaderInfo\(selectedTeabreakDept\)\}/g, '{renderLeaderInfo(selectedTeabreakDept, "teabreak")}');
content = content.replace(/\{renderLeaderInfo\(selectedOtherDept\)\}/g, '{renderLeaderInfo(selectedOtherDept, "other")}');

// 3. Fix tour guide select
const oldTourGuideSelect = `<select 
                              disabled={!isSetupEditable || tourGuide !== 'other'}
                              className="w-full max-w-sm px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors disabled:bg-gray-100 disabled:opacity-50 outline-none text-sm bg-white"
                            >
                              <option value="">Chọn từ danh sách tham dự...</option>
                              <option value="1">Trần Văn B (Phòng Hành chính)</option>
                              <option value="2">Lê Thị C (Phòng Tuyển sinh)</option>
                            </select>
                            <button disabled={!isSetupEditable || tourGuide !== 'other'} className="flex items-center gap-1 px-3 py-2 bg-orange-50/80 text-orange-600 border border-orange-200 font-bold rounded-lg text-sm hover:bg-orange-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed outline-none">
                              <Plus className="w-4 h-4" /> Thêm
                            </button>
                          </div>`;

const newTourGuideSelect = `<select 
                              value={selectedTourGuide}
                              onChange={(e) => setSelectedTourGuide(e.target.value)}
                              disabled={!isSetupEditable || tourGuide !== 'other'}
                              className="w-full max-w-sm px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors disabled:bg-gray-100 disabled:opacity-50 outline-none text-sm bg-white"
                            >
                              <option value="">Chọn từ danh sách tham dự...</option>
                              <option value="Trần Văn B (Phòng Hành chính)">Trần Văn B (Phòng Hành chính)</option>
                              <option value="Lê Thị C (Phòng Tuyển sinh)">Lê Thị C (Phòng Tuyển sinh)</option>
                            </select>
                            <button 
                              type="button"
                              onClick={() => {
                                if (selectedTourGuide && !addedTourGuides.includes(selectedTourGuide)) {
                                  setAddedTourGuides([...addedTourGuides, selectedTourGuide]);
                                  setSelectedTourGuide('');
                                }
                              }}
                              disabled={!isSetupEditable || tourGuide !== 'other' || !selectedTourGuide} 
                              className="flex items-center gap-1 px-3 py-2 bg-orange-50/80 text-orange-600 border border-orange-200 font-bold rounded-lg text-sm hover:bg-orange-100 transition-colors disabled:opacity-50 disabled:cursor-not-allowed outline-none cursor-pointer"
                            >
                              <Plus className="w-4 h-4" /> Thêm
                            </button>
                          </div>
                          {addedTourGuides.length > 0 && tourGuide === 'other' && (
                            <div className="mt-4 flex flex-wrap gap-2">
                              {addedTourGuides.map((guide) => (
                                <div key={guide} className="flex items-center gap-2 bg-white px-3 py-1.5 rounded-full border border-gray-200 shadow-sm text-sm">
                                  <div className="w-6 h-6 rounded-full bg-[#004c91] text-white flex items-center justify-center font-bold text-xs">
                                    {guide.charAt(0)}
                                  </div>
                                  <span className="font-medium text-gray-700">{guide}</span>
                                  {isSetupEditable && (
                                    <button
                                      type="button"
                                      onClick={() => setAddedTourGuides(addedTourGuides.filter((g) => g !== guide))}
                                      className="w-5 h-5 rounded-full hover:bg-red-50 text-gray-400 hover:text-red-500 flex items-center justify-center transition-colors outline-none ml-1"
                                    >
                                      <X className="w-3 h-3" />
                                    </button>
                                  )}
                                </div>
                              ))}
                            </div>
                          )}`;

content = content.replace(oldTourGuideSelect, newTourGuideSelect);

fs.writeFileSync('src/pages/dashboard/visit/VisitProcess.tsx', content);
