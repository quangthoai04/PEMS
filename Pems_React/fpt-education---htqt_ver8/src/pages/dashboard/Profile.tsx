// Đây là trang về thông tin cá nhân của người dùng
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { Edit2, Phone, Facebook, School, ShieldCheck, User, Save, X, Mail, BookOpen } from 'lucide-react';
import avatarImg from '../../assets/Avatar/AvatarDefault.png';

export function Profile() {
  const navigate = useNavigate();

  // Get user from localStorage
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : {
    name: 'Khách',
    campus: 'Không rõ',
    role: 'GUEST'
  };

  const [isEditing, setIsEditing] = useState(false);
  const userRole = user.role?.toUpperCase();
  
  const getInitialEmail = () => {
    if (userRole === 'ADMIN') return 'admin@gmail.com';
    if (userRole === 'STUDENT') return 'student@gmail.com';
    if (userRole === 'STAFF') return 'staff@gmail.com';
    return 'ho@gmail.com';
  };
  
  const [profileData, setProfileData] = useState({
    name: user.name || 'Khách',
    phone: '0369182718',
    gender: 'Nam',
    email: getInitialEmail(),
    major: 'JS',
    facebook: 'https://facebook.com/hihi',
    facebookName: 'hihi',
  });

  const handleSave = () => {
    setIsEditing(false);
    // Logic lưu thông tin (call API, update localStorage...) sẽ ở đây
  };

  const roleColors = 'bg-[#fff0e6] text-[#f37021] border-[#fcd5ba]';

  return (
    <motion.div 
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
      className="p-8 pb-12 max-w-4xl mx-auto"
    >
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Hồ sơ cá nhân</span>
      </div>

      <div className="mb-8">
        <h1 className="text-3xl font-bold text-[#004c91]">Hồ sơ cá nhân</h1>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden relative">
        {/* Banner */}
        <div className="h-40 bg-gradient-to-r from-[#004c91] to-[#0066c0] relative">
          <div className="absolute inset-0 opacity-10 bg-[radial-gradient(circle_at_center,_white_1px,_transparent_1px)] bg-[length:20px_20px]"></div>
          
          {!isEditing ? (
            <button 
              onClick={() => setIsEditing(true)}
              className="absolute top-6 right-6 flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white border border-white/40 px-4 py-2 rounded-xl backdrop-blur-sm transition-all font-semibold shadow-sm text-sm"
            >
              <Edit2 className="w-4 h-4" />
              Chỉnh sửa
            </button>
          ) : (
            <div className="absolute top-6 right-6 flex items-center gap-2">
              <button 
                onClick={() => setIsEditing(false)}
                className="flex items-center gap-1.5 bg-white/20 hover:bg-white/30 text-white border border-white/40 px-4 py-2 rounded-xl backdrop-blur-sm transition-all font-semibold shadow-sm text-sm"
              >
                <X className="w-4 h-4" />
                Hủy
              </button>
              <button 
                onClick={handleSave}
                className="flex items-center gap-1.5 bg-white text-[#004c91] border border-white px-4 py-2 rounded-xl backdrop-blur-sm transition-all font-bold shadow-sm text-sm hover:bg-gray-50"
              >
                <Save className="w-4 h-4" />
                Lưu
              </button>
            </div>
          )}
        </div>

        {/* Profile Content */}
        <div className="px-8 pb-10">
          <div className="flex flex-col md:flex-row gap-8 items-start">
            
            {/* Avatar Section */}
            <div className="-mt-16 relative flex-shrink-0 flex flex-col items-center">
              <div className="w-36 h-36 bg-gray-100 rounded-full shadow-md overflow-hidden flex items-center justify-center">
                <img src={avatarImg} alt="Avatar" className="w-full h-full object-cover" />
              </div>
              
              <div className="mt-4 flex flex-col items-center">
                 <div className={`flex items-center gap-1.5 px-3 py-1 text-xs font-bold rounded-full border uppercase tracking-wide shadow-sm ${roleColors}`}>
                    <ShieldCheck className="w-3.5 h-3.5" />
                    {user.role}
                 </div>
              </div>
            </div>

            {/* User Info Section */}
            <div className="pt-4 flex-1 w-full">
              {isEditing ? (
                <input 
                  type="text" 
                  value={profileData.name} 
                  onChange={e => setProfileData({...profileData, name: e.target.value})} 
                  className="text-3xl font-bold text-gray-900 tracking-tight border-none focus:outline-none bg-transparent w-full pb-3 mb-3" 
                />
              ) : (
                <div className="border-b border-solid border-gray-300 pb-3 mb-3">
                  <h2 className="text-3xl font-bold text-gray-900 tracking-tight">{profileData.name}</h2>
                </div>
              )}

              <div className="flex items-center gap-2 mt-2 text-[#f37021] font-semibold">
                <School className="w-5 h-5" />
                <span>Campus: {user.campus}</span>
              </div>

              <div className="mt-8 grid grid-cols-1 md:grid-cols-2 gap-6">
                 {/* Detail Item - Phone */}
                 <div className="bg-[#f0f7fc] border border-[#d2e5f5] rounded-2xl p-4 flex items-center gap-4 hover:shadow-sm transition-all">
                    <div className="w-10 h-10 rounded-full bg-white text-[#004c91] flex items-center justify-center flex-shrink-0 shadow-sm border border-[#eef5fa]">
                      <Phone className="w-5 h-5" />
                    </div>
                    <div className="flex-1">
                      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-0.5">Số điện thoại</p>
                      {isEditing ? (
                        <input 
                          type="text" 
                          value={profileData.phone} 
                          onChange={e => setProfileData({...profileData, phone: e.target.value})} 
                          className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white" 
                        />
                      ) : (
                        <p className="text-gray-900 font-medium">{profileData.phone}</p>
                      )}
                    </div>
                 </div>

                 {/* Detail Item - Gender */}
                 <div className="bg-[#f0f7fc] border border-[#d2e5f5] rounded-2xl p-4 flex items-center gap-4 hover:shadow-sm transition-all">
                    <div className="w-10 h-10 rounded-full bg-white text-[#004c91] flex items-center justify-center flex-shrink-0 shadow-sm border border-[#eef5fa]">
                      <User className="w-5 h-5" />
                    </div>
                    <div className="flex-1">
                      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-0.5">Giới tính</p>
                      {isEditing ? (
                        <select 
                          value={profileData.gender} 
                          onChange={e => setProfileData({...profileData, gender: e.target.value})} 
                          className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white"
                        >
                          <option value="Nam">Nam</option>
                          <option value="Nữ">Nữ</option>
                          <option value="Khác">Khác</option>
                        </select>
                      ) : (
                        <p className="text-gray-900 font-medium">{profileData.gender}</p>
                      )}
                    </div>
                 </div>

                 {/* Detail Item - Email */}
                 <div className="bg-[#f0f7fc] border border-[#d2e5f5] rounded-2xl p-4 flex items-center gap-4 hover:shadow-sm transition-all md:col-span-2">
                    <div className="w-10 h-10 rounded-full bg-white text-[#004c91] flex items-center justify-center flex-shrink-0 shadow-sm border border-[#eef5fa]">
                      <Mail className="w-5 h-5" />
                    </div>
                    <div className="flex-1">
                      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-0.5">Email</p>
                      {isEditing ? (
                        <input 
                          type="email" 
                          value={profileData.email} 
                          onChange={e => setProfileData({...profileData, email: e.target.value})} 
                          className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white" 
                        />
                      ) : (
                        <p className="text-gray-900 font-medium">{profileData.email}</p>
                      )}
                    </div>
                 </div>

                 {/* Detail Item - Major */}
                 {userRole === 'STUDENT' && (
                   <div className="bg-[#f0f7fc] border border-[#d2e5f5] rounded-2xl p-4 flex items-center gap-4 hover:shadow-sm transition-all md:col-span-2">
                      <div className="w-10 h-10 rounded-full bg-white text-[#004c91] flex items-center justify-center flex-shrink-0 shadow-sm border border-[#eef5fa]">
                        <BookOpen className="w-5 h-5" />
                      </div>
                      <div className="flex-1">
                        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-0.5">Chuyên ngành</p>
                        {isEditing ? (
                          <input 
                            type="text" 
                            value={profileData.major} 
                            onChange={e => setProfileData({...profileData, major: e.target.value})} 
                            className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white" 
                          />
                        ) : (
                          <p className="text-gray-900 font-medium">{profileData.major}</p>
                        )}
                      </div>
                   </div>
                 )}

                 {/* Detail Item - Facebook */}
                 <div className="bg-[#f0f7fc] border border-[#d2e5f5] rounded-2xl p-4 flex items-start gap-4 hover:shadow-sm transition-all md:col-span-2">
                    <div className="w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 text-white shadow-sm mt-0.5" style={{backgroundColor: '#1877F2'}}>
                      <Facebook className="w-5 h-5 fill-current" />
                    </div>
                    <div className="flex-1">
                      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-0.5">Facebook</p>
                      {isEditing ? (
                        <div className="flex flex-col gap-3 mt-1">
                          <div>
                            <label className="text-xs text-gray-500 mb-1 block">Tên hiển thị</label>
                            <input 
                              type="text" 
                              placeholder="VD: hihi" 
                              value={profileData.facebookName} 
                              onChange={e => setProfileData({...profileData, facebookName: e.target.value})} 
                              className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white" 
                            />
                          </div>
                          <div>
                            <label className="text-xs text-gray-500 mb-1 block">Liên kết (URL)</label>
                            <input 
                              type="text" 
                              placeholder="https://facebook.com/..." 
                              value={profileData.facebook} 
                              onChange={e => setProfileData({...profileData, facebook: e.target.value})} 
                              className="text-gray-900 font-medium px-3 py-1.5 rounded-lg border border-[#b6d4f0] focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] w-full bg-white" 
                            />
                          </div>
                        </div>
                      ) : (
                        <a href={profileData.facebook} target="_blank" rel="noreferrer" className="text-[#004c91] hover:underline font-medium break-all">
                          {profileData.facebookName}
                        </a>
                      )}
                    </div>
                 </div>

              </div>

            </div>
          </div>
        </div>
      </div>
    </motion.div>
  );
}
