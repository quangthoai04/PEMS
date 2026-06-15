/**
 * Trang PartnersPage (Public)
 * Trang biểu thị danh mục lưới liên kết tổng hợp đối ngoại.
 */

// Đây là trang hiển thị danh sách tất cả các đối tác ở giao diện phía người dùng, tích hợp các tính năng Tìm kiếm, Bộ lọc quốc gia, và Bảng đối tác phân trang.
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  ArrowRight, 
  Search, 
  Globe, 
  MapPin, 
  ExternalLink, 
  ChevronLeft, 
  ChevronRight, 
  Sparkles, 
  Building2, 
  GraduationCap, 
  Layers, 
  ArrowUpRight, 
  FilterX,
  RefreshCw
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import GlobeComponent from '../components/partners/GlobeComponent';
import { VisitingFormPopup } from '../components/modals/VisitingFormPopup';

// Dynamically import partner logos from assets/Logo using Vite's glob function
const logoModules = import.meta.glob("../assets/Logo/*", {
  eager: true,
  import: "default",
});
const logoList = Object.values(logoModules) as string[];

export interface PartnerItem {
  id: number;
  name: string;
  code: string;
  country: string;
  type: "University" | "Enterprise" | "Academy" | "Research";
  web: string;
  campuses: string[];
  sector: string;
  category: string;
  description: string;
  email: string;
  phone: string;
  address: string;
  coverImage: string;
}

