const fs = require('fs');
const path = require('path');

const filePath = path.join(__dirname, 'src/pages/dashboard/home/SharedDashboardView.tsx');
let content = fs.readFileSync(filePath, 'utf8');

// 1. Replace type definitions
content = content.replace(
  /category: 'Lịch đón khách' \| 'Thư mời' \| 'Đơn yêu cầu';/g,
  "category: 'Lời mời tham gia' | 'Đơn yêu cầu mượn đồ';"
);

// 2. Replace occurrences in code
content = content.replace(/'Thư mời'/g, "'Lời mời tham gia'");
content = content.replace(/'Đơn yêu cầu'/g, "'Đơn yêu cầu mượn đồ'");
content = content.replace(/"Thư mời"/g, '"Lời mời tham gia"');
content = content.replace(/"Đơn yêu cầu"/g, '"Đơn yêu cầu mượn đồ"');
content = content.replace(/Thư mời/g, "Lời mời tham gia");

// 3. Remove Lịch đón khách events from INITIAL_EVENTS
content = content.replace(/\{\s*id:\s*'e1'[\s\S]*?carBooking:.*?\n\s*\},\s*/g, '');

// 4. Update the detailed modal layout in 'Lời mời tham gia' category
const oldDetailModal = `{showDetailSection && (
                      <div className="mt-3 p-5 bg-white border border-orange-100 rounded-xl shadow-sm space-y-4 animate-fade-in-quick">
                        <div>
                          <h4 className="text-xs font-bold text-slate-800 mb-1 border-b pb-1">1. Thông tin người tạo</h4>
                          <p className="text-xs text-slate-600">Chi tiết về người liên hệ, đơn vị phụ trách đăng ký lịch</p>
                        </div>
                        <div>
                          <h4 className="text-xs font-bold text-slate-800 mb-1 border-b pb-1">2. Thông tin đoàn khách</h4>
                          <p className="text-xs text-slate-600">Tên cơ quan, thời gian, cơ sở hoạt động và mục đích đối ngoại</p>
                        </div>
                        <div>
                          <h4 className="text-xs font-bold text-slate-800 mb-1 border-b pb-1">3. Setup</h4>
                          <p className="text-xs text-slate-600">Tiêu chí bố trí tham quan, chương trình chi tiết & thành phần tham gia</p>
                        </div>
                        <div>
                          <h4 className="text-xs font-bold text-slate-800 mb-1 border-b pb-1">4. Detail setup</h4>
                          <p className="text-xs text-slate-600">Yêu cầu kỹ thuật về khẩu hiệu trình chiếu LED và công tác chuẩn bị đón tiếp Campus Tour</p>
                        </div>
                      </div>
                    )}`;

