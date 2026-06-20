/**
 * Khai báo Component/Trang: PartnerManagement
 * Thuộc cấu trúc: partners
 * Chức năng: Hiển thị giao diện và logic liên quan đến PartnerManagement
 */

// Trang quản lý đối tác (Partner Management) trong Dashboard quản trị, dùng để hiển thị, tìm kiếm, thêm, sửa và xóa thông tin các trường đại học đối tác quốc tế.
import React, { useState } from "react";
import {
  Search,
  Plus,
  Eye,
  Check,
  X,
  ChevronLeft,
  ChevronRight,
  MapPin,
  Globe2,
} from "lucide-react";
import { useNavigate } from "react-router-dom";

const logoModules = import.meta.glob("../../../assets/Logo/*", {
  eager: true,
});
const logoList = Object.values(logoModules).map((m: any) => m.default || m) as string[];

const campuses = ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Quy Nhơn"];
const creators = [
  "Nguyễn Văn A",
  "Nguyễn Văn B",
  "Trần Thị B",
  "Lê Văn C",
  "Phạm Thị D",
  "Admin",
];

const baseData = [
  {
    id: 1,
    code: "Deakin",
    name: "Đại học Deakin",
    country: "Úc",
    status: "Đã duyệt",
    creator: "Nguyễn Văn A",
    campus: "Hà Nội",
  },
  {
    id: 2,
    code: "Pana",
    name: "Tập đoàn Panasonic",
    country: "Nhật Bản",
    status: "Đã duyệt",
    creator: "Admin",
    campus: "Hà Nội",
  },
  {
    id: 3,
    code: "Chula",
    name: "Đại học Chulalongkorn",
    country: "Thái Lan",
    status: "Chờ duyệt",
    creator: "Trần Thị B",
    campus: "Hồ Chí Minh",
  },
  {
    id: 4,
    code: "AirTransat",
    name: "AirTransat",
    country: "Brazil",
    status: "Từ chối",
    creator: "Lê Văn C",
    campus: "Đà Nẵng",
  },
  {
    id: 5,
    code: "FPT",
    name: "Đại học FPT",
    country: "Việt Nam",
    status: "Đã duyệt",
    creator: "Nguyễn Văn B",
    campus: "Hà Nội",
  },
  {
    id: 6,
    code: "HELP",
    name: "HELP UNIVERSITY",
    country: "Malaysia",
    status: "Đã duyệt",
    creator: "Nguyễn Văn B",
    campus: "Hà Nội",
  },
  {
    id: 7,
    code: "Oshuku",
    name: "Mori no Kaze Oshuku",
    country: "Nhật Bản",
    status: "Đã duyệt",
    creator: "Trần Thị B",
    campus: "Hồ Chí Minh",
  },
  {
    id: 8,
    code: "Yuan",
    name: "Yuan Ze University",
    country: "Đài Loan",
    status: "Từ chối",
    creator: "Lê Văn C",
    campus: "Đà Nẵng",
  },
  {
    id: 9,
    code: "FJA",
    name: "FPT Japan Academy",
    country: "Nhật Bản",
    status: "Đã duyệt",
    creator: "Phạm Thị D",
    campus: "Cần Thơ",
  },
  {
    id: 10,
    code: "Sydney",
    name: "Đại học Sydney",
    country: "Úc",
    status: "Chờ duyệt",
    creator: "Nguyễn Văn A",
    campus: "Hà Nội",
  },
];

const mockPartnerData = [...baseData];
const countries = [
  "Việt Nam",
  "Nhật Bản",
  "Trung Quốc",
  "Thái Lan",
  "Úc",
  "Singapore",
  "Malaysia",
  "Đài Loan",
  "Hàn Quốc",
  "Mỹ",
  "Anh",
];
const statuses = ["Đã duyệt", "Chờ duyệt", "Từ chối"];

// Generate up to 24
for (let i = 11; i <= 24; i++) {
  mockPartnerData.push({
    id: i,
    code: `PARTNER${i.toString().padStart(3, "0")}`,
    name: `Đối tác Liên kết ${i}`,
    country: countries[i % countries.length],
    status: statuses[i % 3],
    creator: creators[i % creators.length],
    campus: campuses[i % campuses.length],
  });
}

