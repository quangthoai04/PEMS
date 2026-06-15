/**
 * Trang ApiManagement
 * Quản lý khóa API, nhật ký sử dụng hệ thống API webhook liên kết tự động.
 */

import React, { useState } from 'react';
import { 
  Search, 
  Plus, 
  Edit3, 
  Trash2, 
  Cpu, 
  ChevronRight, 
  X,
  Zap,
  Activity,
  Server,
  AlertCircle,
  CheckCircle2,
  Filter,
  Calendar,
  Clock,
  Eye,
  HardDrive,
  Mail,
  ScanLine,
  Settings2,
  Terminal
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';

type ApiConfig = {
  id: string;
  name: string;
  icon: string;
  baseUrl: string;
  status: boolean;
  rateLimit: number;
  authType: string;
  secretKey?: string;
};

type ApiLog = {
  id: string;
  timestamp: string;
  apiName: string;
  method: string;
  statusCode: number;
  responseTime: number;
  requestBody: string;
  responseBody: string;
};

const initialApis: ApiConfig[] = [
  { id: '1', name: 'Google Drive API', icon: 'google', baseUrl: 'https://www.googleapis.com/drive/v3', status: true, rateLimit: 1000, authType: 'OAuth2' },
  { id: '2', name: 'OCR Card Reader API', icon: 'scan', baseUrl: 'https://api.ocr-reader.com/v1', status: true, rateLimit: 500, authType: 'API Key' },
  { id: '3', name: 'FPT Email Server', icon: 'mail', baseUrl: 'https://mail.fpt.edu.vn/api', status: false, rateLimit: 2000, authType: 'Bearer Token' },
];

const initialLogs: ApiLog[] = [
  { id: '101', timestamp: '10/06/2026 00:49:02.145', apiName: 'Google Drive API', method: 'POST', statusCode: 200, responseTime: 120, requestBody: '{\n  "name": "Report_2026.pdf",\n  "mimeType": "application/pdf"\n}', responseBody: '{\n  "id": "1A2B3C4D5E6F",\n  "status": "success"\n}' },
  { id: '102', timestamp: '10/06/2026 00:52:14.332', apiName: 'OCR Card Reader API', method: 'POST', statusCode: 401, responseTime: 45, requestBody: '{\n  "image_url": "https://storage.example.com/id.jpg"\n}', responseBody: '{\n  "error": {\n    "code": 401,\n    "message": "Invalid API Key."\n  }\n}' },
  { id: '103', timestamp: '10/06/2026 01:15:05.890', apiName: 'FPT Email Server', method: 'GET', statusCode: 500, responseTime: 5020, requestBody: '{}', responseBody: '{\n  "error": "Internal Server Error"\n}' },
  { id: '104', timestamp: '10/06/2026 02:30:11.100', apiName: 'Google Drive API', method: 'GET', statusCode: 200, responseTime: 85, requestBody: '{}', responseBody: '{\n  "files": [\n    {"name": "file1"}\n  ]\n}' },
];

export function ApiManagement() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<'config' | 'logs'>('config');

  // Config State
  const [apis, setApis] = useState<ApiConfig[]>(initialApis);
  const [searchApi, setSearchApi] = useState('');
  const [isApiModalOpen, setIsApiModalOpen] = useState(false);
  const [editingApi, setEditingApi] = useState<ApiConfig | null>(null);
  const [apiToDelete, setApiToDelete] = useState<ApiConfig | null>(null);
  const [pingResult, setPingResult] = useState<{apiId: string, status: 'success' | 'error', message: string} | null>(null);

  // Form State
  const [formData, setFormData] = useState({
    name: '',
    authType: 'API Key',
    baseUrl: '',
    secretKey: '',
    rateLimit: '1000'
  });

  // Logs State
  const [searchLog, setSearchLog] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [apiFilter, setApiFilter] = useState('ALL');
  const [selectedLog, setSelectedLog] = useState<ApiLog | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const getApiIcon = (iconName: string) => {
    switch(iconName) {
      case 'google': return <HardDrive className="w-8 h-8 text-blue-500" />;
      case 'scan': return <ScanLine className="w-8 h-8 text-purple-500" />;
      case 'mail': return <Mail className="w-8 h-8 text-orange-500" />;
      default: return <Server className="w-8 h-8 text-slate-500" />;
    }
  };

  const filteredApis = apis.filter(a => a.name.toLowerCase().includes(searchApi.toLowerCase()));
  
  const filteredLogs = initialLogs.filter(log => {
    const matchSearch = log.apiName.toLowerCase().includes(searchLog.toLowerCase()) || 
                       log.requestBody.toLowerCase().includes(searchLog.toLowerCase()) ||
                       log.responseBody.toLowerCase().includes(searchLog.toLowerCase());
    const matchStatus = statusFilter === 'ALL' || 
                       (statusFilter === '2XX' && log.statusCode >= 200 && log.statusCode < 300) ||
                       (statusFilter === '4XX' && log.statusCode >= 400 && log.statusCode < 500) ||
                       (statusFilter === '5XX' && log.statusCode >= 500);
    const matchApi = apiFilter === 'ALL' || log.apiName === apiFilter;
    
    return matchSearch && matchStatus && matchApi;
  });

  const totalLogs = filteredLogs.length;
  const totalPages = Math.max(1, Math.ceil(totalLogs / pageSize));
  const paginatedLogs = filteredLogs.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const handleToggleStatus = (id: string) => {
    setApis(apis.map(api => api.id === id ? { ...api, status: !api.status } : api));
  };

  const openEditModal = (api?: ApiConfig) => {
    if (api) {
      setEditingApi(api);
      setFormData({
        name: api.name,
        authType: api.authType,
        baseUrl: api.baseUrl,
        secretKey: api.secretKey || '',
        rateLimit: api.rateLimit.toString()
      });
    } else {
      setEditingApi(null);
      setFormData({
        name: '',
        authType: 'API Key',
        baseUrl: '',
        secretKey: '',
        rateLimit: '1000'
      });
    }
    setIsApiModalOpen(true);
  };

  const handleSaveApi = () => {
    if (editingApi) {
      setApis(apis.map(api => api.id === editingApi.id ? { 
        ...api, 
        name: formData.name,
        authType: formData.authType,
        baseUrl: formData.baseUrl,
        secretKey: formData.secretKey,
        rateLimit: parseInt(formData.rateLimit) || 0
      } : api));
    } else {
      const newApi: ApiConfig = {
        id: Date.now().toString(),
        name: formData.name,
        icon: 'server',
        baseUrl: formData.baseUrl,
        status: true,
        authType: formData.authType,
        secretKey: formData.secretKey,
        rateLimit: parseInt(formData.rateLimit) || 0
      };
      setApis([...apis, newApi]);
    }
    setIsApiModalOpen(false);
  };

  const handleDelete = () => {
    if (apiToDelete) {
      setApis(apis.filter(a => a.id !== apiToDelete.id));
      setApiToDelete(null);
    }
  };

  const handleTestConnection = (api: ApiConfig) => {
    // Simulate ping
    setPingResult(null);
    setTimeout(() => {
      const isSuccess = Math.random() > 0.3;
      if (isSuccess) {
        setPingResult({ 
          apiId: api.id, 
          status: 'success', 
          message: `Thử nghiệm Kết nối Thành công - Ping ${Math.floor(Math.random() * 100) + 10}ms` 
        });
      } else {
        setPingResult({ 
          apiId: api.id, 
          status: 'error', 
          message: `Lỗi Kết nối: Sai API Key (Code 401)` 
        });
      }
      setTimeout(() => setPingResult(null), 4000);
    }, 600);
  };

  const getStatusColor = (code: number) => {
    if (code >= 200 && code < 300) return 'text-emerald-600 bg-emerald-50 border-emerald-200';
    if (code >= 400 && code < 500) return 'text-orange-600 bg-orange-50 border-orange-200';
    if (code >= 500) return 'text-red-600 bg-red-50 border-red-200';
    return 'text-slate-600 bg-slate-50 border-slate-200';
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-500 pb-24 pt-4 h-full flex flex-col">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý API</span>
      </div>

      {/* Header */}
      <div className="flex items-center gap-4">
        <div>
          <h2 className="text-3xl font-black text-[#004c91] tracking-tight">Quản lý API</h2>
          <p className="text-base font-medium text-slate-500 mt-1">Cấu hình kết nối API và giám sát lưu lượng máy chủ</p>
        </div>
      </div>

      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm flex flex-col h-[calc(100vh-240px)] min-h-[500px] flex-1 overflow-hidden">
        {/* Tabs */}
        <div className="flex border-b border-slate-200 bg-slate-50 overflow-x-auto shrink-0">
          <button 
            onClick={() => setActiveTab('config')} 
            className={`px-8 py-4 font-bold text-sm border-b-2 transition-colors whitespace-nowrap outline-none ${activeTab === 'config' ? 'border-[#004c91] text-[#004c91] bg-white' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100'}`}
          >
            <div className="flex items-center gap-2">
              <Settings2 className="w-4 h-4" />
              Cấu hình Kết nối API
            </div>
          </button>
          <button 
            onClick={() => setActiveTab('logs')} 
            className={`px-8 py-4 font-bold text-sm border-b-2 transition-colors whitespace-nowrap outline-none ${activeTab === 'logs' ? 'border-[#004c91] text-[#004c91] bg-white' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100'}`}
          >
            <div className="flex items-center gap-2">
              <Terminal className="w-4 h-4" />
              Nhật ký cuộc gọi & Giám sát Lỗi
            </div>
          </button>
        </div>

        {/* Tab 1 Content */}
        {activeTab === 'config' && (
          <div className="p-6 flex-1 overflow-y-auto bg-slate-50/30">
            {/* Toolbar */}
            <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mb-6">
              <div className="relative w-full sm:w-80">
                <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                <input 
                  type="text" 
                  placeholder="Tìm theo tên API..." 
                  value={searchApi}
                  onChange={(e) => setSearchApi(e.target.value)}
                  className="w-full bg-white border border-slate-200 rounded-xl pl-11 pr-4 py-2.5 text-sm font-medium text-slate-800 placeholder-slate-400 focus:outline-none focus:border-[#004c91] transition-colors shadow-sm"
                />
              </div>
              <button 
                onClick={() => openEditModal()}
                className="flex w-full sm:w-auto items-center justify-center gap-2 bg-[#f37021] hover:bg-[#d95d18] text-white px-6 py-2.5 rounded-xl font-bold transition-all shadow-[0_4px_12px_rgba(243,112,33,0.25)] active:scale-95"
              >
                <Plus className="w-5 h-5" />
                <span>Thêm Cấu Hình API</span>
              </button>
            </div>

            {/* Grid display */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {filteredApis.map(api => (
                <div key={api.id} className={`bg-white rounded-2xl border ${api.status ? 'border-slate-200 shadow-sm' : 'border-slate-200 bg-slate-50 grayscale[0.5] opacity-80'} overflow-hidden flex flex-col transition-all hover:shadow-md`}>
                  <div className="p-5 flex-1 relative">
                    <div className="flex justify-between items-start mb-4">
                      <div className="flex items-center gap-3">
                        <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${api.status ? 'bg-slate-50' : 'bg-slate-200'}`}>
                          {getApiIcon(api.icon)}
                        </div>
                        <div>
                          <h3 className="font-bold text-slate-800 text-lg leading-tight">{api.name}</h3>
                          <span className="inline-block px-2 py-0.5 bg-slate-100 text-slate-500 rounded text-xs font-bold mt-1 border border-slate-200">{api.authType}</span>
                        </div>
                      </div>
                      
                      <label className="relative inline-flex items-center cursor-pointer">
                        <input type="checkbox" className="sr-only peer" checked={api.status} onChange={() => handleToggleStatus(api.id)} />
                        <div className="w-11 h-6 bg-slate-200 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-emerald-500 shadow-inner"></div>
                      </label>
                    </div>

                    <div className="space-y-3">
                      <div>
                        <p className="text-xs font-bold text-slate-500 mb-1 uppercase tracking-wider">Base URL</p>
                        <p className="text-sm font-medium text-slate-700 bg-slate-50 px-3 py-2 rounded-lg border border-slate-100 truncate" title={api.baseUrl}>
                          {api.baseUrl}
                        </p>
                      </div>
                      <div 
                        className="cursor-pointer group flex items-center justify-between bg-blue-50/50 hover:bg-blue-50 px-3 py-2 rounded-lg border border-blue-100 transition-colors"
                        onClick={() => openEditModal(api)}
                      >
                        <p className="text-sm font-bold text-slate-600 group-hover:text-[#004c91] transition-colors">Giới hạn Rate Limit:</p>
                        <p className="text-sm font-black text-[#004c91]">{api.rateLimit.toLocaleString()} <span className="text-xs text-slate-500 font-bold">req/phút</span></p>
                      </div>
                    </div>

                    {/* Ping status float */}
                    <AnimatePresence>
                      {pingResult && pingResult.apiId === api.id && (
                        <motion.div 
                          initial={{ opacity: 0, y: 10 }}
                          animate={{ opacity: 1, y: 0 }}
                          exit={{ opacity: 0, scale: 0.95 }}
                          className={`absolute top-0 left-0 right-0 m-2 p-3 rounded-lg text-sm font-bold shadow-lg flex items-start gap-2 z-10 ${
                            pingResult.status === 'success' ? 'bg-emerald-50 border border-emerald-200 text-emerald-700' : 'bg-red-50 border border-red-200 text-red-700'
                          }`}
                        >
                          {pingResult.status === 'success' ? <CheckCircle2 className="w-5 h-5 shrink-0" /> : <AlertCircle className="w-5 h-5 shrink-0" />}
                          <p className="leading-tight">{pingResult.message}</p>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                  
                  <div className="bg-slate-50 px-5 py-3 border-t border-slate-200 flex items-center justify-between">
                    <button 
                      onClick={() => handleTestConnection(api)}
                      className="flex items-center gap-1.5 text-sm font-bold text-slate-600 hover:text-emerald-600 transition-colors outline-none"
                    >
                      <Zap className="w-4 h-4" />
                      Kiểm tra kết nối
                    </button>
                    <div className="flex items-center gap-2">
                      <button 
                        onClick={() => openEditModal(api)}
                        className="p-1.5 text-slate-400 hover:text-[#004c91] hover:bg-blue-100 rounded-md transition-colors outline-none cursor-pointer"
                        title="Chỉnh sửa"
                      >
                        <Edit3 className="w-4 h-4" />
                      </button>
                      <button 
                        onClick={() => setApiToDelete(api)}
                        className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-100 rounded-md transition-colors outline-none cursor-pointer"
                        title="Xóa cấu hình"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
              
            </div>
          </div>
        )}

        {/* Tab 2 Content */}
        {activeTab === 'logs' && (
          <div className="flex flex-col flex-1 h-full overflow-hidden">
            <div className="p-4 border-b border-slate-200 bg-white grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 shrink-0">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input 
                  type="text" 
                  placeholder="Tìm theo nội dung, Request Body..." 
                  value={searchLog}
                  onChange={(e) => setSearchLog(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm font-medium focus:outline-none focus:border-[#004c91]"
                />
              </div>
              <div className="relative">
                <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <select 
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm font-bold text-slate-700 focus:outline-none focus:border-[#004c91] appearance-none"
                >
                  <option value="ALL">Tất cả trạng thái HTTP</option>
                  <option value="2XX">✓ Thành công (2xx)</option>
                  <option value="4XX">⚠ Lỗi Client (4xx)</option>
                  <option value="5XX">✖ Lỗi Server (5xx)</option>
                </select>
              </div>
              <div className="relative">
                <Server className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <select 
                  value={apiFilter}
                  onChange={(e) => setApiFilter(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm font-bold text-slate-700 focus:outline-none focus:border-[#004c91] appearance-none"
                >
                  <option value="ALL">Tất cả Tên API</option>
                  <option value="Google Drive API">Google Drive API</option>
                  <option value="OCR Card Reader API">OCR Card Reader API</option>
                  <option value="FPT Email Server">FPT Email Server</option>
                </select>
              </div>
              <div className="relative">
                <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input 
                  type="date" 
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-lg text-sm font-medium text-slate-700 focus:outline-none focus:border-[#004c91]"
                />
              </div>
            </div>

            <div className="flex-1 overflow-auto bg-slate-50/50">
              <table className="w-full text-left border-collapse text-sm">
                <thead className="sticky top-0 bg-white shadow-[0_1px_2px_rgba(0,0,0,0.05)] z-10">
                  <tr className="border-b border-slate-200 text-slate-500">
                    <th className="px-6 py-4 font-bold uppercase tracking-wider text-xs">Thời gian</th>
                    <th className="px-6 py-4 font-bold uppercase tracking-wider text-xs">Tên API & Phương thức</th>
                    <th className="px-6 py-4 font-bold uppercase tracking-wider text-xs">Mã phản hồi</th>
                    <th className="px-6 py-4 font-bold uppercase tracking-wider text-xs">Thời gian phản hồi</th>
                    <th className="px-6 py-4 font-bold uppercase tracking-wider text-xs text-center">Hành động</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {paginatedLogs.map(log => (
                    <tr key={log.id} className="hover:bg-slate-50/80 transition-colors">
                      <td className="px-6 py-4 font-mono text-xs text-slate-500">{log.timestamp}</td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <span className={`px-2 py-0.5 rounded text-xs font-bold ${log.method === 'GET' ? 'bg-blue-100 text-blue-700' : 'bg-orange-100 text-orange-700'}`}>
                            {log.method}
                          </span>
                          <span className="font-bold text-slate-700">{log.apiName}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`inline-flex px-2.5 py-1 rounded-md text-xs font-bold border ${getStatusColor(log.statusCode)}`}>
                          Code {log.statusCode}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-1.5">
                          <Clock className={`w-4 h-4 ${log.responseTime > 2000 ? 'text-red-500' : 'text-slate-400'}`} />
                          <span className={`font-mono text-sm ${log.responseTime > 2000 ? 'text-red-600 font-bold' : 'text-slate-600'}`}>{log.responseTime} ms</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-center">
                        <button 
                          onClick={() => setSelectedLog(log)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-slate-600 font-bold hover:text-[#004c91] hover:bg-blue-50 hover:border-blue-200 transition-colors text-xs shadow-sm"
                        >
                          <Eye className="w-4 h-4" />
                          Xem chi tiết
                        </button>
                      </td>
                    </tr>
                  ))}
                  {paginatedLogs.length === 0 && (
                    <tr>
                      <td colSpan={5} className="px-6 py-12 text-center text-slate-400">
                        <Activity className="w-12 h-12 mx-auto mb-3 opacity-20" />
                        <p className="font-medium text-slate-500">Không tìm thấy nhật ký hợp lệ</p>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            <div className="border-t border-slate-200 p-4 bg-white shrink-0 flex flex-col sm:flex-row items-center justify-between gap-4">
              <div className="flex items-center gap-3 text-sm text-slate-500">
                <span className="font-medium">Hiển thị</span>
                <select 
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setCurrentPage(1);
                  }}
                  className="bg-slate-50 border border-slate-200 text-slate-700 font-bold rounded-lg px-2 py-1 focus:outline-none focus:border-[#004c91]"
                >
                  <option value={10}>10</option>
                  <option value={20}>20</option>
                  <option value={50}>50</option>
                </select>
                <span className="font-medium">bản ghi mỗi trang</span>
              </div>
              
              <div className="flex items-center gap-4">
                <span className="text-sm font-medium text-slate-500">Trang {currentPage} / {totalPages}</span>
                <div className="flex items-center gap-2">
                  <button 
                    onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
                    disabled={currentPage === 1}
                    className="px-3 py-1.5 rounded-lg border border-slate-200 text-sm font-medium hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    Trước
                  </button>
                  <button 
                    onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
                    disabled={currentPage === totalPages}
                    className="px-3 py-1.5 rounded-lg border border-slate-200 text-sm font-medium hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    Sau
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Drawer Xem chi tiết Log */}
      <AnimatePresence>
        {selectedLog && (
          <>
            <motion.div 
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              onClick={() => setSelectedLog(null)}
              className="fixed inset-0 bg-slate-900/20 backdrop-blur-sm z-40"
            />
            <motion.div 
              initial={{ x: '100%' }}
              animate={{ x: 0 }}
              exit={{ x: '100%' }}
              transition={{ type: "spring", damping: 25, stiffness: 200 }}
              className="fixed top-0 right-0 bottom-0 w-full sm:w-[600px] bg-white shadow-2xl z-50 flex flex-col border-l border-slate-200"
            >
              <div className="bg-slate-800 text-white px-6 py-4 flex items-center justify-between shrink-0">
                <div>
                  <h3 className="font-bold text-lg flex items-center gap-2">
                    <Terminal className="w-5 h-5 text-emerald-400" />
                    Chi tiết Request
                  </h3>
                  <p className="text-xs text-slate-400 font-mono mt-1">{selectedLog.timestamp}</p>
                </div>
                <button 
                  onClick={() => setSelectedLog(null)} 
                  className="p-2 hover:bg-slate-700 rounded-full transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="p-6 overflow-y-auto flex-1 bg-slate-50 space-y-6">
                <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col gap-3">
                  <div className="flex justify-between items-center border-b border-slate-100 pb-3">
                    <span className="text-sm font-bold text-slate-500 uppercase">Thông tin Định tuyến</span>
                    <span className={`inline-flex px-2 py-1 rounded text-xs font-bold border ${getStatusColor(selectedLog.statusCode)}`}>
                      HTTP {selectedLog.statusCode}
                    </span>
                  </div>
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-slate-500 mb-1">API Name</p>
                      <p className="font-bold text-slate-800">{selectedLog.apiName}</p>
                    </div>
                    <div>
                      <p className="text-slate-500 mb-1">Method</p>
                      <p className="font-bold text-[#f37021]">{selectedLog.method}</p>
                    </div>
                    <div>
                      <p className="text-slate-500 mb-1">Response Time</p>
                      <p className="font-mono font-bold text-slate-700">{selectedLog.responseTime} ms</p>
                    </div>
                  </div>
                </div>

                <div>
                  <h4 className="font-bold text-slate-700 mb-2 flex items-center gap-2">
                    <Activity className="w-4 h-4 text-[#004c91]" />
                    Request Body (Gửi đi)
                  </h4>
                  <div className="bg-[#1e1e1e] rounded-xl p-4 overflow-hidden shadow-inner">
                    <pre className="text-[13px] text-green-400 font-mono whitespace-pre-wrap overflow-x-auto">
                      {selectedLog.requestBody}
                    </pre>
                  </div>
                </div>

                <div>
                  <h4 className="font-bold text-slate-700 mb-2 flex items-center gap-2">
                    <Server className="w-4 h-4 text-orange-500" />
                    Response Body (Trả về)
                  </h4>
                  <div className="bg-[#1e1e1e] rounded-xl p-4 overflow-hidden shadow-inner">
                    <pre className={`text-[13px] font-mono whitespace-pre-wrap overflow-x-auto ${selectedLog.statusCode >= 400 ? 'text-red-400' : 'text-blue-300'}`}>
                      {selectedLog.responseBody}
                    </pre>
                  </div>
                </div>
              </div>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {/* Modal Cấu hình API */}
      <AnimatePresence>
        {isApiModalOpen && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 20 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden flex flex-col"
            >
              <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between">
                 <h2 className="text-lg font-bold text-white tracking-tight">{editingApi ? 'Chỉnh sửa Cấu hình API' : 'Thêm Cấu hình API mới'}</h2>
                 <button onClick={() => setIsApiModalOpen(false)} className="text-white/80 hover:text-white p-1 rounded-full transition-colors outline-none cursor-pointer">
                    <X className="w-5 h-5" />
                 </button>
              </div>
              <div className="p-6 space-y-4">
                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Tên hiển thị API <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Ví dụ: FPT Payment API"
                    className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-medium text-slate-800 transition-colors"
                  />
                </div>
                
                <div className="grid grid-cols-2 gap-4">
                  <div className="col-span-2 sm:col-span-1">
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Loại Xác thực (Auth Type)</label>
                    <select 
                      value={formData.authType}
                      onChange={(e) => setFormData({...formData, authType: e.target.value})}
                      className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-bold text-slate-700 transition-colors appearance-none"
                    >
                      <option value="API Key">API Key</option>
                      <option value="Bearer Token">Bearer Token</option>
                      <option value="OAuth2">OAuth2</option>
                      <option value="None">Không yêu cầu</option>
                    </select>
                  </div>
                  <div className="col-span-2 sm:col-span-1">
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Giới hạn (Req/Phút)</label>
                    <input 
                      type="number" 
                      value={formData.rateLimit}
                      onChange={(e) => setFormData({...formData, rateLimit: e.target.value})}
                      className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-mono font-bold text-slate-800 transition-colors"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-bold text-slate-700 mb-1.5">Base URL (Đường dẫn gốc) <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    value={formData.baseUrl}
                    onChange={(e) => setFormData({...formData, baseUrl: e.target.value})}
                    placeholder="https://api.example.com/v1"
                    className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-mono text-sm text-slate-800 transition-colors"
                  />
                </div>
                
                {formData.authType !== 'None' && (
                  <div>
                    <label className="block text-sm font-bold text-slate-700 mb-1.5">Secret Token / API Key</label>
                    <div className="relative">
                      <input 
                        type="password" 
                        value={formData.secretKey}
                        onChange={(e) => setFormData({...formData, secretKey: e.target.value})}
                        placeholder="••••••••••••••••••••"
                        className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:border-[#004c91] focus:bg-white font-mono text-sm text-slate-800 transition-colors"
                      />
                    </div>
                    <p className="text-xs text-slate-400 mt-1.5">Khóa bí mật sẽ được mã hóa an toàn trên hệ thống máy chủ C14.</p>
                  </div>
                )}
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-200 flex items-center gap-3 justify-end">
                <button 
                  onClick={() => setIsApiModalOpen(false)}
                  className="px-6 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold hover:bg-slate-100 transition-colors"
                >
                  Hủy bỏ
                </button>
                <button 
                  onClick={handleSaveApi}
                  disabled={!formData.name.trim() || !formData.baseUrl.trim()}
                  className="px-6 py-2.5 rounded-xl bg-[#004c91] text-white font-bold hover:bg-[#00386b] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Lưu cấu hình
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Modal Xóa API */}
      <AnimatePresence>
        {apiToDelete && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0, y: 20 }}
              animate={{ scale: 1, opacity: 1, y: 0 }}
              exit={{ scale: 0.95, opacity: 0, y: 20 }}
              className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden flex flex-col"
            >
              <div className="bg-red-600 px-6 py-4 flex items-center justify-between">
                 <h2 className="text-lg font-bold text-white tracking-tight">Xác nhận Xóa API</h2>
                 <button onClick={() => setApiToDelete(null)} className="text-white/80 hover:text-white p-1 rounded-full transition-colors outline-none cursor-pointer">
                    <X className="w-5 h-5" />
                 </button>
              </div>
              <div className="p-6 flex flex-col items-center text-center space-y-4">
                <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center text-red-600">
                  <Trash2 className="w-8 h-8" />
                </div>
                <div>
                  <h3 className="text-lg font-bold text-slate-800">Bạn có chắc chắn muốn xóa?</h3>
                  <p className="text-sm font-medium text-slate-500 mt-2">
                    Cấu hình kết nối cho <strong className="text-slate-800">{apiToDelete.name}</strong> sẽ bị xóa vĩnh viễn và không thể phục hồi. Các dịch vụ đang chạy có thể bị gián đoạn.
                  </p>
                </div>
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-200 flex items-center gap-3 justify-end">
                <button 
                  onClick={() => setApiToDelete(null)}
                  className="px-5 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold hover:bg-slate-100 transition-colors"
                >
                  Hủy bỏ
                </button>
                <button 
                  onClick={handleDelete}
                  className="px-5 py-2.5 rounded-xl bg-red-600 text-white font-bold hover:bg-red-700 transition-colors shadow-sm"
                >
                  Xác nhận xóa
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

    </div>
  );
}