const newDetailModal = `{showDetailSection && (
                      <div className="mt-4 bg-white border border-orange-100 rounded-2xl shadow-sm overflow-hidden animate-fade-in-quick text-sm">
                        
                        {/* 1. Thông tin người tạo */}
                        <div className="p-5 border-b border-orange-100">
                          <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                            <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">1</span>
                            Thông tin người tạo
                          </h4>
                          <p className="text-xs text-slate-500 mb-4">Chi tiết về người liên hệ, đơn vị phụ trách đăng ký lịch</p>
                          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 bg-slate-50 p-4 rounded-xl text-xs">
                            <div>
                              <p className="text-slate-500 mb-1">Họ và tên</p>
                              <p className="font-bold text-slate-800">Nguyễn Hữu Trí</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Email</p>
                              <p className="font-bold text-slate-800">tringuyenh@fpt.edu.vn</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Đơn vị công tác</p>
                              <p className="font-bold text-slate-800">Office of International Affairs (OIA) & Staff Leaders</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Chức danh</p>
                              <p className="font-bold text-slate-800">Director of OIA</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Số điện thoại (SĐT)</p>
                              <p className="font-bold text-slate-800">0905.111.222</p>
                            </div>
                          </div>
                        </div>

                        {/* 2. Thông tin đoàn khách */}
                        <div className="p-5 border-b border-orange-100">
                          <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                            <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">2</span>
                            Thông tin đoàn khách
                          </h4>
                          <p className="text-xs text-slate-500 mb-4">Tên cơ quan, thời gian, cơ sở hoạt động và mục đích đối ngoại</p>
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50 p-4 rounded-xl text-xs mb-4">
                            <div>
                              <p className="text-slate-500 mb-1">Tên đoàn</p>
                              <p className="font-bold text-slate-800">15+ International Professors & 40 inbound exchange students</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Cơ sở đón tiếp</p>
                              <p className="font-bold text-slate-800">Grand Hall, Alpha Campus, FPT University</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Ngày bắt đầu</p>
                              <p className="font-bold text-slate-800">Thứ Tư, 26/8/2026</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1">Thời gian</p>
                              <p className="font-bold text-slate-800">18:00 - 22:00</p>
                            </div>
                          </div>
                          <div className="space-y-3 bg-slate-50 p-4 rounded-xl text-xs">
                            <div>
                              <p className="text-slate-500 mb-1 font-bold">Mục đích thăm</p>
                              <p className="text-slate-800">Giao lưu Tất niên đậm bản sắc văn hóa Việt dành cho đội ngũ giáo sư & sinh viên nước ngoài.</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-1 font-bold">Nội dung làm việc</p>
                              <p className="text-slate-800">Phiên làm việc phối hợp chặt chẽ giữa đơn vị chủ trì Đại học FPT cùng đoàn đối tác nhằm cụ thể hoá các hoạt động trao đổi văn hoá học thuật, rà soát chi tiết cơ sở vật chất hạ tầng của cơ sở Hòa Lạc phục vụ học viên quốc tế học tập, nghiên cứu và sinh hoạt đối ngoại.</p>
                            </div>
                          </div>
                        </div>

                        {/* 3. Setup */}
                        <div className="p-5 border-b border-orange-100">
                          <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                            <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">3</span>
                            Setup
                          </h4>
                          <p className="text-xs text-slate-500 mb-4">Tiêu chí bố trí tham quan, chương trình chi tiết & thành phần tham gia</p>
                          <div className="bg-slate-50 p-4 rounded-xl text-xs space-y-4">
                            <div>
                              <p className="text-slate-500 mb-1">Loại hình tham quan</p>
                              <p className="font-bold text-slate-800">Đón tiếp đoàn khách quốc tế và sự kiện</p>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-2 font-bold">Agenda chi tiết</p>
                              <table className="w-full text-left bg-white border border-slate-200 rounded-lg overflow-hidden">
                                <thead className="bg-[#004c91] text-white">
                                  <tr>
                                    <th className="p-2 w-1/4">Khung Giờ</th>
                                    <th className="p-2">Khung nội dung chi tiết đón tiếp & tham quan dự kiến</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  <tr className="border-b"><td className="p-2 font-bold">18:00 - 18:15</td><td className="p-2">Tập trung phái đoàn, đón tiếp xã giao sảnh Alpha, chụp hình lưu niệm check-in.</td></tr>
                                  <tr className="border-b"><td className="p-2 font-bold">18:15 - 19:30</td><td className="p-2">Làm việc trao đổi học thuật, thảo luận chi tiết hợp tác hành chính tại phòng họp VIP sảnh Alpha.</td></tr>
                                  <tr><td className="p-2 font-bold">19:30 - 22:00</td><td className="p-2">Campus Tour: Di chuyển bằng xe điện tham quan khu phát triển công nghệ cao, Thư viện số và chào tạm biệt đoàn.</td></tr>
                                </tbody>
                              </table>
                            </div>
                            <div>
                              <p className="text-slate-500 mb-2 font-bold">Thành phần tham gia</p>
                              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                                <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Host</p><p className="font-bold">Nguyễn Văn A</p></div>
                                <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người hỗ trợ bên IC</p><p className="font-bold">Nguyễn Văn B</p></div>
                                <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người thuộc phòng ban khác</p><p className="font-bold">Nguyễn Văn C</p></div>
                                <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Sinh viên hỗ trợ</p><p className="font-bold">Nguyễn Văn D</p></div>
                              </div>
                            </div>
                          </div>
                        </div>

                        {/* 4. Detail setup */}
                        <div className="p-5 bg-orange-50/50">
                          <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                            <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">4</span>
                            Detail setup
                          </h4>
                          <p className="text-xs text-slate-500 mb-4">Yêu cầu kỹ thuật về khẩu hiệu trình chiếu LED và công tác chuẩn bị đón tiếp Campus Tour</p>
                          <div className="space-y-4">
                            <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                              <h5 className="font-bold text-slate-800 border-b pb-2 mb-2 text-[13px]">Mục 1: Trình chiếu khẩu hiệu LED</h5>
                              <p className="mb-1"><span className="font-bold text-[#0aa14f]">Có sử dụng</span> <span className="text-slate-500">(Hiển thị chạy tự động dọc theo màn hình LED lớn sảnh chính đón khách)</span></p>
                              <p className="bg-slate-100 p-2 rounded text-[#f37021] font-bold text-center border border-slate-200 mt-2">"FPT UNIVERSITY LUNAR NEW YEAR EVE CELEBRATION FOR INTERNATIONALS"</p>
                            </div>
                            
                            <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                              <h5 className="font-bold text-slate-800 border-b pb-2 mb-3 text-[13px]">Mục 2: Chuẩn bị cho Campus Tour</h5>
                              <div className="space-y-3">
                                <div>
                                  <p className="font-bold text-[#004c91] mb-1">Phần 1: Người dẫn</p>
                                  <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Bố trí 02 Đại sứ sinh viên xuất sắc hướng dẫn dẫn đoàn và thuyết minh lưu loát bằng tiếng Anh/Việt.</p>
                                </div>
                                <div>
                                  <p className="font-bold text-[#004c91] mb-1">Phần 2: Xe điện</p>
                                  <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Chuẩn bị sẵn 01 xe điện sạc đầy pin 100%, bảo dưỡng lốp, lau dọn khu vực ghế tươm tất.</p>
                                </div>
                                <div>
                                  <p className="font-bold text-[#004c91] mb-1">Phần 3: Người lái</p>
                                  <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Cử cán bộ lái xe điện chuyên trách túc trực, trang phục lịch thiệp, an toàn.</p>
                                </div>
                              </div>
                            </div>
                          </div>
                        </div>

                      </div>
                    )}`;

content = content.replace(oldDetailModal, newDetailModal);

// Clean up any remaining Lịch đón khách
content = content.replace(/Lịch đón khách/g, "Lời mời tham gia");
content = content.replace(/'Lời mời tham gia' \| 'Lời mời tham gia' \| 'Đơn yêu cầu mượn đồ';/, "'Lời mời tham gia' | 'Đơn yêu cầu mượn đồ';");

fs.writeFileSync(filePath, content, 'utf8');
console.log('Done replacing.');
