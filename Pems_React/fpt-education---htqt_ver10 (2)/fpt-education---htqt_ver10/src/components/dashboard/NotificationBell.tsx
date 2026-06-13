import React, { useState, useRef, useEffect } from 'react';
import { Bell } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';

interface NotificationInfo {
  id: string;
  title: string;
  desc: string;
  time: string;
  isRead: boolean;
  link?: string;
}

const mockNotifications: NotificationInfo[] = [
  {
    id: 'n1',
    title: 'Yêu cầu tham quan mới',
    desc: 'Đoàn trường THPT Chuyên Sư phạm vừa đăng ký.',
    time: '5 phút trước',
    isRead: false,
    link: '/dashboard/visit/request'
  },
  {
    id: 'n2',
    title: 'Cập nhật trạng thái',
    desc: 'HO đã phê duyệt lịch trình cho đoàn A.',
    time: '2 giờ trước',
    isRead: false,
    link: '/dashboard/visit/process'
  },
  {
    id: 'n3',
    title: 'Đánh giá mới',
    desc: 'Bạn nhận được 1 đánh giá 5 sao từ khách.',
    time: '1 ngày trước',
    isRead: true,
    link: '/dashboard/feedback'
  }
];

export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState<NotificationInfo[]>(mockNotifications);
  const popoverRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  const unreadCount = notifications.filter(n => !n.isRead).length;

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (popoverRef.current && !popoverRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  const markAllAsRead = () => {
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  };
  
  const handleItemClick = (n: NotificationInfo) => {
    setNotifications(prev => prev.map(item => item.id === n.id ? { ...item, isRead: true } : item));
    setIsOpen(false);
    if (n.link) {
      navigate(n.link);
    }
  };

  return (
    <div className="relative" ref={popoverRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="p-2 text-gray-600 hover:bg-gray-100 rounded-full transition-colors relative"
        aria-label="Thông báo"
      >
        <Bell className="w-6 h-6" />
        {unreadCount > 0 && (
          <span className="absolute top-1 right-1.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
            {unreadCount}
          </span>
        )}
      </button>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, y: 10, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 10, scale: 0.95 }}
            transition={{ duration: 0.2 }}
            className="absolute right-0 mt-2 w-80 sm:w-96 bg-white rounded-xl shadow-xl border border-gray-100 z-50 overflow-hidden"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50/50">
              <h3 className="font-semibold text-gray-800">Thông báo</h3>
              {unreadCount > 0 && (
                <button 
                  onClick={markAllAsRead}
                  className="text-xs font-medium text-[#004c91] hover:underline"
                >
                  Đánh dấu đã đọc
                </button>
              )}
            </div>

            <div className="max-h-[70vh] overflow-y-auto">
              {notifications.length === 0 ? (
                <div className="p-6 text-center text-gray-500">
                  <p className="text-sm">Không có thông báo nào</p>
                </div>
              ) : (
                <div className="flex flex-col divide-y divide-gray-50">
                  {notifications.map((notif) => (
                    <button
                      key={notif.id}
                      onClick={() => handleItemClick(notif)}
                      className={`flex flex-col gap-1 p-4 text-left transition-colors hover:bg-gray-50 \${
                        !notif.isRead ? 'bg-blue-50/30' : ''
                      }`}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <span className={`text-sm font-medium \${!notif.isRead ? 'text-gray-900' : 'text-gray-700'}`}>
                          {notif.title}
                        </span>
                        <span className="text-[10px] text-gray-500 shrink-0 mt-0.5">
                          {notif.time}
                        </span>
                      </div>
                      <p className="text-xs text-gray-600 line-clamp-2">
                        {notif.desc}
                      </p>
                    </button>
                  ))}
                </div>
              )}
            </div>
            
            <div className="p-2 border-t border-gray-100 bg-gray-50 text-center">
              <button className="text-xs font-medium text-gray-500 hover:text-gray-900 transition-colors">
                Xem tất cả thông báo
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
