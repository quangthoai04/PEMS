/**
 * Component VisitDuringTab
 * Cụm module chức năng giám sát thực thi biểu mẫu thời gian thực khách tham quan.
 */

import React, { useState, useRef, useEffect } from 'react';
import {
  ChevronDown,
  ChevronUp,
  Calendar,
  Users,
  User,
  Clock,
  Upload,
  AlertCircle,
  FileText,
  Building2,
  Briefcase,
  Phone,
  Mail,
  Plus,
  X,
  CheckCircle2,
  Link2,
  MapPin,
  Loader2
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { MinutesCard } from './MinutesCard';
import { vietnamNowDateTimeLocal } from '../../../shared/utils/vietnamTime';
import { businessCardOcrApi } from '../../../features/business-card-ocr/api/businessCardOcrApi';
import { showLoadingToast, updateToastSuccess, updateToastError, showMessageErrorToast } from '../../../shared/utils/toast';
import type { VisitProcessGuestMember } from '../../../features/delegations/types/delegations.types';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import { visitDocumentsApi } from '../../../features/delegations/api/visitDocumentsApi';

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

  // Main sections open/close state
  const [isSection1Expanded, setIsSection1Expanded] = useState(true);
  const [isSection2Expanded, setIsSection2Expanded] = useState(true);
  const [isSection3Expanded, setIsSection3Expanded] = useState(true);
  const [isSection4Expanded, setIsSection4Expanded] = useState(true);


  const [currentOcrJobId, setCurrentOcrJobId] = useState<number | null>(null);

  // Business-card OCR result — empty by default; populated only from a real scan, never seeded.
  const [scannedInfo, setScannedInfo] = useState({
    name: "",
    title: "",
    company: "",
    phone: "",
    email: "",
    website: "",
    address: ""
  });

  // Document and Contact Linkage States
  const [partnerDocFile, setPartnerDocFile] = useState<{ name: string; size: string; type: string } | null>(null);
  // 0..N partners tagged to the document being prepared. Empty = saved as "Theo đoàn khách" (VISIT).
  const [selectedPartnersForDoc, setSelectedPartnersForDoc] = useState<string[]>([]);
  const docFileInputRef = useRef<HTMLInputElement>(null);
  const [docError, setDocError] = useState("");
  // Documents added THIS session. The real record is the backend (visitDocumentsApi.upload), but
  // there is no "documents uploaded during visit X" query to rehydrate this list from — a document
  // here can be linked to any partner name typed in, not just the visit's own delegation. So this
  // display list is mirrored into localStorage keyed by visitInstanceId, purely to survive a page
  // reload; it is not the source of truth for the document itself.
  const [uploadedDocuments, setUploadedDocuments] = useState<Array<{
    id: number;
    fileName: string;
    fileSize: string;
    partners: string[];
    uploadedAt: string;
  }>>([]);
  const uploadedDocumentsStorageKey = visitInstanceId ? `visitDuringTab.uploadedDocuments.${visitInstanceId}` : null;

  useEffect(() => {
    if (!uploadedDocumentsStorageKey) return;
    try {
      const cached = localStorage.getItem(uploadedDocumentsStorageKey);
      if (cached) setUploadedDocuments(JSON.parse(cached));
    } catch {
      // Corrupt cache entry — start from an empty list rather than crash the tab.
    }
  }, [uploadedDocumentsStorageKey]);

  const [selectedPartnerForContact, setSelectedPartnerForContact] = useState("");
  const [contactSaveSuccess, setContactSaveSuccess] = useState(false);
  // Contacts saved THIS session. Real persistence is the backend (partnersApi.createContact /
  // businessCardOcrApi.confirmContact); session-only display, NOT cached in localStorage.
  const [savedContactsList, setSavedContactsList] = useState<Array<{
    name: string;
    title: string;
    company: string;
    phone: string;
    email: string;
    website: string;
    address: string;
    targetPartner: string;
  }>>([]);

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
  const toggleSelectedPartnerForDoc = (name: string) => {
    setSelectedPartnersForDoc(prev =>
      prev.includes(name) ? prev.filter(p => p !== name) : [...prev, name]);
  };
  const handleAddDocument = async () => {
    if (!partnerDocFile || !docFileInputRef.current?.files?.[0]) {
      setDocError("Vui lòng tải lên tài liệu / ảnh.");
      return;
    }
    if (!visitInstanceId) {
      setDocError("Không xác định được chuyến tiếp khách.");
      return;
    }
    setDocError("");
    setIsUploadingDoc(true);
    const toastId = showLoadingToast('Đang tải lên tài liệu...', 'upload-doc');
    try {
      // Backend resolves/creates từng đối tác theo tên và lưu 1 file, N document (mỗi đối tác 1
      // document trỏ cùng fileId) — hoặc 1 document loại "Theo đoàn khách" nếu không chọn đối tác nào.
      const result = await visitDocumentsApi.upload(
        visitInstanceId, docFileInputRef.current.files[0], partnerDocFile.name, selectedPartnersForDoc);

      const newDoc = {
        id: Date.now(),
        fileName: result.fileName || partnerDocFile.name,
        fileSize: partnerDocFile.size,
        partners: result.partners.map(p => p.partnerName),
        uploadedAt: vietnamNowDateTimeLocal().replace('T', ' ')
      };
      setUploadedDocuments(prev => {
        const next = [...prev, newDoc];
        if (uploadedDocumentsStorageKey) {
          try { localStorage.setItem(uploadedDocumentsStorageKey, JSON.stringify(next)); } catch {}
        }
        return next;
      });
      setPartnerDocFile(null);
      setSelectedPartnersForDoc([]);
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
    setSelectedPartnersForDoc([]);
    setDocError("");
    if (docFileInputRef.current) {
      docFileInputRef.current.value = "";
    }
  };

  const [isSavingContact, setIsSavingContact] = useState(false);
  const handleSaveContactToPartner = async () => {
    if (!selectedPartnerForContact) {
      showMessageErrorToast('Vui lòng chọn đối tác để lưu thông tin liên hệ.');
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


  return (
    <div className="space-y-8 animate-in fade-in zoom-in-95 duration-300">

      {/* Đánh giá chuyến thăm đã chuyển sang tab Sau tiếp khách (VisitAfterTab) —
          chỉ đánh giá sau khi Host xác nhận kết thúc tiếp khách. */}

      {/* 1. Biên bản cuộc họp — bản thật (backend + cơ chế lock) khi có visitInstanceId; nếu không, dùng mock cũ */}
      {visitInstanceId && (
        <MinutesCard visitInstanceId={visitInstanceId} isReadOnly={isReadOnly} />
      )}

       {/* 2. Đối tác & Tài liệu */}
       {!isDept && (
         <>
       <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm relative transition-all">
         <div 
          className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
          onClick={() => setIsSection4Expanded(!isSection4Expanded)}
         >
          <div>
            <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
               <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">2</span>
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
                      <label className="block text-sm font-bold text-gray-700">
                        Chọn đối tác liên kết tài liệu này <span className="font-normal text-gray-400 normal-case">(có thể chọn nhiều, để trống = Theo đoàn khách)</span>
                      </label>
                      {selectedPartnersForDoc.length > 0 && (
                        <div className="flex flex-wrap gap-1.5 mb-1.5">
                          {selectedPartnersForDoc.map((name) => (
                            <span key={name} className="inline-flex items-center gap-1 px-2.5 py-1 bg-[#004c91]/10 text-[#004c91] text-[11px] font-bold rounded-full">
                              {name}
                              <button type="button" onClick={() => toggleSelectedPartnerForDoc(name)} className="hover:text-red-600 cursor-pointer outline-none">
                                <X className="w-3 h-3" />
                              </button>
                            </span>
                          ))}
                        </div>
                      )}
                      <div className="border border-gray-300 rounded-xl bg-white max-h-40 overflow-y-auto divide-y divide-gray-100">
                        {getAvailablePartners().drafts.length === 0 && getAvailablePartners().existing.length === 0 ? (
                          <p className="px-3 py-2.5 text-xs text-gray-400">Chưa có đối tác gợi ý — để trống sẽ lưu theo đoàn khách.</p>
                        ) : (
                          <>
                            {getAvailablePartners().drafts.length > 0 && (
                              <div className="px-3 py-1.5 bg-gray-50 text-[10px] font-bold text-gray-500 uppercase tracking-wider">Đối tác Draft (tạo ở trên)</div>
                            )}
                            {getAvailablePartners().drafts.map((org, idx) => (
                              <label key={`doc-draft-${idx}`} className="flex items-center gap-2 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 cursor-pointer">
                                <input type="checkbox" checked={selectedPartnersForDoc.includes(org)} onChange={() => toggleSelectedPartnerForDoc(org)} className="accent-[#004c91]" />
                                {org}
                              </label>
                            ))}
                            {getAvailablePartners().existing.length > 0 && (
                              <div className="px-3 py-1.5 bg-gray-50 text-[10px] font-bold text-gray-500 uppercase tracking-wider">Đối tác đã có trên hệ thống</div>
                            )}
                            {getAvailablePartners().existing.map((org, idx) => (
                              <label key={`doc-existing-${idx}`} className="flex items-center gap-2 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 cursor-pointer">
                                <input type="checkbox" checked={selectedPartnersForDoc.includes(org)} onChange={() => toggleSelectedPartnerForDoc(org)} className="accent-[#004c91]" />
                                {org}
                              </label>
                            ))}
                          </>
                        )}
                      </div>
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
                        Chưa có tài liệu nào được tải lên trong phiên này.
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
                              <div className="text-[10px] text-gray-500 font-medium">
                                {doc.partners.length > 0 ? (
                                  <>Đối tác: <span className="font-bold text-[#004c91]">{doc.partners.join(', ')}</span></>
                                ) : (
                                  <span className="font-bold text-[#004c91]">Theo đoàn khách</span>
                                )} • {doc.fileSize}
                              </div>
                              <div className="text-[9px] text-gray-400">{doc.uploadedAt}</div>
                            </div>
                            <button
                              type="button"
                              onClick={() => setUploadedDocuments(prev => {
                                const next = prev.filter(d => d.id !== doc.id);
                                if (uploadedDocumentsStorageKey) {
                                  try { localStorage.setItem(uploadedDocumentsStorageKey, JSON.stringify(next)); } catch {}
                                }
                                return next;
                              })}
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
      
       {/* 3. Thông tin khác */}
       <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm relative transition-all">
         <div 
          className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
          onClick={() => setIsSection3Expanded(!isSection3Expanded)}
         >
          <div>
            <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
               <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">3</span>
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
                        <div className="space-y-1.5 font-sans">
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


    </div>
  );
}