export function PartnerManagement() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase() || "";
  const isStudent = userRole === "STUDENT";
  const isStaff = userRole === "STAFF";
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isAdmin = userRole === "ADMIN" || isStaffLeader;
  const isHO = userRole === "HO";

  const [data, setData] = useState(mockPartnerData);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(isStudent ? 12 : 5);

  const [searchQuery, setSearchQuery] = useState("");
  const [countryFilter, setCountryFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [campusFilter, setCampusFilter] = useState("");

  if (!["STAFF", "ADMIN", "HO", "STUDENT"].includes(userRole)) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center bg-slate-50/50">
        <div className="text-center bg-white p-8 rounded-xl shadow-sm border border-gray-100">
          <h2 className="text-2xl font-bold text-[#004c91] mb-2">
            Không có quyền truy cập
          </h2>
          <p className="text-gray-500">
            Trang này chỉ dành cho tài khoản nội bộ hoặc sinh viên.
          </p>
          <button
            onClick={() => navigate("/dashboard")}
            className="mt-6 px-6 py-2 bg-[#004c91] hover:bg-[#003a70] text-white rounded-lg font-medium transition-colors outline-none cursor-pointer"
          >
            Quay lại Dashboard
          </button>
        </div>
      </div>
    );
  }

  const filteredData = data.filter((item) => {
    const matchSearch =
      item.code.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.name.toLowerCase().includes(searchQuery.toLowerCase());
    const matchStatus = statusFilter ? item.status === statusFilter : true;
    const matchCountry = countryFilter ? item.country === countryFilter : true;
    const matchCampus = campusFilter ? item.campus === campusFilter : true;
    const matchStaffLeaderCampus = (isStaffLeader || isStaff) ? item.campus === 'Hà Nội' : true;
    // Sinh viên chỉ thấy đối tác Đã duyệt (trong trường hợp chung) nhưng yêu cầu bảo có thể hiển thị tất cả
    // Tuy nhiên theo prompt không nhắc tới việc filter đi, cứ show theo logic.
    return matchSearch && matchStatus && matchCountry && matchCampus && matchStaffLeaderCampus;
  });

  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const validPage = Math.max(1, Math.min(page, totalPages || 1));
  const paginatedData = filteredData.slice(
    (validPage - 1) * itemsPerPage,
    validPage * itemsPerPage,
  );

  const getLogo = (index: number) => {
    if (!logoList.length) return undefined;
    return logoList[index % logoList.length];
  };

  const StatusBadge = ({ status }: { status: string }) => {
    switch (status) {
      case "Chờ duyệt":
        return (
          <span className="text-[#eab308] bg-yellow-50 px-3 py-1 rounded-md text-[13px] font-bold tracking-wide">
            Chờ duyệt
          </span>
        );
      case "Từ chối":
        return (
          <span className="text-[#ef4444] bg-red-50 px-3 py-1 rounded-md text-[13px] font-bold tracking-wide">
            Từ chối
          </span>
        );
      case "DRAFT":
        return (
          <span className="text-gray-500 bg-gray-100 px-3 py-1 rounded-md text-[13px] font-bold tracking-wide">
            DRAFT
          </span>
        );
      case "Đã duyệt":
        return (
          <span className="text-[#22c55e] bg-green-50 px-3 py-1 rounded-md text-[13px] font-bold tracking-wide">
            Đã duyệt
          </span>
        );
      default:
        return <span>{status}</span>;
    }
  };

  const renderActions = (status: string, creator: string, creatorCampus: string, id: number) => {
    const viewBtn = (
      <button
        key="view"
        onClick={() => navigate(`/dashboard/partners/${id}`)}
        className="p-1.5 rounded-lg text-gray-400 hover:bg-[#e6eff7] hover:text-[#004c91] transition-colors outline-none cursor-pointer"
        title="Xem chi tiết"
      >
        <Eye className="w-[16px] h-[16px]" />
      </button>
    );

    const acceptBtn = (
      <button
        key="accept"
        className="p-1.5 rounded-lg text-gray-400 hover:bg-[#eaffe4] hover:text-[#0aa14f] transition-colors outline-none cursor-pointer"
        title="Duyệt"
      >
        <Check className="w-[18px] h-[18px] stroke-[2.5]" />
      </button>
    );

    const denyBtn = (
      <button
        key="deny"
        className="p-1.5 rounded-lg text-gray-400 hover:bg-red-50 hover:text-red-600 transition-colors outline-none cursor-pointer"
        title="Từ chối"
      >
        <X className="w-[18px] h-[18px] stroke-[2.5]" />
      </button>
    );

    const checkStatus = () => {
      switch (status) {
        case "Chờ duyệt":
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
              {acceptBtn}
              {denyBtn}
            </div>
          );
        case "Đã duyệt":
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
            </div>
          );
        case "Từ chối":
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
            </div>
          );
        default:
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
            </div>
          );
      }
    };

    if (isHO) {
      return (
        <div className="flex items-center justify-center gap-1">
          {viewBtn}
        </div>
      );
    }

    if (isAdmin) {
      if (creatorCampus === user?.campus) {
        return checkStatus();
      } else {
        return (
          <div className="flex items-center justify-center gap-1">
            {viewBtn}
          </div>
        );
      }
    }

    if (isStaff) {
      if (creator === user?.name && creatorCampus === user?.campus) {
        if (status === "Chờ duyệt" || status === "Đã duyệt") {
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
            </div>
          );
        } else {
          return (
            <div className="flex items-center justify-center gap-1">
              {viewBtn}
            </div>
          );
        }
      } else {
        return (
          <div className="flex items-center justify-center gap-1">
            {viewBtn}
          </div>
        );
      }
    }

    return (
      <div className="flex items-center justify-center gap-1">{viewBtn}</div>
    );
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button
          onClick={() => navigate("/dashboard")}
          className="hover:text-[#004c91] transition-colors"
        >
          Dashboard
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý đối tác</span>
      </div>

      {/* Header */}
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý đối tác</h1>
      </div>

      {isStudent ? (
        // ================= STUDENT VIEW: GRID =================
        <div>
          {/* Toolbar Mảnh Mai & Bộ lọc */}
          <div className="flex items-center gap-3 mb-8">
            <div className="relative flex-1 max-w-lg">
              <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                placeholder="Tìm kiếm đối tác toàn cầu..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-10 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:border-[#004c91] text-gray-700 bg-white shadow-sm font-medium transition-all"
              />
            </div>

            <select
              value={countryFilter}
              onChange={(e) => setCountryFilter(e.target.value)}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none min-w-[180px]"
            >
              <option value="">Tất cả quốc gia</option>
              {countries.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>

          {/* Grid Layout 4 cột */}
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-6 mb-8">
            {paginatedData.map((item, idx) => (
              <div
                key={item.id}
                onClick={() => navigate(`/dashboard/partners/${item.id}`)}
                className="bg-white border text-center border-gray-100 rounded-2xl p-6 shadow-sm hover:shadow-lg hover:-translate-y-1 hover:border-[#004175]/30 transition-all duration-300 ease-in-out flex flex-col items-center cursor-pointer group"
              >
                {/* Logo */}
                <div className="w-32 h-32 mb-4 overflow-hidden flex items-center justify-center p-2 group-hover:scale-105 transition-transform duration-300">
                  {getLogo(idx) ? (
                    <img
                      src={getLogo(idx)}
                      alt={item.name}
                      className="w-full h-full object-contain mix-blend-multiply"
                    />
                  ) : (
                    <Globe2 className="w-12 h-12 text-gray-300" />
                  )}
                </div>
                {/* Info */}
                <h3 className="text-base font-bold text-[#004175] mb-2 line-clamp-2 min-h-[48px] px-2 leading-snug group-hover:text-[#F7931E] transition-colors">
                  {item.name}
                </h3>
                <div className="mt-auto flex items-center gap-1.5 text-sm font-medium text-gray-500 bg-slate-50 px-3 py-1.5 rounded-full border border-gray-100">
                  <MapPin className="w-3.5 h-3.5 text-[#00A651]" />
                  <span>{item.country}</span>
                </div>
              </div>
            ))}
            {paginatedData.length === 0 && (
              <div className="col-span-full py-16 text-center text-gray-500 bg-white rounded-2xl border border-gray-100">
                Không tìm thấy đối tác nào phù hợp
              </div>
            )}
          </div>
        </div>
      ) : (
        // ================= STAFF/ADMIN VIEW: TABLE =================
        <div>
          {/* Filters */}
          <div className="flex flex-wrap items-center gap-3 mb-6">
            <div className="relative flex-1 min-w-[250px] max-w-md">
              <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                placeholder="Tìm kiếm theo mã hoặc tên..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm"
              />
            </div>

            <select
              value={countryFilter}
              onChange={(e) => setCountryFilter(e.target.value)}
              className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none min-w-[150px]"
            >
              <option value="">Tất cả quốc gia</option>
              {countries.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>

            {!isStaffLeader && !isStaff && (
              <select
                value={campusFilter}
                onChange={(e) => setCampusFilter(e.target.value)}
                className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none min-w-[150px]"
              >
                <option value="">Tất cả cơ sở</option>
                {campuses.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            )}

            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none min-w-[150px]"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Đã duyệt">Đã duyệt</option>
              <option value="Từ chối">Từ chối</option>
              <option value="Chờ duyệt">Chờ duyệt</option>
            </select>

            {!isHO && !isStaffLeader && (
              <button onClick={() => navigate('/dashboard/partners/create')} className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-md text-sm font-bold flex items-center gap-1.5 transition-colors shadow-sm outline-none tracking-wide">
                <Plus className="w-4 h-4 flex-shrink-0" /> Thêm mới đối tác
              </button>
            )}
          </div>

          {/* Table */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-2">
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase whitespace-nowrap text-center">
                    <th className="p-3 font-bold w-[50px] whitespace-nowrap">
                      STT
                    </th>
                    <th className="p-3 font-bold w-[12%] text-left pl-6">
                      Mã đối tác
                    </th>
                    <th className="p-3 font-bold w-[20%] text-left pl-6">
                      Tên đối tác
                    </th>
                    <th className="p-3 font-bold w-[13%] text-left pl-6">
                      Quốc gia
                    </th>
                    <th className="p-3 font-bold w-[15%] text-center">
                      Người tạo
                    </th>
                    <th className="p-3 font-bold w-[120px] whitespace-nowrap">
                      Trạng thái
                    </th>
                    <th className="p-3 font-bold w-[120px] whitespace-nowrap">
                      Hành động
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {paginatedData.length > 0 ? (
                    paginatedData.map((item, index) => (
                      <tr
                        key={item.id}
                        className="hover:bg-gray-50/80 transition-colors group text-center"
                      >
                        <td className="p-3 align-middle text-sm text-gray-600 font-medium whitespace-nowrap">
                          {(validPage - 1) * itemsPerPage + index + 1}
                        </td>
                        <td className="p-3 align-middle pl-6 text-sm font-bold text-gray-800 whitespace-nowrap text-left">
                          {item.code}
                        </td>
                        <td className="p-3 align-middle pl-6 text-sm text-gray-700 whitespace-nowrap overflow-hidden text-ellipsis max-w-[200px] text-left">
                          {item.name}
                        </td>
                        <td className="p-3 align-middle pl-6 text-sm text-gray-600 whitespace-nowrap text-left">
                          {item.country}
                        </td>
                        <td className="p-3 align-middle whitespace-nowrap text-center">
                          {(isStaffLeader || isStaff) ? (
                            item.campus === 'Hà Nội' ? (
                              <div className="flex flex-col items-center">
                                <span className="text-sm font-bold text-[#004c91]">
                                  {item.creator}
                                </span>
                              </div>
                            ) : null
                          ) : (
                            <div className="flex flex-col items-center">
                              <span className="text-sm font-bold text-[#004c91]">
                                {item.creator}
                              </span>
                              <span className="text-xs text-gray-500 mt-0.5">
                                {item.campus}
                              </span>
                            </div>
                          )}
                        </td>
                        <td className="p-3 align-middle whitespace-nowrap">
                          <StatusBadge status={item.status} />
                        </td>
                        <td className="p-3 align-middle whitespace-nowrap">
                          {renderActions(item.status, item.creator, item.campus, item.id)}
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td
                        colSpan={7}
                        className="py-12 text-center text-gray-500 bg-white"
                      >
                        Không tìm thấy đối tác nào
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Pagination - Shared */}
      <div className="flex items-center justify-between mt-6">
        <div className="flex items-center gap-3 text-sm text-gray-600 font-medium">
          <span>Hiển thị</span>
          <select
            value={itemsPerPage}
            onChange={(e) => {
              setItemsPerPage(Number(e.target.value));
              setPage(1);
            }}
            className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
          >
            {isStudent ? (
              <>
                <option value="4">4</option>
                <option value="8">8</option>
                <option value="12">12</option>
                <option value="24">24</option>
              </>
            ) : (
              <>
                <option value="5">5</option>
                <option value="10">10</option>
                <option value="20">20</option>
                <option value="50">50</option>
                <option value="100">100</option>
              </>
            )}
          </select>
          <span>bản ghi / trang</span>
        </div>

        <div className="flex items-center gap-1.5">
          <button
            className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm cursor-pointer"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={validPage === 1}
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <div className="flex items-center gap-1">
            {(() => {
              const pages = [];
              if (totalPages <= 7) {
                for (let i = 1; i <= totalPages; i++) {
                  pages.push(i);
                }
              } else {
                if (validPage <= 4) {
                  for (let i = 1; i <= 5; i++) {
                    pages.push(i);
                  }
                  pages.push('...-1');
                  pages.push(totalPages);
                } else if (validPage >= totalPages - 3) {
                  pages.push(1);
                  pages.push('...-1');
                  for (let i = totalPages - 4; i <= totalPages; i++) {
                    pages.push(i);
                  }
                } else {
                  pages.push(1);
                  pages.push('...-1');
                  pages.push(validPage - 1);
                  pages.push(validPage);
                  pages.push(validPage + 1);
                  pages.push('...-2');
                  pages.push(totalPages);
                }
              }

              return pages.map((p) => {
                if (typeof p === 'string') {
                  return (
                    <span key={p} className="p-2 text-gray-400 select-none">
                      ...
                    </span>
                  );
                }
                return (
                  <button
                    key={p}
                    className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors cursor-pointer border flex items-center justify-center ${validPage === p ? "bg-[#004c91] text-white shadow-sm border-[#004c91]" : "text-gray-600 hover:bg-gray-100 border-transparent"}`}
                    onClick={() => setPage(p)}
                  >
                    {p}
                  </button>
                );
              });
            })()}
          </div>
          <button
            className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm cursor-pointer"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={validPage === totalPages || totalPages === 0}
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
