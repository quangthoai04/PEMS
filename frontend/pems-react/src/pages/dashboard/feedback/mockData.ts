/**
 * Module Mock Data
 * Mô phỏng dữ liệu mock cho hệ thống đánh giá khách khi rời đi (Feedback Data).
 */

export const MOCK_VISIT_FEEDBACKS = [
  { 
    id: 1, 
    guestName: 'Đoàn khách Đại học Monash', 
    averageRating: 4.8, 
    date: '18/10/2026', 
    feedbacks: [
      { id: 101, reviewer: 'Nguyễn Văn C', rating: 5, spaceRating: 4, supportRating: 5, date: '18/10/2026', comment: 'Rất hài lòng với sự đón tiếp.' },
      { id: 102, reviewer: 'Trần Thị D', rating: 4, spaceRating: 5, supportRating: 4, date: '18/10/2026', comment: 'Không gian sạch sẽ, hướng dẫn nhiệt tình.' },
    ]
  },
  { 
    id: 2, 
    guestName: 'Đoàn trường THPT Lê Lợi', 
    averageRating: 4.5, 
    date: '19/10/2026', 
    feedbacks: [
      { id: 201, reviewer: 'Trần Thị B', rating: 4, spaceRating: 5, supportRating: 4, date: '19/10/2026', comment: 'Không gian sạch sẽ, hướng dẫn nhiệt tình.' },
      { id: 202, reviewer: 'Lê Văn C', rating: 5, spaceRating: 5, supportRating: 5, date: '19/10/2026', comment: 'Chương trình tham quan rất hay, các bạn tư vấn hỗ trợ nhiệt tình.' },
    ]
  },
  { 
    id: 3, 
    guestName: 'Đoàn Sở Giáo Dục Nam Định', 
    averageRating: 3.5, 
    date: '20/10/2026', 
    feedbacks: [
      { id: 301, reviewer: 'Phạm Thị A', rating: 3, spaceRating: 4, supportRating: 3, date: '20/10/2026', comment: 'Cần cải thiện tốc độ di chuyển giữa các trạm.' },
      { id: 302, reviewer: 'Nguyễn Văn B', rating: 4, spaceRating: 4, supportRating: 4, date: '20/10/2026', comment: 'Mọi thứ ổn, tuy nhiên cần có thêm thời gian nghỉ ngơi.' }
    ]
  },
  { 
    id: 4, 
    guestName: 'Phụ huynh trúng tuyển HN', 
    averageRating: 5.0, 
    date: '21/10/2026', 
    feedbacks: [
      { id: 401, reviewer: 'Lý Quốc D', rating: 5, spaceRating: 5, supportRating: 5, date: '21/10/2026', comment: 'Chương trình tham quan rất tuyệt vời.' }
    ]
  },
  { 
    id: 5, 
    guestName: 'Đoàn trường THPT FPT', 
    averageRating: 4.0, 
    date: '22/10/2026', 
    feedbacks: [
      { id: 501, reviewer: 'Vũ Văn E', rating: 4, spaceRating: 4, supportRating: 5, date: '22/10/2026', comment: 'Tiến độ tour hơi gấp nhưng tổng thể rất tốt.' }
    ]
  },
  { 
    id: 6, 
    guestName: 'FU Cần Thơ', 
    averageRating: 5.0, 
    date: '23/10/2026', 
    feedbacks: [
      { id: 601, reviewer: 'Lê T', rating: 5, spaceRating: 5, supportRating: 5, date: '23/10/2026', comment: '' }
    ]
  },
  { 
    id: 7, 
    guestName: 'Đoàn Sở Giáo Dục Vĩnh Phúc', 
    averageRating: 3.0, 
    date: '24/10/2026', 
    feedbacks: [
      { id: 701, reviewer: 'Nguyễn T', rating: 3, spaceRating: 3, supportRating: 3, date: '24/10/2026', comment: 'Tour diễn ra ổn nhưng thời tiết hơi nóng.' }
    ]
  },
];
