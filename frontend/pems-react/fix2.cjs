const fs = require('fs');
const filePath = 'd:/ki9/PEMS/frontend/pems-react/src/pages/dashboard/home/SharedDashboardView.tsx';
let content = fs.readFileSync(filePath, 'utf8');

// 1. Remove the duplicate block
const duplicateStart = '                    <button\\r?\\n                      onClick={() => setShowDetailSection(!showDetailSection)}';
const regexStart = new RegExp('                  \\{\\/\\*.*?\\*\\/\\}\\r?\\n                  <div className="pt-2">\\r?\\n' + duplicateStart);
// Wait, regex might be tricky. Let's just find the index of "setShowDetailSection"
let firstIdx = content.indexOf('setShowDetailSection(!showDetailSection)');
let secondIdx = content.indexOf('setShowDetailSection(!showDetailSection)', firstIdx + 1);

if (secondIdx !== -1) {
    // Traverse backwards to find <div className="pt-2">
    const containerAnchor = '<div className="pt-2">';
    const containerStart = content.lastIndexOf(containerAnchor, secondIdx);
    
    // Traverse forwards to find the end of the duplicate block
    const nextAnchor = '<div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">';
    const containerEnd = content.indexOf(nextAnchor, secondIdx);
    
    if (containerStart !== -1 && containerEnd !== -1) {
        // Also remove the "Xem chi tiết..." comment if it is before containerStart
        let actualStart = containerStart;
        const commentAnchor = '{/* ';
        const commentStart = content.lastIndexOf(commentAnchor, containerStart);
        if (commentStart !== -1 && (containerStart - commentStart) < 100) {
             // Let's just remove the exact span
             actualStart = content.lastIndexOf('                  ', containerStart);
             if (actualStart === -1) actualStart = containerStart;
        }

        content = content.substring(0, actualStart) + content.substring(containerEnd);
        console.log('Removed duplicate block');
    } else {
        console.log('Failed to find bounds of duplicate block');
    }
}

// 2. Replace INITIAL_EVENTS completely
const initEventsStart = 'const INITIAL_EVENTS: Event[] = [';
const startIndex2 = content.indexOf(initEventsStart);
const initEventsEndAnchor = 'const MOCK_STAFF_LIST';
const endIndex2 = content.indexOf(initEventsEndAnchor, startIndex2);

if (startIndex2 !== -1 && endIndex2 !== -1) {
    // We want to replace from startIndex2 to endIndex2. We need to preserve 'const MOCK_STAFF_LIST'
    // But we need to close the array properly.
    
    // Let's find the closing bracket before MOCK_STAFF_LIST
    const closingBracketIndex = content.lastIndexOf('];', endIndex2);
    
    if (closingBracketIndex !== -1 && closingBracketIndex > startIndex2) {
        const newEvents = `const INITIAL_EVENTS: Event[] = [
  {
    id: 'e-invitation-8',
    title: 'Thư mời tham gia sự kiện',
    date: '2026-08-08',
    time: '14:00 - 16:30',
    category: 'Lời mời tham gia',
    color: 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
    hoverColor: 'border-emerald-500',
    location: 'Hội trường sảnh tòa nhà Alpha',
    host: 'Nguyễn Văn A',
    guests: 'Đoàn đối tác Nhật Bản',
    checklist: [],
    purpose: 'Trân trọng kính mời anh/chị tham gia tiếp đón và giao lưu cùng đoàn đối tác từ Nhật Bản. Sự kiện diễn ra tại hội trường sảnh tòa nhà Alpha, sau đó di chuyển tham quan.\\n\\nVui lòng chuẩn bị tài liệu liên quan để trao đổi hợp tác.',
    vipLevel: 'Standard',
    contactPerson: 'Nguyễn Văn A'
  },
  {
    id: 'safuri-car-event',
    title: 'Yêu cầu mượn xe điện cho đoàn khách Safuri',
    date: '2026-08-08',
    time: '08:00 - 17:30',
    category: 'Đơn yêu cầu mượn đồ',
    color: 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100',
    hoverColor: 'border-orange-500',
    location: 'Campus Hòa Lạc, Đại học FPT',
    host: 'Phòng Hậu cần & Đội xe điện',
    guests: 'Đoàn khách Safuri',
    checklist: ['Kiểm tra xe bảo dưỡng', 'Cử tài xế túc trực', 'Hoàn tất biên bản bàn giao'],
    purpose: 'Mượn xe điện phục vụ di chuyển đoàn khách Safuri tham quan doanh nghiệp và campus.',
    vipLevel: 'VIP',
    contactPerson: 'Trần Văn Tuyến (Điều hành xe - 0914.555.666)'
  }
];`;
        content = content.substring(0, startIndex2) + newEvents + content.substring(closingBracketIndex + 2);
        console.log('Replaced INITIAL_EVENTS');
    }
} else {
    console.log('Failed to find INITIAL_EVENTS');
}

fs.writeFileSync(filePath, content, 'utf8');
console.log('Done.');