export const partnersData: PartnerItem[] = [
  { 
    id: 1, 
    name: "Đại học Deakin (Deakin University)", 
    code: "DEAKIN", 
    country: "Úc", 
    type: "University", 
    web: "https://www.deakin.edu.au", 
    campuses: ["Hà Nội", "Hồ Chí Minh"], 
    sector: "Trao đổi sinh viên quốc tế & chương trình cử nhân liên kết 2+2",
    category: "Đối tác Giáo dục",
    description: "Đại học Deakin là một trong những trường đại học công lập hàng đầu tại Úc, nổi tiếng với môi trường học thuật sáng tạo, chương trình đào tạo ứng dụng thực tế cao và cơ sở vật chất hiện đại bậc nhất thế giới. Trường liên tục nằm trong top 1% các trường đại học tốt nhất toàn cầu, đi đầu trong việc liên kết quốc tế và chuyển giao công nghệ giáo dục số hóa.",
    email: "international-study@deakin.edu.au",
    phone: "+61 3 9244 6333",
    address: "75 Burwood Highway, Burwood, Victoria 3125, Australia",
    coverImage: "https://images.unsplash.com/photo-1541339907198-e08756dedf3f?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 2, 
    name: "Tập đoàn công nghệ Panasonic", 
    code: "PANASONIC", 
    country: "Nhật Bản", 
    type: "Enterprise", 
    web: "https://holding.panasonic/global", 
    campuses: ["Hà Nội", "Đà Nẵng"], 
    sector: "Chương trình thực tập thực tế OJT & Phát triển giải pháp IoT thông minh",
    category: "Đối tác Doanh nghiệp",
    description: "Panasonic là tập đoàn sản xuất và phát triển công nghệ điện tử đa quốc gia hàng đầu thế giới của Nhật Bản. Hợp tác chiến lược giữa FPT và Panasonic tập trung vào chuyển giao giải pháp thông minh IoT, hệ thống nhúng tự động hóa, đồng thời tạo ra hàng trăm cơ hội thực tập On-the-Job Training chất lượng cao hàng năm cho thế hệ sinh viên kỹ thuật FPT.",
    email: "careers.vietnam@vn.panasonic.com",
    phone: "+84 24 3795 0101",
    address: "1006 Kadoma, Kadoma City, Osaka 571-8501, Japan",
    coverImage: "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 3, 
    name: "Đại học Chulalongkorn", 
    code: "CHULA", 
    country: "Thái Lan", 
    type: "University", 
    web: "https://www.chula.ac.th", 
    campuses: ["Hồ Chí Minh"], 
    sector: "Trao đổi học thuật vùng ASEAN & Học kỳ giao lưu văn hóa ngắn hạn",
    category: "Đối tác Giáo dục",
    description: "Được thành lập từ năm 1917, Chulalongkorn là trường đại học lâu đời và danh giá nhất Thái Lan. Trường là đại diện ưu tú dẫn đầu khu vực Đông Nam Á về chất lượng nghiên cứu khoa học, đào tạo nghệ thuật, văn hóa và quản lý kinh tế quốc tế, mở ra hướng hợp tác trao đổi sinh viên đa trải nghiệm đầy năng động.",
    email: "int.off@chula.ac.th",
    phone: "+66 2215 0871",
    address: "254 Phayathai Road, Wangmai, Pathumwan, Bangkok 10330, Thailand",
    coverImage: "https://images.unsplash.com/photo-1523050854058-8df90110c9f1?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 4, 
    name: "Yuan Ze University (Đại học Nguyên Trí)", 
    code: "YUANZE", 
    country: "Đài Loan", 
    type: "University", 
    web: "https://www.yzu.edu.tw", 
    campuses: ["Cần Thơ", "Hà Nội"], 
    sector: "Hợp tác đào tạo vi mạch bán dẫn & Liên kết thạc sĩ tài năng khoa học máy tính",
    category: "Đối tác Giáo dục",
    description: "Đại học Nguyên Trí là cơ sở đào tạo công nghệ tư thục hàng đầu tại Đài Loan, nổi trội về các nhóm ngành kỹ thuật vi mạch bán dẫn, sản xuất thông minh và khoa học máy tính. Sự liên kết bền vững giúp sinh viên FPTU tiếp cận công nghệ mới nhất về phát triển chip bán dẫn và cơ hội nhận học bổng kép giá trị lớn.",
    email: "admission@saturn.yzu.edu.tw",
    phone: "+886 3 463 8800",
    address: "135 Yuan-Ze Road, Chung-Li, Taoyuan 32003, Taiwan",
    coverImage: "https://images.unsplash.com/photo-1562774053-f5a02f68995b?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 5, 
    name: "HELP University (Đại học HELP)", 
    code: "HELP", 
    country: "Malaysia", 
    type: "University", 
    web: "https://help.edu.my", 
    campuses: ["Hà Nội", "Đà Nẵng"], 
    sector: "Kiểm định tiếng Anh học thuật & Chương trình trao đổi tín chỉ học tập",
    category: "Đối tác Giáo dục",
    description: "Đại học HELP là điểm sáng giáo dục đại học chất lượng của Malaysia, sở hữu các kiểm định uy tín quốc tế cho khối ngành kinh doanh, tâm lý học và công nghệ số. FPTU phối hợp chặt chẽ với HELP trong tổ chức các học kỳ học tiếng Anh chuyên sâu ngắn hạn cùng các dự án giao lưu khởi nghiệp năng động của học sinh hai nước.",
    email: "marketing@help.edu.my",
    phone: "+60 3 2716 2000",
    address: "No. 15, Jalan Sri Semantan 1, Off Jalan Semantan, Bukit Damansara, 50490 Kuala Lumpur, Malaysia",
    coverImage: "https://images.unsplash.com/photo-1527891751199-7225231a68dd?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 6, 
    name: "Học viện Ngôn ngữ FPT Japan (FJA)", 
    code: "FJA", 
    country: "Nhật Bản", 
    type: "Academy", 
    web: "https://www.fpt-japan-academy.jp", 
    campuses: ["Hà Nội", "Quy Nhơn", "TP. HCM"], 
    sector: "Nhập môn văn hóa Nhật Bản & Đào tạo cử nhân trở thành kỹ sư cầu nối (BrSE)",
    category: "Đối tác Thành viên",
    description: "Học viện Ngôn ngữ FPT Japan (FJA) là thành viên trực thuộc Tổ chức Giáo dục FPT tại Tokyo, Nhật Bản. Sứ mệnh của học viện là nâng đỡ và mài dũa kỹ năng ngoại ngữ chuyên sâu cùng phong cách làm việc bản địa chuyên nghiệp, mở đường cho hàng nghìn kỹ sư phần mềm Việt Nam thăng tiến vượt bậc trên thị trường quốc tế.",
    email: "info@fpt-japan-academy.jp",
    phone: "+81 3 5689 3131",
    address: "5F/6F PMO Yushima, 3-24-11 Yushima, Bunkyo-ku, Tokyo 113-0034, Japan",
    coverImage: "https://images.unsplash.com/photo-1503899036084-c55cdd92da26?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 7, 
    name: "Đại học Sydney (USYD)", 
    code: "SYDNEY", 
    country: "Úc", 
    type: "University", 
    web: "https://www.sydney.edu.au", 
    campuses: ["Hà Nội", "Hồ Chí Minh"], 
    sector: "Nghiên cứu chung lĩnh vực AI, Blockchain & Học bổng mùa hè quốc tế",
    category: "Đối tác Giáo dục",
    description: "Nằm trong mạng lưới danh giá toàn cầu, Đại học Sydney là biểu tượng lâu đời của hệ thống học thuật chất lượng danh vọng tại Úc. Sự hợp tác mang đến những dự án nghiên cứu quy mô tích hợp các ứng dụng đột phá của Trí tuệ nhân tạo (AI), thiết kế chuỗi khối (Blockchain) và hỗ trợ trao đổi giảng viên chất lượng cao.",
    email: "international.recruitment@sydney.edu.au",
    phone: "+61 2 8627 1444",
    address: "Camperdown NSW 2006, Sydney, Australia",
    coverImage: "https://images.unsplash.com/photo-1510519138101-570d1dca3d66?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 8, 
    name: "Đại học Swinburne (Swinburne University)", 
    code: "SWINBURNE", 
    country: "Úc", 
    type: "University", 
    web: "https://www.swinburne.edu.au", 
    campuses: ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng"], 
    sector: "Chương trình Liên kết bằng cấp toàn diện & Phối hợp đào tạo CNTT chuẩn Úc",
    category: "Đối tác Chiến lược",
    description: "Đại học Công nghệ Swinburne sở hữu vị trí đứng đầu trong lĩnh vực phát triển kỹ thuật số, lập trình ứng dụng và phương tiện truyền thông thông minh. Việc liên kết sâu rộng tạo điều kiện cho sinh viên liên thông hoàn tất bằng cấp quốc tế danh giá với giáo trình tiên tiến đạt chuẩn vàng của Úc.",
    email: "international@swin.edu.au",
    phone: "+61 3 9214 8000",
    address: "John St, Hawthorn, Victoria 3122, Australia",
    coverImage: "https://images.unsplash.com/photo-1509062522246-3755977927d7?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 9, 
    name: "Tập đoàn bán dẫn NVIDIA", 
    code: "NVIDIA", 
    country: "Mỹ", 
    type: "Enterprise", 
    web: "https://www.nvidia.com", 
    campuses: ["Hà Nội", "Hồ Chí Minh"], 
    sector: "Xây dựng phòng thực hành Ultra GPU Lab & Chuyển giao giáo trình kỹ sư AI",
    category: "Đối tác Công nghệ",
    description: "NVIDIA là tập đoàn tiên phong số một thế giới về đồ họa điện toán và điện toán trí tuệ nhân tạo (AI). Hợp tác mang tính bứt phá vượt bậc khi chuyển giao toàn diện giáo trình kỹ sư AI chính hãng cho FPTU, cùng dự án xây dựng các tổ hợp phòng lab trang bị siêu GPU siêu mạnh mẽ phục vụ giáo dục.",
    email: "edu-partner@nvidia.com",
    phone: "+1 408 486 2000",
    address: "2788 San Tomas Expressway, Santa Clara, CA 95051, USA",
    coverImage: "https://images.unsplash.com/photo-1518770660439-4636190af475?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 10, 
    name: "Tập đoàn công nghệ Microsoft", 
    code: "MICROSOFT", 
    country: "Mỹ", 
    type: "Enterprise", 
    web: "https://microsoft.com", 
    campuses: ["Hệ thống toàn quốc"], 
    sector: "Cấp chứng chỉ điện toán đám mây Azure & Đổi mới trang bị số cho sinh viên",
    category: "Đối tác Công nghệ",
    description: "Microsoft đồng hành cùng Tổ chức Giáo dục FPT thúc đẩy chuyển đổi số và nâng tầm hạ tầng học tập thế hệ mới. Toàn bộ sinh viên FPTU được cung cấp kho phần mềm bản quyền chính hãng và tiếp cận lộ trình thi chứng chỉ quốc tế uy tín trực tiếp trên nền tảng Azure.",
    email: "partner-support@microsoft.com",
    phone: "+1 800 642 7676",
    address: "One Microsoft Way, Redmond, WA 98052, USA",
    coverImage: "https://images.unsplash.com/photo-1497366216548-37526070297c?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 11, 
    name: "Singapore Management University (SMU)", 
    code: "SMU", 
    country: "Singapore", 
    type: "University", 
    web: "https://www.smu.edu.sg", 
    campuses: ["Hồ Chí Minh"], 
    sector: "Trại hè Khởi nghiệp Đổi mới sáng tạo & Đào tạo quản trị tài chính số Fintech",
    category: "Đối tác Giáo dục",
    description: "Đại học Quản lý Singapore (SMU) dẫn đầu Châu Á về phương pháp giáo dục mang phong cách buổi học thảo luận chuyên sâu kiểu Mỹ. Chương trình kết hợp ưu tiên đào tạo thế hệ quản lý doanh nghiệp tương lai, tài chính kỹ thuật số và tổ chức các hội nghị nghiên cứu đổi mới sáng tạo xã hội.",
    email: "smuadm@smu.edu.sg",
    phone: "+65 6828 0100",
    address: "81 Victoria St, Singapore 188065",
    coverImage: "https://images.unsplash.com/photo-1454165804606-c3d57bc86b40?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 12, 
    name: "Kyoto Computer Gakuin (KCG)", 
    code: "KCG", 
    country: "Nhật Bản", 
    type: "Academy", 
    web: "https://www.kcg.edu", 
    campuses: ["Đà Nẵng", "Quy Nhơn"], 
    sector: "Chương trình trao đổi sinh viên thiết kế đồ họa & Giao lưu văn hoá anime kỹ thuật số",
    category: "Đối tác Giáo dục",
    description: "Kyoto Computer Gakuin là trường đào tạo tin học đầu tiên của Nhật Bản với bề dày truyền thống lâu sắc. Sự hợp tác mang đến một dải chương trình chuyên sâu về kỹ thuật đồ họa game, thiết kế nhân vật manga 3D và các hoạt động hội trại mang màu sắc giao lưu văn hóa đặc thù của xứ sở hoa anh đào.",
    email: "admission@kcg.edu",
    phone: "+81 75 662 1200",
    address: "7 Monzen-cho, Nishikujo, Minami-ku, Kyoto 601-8407, Japan",
    coverImage: "https://images.unsplash.com/photo-1542051841857-5f90071e7989?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 13, 
    name: "Đại học Quốc gia Singapore (NUS)", 
    code: "NUS", 
    country: "Singapore", 
    type: "University", 
    web: "https://www.nus.edu.sg", 
    campuses: ["Hà Nội"], 
    sector: "Diễn đàn khoa học quốc tế thường niên & Trao đổi nghiên cứu sinh cao cấp",
    category: "Đối tác Giáo dục",
    description: "NUS là trường đại học quốc gia danh giá xếp hạng số một toàn Châu Á. FPTU phối hợp bền vững with NUS trong chế tác các hoạt động học thuật hội nghị quốc tế và trao đổi nghiên cứu sinh hàng đầu ngành kỹ thuật máy tính, trí tuệ nhân tạo và y sinh.",
    email: "nusreach@nus.edu.sg",
    phone: "+65 6516 6666",
    address: "21 Lower Kent Ridge Rd, Singapore 119077",
    coverImage: "https://images.unsplash.com/photo-1558021211-6d1403321394?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 14, 
    name: "Đại học Hàn Quốc (Korea University)", 
    code: "KOREA", 
    country: "Hàn Quốc", 
    type: "University", 
    web: "https://www.korea.edu", 
    campuses: ["Hệ thống toàn quốc"], 
    sector: "Học bổng ngôn ngữ Hàn và Trại giao lưu nghiên cứu trí tuệ nhân tạo trẻ",
    category: "Đối tác Giáo dục",
    description: "Đại học Hàn Quốc thuộc liên minh tinh hoa SKY - một trong những cơ sở giáo dục danh tiếng nhất Hàn Quốc. Sự hợp tác đem đến các hội trải hè văn hóa sôi động cùng lộ trình trao đổi dành riêng cho sinh viên ngành thiết kế đa phương tiện, ngôn ngữ Hàn Quốc.",
    email: "admission@korea.ac.kr",
    phone: "+82 2 3290 1114",
    address: "145 Anam-ro, Seongbuk-gu, Seoul 02841, South Korea",
    coverImage: "https://images.unsplash.com/photo-1533105079780-92b9be482077?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 15, 
    name: "Đại học Quốc gia Đông Hoa (NDHU)", 
    code: "NDHU", 
    country: "Đài Loan", 
    type: "University", 
    web: "https://www.ndhu.edu.tw", 
    campuses: ["Cần Thơ", "Hồ Chí Minh"], 
    sector: "Đào tạo hệ nhúng, kỹ sư điện tử thiết kế nguồn năng lượng xanh",
    category: "Đối tác Giáo dục",
    description: "Đại học Quốc gia Đông Hoa là đại học đa ngành uy tín nổi danh toàn Đài Loan với khuôn viên thơ mộng và chất lượng khoa học kỹ thuật vững thâm. Hai trường cùng phối hợp đắp nặn các hệ phân tích mạch tích hợp, kỹ nghệ nguồn điện tử thông minh và chuyển tiếp sinh viên.",
    email: "intl@ndhu.edu.tw",
    phone: "+886 3 890 3000",
    address: "No. 1, Sec. 2, Da Hsueh Rd, Shoufeng, Hualien 974301, Taiwan",
    coverImage: "https://images.unsplash.com/photo-1519074002996-a69e7ac46a42?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 16, 
    name: "Đại học Coimbra (University of Coimbra)", 
    code: "COIMBRA", 
    country: "Anh", 
    type: "University", 
    web: "https://www.uc.pt", 
    campuses: ["Đà Nẵng"], 
    sector: "Trao đổi giảng viên chương trình Châu Âu Erasmus+ & Nghiên cứu sử học mở",
    category: "Đối tác Giáo dục",
    description: "Đại học Coimbra sở hữu lịch sử vô tiền khoáng hậu khi có niên đại từ thế kỷ 13, được công nhận là Di sản Văn hóa Thế giới UNESCO. Chương trình cộng tác thúc đẩy giao thoa tri thức toàn cầu, chuyển giao kinh nghiệm thông qua dự án trao đổi giảng viên danh tiếng Erasmus+.",
    email: "dri@uc.pt",
    phone: "+351 239 859 900",
    address: "Paco das Escolas, 3004-531 Coimbra, Portugal",
    coverImage: "https://images.unsplash.com/photo-1506784983877-45594efa4cbe?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 17, 
    name: "Taylor's University", 
    code: "TAYLORS", 
    country: "Malaysia", 
    type: "University", 
    web: "https://university.taylors.edu.my", 
    campuses: ["Hồ Chí Minh", "Cần Thơ"], 
    sector: "Chuyển tiếp sinh viên ngành Quản trị Khách sạn bền vững & Khai phóng học thuật",
    category: "Đối tác Giáo dục",
    description: "Taylor's University là trường đại học tư thục hàng đầu tại Malaysia, xếp hạng số 1 về nhóm ngành Quản trị du lịch, ẩm thực và khách sạn cao cấp tại Đông Nam Á. Sự liên kết chặt chẽ mang tính thực tế giúp sinh viên FPT chạm tay vào trải nghiệm nghiệp vụ xa xỉ mang đẳng cấp quốc tế.",
    email: "admissions@taylors.edu.my",
    phone: "+60 3 5629 5000",
    address: "No. 1 Jalan Taylor's, 47500 Subang Jaya, Selangor, Malaysia",
    coverImage: "https://images.unsplash.com/photo-1560523160-754a9e25c68f?q=80&w=1200&auto=format&fit=crop"
  },
  { 
    id: 18, 
    name: "Nanyang Technological University (NTU)", 
    code: "NTU", 
    country: "Singapore", 
    type: "University", 
    web: "https://www.ntu.edu.sg", 
    campuses: ["Hà Nội", "Hồ Chí Minh"], 
    sector: "Nghiên cứu cơ bản vi xử lý lượng tử & Chuyển giao chương trình bán dẫn cao cấp",
    category: "Đối tác Chiến lược",
    description: "Đại học Công nghệ Nanyang (NTU) liên tục giữ vững ngôi vị á quân các bảng xếp hạng công nghệ xuất sắc toàn cầu. Hợp tác chuyển tiếp giáo dục tập trung đào tạo chuyên sâu khoa học thông tin lượng tử, sản xuất kỹ nghệ linh kiện tự động thông minh bậc cao.",
    email: "adm_local@ntu.edu.sg",
    phone: "+65 6791 1744",
    address: "50 Nanyang Ave, Singapore 639798",
    coverImage: "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?q=80&w=1200&auto=format&fit=crop"
  }
];

