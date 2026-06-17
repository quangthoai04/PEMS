import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X } from 'lucide-react';
import logo from '../../assets/images/2021-FPTU-Eng.png';
import type { LoginPortal } from '../../features/authentication/types/authentication.types';
import { InternalLoginForm, VisitorLoginForm } from '../../features/authentication/components/DualPortalLoginForms';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function LoginModal({ isOpen, onClose }: LoginModalProps) {
  const [portal, setPortal] = useState<LoginPortal>('INTERNAL');

  const handleClose = () => {
    onClose();
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="fixed inset-0 z-[200] flex items-center justify-center p-4">
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={handleClose}
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
          />

          {/* Modal Container */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="relative bg-white w-full max-w-[460px] rounded-2xl shadow-2xl overflow-hidden"
          >
            <div className="p-6 md:p-8 flex flex-col items-center">
              <button 
                onClick={handleClose}
                className="absolute top-4 right-4 p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-full transition-colors"
              >
                <X className="w-5 h-5" />
              </button>

              <img src={logo} alt="FPT University" className="h-16 md:h-20 mb-6 object-contain" />

              <h2 className="text-[#004c91] text-xl md:text-2xl font-black text-center leading-tight mb-2 tracking-tight">
                PEMS Login
              </h2>
              <p className="text-gray-500 text-[14px] text-center mb-6 font-medium">
                Partnership Engagement Management System
              </p>

              {/* Portal Tabs */}
              <div className="w-full mb-6">
                <div className="flex p-1 bg-gray-100 rounded-xl">
                  <button
                    type="button"
                    onClick={() => setPortal('INTERNAL')}
                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${
                      portal === 'INTERNAL'
                        ? 'bg-white text-[#004c91] shadow-sm'
                        : 'text-gray-500 hover:text-gray-700'
                    }`}
                  >
                    Nội bộ (Internal)
                  </button>
                  <button
                    type="button"
                    onClick={() => setPortal('VISITOR')}
                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${
                      portal === 'VISITOR'
                        ? 'bg-white text-[#004c91] shadow-sm'
                        : 'text-gray-500 hover:text-gray-700'
                    }`}
                  >
                    Khách (Visitor)
                  </button>
                </div>
                <p className="text-xs text-gray-500 text-center mt-3">
                  {portal === 'INTERNAL' 
                    ? 'Dành cho Cán bộ, Giảng viên, và Sinh viên FPTU.' 
                    : 'Dành cho Khách, Đối tác theo dõi thông tin chuyến thăm.'}
                </p>
              </div>

              {/* Shared Forms */}
              <div className="w-full text-left">
                {portal === 'INTERNAL' ? (
                  <InternalLoginForm onSuccess={handleClose} />
                ) : (
                  <VisitorLoginForm onSuccess={handleClose} />
                )}
              </div>
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
