import React from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X } from 'lucide-react';
import logo from '../../assets/images/2021-FPTU-Eng.png';
import { LoginForm } from '../../features/authentication/components/LoginForm';
import { useTranslation } from 'react-i18next';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function LoginModal({ isOpen, onClose }: LoginModalProps) {
  const { t } = useTranslation(['loginModal']);

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
          {/* Tween ngắn, không translateY: spring + scale + trượt dọc để lại một nhịp settle nhỏ ngay
              sau khi modal hiện ra — nhìn như bị giật một cái. Fade + scale nhẹ vào đúng vị trí thì
              không có nhịp thừa nào để thấy. */}
          <motion.div
            initial={{ opacity: 0, scale: 0.98 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.98 }}
            transition={{ duration: 0.16, ease: 'easeOut' }}
            className="relative bg-white/95 backdrop-blur-xl w-full max-w-[380px] rounded-[24px] shadow-[0_20px_60px_-15px_rgba(0,0,0,0.1)] border border-white/80 overflow-hidden"
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

              <img src={logo} alt="FPT University" className="h-20 md:h-24 mb-5 object-contain drop-shadow-sm" />

              <h2 className="text-[#004c91] text-2xl font-black text-center leading-tight tracking-tight">
                {t('loginModal:title')}
              </h2>
              <p className="text-gray-500 text-[13px] text-center mb-6 font-normal mt-1">
                {t('loginModal:subtitle')}
              </p>

              <div className="w-full text-left">
                <LoginForm onSuccess={handleClose} />
              </div>
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