export function PartnersPage() {
  const [isVisitorFormOpen, setIsVisitorFormOpen] = useState(false);
  const navigate = useNavigate();

  // Search and Filter States
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCountry, setSelectedCountry] = useState("Tất cả quốc gia");
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 12; // 12 items per page for a balanced grid footprint

  // Extract unique countries list
  const countries = ["Tất cả quốc gia", ...Array.from(new Set(partnersData.map(p => p.country)))];

  // Helper to dynamically get the corresponding logo from the dynamic list
  const getLogo = (id: number) => {
    if (logoList && logoList.length > 0) {
      return logoList[(id - 1) % logoList.length];
    }
    return "";
  };

  // Reset page when queries/filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, selectedCountry]);

  // Filter Logic
  const filteredPartners = partnersData.filter(partner => {
    const matchesSearch = 
      partner.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
      partner.code.toLowerCase().includes(searchQuery.toLowerCase()) ||
      partner.sector.toLowerCase().includes(searchQuery.toLowerCase());
      
    const matchesCountry = 
      selectedCountry === "Tất cả quốc gia" || 
      partner.country === selectedCountry;

    return matchesSearch && matchesCountry;
  });

  // Pagination Logic
  const totalPages = Math.ceil(filteredPartners.length / itemsPerPage);
  const indexOfLastItem = currentPage * itemsPerPage;
  const indexOfFirstItem = indexOfLastItem - itemsPerPage;
  const currentPartners = filteredPartners.slice(indexOfFirstItem, indexOfLastItem);

  const paginate = (pageNumber: number) => {
    setCurrentPage(pageNumber);
    // Smooth scroll down to the table directory section of the page with slight timeout to handle render height adjustments
    setTimeout(() => {
      const tableElement = document.getElementById("partners-directory");
      if (tableElement) {
        tableElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }, 40);
  };

  const clearFilters = () => {
    setSearchQuery("");
    setSelectedCountry("Tất cả quốc gia");
  };

  return (
    <>
      <div className="pt-24 pb-20 bg-[#f8fafc] min-h-screen overflow-x-clip">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          
          {/* Top Hero and Globe Section */}
          <div className="flex flex-col lg:flex-row gap-12 lg:items-start items-center">
            
            {/* Left Content */}
            <div className="w-full lg:w-1/2 flex flex-col items-start relative z-10 lg:pt-10">
              {/* Tag */}
              <div className="flex items-center gap-4 mb-6">
                <div className="h-[2px] w-12 bg-[#f37021]"></div>
                <span className="text-[#f37021] font-bold tracking-widest uppercase text-xs">
                  FPT University
                </span>
                <div className="h-[2px] w-12 bg-[#f37021]"></div>
              </div>

              {/* Slogan */}
              <h1 className="text-5xl md:text-6xl lg:text-[76px] font-black leading-[1.1] mb-6">
                <span className="text-[#004c91] block mb-2 tracking-tight">Kết sức mạnh,</span>
                <span className="text-[#f37021] block tracking-tight">Nối tầm nhìn</span>
              </h1>

              {/* Subtitle */}
              <p className="text-gray-500 text-[14px] md:text-[15px] font-medium mb-8 max-w-[480px] leading-relaxed">
                Cộng hưởng giá trị tri thức toàn cầu — Bệ phóng cho những ý tưởng đổi mới và khởi nghiệp thành công.
              </p>

              {/* Buttons */}
              <div className="flex flex-col sm:flex-row gap-4 w-full sm:w-auto">
                <button 
                  onClick={() => setIsVisitorFormOpen(true)}
                  className="flex justify-center items-center gap-2 px-8 py-3.5 bg-[#f37021] text-white font-bold rounded-lg shadow-[0_8px_25px_rgba(243,112,33,0.35)] hover:-translate-y-1 hover:shadow-[0_12px_30px_rgba(243,112,33,0.45)] transition-all duration-300 group text-[15px] cursor-pointer"
                >
                  Đăng ký ghé thăm
                  <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                </button>
                
                <button 
                  onClick={() => navigate('/visit-fptu')}
                  className="flex justify-center items-center px-8 py-3.5 bg-white text-gray-600 font-bold border-[1.5px] border-gray-200 rounded-lg hover:text-white hover:bg-[#004c91] hover:border-[#004c91] hover:shadow-lg transition-all duration-300 text-[15px] cursor-pointer"
                >
                  Khám phá trực tuyến
                </button>
              </div>

              {/* Statistics */}
              <div className="w-full mt-12 pt-8 border-t border-gray-200/80">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6 text-center divide-y md:divide-y-0 md:divide-x divide-gray-200/80">
                  {/* Stat 1 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">180+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Đối tác chiến lược:</strong> Mạng lưới các tập đoàn lớn (Nvidia, Microsoft, AWS...) khẳng định uy tín toàn cầu.
                    </span>
                  </div>
                  {/* Stat 2 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">200+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Tổ chức giáo dục:</strong> Mở rộng hợp tác, mang đến môi trường học thuật và cơ hội trao đổi quốc tế đa dạng.
                    </span>
                  </div>
                  {/* Stat 3 */}
                  <div className="flex flex-col items-center px-2 py-4 md:py-0">
                    <span className="text-[40px] leading-tight font-black text-[#f37021] mb-2 tracking-tight">40+</span>
                    <span className="text-gray-500 text-sm leading-relaxed max-w-[200px]">
                      <strong>Quốc gia & Vùng lãnh thổ:</strong> Dấu ấn toàn cầu, đón hơn 2.000 sinh viên quốc tế đến Việt Nam mỗi năm.
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Right Globe */}
            <div className="w-full lg:w-1/2 h-[450px] sm:h-[550px] lg:h-[700px] flex items-start justify-center relative mt-10 lg:mt-0 lg:-mr-32">
              {/* Soft glow behind the globe */}
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-sky-200/30 blur-[100px] rounded-full z-0"></div>
              <GlobeComponent onSelectCountry={(country) => {
                setSelectedCountry(country);
                setTimeout(() => {
                  const tableElement = document.getElementById("partners-directory");
                  if (tableElement) {
                    tableElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
                  }
                }, 80);
              }} />
            </div>

          </div>

          {/* Interactive Partners Directory Section */}
          <div id="partners-directory" className="mt-20 pt-16 border-t border-slate-200 relative">
            <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[40%] h-[150px] bg-gradient-to-b from-[#004c91]/5 to-transparent blur-[80px] rounded-full pointer-events-none"></div>

            {/* Header of Section */}
            <div className="text-center max-w-3xl mx-auto mb-12">
              <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-sky-50 text-[#004c91] border border-sky-100 rounded-full text-xs font-bold uppercase tracking-wider mb-4">
                <Sparkles className="w-4 h-4 text-[#f37021]" />
                Mạng Lưới Quốc Tế
              </div>
              <h2 className="text-3xl md:text-4xl font-black text-slate-800 tracking-tight leading-tight">
                Danh sách <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#004c91] to-[#f37021]">đối tác chiến lược & liên kết</span>
              </h2>
              <p className="text-slate-500 text-sm md:text-base mt-2">
                Hỗ trợ tra cứu nhanh chóng cơ sở liên kết, thông tin lĩnh vực hợp tác học thuật của trường Đại học FPT với các tổ chức hàng đầu thế giới.
              </p>
            </div>

            {/* SEARCH AND FILTERS GRID PANEL */}
            <div className="bg-white border border-slate-100 rounded-2xl shadow-lg p-6 md:p-8 mb-8 relative z-20">
              <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-end">
                
                {/* Search Bar - column col-span-12 or md:col-span-8 */}
                <div className="md:col-span-8 flex flex-col gap-2">
                  <label htmlFor="partner-name-search" className="text-xs font-bold text-slate-700 uppercase tracking-widest pl-1">
                    Tên đối tác
                  </label>
                  <div className="relative group/search">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none">
                      <Search className="w-5 h-5 text-slate-400 group-focus-within/search:text-[#f37021] transition-colors" />
                    </div>
                    <input
                      id="partner-name-search"
                      type="text"
                      placeholder="Nhập tên đối tác, mã Code (VD: Nvidia, Swinburne, HELP...)"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full pl-11 pr-10 py-3.5 bg-slate-50/50 hover:bg-slate-50 border border-slate-200 hover:border-slate-300 focus:border-[#f37021] focus:bg-white text-slate-800 text-sm font-medium rounded-xl outline-none transition-all focus:ring-4 focus:ring-orange-100 shadow-inner"
                    />
                    {searchQuery && (
                      <button 
                        onClick={() => setSearchQuery("")}
                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600 focus:outline-none text-xs font-bold cursor-pointer"
                      >
                        Xóa
                      </button>
                    )}
                  </div>
                </div>

                {/* Dropdownlist for country filter - column col-span-12 or md:col-span-4 */}
                <div className="md:col-span-4 flex flex-col gap-2">
                  <label htmlFor="country-filter" className="text-xs font-bold text-slate-700 uppercase tracking-widest pl-1">
                    Quốc gia
                  </label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none">
                      <Globe className="w-5 h-5 text-slate-400" />
                    </div>
                    <select
                      id="country-filter"
                      value={selectedCountry}
                      onChange={(e) => setSelectedCountry(e.target.value)}
                      className="w-full pl-11 pr-10 py-3.5 bg-slate-50/50 border border-slate-200 hover:border-slate-300 focus:border-[#f37021] focus:bg-white text-slate-800 text-sm font-semibold rounded-xl outline-none transition-all cursor-pointer focus:ring-4 focus:ring-orange-100 appearance-none shadow-inner"
                    >
                      {countries.map(country => (
                        <option key={country} value={country} className="font-medium text-slate-800 py-2">
                          {country}
                        </option>
                      ))}
                    </select>
                    {/* Visual caret down decoration */}
                    <div className="absolute inset-y-0 right-0 pr-4 flex items-center pointer-events-none text-slate-400">
                      <ChevronRight className="w-4 h-4 rotate-90" />
                    </div>
                  </div>
                </div>

              </div>

              {/* Filter metrics status */}
              {(searchQuery || selectedCountry !== "Tất cả quốc gia") && (
                <div className="mt-5 flex items-center justify-between flex-wrap gap-3 bg-orange-50/40 border border-orange-100/50 rounded-xl p-3.5">
                  <div className="flex items-center gap-2 text-xs font-medium text-slate-700">
                    <Layers className="w-4 h-4 text-[#f37021]" />
                    Tìm thấy <span className="font-bold text-[#f37021]">{filteredPartners.length}</span> kết quả phù hợp cho bộ lọc hiện tại.
                  </div>
                  <button 
                    onClick={clearFilters}
                    className="flex items-center gap-1.5 text-xs font-bold text-[#004c91] hover:text-[#f37021] transition-colors focus:outline-none"
                  >
                    <FilterX className="w-4 h-4" /> Làm mới bộ lọc
                  </button>
                </div>
              )}
            </div>

            {/* MAIN PARTNER DIRECTORY / GRID BENTO LAYOUT */}
            <div className="mb-12 relative z-10">
              <AnimatePresence mode="popLayout">
                {currentPartners.length > 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
                    {currentPartners.map((partner, index) => {
                      const logoSrc = getLogo(partner.id);
                      
                      return (
                        <motion.div
                          key={partner.id}
                          initial={{ opacity: 0, y: 25, scale: 0.95 }}
                          animate={{ opacity: 1, y: 0, scale: 1 }}
                          exit={{ opacity: 0, scale: 0.95 }}
                          transition={{ duration: 0.3, delay: (index % 4) * 0.05, ease: "easeOut" }}
                          onClick={() => navigate(`/partners/${partner.id}`)}
                          className="group bg-white rounded-3xl p-6 flex flex-col items-center text-center shadow-[0_5px_20px_-3px_rgba(0,0,0,0.05)] hover:shadow-[0_20px_40px_-5px_rgba(0,76,145,0.12)] hover:-translate-y-2.5 transition-all duration-300 relative overflow-hidden cursor-pointer"
                        >
                          {/* Modern glowing accents in gradient matching FPT University */}
                          <div className="absolute top-0 left-0 w-full h-[3px] bg-gradient-to-r from-[#004c91] via-orange-400 to-[#f37021] transform origin-left scale-x-0 group-hover:scale-x-100 transition-transform duration-500" />
                          <div className="absolute inset-0 bg-gradient-to-b from-[#004c91]/2 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none" />
                          
                          {/* 1. Dynamic Floating Small Logo Container */}
                          <div className="w-full h-24 flex items-center justify-center p-2 transition-all duration-300 relative my-1">
                            {logoSrc ? (
                              <img 
                                src={logoSrc} 
                                alt={partner.name} 
                                className="max-w-[85%] max-h-[85%] object-contain scale-100 group-hover:scale-110 transition-transform duration-500"
                                referrerPolicy="no-referrer"
                              />
                            ) : (
                              <div className="w-14 h-14 bg-[#004c91]/5 text-[#004c91] font-black text-lg rounded-full flex items-center justify-center uppercase border border-[#004c91]/10 shadow-inner">
                                {partner.code.slice(0, 2)}
                              </div>
                            )}
                          </div>

                          {/* 2. Partner Name in center – default to brand navy blue #004c91 */}
                          <div className="flex-grow flex flex-col justify-center my-3 w-full">
                            <h3 className="text-[17px] font-black text-[#004c91] leading-snug group-hover:text-orange-600 transition-colors line-clamp-2 px-1">
                              {partner.name}
                            </h3>
                          </div>

                          {/* Thin decorative dotted divider line */}
                          <div className="w-full border-t border-dashed border-slate-100 my-2" />

                          {/* 3. Partner Country Badge (Centered, no external link button) */}
                          <div className="w-full flex justify-center mt-2.5">
                            <div className="inline-flex items-center gap-1.5 py-1.5 px-4 bg-[#004c91]/5 border border-[#004c91]/10 text-[#004c91] text-xs font-bold rounded-xl group-hover:bg-orange-50 group-hover:border-orange-200/50 group-hover:text-[#f37021] transition-all duration-300 shadow-sm shadow-[#004c91]/2">
                              <MapPin className="w-3.5 h-3.5 text-[#f37021]" />
                              <span>{partner.country}</span>
                            </div>
                          </div>

                        </motion.div>
                      );
                    })}
                  </div>
                ) : (
                  <div className="bg-white rounded-2xl border border-slate-100 shadow-xl py-16 px-6 text-center">
                    <div className="max-w-md mx-auto flex flex-col items-center">
                      <div className="w-16 h-16 rounded-full bg-slate-50 flex items-center justify-center text-slate-355 mb-4 animate-pulse">
                        <RefreshCw className="w-8 h-8 animate-spin" style={{ animationDuration: '3s' }} />
                      </div>
                      <h3 className="text-base font-bold text-slate-700 mb-1">
                        Không tìm thấy đối tác liên kết
                      </h3>
                      <p className="text-xs text-slate-400 max-w-sm mb-4">
                        Không tìm thấy kết quả phù hợp với từ khóa "<strong>{searchQuery}</strong>" trong danh mục quốc gia "<strong>{selectedCountry}</strong>".
                      </p>
                      <button 
                        onClick={clearFilters}
                        className="px-5 py-2.5 bg-[#004c91] hover:bg-[#f37021] text-white text-xs font-bold rounded-lg transition-all shadow-md active:scale-95 cursor-pointer"
                      >
                        Thử lại tất cả đối tác
                      </button>
                    </div>
                  </div>
                )}
              </AnimatePresence>

              {/* PAGINATION PANEL FOR MODERN CARD GRID */}
              {totalPages >= 1 && (
                <div className="mt-12 flex justify-center w-full">
                  
                  {/* Navigation key buttons */}
                  <div className="flex items-center gap-1.5 justify-center">
                    
                    {/* Previous Button */}
                    <button
                      type="button"
                      onClick={(e) => {
                        e.preventDefault();
                        e.currentTarget.blur();
                        if (currentPage > 1) {
                          paginate(currentPage - 1);
                        }
                      }}
                      disabled={currentPage === 1}
                      className={`w-9 h-9 rounded-xl border flex items-center justify-center transition-all ${
                        currentPage === 1 
                          ? "bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed" 
                          : "bg-white border-slate-200 text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] cursor-pointer shadow-sm hover:shadow"
                      }`}
                      title="Trang trước"
                    >
                      <ChevronLeft className="w-4 h-4" />
                    </button>

                    {/* Numeric buttons generator */}
                    {Array.from({ length: totalPages }).map((_, i) => {
                      const pageNum = i + 1;
                      const isActive = pageNum === currentPage;
                      
                      return (
                        <button
                          key={pageNum}
                          type="button"
                          onClick={(e) => {
                            e.preventDefault();
                            e.currentTarget.blur();
                            paginate(pageNum);
                          }}
                          className={`w-9 h-9 rounded-xl font-bold text-xs transition-all flex items-center justify-center border cursor-pointer shadow-sm hover:shadow ${
                            isActive 
                              ? "bg-[#f37021] border-[#f37021] text-white shadow-md shadow-orange-500/10 scale-105" 
                              : "bg-white border-slate-200 text-slate-600 hover:bg-[#004c91]/10 hover:border-[#004c91]/20 hover:text-[#004c91]"
                          }`}
                        >
                          {pageNum}
                        </button>
                      );
                    })}

                    {/* Next Button */}
                    <button
                      type="button"
                      onClick={(e) => {
                        e.preventDefault();
                        e.currentTarget.blur();
                        if (currentPage < totalPages) {
                          paginate(currentPage + 1);
                        }
                      }}
                      disabled={currentPage === totalPages}
                      className={`w-9 h-9 rounded-xl border flex items-center justify-center transition-all ${
                        currentPage === totalPages 
                          ? "bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed" 
                          : "bg-white border-slate-200 text-slate-600 hover:bg-[#004c91] hover:text-white hover:border-[#004c91] cursor-pointer shadow-sm hover:shadow"
                      }`}
                      title="Trang tiếp"
                    >
                      <ChevronRight className="w-4 h-4" />
                    </button>

                  </div>

                </div>
              )}
            </div>

            {/* Quick Strategic Footer Call to Action Banner */}
            <div className="relative rounded-3xl overflow-hidden bg-gradient-to-br from-sky-50/50 via-white to-orange-50/30 p-8 md:p-12 shadow-[0_20px_50px_-12px_rgba(0,76,145,0.08)] hover:shadow-[0_30px_70px_-10px_rgba(243,112,33,0.15)] border border-slate-100 hover:border-orange-200/30 hover:-translate-y-1.5 transition-all duration-500 group">
              
              {/* Dual custom gradient washes (blue left, orange right) */}
              <div className="absolute top-0 left-0 bottom-0 w-1/2 bg-gradient-to-r from-[#004c91]/5 via-sky-500/1 to-transparent pointer-events-none rounded-l-3xl transition-opacity duration-700" />
              <div className="absolute top-0 right-0 bottom-0 w-1/2 bg-gradient-to-l from-[#f37021]/8 via-orange-400/2 to-transparent pointer-events-none rounded-r-3xl transition-opacity duration-700" />
              
              {/* Elevated animated glowing orbs - Orange on right, Blue on left */}
              <div className="absolute -top-[20%] -right-[10%] w-[420px] h-[420px] bg-gradient-to-br from-[#f37021]/15 to-orange-300/5 rounded-full blur-[90px] pointer-events-none group-hover:scale-115 transition-transform duration-1000" />
              <div className="absolute -bottom-[20%] -left-[10%] w-[380px] h-[380px] bg-gradient-to-tr from-[#004c91]/15 to-sky-400/5 rounded-full blur-[90px] pointer-events-none group-hover:scale-115 transition-transform duration-1000" />
              
              <div className="relative z-10 grid grid-cols-1 lg:grid-cols-12 gap-8 items-center">
                {/* Left side info column */}
                <div className="lg:col-span-7 space-y-5">
                  <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#004c91]/5 border border-[#004c91]/10 text-[#004c91] text-xs font-black uppercase tracking-wider">
                    <Globe className="w-3.5 h-3.5 text-[#f37021] animate-spin" style={{ animationDuration: '20s' }} />
                    <span>Mạng Lưới Toàn Cầu</span>
                  </div>
                  
                  <h3 className="text-3xl md:text-4xl font-black tracking-tight leading-tight">
                    <span className="text-[#004c91]">Bạn mong muốn cùng xây dựng</span> <br className="hidden sm:inline" />
                    <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#004c91] to-[#f37021] font-extrabold">
                      tương lai giáo dục?
                    </span>
                  </h3>
                  
                  <p className="text-slate-600 text-[15px] font-medium leading-relaxed max-w-xl">
                    Đại học FPT liên tục đồng hành cùng đối tác chiến lược quốc tế, kiến tạo môi trường học tập không giới hạn cho sinh viên toàn cầu. Đăng ký để thảo luận chương trình liên kết đào tạo, chuyển giao học thuật ngay hôm nay.
                  </p>

                  {/* Trust markers */}
                  <div className="pt-2 flex flex-wrap gap-5 text-xs text-slate-500 font-bold">
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Hỗ trợ 24/7
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Quy trình tinh gọn
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="w-4 h-4 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center font-black">✓</span>
                      Hơn 40 quốc gia
                    </div>
                  </div>
                </div>

                {/* Right side contact CTA & stats */}
                <div className="lg:col-span-5 flex flex-col justify-center items-center lg:items-end gap-6">
                  {/* Micro dashboard layout inside cards */}
                  <div className="grid grid-cols-2 gap-4 w-full max-w-sm">
                    <div className="bg-white/70 backdrop-blur-sm p-4 rounded-2xl border border-slate-100 shadow-sm text-center">
                      <div className="text-2xl font-black text-[#004c91]">40+</div>
                      <div className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Quốc Gia</div>
                    </div>
                    <div className="bg-white/70 backdrop-blur-sm p-4 rounded-2xl border border-slate-100 shadow-sm text-center">
                      <div className="text-2xl font-black text-[#f37021]">500+</div>
                      <div className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Đối Tác Quốc Tế</div>
                    </div>
                  </div>

                  {/* Main Call to Action button */}
                  <div className="w-full max-w-sm text-center lg:text-right">
                    <button 
                      onClick={() => setIsVisitorFormOpen(true)}
                      className="w-full px-6 py-4 bg-gradient-to-r from-[#004c91] to-[#0461b5] hover:from-[#f37021] hover:to-orange-600 font-extrabold text-sm rounded-2xl text-center text-white shadow-lg shadow-[#004c91]/25 hover:shadow-orange-500/25 transition-all duration-300 flex items-center justify-center gap-2 hover:-translate-y-1 active:translate-y-0 cursor-pointer"
                    >
                      <span>Gửi yêu cầu hợp tác</span>
                      <ArrowUpRight className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>
            </div>

          </div>

        </div>
      </div>
      <VisitingFormPopup isOpen={isVisitorFormOpen} onClose={() => setIsVisitorFormOpen(false)} />
    </>
  );
}
