/**
 * Component VisitDuringTab
 * Cụm module chức năng giám sát thực thi biểu mẫu thời gian thực khách tham quan.
 */

import React, { useState, useRef, useEffect } from 'react';
import { 
  ChevronDown, 
  ChevronUp, 
  Star, 
  Calendar, 
  Users, 
  Clock, 
  CheckSquare, 
  Square, 
  Upload, 
  AlertCircle,
  FileText, 
  Building2, 
  User, 
  Briefcase, 
  Phone, 
  Mail, 
  ExternalLink,
  Plus,
  CheckCircle2,
  X,
  Check,
  Globe,
  Link2,
  MapPin,
  Loader2
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { MinutesCard } from './MinutesCard';
import { LogisticsHandoverSection } from '../../../features/delegations/components/LogisticsHandoverSection';
import { vietnamNowDateTimeLocal } from '../../../shared/utils/vietnamTime';
import { businessCardOcrApi } from '../../../features/business-card-ocr/api/businessCardOcrApi';
import { showLoadingToast, updateToastSuccess, updateToastError } from '../../../shared/utils/toast';
import type { VisitProcessGuestMember } from '../../../features/delegations/types/delegations.types';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import { filesApi } from '../../../shared/api/filesApi';

export function VisitDuringTab({ isReadOnly = false, isDept = false, visitInstanceId, guestMembers = [], supportMembers = [] }: { isReadOnly?: boolean, isDept?: boolean, visitInstanceId?: number, guestMembers?: VisitProcessGuestMember[], supportMembers?: VisitProcessGuestMember[] }) {
  const navigate = useNavigate();
  // Image Upload and Card Scanning States
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [uploadedCardImage, setUploadedCardImage] = useState<string | null>(null);
  const [isScanned, setIsScanned] = useState(false);
  const [isScanning, setIsScanning] = useState(false);
  const [isGuideModalOpen, setIsGuideModalOpen] = useState(false);
  const [matchedPartnerFromEmail, setMatchedPartnerFromEmail] = useState<string | null>(null);

  const [suggestedPartners, setSuggestedPartners] = useState<string[]>([]);

  useEffect(() => {
    async function fetchMatches() {
      const allMembers = [...guestMembers, ...supportMembers];
      const orgs = Array.from(new Set(allMembers.map(g => g.organization).filter(Boolean))) as string[];
      if (!orgs.length) return;
      const suggestions = new Set<string>();
      for (const org of orgs) {
        try {
          const res = await partnersApi.matchPartner(org);
          if (res.partnerName) suggestions.add(res.partnerName);
          if (res.candidates) {
            res.candidates.forEach(c => suggestions.add(c.name));
          }
        } catch (err) {}
      }
      setSuggestedPartners(Array.from(suggestions));
    }
    fetchMatches();
  }, [guestMembers, supportMembers]);

  // State for Feedback table expansions
  const [expandedRow1, setExpandedRow1] = useState(false);
  const [expandedRow3, setExpandedRow3] = useState(false);
  const [expandedRow4, setExpandedRow4] = useState(false);

  const [ratings, setRatings] = useState<Record<string, number>>({
    "guide": 5, "agenda": 4, "service": 5, "facility": 5
  });
  const [feedbacks, setFeedbacks] = useState<Record<string, string>>({
    "guide": "Rất nhiệt tình và chuyên nghiệp.",
    "agenda": "Lịch trình hợp lý, đúng giờ.",
    "service": "Chu đáo, thân thiện.",
    "facility": "Cơ sở vật chất hiện đại, khang trang."
  });

  const handleRating = (key: string, value: number) => setRatings(prev => ({ ...prev, [key]: value }));
  const handleFeedback = (key: string, value: string) => setFeedbacks(prev => ({ ...prev, [key]: value }));

  // Meeting Minutes state
  const [notes, setNotes] = useState("Đoàn đánh giá cao cơ sở vật chất.\nTrao đổi tích cực về cơ hội hợp tác.");
  const [actionItems, setActionItems] = useState([
    { id: 1, text: "Gửi tài liệu giới thiệu chương trình", done: false, deadline: "2023-10-30" },
    { id: 2, text: "Lên lịch họp chuyên sâu", done: false, deadline: "2023-11-05" }
  ]);
  const [meetingCompleted, setMeetingCompleted] = useState(false);

  // Scan result state
  // Main sections open/close state
  const [isSection1Expanded, setIsSection1Expanded] = useState(true);
  const [isSection2Expanded, setIsSection2Expanded] = useState(true);
  const [isSection3Expanded, setIsSection3Expanded] = useState(true);
  const [isSection4Expanded, setIsSection4Expanded] = useState(true);

  // Meeting Minutes state
  const [isMeetingMinutesCreatedState, setIsMeetingMinutesCreated] = useState(false);
  const isMeetingMinutesCreated = isMeetingMinutesCreatedState || isReadOnly;
  const [isMeetingMinutesModalOpen, setIsMeetingMinutesModalOpen] = useState(false);
  const [meetingDate, setMeetingDate] = useState("2023-11-20");
  const [meetingName, setMeetingName] = useState("Họp trao đổi hợp tác");
  const [attendees, setAttendees] = useState(["BGH FPTU", "Quản lý IC", "Đại diện ĐH Tokyo"]);
  const [newAttendee, setNewAttendee] = useState("");
  const [isAddingAttendee, setIsAddingAttendee] = useState(false);

  // Redesigned participant list with confirmation, organization, and partner status
  const [participantsList, setParticipantsList] = useState([
    { id: 1, name: "Prof. Kenji Suzuki", role: "Giám đốc Hợp tác Quốc tế", org: "Đại học Tokyo", isPartner: true, confirmed: true },
    { id: 2, name: "Dr. Yuki Tanaka", role: "Trưởng phòng Đào tạo", org: "Đại học Tokyo", isPartner: true, confirmed: true },
    { id: 3, name: "Mr. Satoshi Nakamoto", role: "Đại diện Nghiên cứu", org: "Đại học Keio", isPartner: false, confirmed: false },
    { id: 4, name: "Nguyễn Văn Nhật", role: "Quản lý Dự án", org: "Tập đoàn công nghệ FPT", isPartner: false, confirmed: true },
    { id: 5, name: "Jean-Pierre", role: "Chuyên viên Nghiệp vụ", org: "Đại học Paris-Saclay", isPartner: false, confirmed: false },
    { id: 6, name: "Nguyễn Thị Hồng Hạnh", role: "Cán bộ IC", org: "Phòng Hợp tác Quốc tế (IC)", isPartner: false, confirmed: true, isInternal: true },
    { id: 7, name: "Trần Thế Minh", role: "Cán bộ IC", org: "Phòng Hợp tác Quốc tế (IC)", isPartner: false, confirmed: true, isInternal: true },
    { id: 8, name: "Phạm Minh Thư", role: "Student / Sinh viên hỗ trợ", org: "Đại học FPT", isPartner: false, confirmed: true, isInternal: true }
  ]);

  const [isPartnerModalOpen, setIsPartnerModalOpen] = useState(false);
  const [activeParticipantId, setActiveParticipantId] = useState<number | null>(null);
  
  // Modal states for creating partner
  const [partnerCode, setPartnerCode] = useState("");
  const [partnerName, setPartnerName] = useState("");
  const [partnerCountry, setPartnerCountry] = useState("");
  const [showModalValidationErr, setShowModalValidationErr] = useState(false);

  // Quick Add new participant state
  const [quickName, setQuickName] = useState("");
  const [quickRole, setQuickRole] = useState("");
  const [quickOrg, setQuickOrg] = useState("");
  const [quickIsPartner, setQuickIsPartner] = useState(false);
  const [showQuickAddError, setShowQuickAddError] = useState(false);

  const handleOpenPartnerModal = (p: any) => {
    setActiveParticipantId(p.id);
    setPartnerName(p.org || "");
    setPartnerCode(p.org ? p.org.toUpperCase().replace(/\s+/g, '-').normalize("NFD").replace(/[\u0300-\u036f]/g, "").substring(0, 10) : "");
    setPartnerCountry("");
    setShowModalValidationErr(false);
    setIsPartnerModalOpen(true);
  };

  const handleSavePartner = () => {
    if (!partnerCode.trim() || !partnerName.trim() || !partnerCountry.trim()) {
      setShowModalValidationErr(true);
      return;
    }
    // Update partner status for all participants with the same org name
    setParticipantsList(prev => prev.map(p => {
      if (p.org.toLowerCase() === partnerName.toLowerCase().trim()) {
        return { ...p, isPartner: true };
      }
      if (p.id === activeParticipantId) {
        return { ...p, isPartner: true, org: partnerName.trim() };
      }
      return p;
    }));

    setIsPartnerModalOpen(false);
    setActiveParticipantId(null);
    setPartnerCode("");
    setPartnerName("");
    setPartnerCountry("");
  };

  const handleToggleConfirm = (id: number) => {
    setParticipantsList(prev => prev.map(p => {
      if (p.id === id) {
        return { ...p, confirmed: !p.confirmed };
      }
      return p;
    }));
  };

  const handleAddParticipant = () => {
    if (!quickName.trim() || !quickOrg.trim()) {
      setShowQuickAddError(true);
      return;
    }
    const newId = participantsList.length > 0 ? Math.max(...participantsList.map(p => p.id)) + 1 : 1;
    setParticipantsList(prev => [
      ...prev,
      {
        id: newId,
        name: quickName.trim(),
        role: quickRole.trim() || "Thành viên",
        org: quickOrg.trim(),
        isPartner: quickIsPartner,
        confirmed: true
      }
    ]);
    setQuickName("");
    setQuickRole("");
    setQuickOrg("");
    setQuickIsPartner(false);
    setShowQuickAddError(false);
  };

  const [currentOcrJobId, setCurrentOcrJobId] = useState<number | null>(null);

  // Scan result state
  const [scannedInfo, setScannedInfo] = useState({
    name: "Takahiro Sato",
    title: "Giám đốc Nhân sự",
    company: "Tập đoàn công nghệ FPT",
    phone: "0345678912",
    email: "takahiro@example.com",
    website: "https://fpt.com.vn",
    address: "Khu Công nghệ cao Hòa Lạc, Hà Nội"
  });

  // Document and Contact Linkage States
  const [partnerDocFile, setPartnerDocFile] = useState<{ name: string; size: string; type: string } | null>(null);
  const [selectedPartnerForDoc, setSelectedPartnerForDoc] = useState("");
  const docFileInputRef = useRef<HTMLInputElement>(null);
  const [docError, setDocError] = useState("");
  const [uploadedDocuments, setUploadedDocuments] = useState<Array<{
    id: number;
    fileName: string;
    fileSize: string;
    partner: string;
    uploadedAt: string;
  }>>(() => {
    if (visitInstanceId) {
      try {
        const saved = localStorage.getItem(`pems_visit_${visitInstanceId}_docs`);
        if (saved) return JSON.parse(saved);
      } catch (e) {}
    }
    return [];
  });

  const [selectedPartnerForContact, setSelectedPartnerForContact] = useState("");
  const [contactSaveSuccess, setContactSaveSuccess] = useState(false);
  const [savedContactsList, setSavedContactsList] = useState<Array<{
    name: string;
    title: string;
    company: string;
    phone: string;
    email: string;
    website: string;
    address: string;
    targetPartner: string;
  }>>(() => {
    if (visitInstanceId) {
      try {
        const saved = localStorage.getItem(`pems_visit_${visitInstanceId}_contacts`);
        if (saved) return JSON.parse(saved);
      } catch (e) {}
    }
    return [];
  });

  // Keep localStorage in sync when state changes
  useEffect(() => {
    if (!visitInstanceId) return;
    localStorage.setItem(`pems_visit_${visitInstanceId}_docs`, JSON.stringify(uploadedDocuments));
  }, [uploadedDocuments, visitInstanceId]);

  useEffect(() => {
    if (!visitInstanceId) return;
    localStorage.setItem(`pems_visit_${visitInstanceId}_contacts`, JSON.stringify(savedContactsList));
  }, [savedContactsList, visitInstanceId]);

  const getAvailablePartners = () => {
    // 1. Existing partners in the system: ONLY suggested partners from DB (similar names)
    const existing = Array.from(new Set([
      ...suggestedPartners
    ]));
    // 2. Draft partners: Guest members' organizations and OCR matches ONLY
    const allMembers = [...guestMembers, ...supportMembers];
    const uniqueDrafts = Array.from(new Set([
      ...allMembers.filter(g => g.organization).map(g => g.organization as string),
      ...(matchedPartnerFromEmail ? [matchedPartnerFromEmail] : [])
    ]));
    
    return {
      existing,
      drafts: uniqueDrafts
    };
  };

  const [isUploadingDoc, setIsUploadingDoc] = useState(false);
  const handleAddDocument = async () => {
    if (!partnerDocFile || !docFileInputRef.current?.files?.[0]) {
      setDocError("Vui lòng tải lên tài liệu / ảnh.");
      return;
    }
    if (!selectedPartnerForDoc) {
      setDocError("Vui lòng chọn đối tác liên kết.");
      return;
    }
    setDocError("");
    setIsUploadingDoc(true);
    const toastId = showLoadingToast('Đang tải lên tài liệu...', 'upload-doc');
    try {
      // 1. Resolve partner ID
      let partnerId: number | null = null;
      const searchRes = await partnersApi.getPartners({ search: selectedPartnerForDoc, pageSize: 1 });
      if (searchRes.items.length > 0 && searchRes.items[0].name.toLowerCase() === selectedPartnerForDoc.toLowerCase()) {
        partnerId = searchRes.items[0].partnerId;
      } else {
        const newP = await partnersApi.createPartner({
          name: selectedPartnerForDoc,
          source: 'MANUAL'
        });
        partnerId = newP.partnerId;
      }

      // 2. Upload file
      const uploadedFile = await filesApi.upload(docFileInputRef.current.files[0], 'PARTNER_DOCUMENT');

      // 3. Save document to partner
      await partnersApi.addDocument(partnerId, {
         fileId: uploadedFile.fileId,
         title: partnerDocFile.name,
         documentCategory: 'MOU_MOA_AGREEMENT'
      });

      const newDoc = {
        id: Date.now(),
        fileName: partnerDocFile.name,
        fileSize: partnerDocFile.size,
        partner: selectedPartnerForDoc,
        uploadedAt: vietnamNowDateTimeLocal().replace('T', ' ')
      };
      setUploadedDocuments(prev => [...prev, newDoc]);
      setPartnerDocFile(null);
      if (docFileInputRef.current) {
        docFileInputRef.current.value = "";
      }
      updateToastSuccess(toastId, 'Tải lên tài liệu thành công!');
    } catch (e: any) {
      updateToastError(toastId, e, 'Lỗi khi tải lên tài liệu.');
    } finally {
      setIsUploadingDoc(false);
    }
  };

  const handleCancelDocument = () => {
    setPartnerDocFile(null);
    setSelectedPartnerForDoc("");
    setDocError("");
    if (docFileInputRef.current) {
      docFileInputRef.current.value = "";
    }
  };

  const [isSavingContact, setIsSavingContact] = useState(false);
  const handleSaveContactToPartner = async () => {
    if (!selectedPartnerForContact) {
      alert("Vui lòng chọn đối tác để lưu thông tin liên hệ!");
      return;
    }
    setIsSavingContact(true);
    const toastId = showLoadingToast('Đang lưu thông tin liên hệ...', 'save-contact');
    try {
      let partnerId: number | null = null;
      const searchRes = await partnersApi.getPartners({ search: selectedPartnerForContact, pageSize: 1 });
      if (searchRes.items.length > 0 && searchRes.items[0].name.toLowerCase() === selectedPartnerForContact.toLowerCase()) {
        partnerId = searchRes.items[0].partnerId;
      } else {
        const newP = await partnersApi.createPartner({
          name: selectedPartnerForContact,
          source: 'BUSINESS_CARD_OCR'
        });
        partnerId = newP.partnerId;
      }

      if (currentOcrJobId) {
        await businessCardOcrApi.confirmContact(currentOcrJobId, {
          partnerId,
          fullName: scannedInfo.name,
          jobTitle: scannedInfo.title,
          phone: scannedInfo.phone,
          email: scannedInfo.email,
          note: `Trích xuất từ card visit (VisitInstance: ${visitInstanceId})`
        });
      } else {
         await partnersApi.createContact(partnerId, {
            fullName: scannedInfo.name,
            jobTitle: scannedInfo.title,
            phone: scannedInfo.phone,
            email: scannedInfo.email,
            note: `Trích xuất từ card visit (VisitInstance: ${visitInstanceId})`
         });
      }

      setSavedContactsList(prev => [
        ...prev,
        {
          ...scannedInfo,
          targetPartner: selectedPartnerForContact
        }
      ]);
      setContactSaveSuccess(true);
      updateToastSuccess(toastId, 'Lưu thông tin liên hệ thành công!');
      setTimeout(() => setContactSaveSuccess(false), 5000);
    } catch (e: any) {
      updateToastError(toastId, e, 'Lỗi khi lưu thông tin liên hệ.');
    } finally {
      setIsSavingContact(false);
    }
  };

  const renderStars = (key: string) => {
    return (
      <div className="flex gap-1">
        {[1, 2, 3, 4, 5].map((star) => (
          <button 
            key={star} 
            type="button" 
            onClick={() => !isReadOnly && handleRating(key, star)}
            disabled={isReadOnly}
            className={`focus:outline-none ${isReadOnly ? 'cursor-default' : 'cursor-pointer'}`}
          >
            <Star 
              className={`w-5 h-5 ${ratings[key] >= star ? 'fill-yellow-400 text-yellow-400' : `text-gray-300 ${isReadOnly ? '' : 'hover:text-yellow-200'}`} transition-colors`} 
            />
          </button>
        ))}
      </div>
    );
  };

  return (
    <div className="space-y-8 animate-in fade-in zoom-in-95 duration-300">

      {/* Ký mượn tài sản hậu cần — phần đầu tiên của tab Đang tiếp khách (real handover API). */}
      {visitInstanceId && (
        <LogisticsHandoverSection 
          visitInstanceId={visitInstanceId} 
          canManage={!isReadOnly && !isDept} 
          handoverPhase="BORROW" 
          sectionNumber="1" 
          theme="blue" 
        />
      )}

      {/* Đánh giá chuyến thăm đã chuyển sang tab Sau tiếp khách (VisitAfterTab) —
          chỉ đánh giá sau khi Host xác nhận kết thúc tiếp khách. */}

      {/* 2. Biên bản cuộc họp — bản thật (backend + cơ chế lock) khi có visitInstanceId; nếu không, dùng mock cũ */}
      {visitInstanceId ? (
        <MinutesCard visitInstanceId={visitInstanceId} isReadOnly={isReadOnly} />
      ) : (
      <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm transition-all relative">
        <div
          className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
          onClick={() => setIsSection2Expanded(!isSection2Expanded)}
        >
          <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
             <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">2</span>
             Biên bản cuộc họp
          </h2>
          <button className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
            {isSection2Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
          </button>
        </div>
        <AnimatePresence>
          {isSection2Expanded && (
            <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
              <fieldset disabled={isReadOnly} className="contents">
                <div className="p-4 sm:p-6 md:p-8">
                {!isMeetingMinutesCreated ? (
                  <div className="text-center py-6">
                    <button 
                      onClick={() => setIsMeetingMinutesModalOpen(true)}
                      className="px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl shadow-sm hover:bg-[#e0611d] transition-colors inline-flex items-center gap-2"
                    >
                      <Plus className="w-5 h-5" />
                      Tạo biên bản cuộc họp
                    </button>
                  </div>
                ) : (
                  <>
                     <div className="flex flex-col sm:flex-row sm:items-start gap-6 mb-8">
                        <div className="flex-1 w-full max-w-[450px]">
                           <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Tên biên bản</label>
                           <input 
                             type="text" 
                             value={meetingName}
                             onChange={(e) => setMeetingName(e.target.value)}
                             className="bg-blue-50 text-blue-900 px-4 py-2.5 rounded-xl font-bold border border-blue-200 outline-none w-full focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all"
                             placeholder="Nhập tên biên bản..."
                           />
                        </div>
                        <div>
                           <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Thời gian</label>
                           <div className="bg-blue-50 text-blue-900 px-4 py-2.5 rounded-xl font-bold flex items-center gap-2 border border-blue-200 transition-all focus-within:ring-2 focus-within:ring-[#004c91]/20 focus-within:border-[#004c91]">
                              <Calendar className="w-5 h-5 text-blue-600 shrink-0" />
                              <input 
                                type="date" 
                                value={meetingDate}
                                onChange={(e) => setMeetingDate(e.target.value)}
                                className="bg-transparent border-none outline-none font-bold text-blue-900 cursor-pointer w-full"
                              />
                           </div>
                        </div>
                     </div>

                      {/* Redesigned Participant Table Section */}
                      <div className="mb-8 overflow-hidden rounded-xl border border-gray-200 bg-white">
                        <div className="bg-gray-50/70 px-5 py-3 border-b border-gray-200 flex items-center justify-between">
                          <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2 font-sans">
                            <Users className="w-4 h-4 text-[#004c91]" />
                            Bảng danh sách chi tiết người tham gia cuộc họp
                          </h3>
                          <span className="text-xs bg-[#004c91]/10 text-[#004c91] px-2.5 py-1 rounded-full font-bold">
                            {participantsList.length} thành viên
                          </span>
                        </div>
                        
                        <div className="overflow-x-auto max-h-[300px] overflow-y-auto">
                          <table className="w-full text-left border-collapse text-sm">
                            <thead className="sticky top-0 bg-white z-10 shadow-[0_1px_0_rgba(229,231,235,1)]">
                              <tr className="border-b border-gray-200 bg-gray-100/50 text-[11px] uppercase tracking-wider text-gray-500 font-extrabold font-sans">
                                <th className="px-4 py-3 text-center w-20">Có mặt</th>
                                <th className="px-5 py-3">Đại biểu tham gia</th>
                                <th className="px-5 py-3">Đơn vị của khách</th>
                                <th className="px-5 py-3 font-semibold">Tình trạng đối tác</th>
                                <th className="px-5 py-3 text-center">Thao tác</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100">
                              {participantsList.map((p) => (
                                <tr key={p.id} className="hover:bg-gray-50/55 transition-colors">
                                  <td className="px-4 py-4 text-center">
                                    <button
                                      type="button"
                                      disabled={isReadOnly}
                                      onClick={() => handleToggleConfirm(p.id)}
                                      className="inline-flex items-center justify-center focus:outline-none transition-transform active:scale-90"
                                    >
                                      {p.confirmed ? (
                                        <CheckSquare className="w-5 h-5 text-green-600 fill-green-50" />
                                      ) : (
                                        <Square className="w-5 h-5 text-gray-300 hover:text-gray-400" />
                                      )}
                                    </button>
                                  </td>
                                  <td className="px-5 py-4 font-sans">
                                    <div className="font-semibold text-gray-900">{p.name}</div>
                                    <div className="text-xs text-gray-500 font-medium">{p.role}</div>
                                  </td>
                                  <td className="px-5 py-4 font-sans">
                                    <div className="flex items-center gap-1.5 text-gray-700 font-medium font-sans">
                                      <Building2 className="w-4 h-4 text-gray-400 shrink-0" />
                                      {p.org}
                                    </div>
                                  </td>
                                  <td className="px-5 py-4 font-sans">
                                    {p.isInternal ? null : p.isPartner ? (
                                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                                        <Check className="w-3 text-green-500 font-black shrink-0" /> Đã thành đối tác
                                      </span>
                                    ) : (
                                      <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200">
                                        <AlertCircle className="w-3 text-amber-500 shrink-0" /> Chưa là đối tác
                                      </span>
                                    )}
                                  </td>
                                  <td className="px-5 py-4 text-center font-sans">
                                    {p.isInternal ? null : !p.isPartner ? (
                                      <button
                                        type="button"
                                        disabled={isReadOnly || (isDept && isMeetingMinutesCreated)}
                                        onClick={() => handleOpenPartnerModal(p)}
                                        className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-bold bg-[#004c91] hover:bg-[#00386b] text-white disabled:opacity-50 disabled:pointer-events-none rounded-lg shadow-sm transition-all cursor-pointer"
                                      >
                                        <Plus className="w-3 h-3" /> Tạo đối tác
                                      </button>
                                    ) : (
                                      <span className="text-xs text-green-600 font-bold flex items-center justify-center gap-1">
                                        <Check className="w-4 h-4 text-green-500" /> Đã kết nối
                                      </span>
                                    )}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </div>

                     <div className="space-y-6">
                        <div>
                          <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#f37021] before:rounded-full">Ghi chú</h3>
                          <textarea 
                            className="w-full bg-gray-50/50 border border-gray-200 rounded-xl p-4 text-sm font-medium text-gray-800 min-h-[120px] focus:bg-white focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all outline-none resize-y"
                            value={notes}
                            onChange={e => setNotes(e.target.value)}
                            placeholder="Nhập ghi chú hoặc biên bản tham quan..."
                          />
                        </div>

                        <div>
                          <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#004c91] before:rounded-full">Đầu mục công việc</h3>
                          <div className="space-y-3 bg-gray-50/50 border border-gray-100 rounded-xl p-4">
                            {actionItems.map((item, index) => (
                              <div key={item.id} className="flex flex-col sm:flex-row sm:items-center gap-3 bg-white p-3 rounded-lg border border-gray-200 shadow-sm">
                                <div className="flex items-center gap-3 flex-1">
                                  <button onClick={() => {
                                    const newItems = [...actionItems];
                                    newItems[index].done = !newItems[index].done;
                                    setActionItems(newItems);
                                  }} className="text-[#004c91]">
                                    {item.done ? <CheckSquare className="w-5 h-5 text-green-600" /> : <Square className="w-5 h-5 text-gray-400" />}
                                  </button>
                                  <input 
                                    type="text" 
                                    value={item.text} 
                                    onChange={e => {
                                      const newItems = [...actionItems];
                                      newItems[index].text = e.target.value;
                                      setActionItems(newItems);
                                    }}
                                    className={`flex-1 bg-transparent border-none outline-none text-sm font-medium ${item.done ? 'line-through text-gray-400' : 'text-gray-800'}`}
                                    placeholder="Thêm hành động..."
                                  />
                                </div>
                                <div className="flex items-center gap-2">
                                   <Calendar className="w-4 h-4 text-orange-500" />
                                   <input type="date" value={item.deadline} onChange={e => {
                                      const newItems = [...actionItems];
                                      newItems[index].deadline = e.target.value;
                                      setActionItems(newItems);
                                   }} className="text-xs font-bold text-orange-700 bg-orange-50 px-2 py-1.5 rounded-md border border-orange-200 outline-none hover:border-orange-300" />
                                </div>
                              </div>
                            ))}
                            <button onClick={() => setActionItems([...actionItems, { id: Date.now(), text: '', done: false, deadline: '' }])} className="text-sm font-bold text-[#004c91] hover:text-[#003366] flex items-center gap-1.5 px-2 py-1 outline-none">
                               <Plus className="w-4 h-4" /> Thêm đầu mục công việc
                            </button>
                          </div>
                        </div>
                     </div>
                  </>
                )}
              </div>
            </fieldset>
          </motion.div>
          )}
        </AnimatePresence>
      </div>
      )}

       {/* 3. Đối tác & Tài liệu */}
       {!isDept && (
         <>
       <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm relative transition-all">
         <div 
          className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
          onClick={() => setIsSection4Expanded(!isSection4Expanded)}
         >
          <div>
            <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
               <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">3</span>
               Tài liệu
            </h2>
          </div>
          <button className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
            {isSection4Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
          </button>
        </div>
        <AnimatePresence>
          {isSection4Expanded && (
            <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
              <div className={`p-8 bg-gray-50/50 ${isReadOnly ? 'pointer-events-none opacity-90 select-none' : ''}`}>
                <div className="flex flex-col md:flex-row gap-8 text-left">
                  {/* Left form side */}
                  <div className="flex-1 space-y-4">
                    <div className="space-y-2">
                      <label className="block text-sm font-bold text-gray-700">Tải lên tài liệu / Hình ảnh đối tác</label>
                      
                      <input 
                        type="file"
                        ref={docFileInputRef}
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (file) {
                            setPartnerDocFile({
                              name: file.name,
                              size: `${(file.size / (1024 * 1024)).toFixed(2)} MB`,
                              type: file.type
                            });
                          }
                        }}
                        className="hidden"
                      />
                      
                      <div 
                        onClick={() => docFileInputRef.current?.click()}
                        className="border-2 border-dashed border-gray-300 rounded-xl p-6 text-center hover:border-[#004c91] hover:bg-white cursor-pointer transition-all bg-gray-50/60 group"
                      >
                        <Upload className="w-8 h-8 text-[#004c91] mx-auto mb-2 group-hover:scale-110 transition-transform" />
                        {partnerDocFile ? (
                          <div className="space-y-1">
                            <p className="text-xs font-bold text-green-700 break-all">{partnerDocFile.name}</p>
                            <p className="text-[10px] text-gray-500 font-semibold">{partnerDocFile.size}</p>
                          </div>
                        ) : (
                          <div className="space-y-1">
                            <p className="text-xs font-bold text-gray-700">Kéo thả hoặc nhấp để chọn tệp tin</p>
                            <p className="text-[10px] text-gray-400">Chấp nhận ảnh (png, jpg), file văn bản (pdf, docx, xlsx)</p>
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="space-y-1.5 font-sans">
                      <label className="block text-sm font-bold text-gray-700">Chọn đối tác liên kết tài liệu này</label>
                      <select
                        value={selectedPartnerForDoc}
                        onChange={(e) => setSelectedPartnerForDoc(e.target.value)}
                        className="w-full px-3 py-2.5 border border-gray-300 rounded-xl text-xs font-medium outline-none bg-white focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all"
                      >
                        <option value="">-- Chọn đối tác --</option>
                        <optgroup label="Đối tác Draft (tạo ở trên)">
                          {getAvailablePartners().drafts.map((org, idx) => (
                            <option key={`doc-draft-${idx}`} value={org}>{org}</option>
                          ))}
                        </optgroup>
                        <optgroup label="Đối tác đã có trên hệ thống">
                          {getAvailablePartners().existing.map((org, idx) => (
                            <option key={`doc-existing-${idx}`} value={org}>{org}</option>
                          ))}
                        </optgroup>
                      </select>
                    </div>

                    {docError && (
                      <p className="text-red-500 text-xs font-bold flex items-center gap-1 font-sans">
                        <AlertCircle className="w-3.5 h-3.5" /> {docError}
                      </p>
                    )}

                    <div className="flex gap-3 pt-2 font-sans">
                      <button
                        disabled={isUploadingDoc}
                        onClick={handleAddDocument}
                        className="flex-1 bg-[#004c91] hover:bg-[#003366] text-white text-xs font-bold px-4 py-3 rounded-xl transition-all shadow-sm outline-none cursor-pointer disabled:opacity-50"
                      >
                        {isUploadingDoc ? 'Đang tải lên...' : 'Thêm tài liệu'}
                      </button>
                      <button
                        type="button"
                        onClick={handleCancelDocument}
                        className="px-4 py-3 bg-gray-100 hover:bg-gray-200 text-gray-700 text-xs font-bold rounded-xl transition-all outline-none cursor-pointer"
                      >
                        Hủy
                      </button>
                    </div>
                  </div>

                  {/* Right list side */}
                  <div className="flex-1 border-t md:border-t-0 md:border-l border-gray-200 pt-6 md:pt-0 md:pl-6 space-y-3 font-sans">
                    <h4 className="text-xs font-bold text-gray-500 uppercase tracking-wider">Danh sách tài liệu đã lưu trữ ({uploadedDocuments.length})</h4>
                    
                    {uploadedDocuments.length === 0 ? (
                      <div className="p-4 sm:p-6 md:p-8 text-center border-2 border-dashed border-gray-200 rounded-xl text-gray-400 text-xs bg-white">
                        Chưa có tài liệu nào thuộc đối tác này được tải lên trong phiên.
                      </div>
                    ) : (
                      <div className="space-y-2.5 max-h-[260px] overflow-y-auto pr-1">
                        {uploadedDocuments.map((doc) => (
                          <div key={doc.id} className="p-3 bg-white border border-gray-200 rounded-xl flex items-start justify-between gap-3 text-xs shadow-sm transition-all hover:border-gray-300">
                            <div className="space-y-1 min-w-0 flex-1">
                              <div className="font-bold text-gray-800 break-all flex items-center gap-1.5 leading-snug">
                                <FileText className="w-4 h-4 text-orange-500 shrink-0" />
                                {doc.fileName}
                              </div>
                              <div className="text-[10px] text-gray-500 font-medium">Đối tác: <span className="font-bold text-[#004c91]">{doc.partner}</span> • {doc.fileSize}</div>
                              <div className="text-[9px] text-gray-400">{doc.uploadedAt}</div>
                            </div>
                            <button
                              type="button"
                              onClick={() => setUploadedDocuments(uploadedDocuments.filter(d => d.id !== doc.id))}
                              className="text-gray-400 hover:text-red-600 p-1.5 rounded-lg hover:bg-red-50 transition-colors cursor-pointer"
                            >
                              <X className="w-4 h-4" />
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
      
       {/* 4. Thông tin khác */}
       <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm relative transition-all">
         <div 
          className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
          onClick={() => setIsSection3Expanded(!isSection3Expanded)}
         >
          <div>
            <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
               <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">4</span>
               Thông tin khác
            </h2>
          </div>
          <button className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
            {isSection3Expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
          </button>
        </div>
        <AnimatePresence>
          {isSection3Expanded && (
            <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
              <div className={`p-8 ${isReadOnly ? 'pointer-events-none opacity-90 select-none' : ''}`}>
           <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              {/* Scan Section */}
              <div className="md:col-span-1 space-y-4">
                 <input 
                    type="file" 
                    ref={fileInputRef} 
                    onChange={async (e) => {
                      const file = e.target.files?.[0];
                      if (file) {
                        const reader = new FileReader();
                        reader.onload = (event) => {
                          if (event.target?.result) {
                            setUploadedCardImage(event.target.result as string);
                          }
                        };
                        reader.readAsDataURL(file);

                        setIsScanning(true);
                        const toastId = showLoadingToast('Đang phân tích danh thiếp...', 'scan-card');
                        try {
                          const result = await businessCardOcrApi.scan(file, { visitInstanceId });
                          setIsScanned(true);
                          setScannedInfo({
                            name: result.parsed?.fullName || "",
                            title: result.parsed?.jobTitle || "",
                            company: result.parsed?.organization || "",
                            phone: result.parsed?.phone || "",
                            email: result.parsed?.email || "",
                            website: result.parsed?.websiteUrl || "",
                            address: result.parsed?.address || ""
                          });
                          setCurrentOcrJobId(result.ocrJobId || null);
                          setMatchedPartnerFromEmail(result.matchedPartner?.partnerName || null);
                          updateToastSuccess(toastId, 'Đã trích xuất thông tin danh thiếp.');
                        } catch (err: any) {
                          updateToastError(toastId, err, 'Lỗi khi quét danh thiếp.');
                        } finally {
                          setIsScanning(false);
                          if (fileInputRef.current) fileInputRef.current.value = '';
                        }
                      }
                    }} 
                    accept="image/*" 
                    className="hidden" 
                 />
                 <div 
                    onClick={() => !isScanning && fileInputRef.current?.click()}
                    className={`border-2 border-dashed border-gray-300 rounded-2xl p-6 text-center transition-all group ${isScanning ? 'opacity-70 cursor-not-allowed' : 'hover:border-[#f37021] hover:bg-orange-50/30 cursor-pointer'}`}
                 >
                    <div className="w-16 h-16 bg-blue-50 rounded-full flex items-center justify-center mx-auto mb-4 group-hover:scale-110 transition-transform">
                      <Upload className="w-8 h-8 text-[#004c91]" />
                    </div>
                    <h3 className="text-base font-bold text-gray-800 mb-2">Scan Card Visit</h3>
                    <p className="text-xs text-gray-500 mb-4">Hoặc tải lên hình ảnh name card để tự động trích xuất thông tin</p>
                    <button disabled={isScanning} type="button" className="px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg shadow-sm hover:bg-[#003366] transition-colors w-full flex items-center justify-center gap-2 disabled:opacity-50">
                       {isScanning && <Loader2 className="w-4 h-4 animate-spin" />}
                       Tải ảnh lên / Chụp ảnh
                    </button>
                 </div>
                 
                 <div className="p-4 bg-gray-50 rounded-xl border border-gray-100 flex flex-col items-center">
                    <p className="text-xs font-bold text-gray-500 mb-2 text-center">Ảnh card đã tải lên</p>
                    <div className="w-full aspect-[1.6/1] bg-gray-200 rounded-lg flex items-center justify-center text-gray-400 overflow-hidden border border-gray-300 relative group">
                       {uploadedCardImage ? (
                         <>
                           <img src={uploadedCardImage} alt="Uploaded card" className="w-full h-full object-contain bg-white" referrerPolicy="no-referrer" />
                           <div className="absolute inset-0 bg-black/45 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                             <button 
                               type="button"
                               onClick={(e) => {
                                 e.stopPropagation();
                                 fileInputRef.current?.click();
                               }}
                               className="px-2 py-1 bg-[#004c91] text-white text-xs font-semibold rounded-md hover:bg-[#003366] transition-colors"
                             >
                               Đổi ảnh
                             </button>
                             <button 
                               type="button"
                               onClick={(e) => {
                                 e.stopPropagation();
                                 setUploadedCardImage(null);
                                 setIsScanned(false);
                                  setScannedInfo({
                                    name: "",
                                    title: "",
                                    company: "",
                                    phone: "",
                                    email: "",
                                    website: "",
                                    address: ""
                                  });
                                  setCurrentOcrJobId(null);
                                  setMatchedPartnerFromEmail(null);
                                 if (fileInputRef.current) {
                                   fileInputRef.current.value = '';
                                 }
                               }}
                               className="px-2 py-1 bg-red-600 text-white text-xs font-semibold rounded-md hover:bg-red-700 transition-colors"
                             >
                               Xóa
                             </button>
                           </div>
                         </>
                       ) : (
                         <div className="flex flex-col items-center justify-center text-center p-3">
                           <FileText className="w-8 h-8 opacity-50 mb-1" />
                           <span className="text-xs opacity-80">Chưa có ảnh</span>
                         </div>
                       )}
                    </div>
                 </div>
                 
                 {matchedPartnerFromEmail && (
                    <div className="mt-4 p-3 bg-indigo-50 border border-indigo-200 text-indigo-700 rounded-xl text-xs font-bold flex items-start gap-2.5 font-sans shadow-sm">
                      <span className="flex items-center justify-center w-6 h-6 bg-indigo-600 rounded-full text-white shrink-0 shadow-sm shadow-indigo-200 mt-0.5">
                        <CheckCircle2 className="w-4 h-4" />
                      </span>
                      <span>
                        Người này đã thuộc vào danh sách liên hệ của đối tác: <b className="text-indigo-900 border-b border-indigo-800 cursor-pointer hover:text-indigo-600 transition-colors">{matchedPartnerFromEmail}</b>
                      </span>
                    </div>
                 )}

                 <button 
                    type="button"
                    onClick={() => setIsGuideModalOpen(true)}
                    className="mt-4 w-full p-2.5 bg-amber-50 hover:bg-amber-100 border border-amber-200 text-amber-800 rounded-xl text-xs font-bold flex items-center justify-between transition-colors outline-none cursor-pointer"
                 >
                    <span className="flex items-center gap-1.5"><FileText className="w-4 h-4" /> Hướng dẫn</span>
                    <span className="bg-amber-200 text-amber-800 px-2 py-0.5 rounded-md text-[10px] uppercase">Xem chi tiết</span>
                 </button>

              </div>

                            {/* Form Section */}
              <div className="md:col-span-2">
                 <div className="bg-gray-50/50 p-6 rounded-2xl border border-gray-100 space-y-5 relative">
                    {isScanned && (
                      <div className="absolute -top-3 right-6 px-3 py-1 bg-green-500 text-white text-[10px] uppercase tracking-wider font-extrabold rounded-full shadow-md animate-bounce flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã trích xuất thông tin!
                      </div>
                    )}
                    <h3 className="text-sm font-bold text-[#004c91] mb-2 border-b border-gray-200 pb-2">Thông tin trích xuất (Có thể chỉnh sửa)</h3>
                    
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                       <div className="space-y-1.5">
                         <label className="flex items-center gap-1.5 text-xs font-bold text-gray-600"><User className="w-3.5 h-3.5" /> Họ tên người</label>
                         <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#f37021] outline-none" value={scannedInfo.name} onChange={e => setScannedInfo({...scannedInfo, name: e.target.value})} />
                       </div>
                       <div className="space-y-1.5">
                         <label className="flex items-center gap-1.5 text-xs font-bold text-gray-600"><Briefcase className="w-3.5 h-3.5" /> Chức danh, phòng ban</label>
                         <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#f37021] outline-none" value={scannedInfo.title} onChange={e => setScannedInfo({...scannedInfo, title: e.target.value})} />
                       </div>
                       <div className="sm:col-span-2 space-y-1.5">
                         <label className="flex items-center gap-1.5 text-xs font-bold text-gray-600"><Building2 className="w-3.5 h-3.5" /> Tên đối tác, công ty</label>
                         <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#f37021] outline-none" value={scannedInfo.company} onChange={e => setScannedInfo({...scannedInfo, company: e.target.value})} />
                       </div>
                       <div className="space-y-1.5">
                         <label className="flex items-center gap-1.5 text-xs font-bold text-gray-600"><Phone className="w-3.5 h-3.5" /> SĐT</label>
                         <input type="tel" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#f37021] outline-none" value={scannedInfo.phone} onChange={e => setScannedInfo({...scannedInfo, phone: e.target.value})} />
                       </div>
                       <div className="space-y-1.5">
                         <label className="flex items-center gap-1.5 text-xs font-bold text-gray-600"><Mail className="w-3.5 h-3.5" /> Email</label>
                         <input type="email" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#004c91] outline-none" value={scannedInfo.email} onChange={e => setScannedInfo({...scannedInfo, email: e.target.value})} />
                        </div>
                        
                        {/* New Website field */}
                        <div className="space-y-1.5 font-sans">
                          <label className="flex items-center gap-1.5 text-xs font-bold text-gray-650"><Link2 className="w-3.5 h-3.5" /> Link website</label>
                          <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#004c91] outline-none" value={scannedInfo.website || ''} onChange={e => setScannedInfo({...scannedInfo, website: e.target.value})} />
                        </div>
                        
                        {/* New Address field */}
                        <div className="sm:col-span-2 space-y-1.5 font-sans">
                          <label className="flex items-center gap-1.5 text-xs font-bold text-gray-650"><MapPin className="w-3.5 h-3.5" /> Địa chỉ</label>
                          <input type="text" className="w-full px-3 py-2.5 rounded-xl border border-gray-300 text-sm font-medium focus:border-[#004c91] outline-none" value={scannedInfo.address || ''} onChange={e => setScannedInfo({...scannedInfo, address: e.target.value})} />
                        </div>
                        


                        {/* Partner Association Selector */}
                        <div className="sm:col-span-2 bg-blue-50/50 p-4 rounded-xl border border-blue-100 space-y-2 mt-2 font-sans">
                          <label className="flex items-center gap-1.5 text-xs font-bold text-gray-700">
                            <Building2 className="w-3.5 h-3.5 text-[#004c91]" />
                            Chọn đối tác liên kết thông tin của Card Visit này
                          </label>
                          <select
                            value={selectedPartnerForContact}
                            onChange={(e) => {
                              setSelectedPartnerForContact(e.target.value);
                              setContactSaveSuccess(false);
                            }}
                            className="w-full px-3 py-2 border border-gray-300 rounded-xl text-xs font-medium outline-none bg-white focus:border-[#004c91]"
                          >
                            <option value="">-- Chọn đối tác lưu trữ --</option>
                            <optgroup label="Đối tác Draft (tạo ở trên)">
                              {getAvailablePartners().drafts.map((org, idx) => (
                                <option key={`sc-draft-${idx}`} value={org}>{org}</option>
                              ))}
                            </optgroup>
                            <optgroup label="Đối tác đã có trên hệ thống">
                              {getAvailablePartners().existing.map((org, idx) => (
                                <option key={`sc-existing-${idx}`} value={org}>{org}</option>
                              ))}
                            </optgroup>
                          </select>
                        </div>
                     </div>
                     
                     {contactSaveSuccess && (
                       <div className="p-3.5 bg-green-50 border border-green-200 text-green-700 rounded-xl text-xs font-bold flex items-center gap-2 mt-3 font-sans shadow-sm leading-relaxed">
                         <CheckCircle2 className="w-4 h-4 text-green-500 shrink-0" />
                         <span>Đã liên kết thành công Card Visit của <b>{scannedInfo.name}</b> vào đối tác <b>{selectedPartnerForContact}</b>!</span>
                       </div>
                     )}

                     {/* Show saved list of contacts linked this turn */}
                     {savedContactsList.length > 0 && (
                       <div className="mt-4 p-4 bg-white border border-gray-200 rounded-xl space-y-2 font-sans">
                         <p className="text-xs font-bold text-gray-700">Các liên hệ đã liên kết trong chuyến thăm:</p>
                         <div className="space-y-1.5 max-h-[120px] overflow-y-auto pr-1">
                           {savedContactsList.map((contact, idx) => (
                             <div key={idx} className="p-2.5 bg-gray-50 border border-gray-105 rounded-lg flex justify-between items-center text-xs">
                               <div>
                                 <span className="font-bold text-gray-800">{contact.name}</span> <span className="text-gray-500">({contact.title})</span>
                               </div>
                               <span className="text-[10px] font-bold bg-[#004c91]/10 text-[#004c91] px-2 py-0.5 rounded-full">{contact.targetPartner}</span>
                             </div>
                           ))}
                         </div>
                       </div>
                     )}

                     <div className="pt-4 flex justify-end font-sans">
                        <button 
                          type="button"
                          disabled={isSavingContact}
                          onClick={handleSaveContactToPartner}
                          className="px-6 py-2.5 bg-green-500 hover:bg-green-600 text-white font-bold rounded-lg shadow-sm transition-colors flex items-center gap-2 text-sm cursor-pointer disabled:opacity-50"
                        >
                           {isSavingContact ? 'Đang xử lý...' : 'Lưu thông tin liên hệ'}
                        </button>
                     </div>
                     </div>
                  </div>
               </div>
            </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
         </>
       )}

      {/* Read-only footer: the stage-transition CTA lives in VisitProcess's renderStageBar, not here. */}
      {isReadOnly && (
        <div className="flex flex-col md:flex-row justify-between items-stretch md:items-center gap-4 pt-4 border-t border-gray-100">
          <div className="w-full flex justify-end">
            <button
              onClick={() => navigate('/dashboard/visit')}
              className="px-8 py-3.5 font-bold text-white bg-[#004c91] hover:bg-[#003366] rounded-xl transition-all shadow-md hover:shadow-lg outline-none cursor-pointer flex items-center gap-2"
            >
              Quay lại danh sách tiếp khách
            </button>
          </div>
        </div>
      )}

      {/* Create Partner Dialog Modal */}
      {isPartnerModalOpen && (
        <div className="fixed inset-0 z-50 overflow-y-auto font-sans">
          {/* Backdrop wrapper */}
          <div className="flex min-h-screen items-center justify-center p-4 text-center">
            <div 
              className="fixed inset-0 bg-gray-900/60 transition-opacity backdrop-blur-sm" 
              onClick={() => {
                setIsPartnerModalOpen(false);
                setActiveParticipantId(null);
              }}
            />

            {/* Modal Content */}
            <div className="relative transform overflow-hidden rounded-2xl bg-white text-left shadow-2xl transition-all w-full max-w-md border border-gray-100 z-10 scale-100 p-6">
              <div className="flex items-start justify-between border-b border-gray-200 pb-4 mb-5">
                <div>
                  <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                    <Building2 className="w-5 h-5 text-[#004c91]" />
                    Tạo mới Đối tác
                  </h3>
                  <p className="text-xs text-gray-500 mt-1">Vui lòng cung cấp đầy đủ thông tin để khởi tạo hồ sơ đối tác doanh nghiệp.</p>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setIsPartnerModalOpen(false);
                    setActiveParticipantId(null);
                  }}
                  className="text-gray-400 hover:text-gray-600 rounded-lg p-1 hover:bg-gray-100 transition-all"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Input section */}
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-bold text-black mb-1.5">
                    Mã đối tác<span className="text-red-500 font-extrabold ml-1">*</span>
                  </label>
                  <input
                    type="text"
                    value={partnerCode}
                    onChange={(e) => setPartnerCode(e.target.value.toUpperCase())}
                    placeholder="VD: TOKYO-UNI"
                    className="w-full px-3 py-2.5 bg-white border border-gray-300 rounded-xl text-sm font-medium text-gray-800 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all"
                  />
                </div>

                <div>
                  <label className="block text-sm font-bold text-black mb-1.5">
                    Tên đối tác<span className="text-red-500 font-extrabold ml-1">*</span>
                  </label>
                  <input
                    type="text"
                    value={partnerName}
                    onChange={(e) => setPartnerName(e.target.value)}
                    placeholder="VD: Đại học Tokyo, Nhật Bản"
                    className="w-full px-3 py-2.5 bg-white border border-gray-300 rounded-xl text-sm font-medium text-gray-800 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all"
                  />
                </div>

                <div>
                  <label className="block text-sm font-bold text-black mb-1.5">
                    Quốc gia<span className="text-red-500 font-extrabold ml-1">*</span>
                  </label>
                  <div className="relative">
                    <Globe className="absolute left-3 top-3.5 w-4 h-4 text-gray-400" />
                    <input
                      type="text"
                      value={partnerCountry}
                      onChange={(e) => setPartnerCountry(e.target.value)}
                      placeholder="VD: Nhật Bản"
                      className="w-full pl-9 pr-3 py-2.5 bg-white border border-gray-300 rounded-xl text-sm font-medium text-gray-800 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all"
                    />
                  </div>
                </div>
              </div>

              {/* Validation alert */}
              {showModalValidationErr && (
                <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2 text-xs text-red-700 font-bold">
                  <AlertCircle className="w-4 h-4 text-red-500 shrink-0" />
                  Mời bạn điền tất cả các trường có dấu sao đỏ (*).
                </div>
              )}

              {/* Action Buttons */}
              <div className="flex justify-end gap-2.5 border-t border-gray-200 mt-6 pt-4">
                <button
                  type="button"
                  onClick={() => {
                    setIsPartnerModalOpen(false);
                    setActiveParticipantId(null);
                  }}
                  className="px-4 py-2 text-xs font-bold text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-xl transition-all"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleSavePartner}
                  className="px-4 py-2 text-xs font-bold text-white bg-green-600 hover:bg-green-700 rounded-xl transition-all shadow-sm"
                >
                  Lưu thông tin
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="pb-10"></div>
      {/* Guide Modal */}
      {isGuideModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between bg-white relative z-10">
              <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
                <FileText className="w-5 h-5 text-amber-500" /> 
                Hướng dẫn
              </h3>
              <button onClick={() => setIsGuideModalOpen(false)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 bg-amber-50/30">
              <p className="text-sm text-gray-800 leading-relaxed mb-5 font-medium">
                Nếu thông tin liên hệ của người đưa card visit đã thuộc 1 đối tác có trên hệ thống thì chia ra 2 TH:
              </p>
              <div className="space-y-4">
                <div className="bg-white p-4 rounded-xl border border-gray-100 shadow-sm relative overflow-hidden">
                  <div className="absolute top-0 left-0 w-1 h-full bg-blue-500"></div>
                  <h4 className="font-bold text-gray-900 mb-1.5 text-sm flex items-center gap-2">
                    <span className="bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded text-[10px] uppercase">TH1</span> 
                    Đơn vị công tác giống nhau
                  </h4>
                  <p className="text-sm text-gray-600">Ấn <b className="text-blue-600">Lưu thông tin</b> ⇒ Hệ thống sẽ tự động cập nhật thông tin của họ trong đơn vị đó.</p>
                </div>
                <div className="bg-white p-4 rounded-xl border border-gray-100 shadow-sm relative overflow-hidden">
                  <div className="absolute top-0 left-0 w-1 h-full bg-fuchsia-500"></div>
                  <h4 className="font-bold text-gray-900 mb-1.5 text-sm flex items-center gap-2">
                    <span className="bg-fuchsia-100 text-fuchsia-700 px-1.5 py-0.5 rounded text-[10px] uppercase">TH2</span> 
                    Đơn vị công tác đã thay đổi
                  </h4>
                  <p className="text-sm text-gray-600">Ấn <b className="text-blue-600">Lưu thông tin</b> ⇒ Hệ thống sẽ tự động xóa thông tin của họ trên đơn vị cũ và cập nhật vào đơn vị mới.</p>
                </div>
              </div>
            </div>
            <div className="p-4 border-t border-gray-100 bg-gray-50 flex justify-end">
              <button 
                onClick={() => setIsGuideModalOpen(false)}
                className="px-6 py-2 bg-[#004c91] hover:bg-[#003366] text-white font-bold rounded-xl transition-colors outline-none cursor-pointer shadow-sm"
              >
                Đã hiểu
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Meeting Minutes Modal */}
      {isMeetingMinutesModalOpen && (
        <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/50 backdrop-blur-sm overflow-y-auto py-8 px-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl relative animate-in fade-in zoom-in-95 duration-200">
            {/* Modal Header */}
            <div className="bg-[#004c91] px-6 py-4 rounded-t-2xl flex items-center justify-between">
              <h2 className="text-lg font-bold text-white flex items-center gap-2">
                <FileText className="w-5 h-5 text-[#f37021]" />
                Biên bản cuộc họp
              </h2>
              <button
                onClick={() => setIsMeetingMinutesModalOpen(false)}
                className="w-8 h-8 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-6 md:p-8 space-y-6">
              {/* Tên biên bản & Thời gian */}
              <div className="flex flex-col sm:flex-row sm:items-start gap-6">
                <div className="flex-1 w-full max-w-[450px]">
                  <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Tên biên bản</label>
                  <input
                    type="text"
                    value={meetingName}
                    onChange={(e) => setMeetingName(e.target.value)}
                    className="bg-blue-50 text-blue-900 px-4 py-2.5 rounded-xl font-bold border border-blue-200 outline-none w-full focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all"
                    placeholder="Nhập tên biên bản..."
                  />
                </div>
                <div>
                  <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Thời gian</label>
                  <div className="bg-blue-50 text-blue-900 px-4 py-2.5 rounded-xl font-bold flex items-center gap-2 border border-blue-200 focus-within:ring-2 focus-within:ring-[#004c91]/20 focus-within:border-[#004c91] transition-all">
                    <Calendar className="w-5 h-5 text-blue-600 shrink-0" />
                    <input
                      type="date"
                      value={meetingDate}
                      onChange={(e) => setMeetingDate(e.target.value)}
                      className="bg-transparent border-none outline-none font-bold text-blue-900 cursor-pointer w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Participant Table */}
              <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
                <div className="bg-gray-50/70 px-5 py-3 border-b border-gray-200 flex items-center justify-between">
                  <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2">
                    <Users className="w-4 h-4 text-[#004c91]" />
                    Danh sách người tham gia
                  </h3>
                  <span className="text-xs bg-[#004c91]/10 text-[#004c91] px-2.5 py-1 rounded-full font-bold">
                    {participantsList.length} thành viên
                  </span>
                </div>
                <div className="overflow-x-auto max-h-[260px] overflow-y-auto">
                  <table className="w-full text-left border-collapse text-sm">
                    <thead className="sticky top-0 bg-white z-10 shadow-[0_1px_0_rgba(229,231,235,1)]">
                      <tr className="border-b border-gray-200 bg-gray-100/50 text-[11px] uppercase tracking-wider text-gray-500 font-extrabold">
                        <th className="px-4 py-3 text-center w-20">Có mặt</th>
                        <th className="px-5 py-3">Đại biểu</th>
                        <th className="px-5 py-3">Đơn vị</th>
                        <th className="px-5 py-3">Tình trạng</th>
                        <th className="px-5 py-3 text-center">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {participantsList.map((p) => (
                        <tr key={p.id} className="hover:bg-gray-50/55 transition-colors">
                          <td className="px-4 py-3 text-center">
                            <button type="button" onClick={() => handleToggleConfirm(p.id)} className="inline-flex items-center justify-center focus:outline-none transition-transform active:scale-90">
                              {p.confirmed ? <CheckSquare className="w-5 h-5 text-green-600 fill-green-50" /> : <Square className="w-5 h-5 text-gray-300 hover:text-gray-400" />}
                            </button>
                          </td>
                          <td className="px-5 py-3">
                            <div className="font-semibold text-gray-900">{p.name}</div>
                            <div className="text-xs text-gray-500">{p.role}</div>
                          </td>
                          <td className="px-5 py-3 text-gray-700 font-medium">{p.org}</td>
                          <td className="px-5 py-3">
                            {p.isInternal ? null : p.isPartner ? (
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                                <Check className="w-3 text-green-500 shrink-0" /> Đã đối tác
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200">
                                <AlertCircle className="w-3 text-amber-500 shrink-0" /> Chưa đối tác
                              </span>
                            )}
                          </td>
                          <td className="px-5 py-3 text-center">
                            {p.isInternal ? null : !p.isPartner ? (
                              <button type="button" onClick={() => handleOpenPartnerModal(p)} className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-bold bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg shadow-sm transition-all cursor-pointer">
                                <Plus className="w-3 h-3" /> Tạo đối tác
                              </button>
                            ) : (
                              <span className="text-xs text-green-600 font-bold flex items-center justify-center gap-1">
                                <Check className="w-4 h-4 text-green-500" /> Đã kết nối
                              </span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Ghi chú */}
              <div>
                <h3 className="text-base font-bold text-gray-800 mb-3 flex items-center gap-2">
                  <span className="w-1.5 h-4 bg-[#f37021] rounded-full inline-block"></span>
                  Ghi chú
                </h3>
                <textarea
                  className="w-full bg-gray-50/50 border border-gray-200 rounded-xl p-4 text-sm font-medium text-gray-800 min-h-[100px] focus:bg-white focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 transition-all outline-none resize-y"
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                  placeholder="Nhập ghi chú..."
                />
              </div>

              {/* Đầu mục công việc */}
              <div>
                <h3 className="text-base font-bold text-gray-800 mb-3 flex items-center gap-2">
                  <span className="w-1.5 h-4 bg-[#004c91] rounded-full inline-block"></span>
                  Đầu mục công việc
                </h3>
                <div className="space-y-3 bg-gray-50/50 border border-gray-100 rounded-xl p-4">
                  {actionItems.map((item, index) => (
                    <div key={item.id} className="flex flex-col sm:flex-row sm:items-center gap-3 bg-white p-3 rounded-lg border border-gray-200 shadow-sm">
                      <div className="flex items-center gap-3 flex-1">
                        <button onClick={() => { const newItems = [...actionItems]; newItems[index].done = !newItems[index].done; setActionItems(newItems); }} className="text-[#004c91]">
                          {item.done ? <CheckSquare className="w-5 h-5 text-green-600" /> : <Square className="w-5 h-5 text-gray-400" />}
                        </button>
                        <input
                          type="text"
                          value={item.text}
                          onChange={e => { const newItems = [...actionItems]; newItems[index].text = e.target.value; setActionItems(newItems); }}
                          className={`flex-1 bg-transparent border-none outline-none text-sm font-medium ${item.done ? 'line-through text-gray-400' : 'text-gray-800'}`}
                          placeholder="Thêm hành động..."
                        />
                      </div>
                      <div className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-orange-500" />
                        <input type="date" value={item.deadline} onChange={e => { const newItems = [...actionItems]; newItems[index].deadline = e.target.value; setActionItems(newItems); }} className="text-xs font-bold text-orange-700 bg-orange-50 px-2 py-1.5 rounded-md border border-orange-200 outline-none hover:border-orange-300" />
                      </div>
                    </div>
                  ))}
                  <button onClick={() => setActionItems([...actionItems, { id: Date.now(), text: '', done: false, deadline: '' }])} className="text-sm font-bold text-[#004c91] hover:text-[#003366] flex items-center gap-1.5 px-2 py-1 outline-none">
                    <Plus className="w-4 h-4" /> Thêm đầu mục công việc
                  </button>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 rounded-b-2xl flex justify-end gap-3">
              <button
                onClick={() => setIsMeetingMinutesModalOpen(false)}
                className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors shadow-sm"
              >
                Hủy
              </button>
              <button
                onClick={() => {
                  setIsMeetingMinutesCreated(true);
                  setIsMeetingMinutesModalOpen(false);
                }}
                className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003366] transition-all shadow-md flex items-center gap-2"
              >
                <CheckCircle2 className="w-5 h-5" />
                Lưu biên bản
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
