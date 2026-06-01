// Đây là component cửa sổ bật lên chứa biểu mẫu đăng ký tham quan
import React, { useState, useEffect } from 'react';
import { X, Plus, Trash2 } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';

interface VisitingFormPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

interface Visitor {
  id: string;
  fullName: string;
  jobTitle: string;
  organization: string;
  nationality: string;
}

export function VisitingFormPopup({ isOpen, onClose }: VisitingFormPopupProps) {
  const [visitors, setVisitors] = useState<Visitor[]>([
    { id: '1', fullName: '', jobTitle: '', organization: '', nationality: '' }
  ]);

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  const addVisitor = () => {
    setVisitors([
      ...visitors,
      { id: Date.now().toString(), fullName: '', jobTitle: '', organization: '', nationality: '' }
    ]);
  };

  const removeVisitor = (id: string) => {
    setVisitors(visitors.filter(v => v.id !== id));
  };

  const updateVisitor = (id: string, field: keyof Visitor, value: string) => {
    setVisitors(visitors.map(v => v.id === id ? { ...v, [field]: value } : v));
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[100] bg-black/50 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.2 }}
            onClick={(e) => e.stopPropagation()}
            className="bg-white w-full max-w-4xl max-h-[90vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative"
          >
            {/* Header */}
            <div className="flex-none p-6 sm:p-8 flex items-center justify-between bg-[#004c91] relative z-10">
              <div>
                <h2 className="text-2xl sm:text-3xl font-bold text-white">Visiting Request Form</h2>
                <p className="text-blue-100 mt-2">Please fill out the form below to request a campus visit.</p>
              </div>
              <button 
                onClick={onClose}
                className="p-2 text-white hover:bg-white/20 rounded-full transition-colors flex-shrink-0 ml-4"
              >
                <X className="w-6 h-6" />
              </button>
            </div>

            {/* Form Body */}
            <div className="flex-1 overflow-y-auto p-6 sm:p-8 bg-gray-50/50">
              <form className="space-y-8" onSubmit={(e) => e.preventDefault()}>
                {/* 1. Organization/Group Name */}
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">
                    Organization/Group Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                    placeholder="Enter your organization or group name"
                  />
                </div>

                {/* 2 & 3. Date/Time & Purpose */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-2">
                      Expected Date & Time of Visit <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="datetime-local"
                      required
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-2">
                      Purpose of Visit <span className="text-red-500">*</span>
                    </label>
                    <select
                      required
                      defaultValue=""
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm appearance-none"
                    >
                      <option value="" disabled>Select purpose...</option>
                      <option value="campus_tour">Campus Tour</option>
                      <option value="academic_exchange">Academic Exchange</option>
                      <option value="partnership_exploration">Partnership Exploration</option>
                      <option value="other">Other</option>
                    </select>
                  </div>
                </div>

                {/* 4. Proposed Agenda */}
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">
                    Proposed Agenda/Discussion Topics <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    required
                    rows={4}
                    className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm resize-none"
                    placeholder="Briefly describe what you would like to discuss or do during the visit..."
                  ></textarea>
                </div>

                {/* 5. Visitor List */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-sm font-semibold text-gray-700">
                      Visitor List <span className="text-red-500">*</span>
                    </label>
                    <span className="text-xs text-gray-500">At least one visitor required</span>
                  </div>
                  
                  <div className="space-y-4">
                    {visitors.map((visitor, index) => (
                      <div key={visitor.id} className="p-4 sm:p-5 bg-white border border-gray-200 shadow-sm rounded-xl relative group transition-colors hover:border-gray-300">
                        <div className="absolute -top-3 -left-3 w-7 h-7 bg-fpt-orange text-white rounded-full flex items-center justify-center text-xs font-bold shadow-md">
                          {index + 1}
                        </div>
                        
                        {visitors.length > 1 && (
                          <button
                            type="button"
                            onClick={() => removeVisitor(visitor.id)}
                            className="absolute top-4 right-4 text-gray-400 hover:text-red-500 transition-colors"
                          >
                            <Trash2 className="w-5 h-5" />
                          </button>
                        )}
                        
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-2">
                          <div>
                            <input
                              type="text"
                              required
                              placeholder="Full Name *"
                              value={visitor.fullName}
                              onChange={(e) => updateVisitor(visitor.id, 'fullName', e.target.value)}
                              className="w-full px-3 py-2 border-b-2 border-gray-200 focus:border-fpt-orange outline-none bg-transparent transition-colors"
                            />
                          </div>
                          <div>
                            <input
                              type="text"
                              required
                              placeholder="Job Title *"
                              value={visitor.jobTitle}
                              onChange={(e) => updateVisitor(visitor.id, 'jobTitle', e.target.value)}
                              className="w-full px-3 py-2 border-b-2 border-gray-200 focus:border-fpt-orange outline-none bg-transparent transition-colors"
                            />
                          </div>
                          <div>
                            <input
                              type="text"
                              required
                              placeholder="Organization *"
                              value={visitor.organization}
                              onChange={(e) => updateVisitor(visitor.id, 'organization', e.target.value)}
                              className="w-full px-3 py-2 border-b-2 border-gray-200 focus:border-fpt-orange outline-none bg-transparent transition-colors"
                            />
                          </div>
                          <div>
                            <input
                              type="text"
                              required
                              placeholder="Nationality *"
                              value={visitor.nationality}
                              onChange={(e) => updateVisitor(visitor.id, 'nationality', e.target.value)}
                              className="w-full px-3 py-2 border-b-2 border-gray-200 focus:border-fpt-orange outline-none bg-transparent transition-colors"
                            />
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>

                  <button
                    type="button"
                    onClick={addVisitor}
                    className="mt-4 flex items-center gap-2 text-fpt-orange font-medium hover:text-orange-700 transition-colors bg-orange-50 px-4 py-2 rounded-lg"
                  >
                    <Plus className="w-4 h-4" />
                    Add Visitor
                  </button>
                </div>

                {/* 6 & 7. Supporting Team & Language */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-2">
                      Supporting Team (If applicable) <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      required
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                      placeholder="Name or details of assisting team"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-3">
                      Preferred Language <span className="text-red-500">*</span>
                    </label>
                    <div className="flex items-center gap-6 mt-2">
                      <label className="flex items-center gap-2 cursor-pointer group">
                        <div className="relative flex items-center">
                          <input type="radio" name="language" value="english" required className="w-5 h-5 text-fpt-orange border-gray-300 focus:ring-fpt-orange" />
                        </div>
                        <span className="text-gray-700 group-hover:text-gray-900">English</span>
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer group">
                        <div className="relative flex items-center">
                          <input type="radio" name="language" value="vietnamese" required className="w-5 h-5 text-fpt-orange border-gray-300 focus:ring-fpt-orange" />
                        </div>
                        <span className="text-gray-700 group-hover:text-gray-900">Vietnamese</span>
                      </label>
                    </div>
                  </div>
                </div>

                {/* 8. Contact Information */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-2">
                      Contact Email <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="email"
                      required
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                      placeholder="example@domain.com"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 mb-2">
                      Contact Phone <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="tel"
                      required
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                      placeholder="+1 234 567 8900"
                    />
                  </div>
                </div>

                {/* 9. Transportation Details */}
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">
                    Transportation Details <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    required
                    className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-fpt-orange focus:ring-1 focus:ring-fpt-orange outline-none transition-shadow bg-white shadow-sm"
                    placeholder="E.g., Bus 45 seats, License plate: 29A-12345"
                  />
                  <p className="text-xs text-gray-500 mt-2 ml-1">We need this information to arrange parking spaces.</p>
                </div>
              </form>
            </div>

            {/* Footer */}
            <div className="flex-none p-6 border-t border-gray-100 bg-white flex justify-end gap-4">
              <button
                type="button"
                onClick={onClose}
                className="px-6 py-2.5 rounded-xl font-medium text-gray-700 hover:bg-gray-100 transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="px-8 py-2.5 rounded-xl font-medium text-white bg-fpt-orange hover:bg-orange-600 shadow-md shadow-orange-500/30 transition-all transform hover:-translate-y-0.5"
              >
                Submit Request
              </button>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
