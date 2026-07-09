import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X } from 'lucide-react';
import logo from '../../assets/images/2021-FPTU-Eng.png';
import type { LoginPortal } from '../../features/authentication/types/authentication.types';
import { InternalLoginForm, VisitorLoginForm } from '../../features/authentication/components/DualPortalLoginForms';
import { useTranslation } from 'react-i18next';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function LoginModal({ isOpen, onClose }: LoginModalProps) {
  const { t } = useTranslation(['loginModal']);
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
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            className="relative bg-white/95 backdrop-blur-xl w-full max-w-[440px] rounded-[24px] shadow-[0_20px_60px_-15px_rgba(0,0,0,0.1)] border border-white/80 overflow-hidden"
          >
            {/* Top decorative gradient bar */}
            <div className="absolute top-0 left-0 right-0 h-1.5 bg-gradient-to-r from-[#004c91] via-[#00a3e0] to-[#f37021]" />

            <div className="p-6 md:p-7 flex flex-col items-center">
              <button 
                onClick={handleClose}
                className="absolute top-5 right-5 p-2 text-gray-400 hover:text-gray-700 hover:bg-gray-100/80 rounded-full transition-colors outline-none focus:ring-2 focus:ring-[#004c91]/20"
              >
                <X className="w-5 h-5" />
              </button>

              <img src={logo} alt="FPT University" className="h-14 md:h-16 mb-5 object-contain drop-shadow-sm" />

              <h2 className="text-[#004c91] text-2xl font-black text-center leading-tight tracking-tight">
                {t('loginModal:title')}
              </h2>
              <p className="text-gray-500 text-[13px] text-center mb-6 font-medium mt-1">
                {t('loginModal:subtitle')}
              </p>

              {/* Portal Tabs (Segmented Control style) */}
              <div className="w-full mb-5">
                <div className="flex p-1 bg-gray-100/80 rounded-xl border border-gray-200/50">
                  <button
                    type="button"
                    onClick={() => setPortal('INTERNAL')}
                    className={`flex-1 py-2.5 text-[13px] font-bold rounded-lg transition-all duration-300 ${
                      portal === 'INTERNAL'
                        ? 'bg-white text-[#004c91] shadow-[0_2px_8px_rgba(0,0,0,0.08)] scale-100'
                        : 'text-gray-500 hover:text-gray-800 hover:bg-gray-200/50'
                    }`}
                  >
                    {t('loginModal:internal')}
                  </button>
                  <button
                    type="button"
                    onClick={() => setPortal('VISITOR')}
                    className={`flex-1 py-2.5 text-[13px] font-bold rounded-lg transition-all duration-300 ${
                      portal === 'VISITOR'
                        ? 'bg-white text-[#004c91] shadow-[0_2px_8px_rgba(0,0,0,0.08)] scale-100'
                        : 'text-gray-500 hover:text-gray-800 hover:bg-gray-200/50'
                    }`}
                  >
                    {t('loginModal:visitor')}
                  </button>
                </div>
                <p className="text-[12px] text-gray-500 text-center mt-3 px-2">
                  {portal === 'INTERNAL' 
                    ? t('loginModal:internalDesc')
                    : t('loginModal:visitorDesc')}
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

