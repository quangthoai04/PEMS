import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const filePath = path.resolve(projectRoot, 'src/pages/dashboard/partners/PartnerDetail.tsx');
let code = fs.readFileSync(filePath, 'utf8');

// 1. imports
code = code.replace(
  `import { ChevronRight, Info, History, FileText, Plus, Trash2, MapPin, Globe, CheckCircle, ArrowLeft, Edit3, Check } from 'lucide-react';`,
  `import { ChevronRight, Info, History, FileText, Plus, Trash2, MapPin, Globe, CheckCircle, ArrowLeft, Edit3, Check, Eye, X } from 'lucide-react';`
);

// 2. State
const oldState = `  const [contacts, setContacts] = useState([
    { id: 1, name: 'Nguyễn Văn A', contact: '0123456789 - a@example.com', role: 'Trưởng phòng' },
    { id: 2, name: 'Trần Thị B', contact: '0987654321 - b@example.com', role: 'Nhân viên' }
  ]);`;

const newState = `  const [contacts, setContacts] = useState([
    { id: 1, name: 'Nguyễn Văn A', phone: '0123456789', email: 'a@example.com', role: 'Trưởng phòng', department: 'Tuyển sinh', company: 'Đại học Deakin', website: 'https://deakin.edu.au', address: 'Victoria, Úc' },
    { id: 2, name: 'Trần Thị B', phone: '0987654321', email: 'b@example.com', role: 'Nhân viên', department: 'Đào tạo', company: 'Đại học Deakin', website: 'https://deakin.edu.au', address: 'Victoria, Úc' }
  ]);
  const [selectedContact, setSelectedContact] = useState<any>(null);
  const [isContactModalOpen, setIsContactModalOpen] = useState(false);`;

code = code.replace(oldState, newState);

// 3. addContact logic
const oldAdd = `  const addContact = () => {
    const newId = contacts.length ? Math.max(...contacts.map(c => c.id)) + 1 : 1;
    setContacts([...contacts, { id: newId, name: '', contact: '', role: '' }]);
  };`;
const newAdd = `  const addContact = () => {
    const newId = contacts.length ? Math.max(...contacts.map(c => c.id)) + 1 : 1;
    setContacts([...contacts, { id: newId, name: '', phone: '', email: '', role: '', department: '', company: partnerDetails.name, website: partnerDetails.website, address: '' }]);
  };`;
code = code.replace(oldAdd, newAdd);

// 4. Table header
const oldTh = `                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[25%] pl-4">Tên người liên hệ</th>
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[40%] pl-4">Thông tin liên lạc</th>
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[25%] pl-4">Chức vụ</th>
                  {isEditingContacts && <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[10%]"></th>}`;
const newTh = `                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Tên người liên hệ</th>
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Email</th>
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">SĐT</th>
                  <th className="py-4 text-left text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Chức vụ</th>
                  <th className="py-4 text-center text-[13px] font-bold text-gray-500 uppercase tracking-wider w-[20%] pl-4">Hành động</th>`;
code = code.replace(oldTh, newTh);

// 5. row colspan
const emptyRow = `<td colSpan={4} className="p-8 text-center text-gray-500 font-medium bg-gray-50/50">`;
const newEmptyRow = `<td colSpan={5} className="p-8 text-center text-gray-500 font-medium bg-gray-50/50">`;
code = code.replace(emptyRow, newEmptyRow);

// 6. row data
const oldTd = `                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.contact} 
                          onChange={(e) => updateContact(contact.id, 'contact', e.target.value)}
                          placeholder="SĐT / Email..."
                          readOnly={!isEditingContacts}
                          className={\`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 \${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}\`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.role} 
                          onChange={(e) => updateContact(contact.id, 'role', e.target.value)}
                          placeholder="Chức vụ..."
                          readOnly={!isEditingContacts}
                          className={\`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 \${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}\`}
                        />
                      </td>
                      {isEditingContacts && (
                        <td className="p-3 text-center">
                          <button 
                            onClick={() => removeContact(contact.id)}
                            className="p-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors outline-none"
                            title="Xóa"
                          >
                            <Trash2 className="w-5 h-5 mx-auto" />
                          </button>
                        </td>
                      )}`;
const newTd = `                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.email} 
                          onChange={(e) => updateContact(contact.id, 'email', e.target.value)}
                          placeholder="Email..."
                          readOnly={!isEditingContacts}
                          className={\`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 \${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}\`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.phone} 
                          onChange={(e) => updateContact(contact.id, 'phone', e.target.value)}
                          placeholder="SĐT..."
                          readOnly={!isEditingContacts}
                          className={\`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 \${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}\`}
                        />
                      </td>
                      <td className="p-3">
                        <input 
                          type="text" 
                          value={contact.role} 
                          onChange={(e) => updateContact(contact.id, 'role', e.target.value)}
                          placeholder="Chức vụ..."
                          readOnly={!isEditingContacts}
                          className={\`w-full bg-transparent px-3 py-2 text-sm focus:outline-none transition-colors font-medium text-gray-800 \${isEditingContacts ? 'border border-gray-200 rounded-lg focus:border-[#00a651] bg-white group-hover:bg-[#f8fdf9]' : 'cursor-default'}\`}
                        />
                      </td>
                      <td className="p-3 text-center">
                        <div className="flex items-center justify-center gap-2">
                          <button 
                            onClick={() => { setSelectedContact(contact); setIsContactModalOpen(true); }}
                            className="p-1 px-2 text-[#00a651] hover:bg-[#eaffe4] rounded-lg transition-colors border border-transparent hover:border-[#ceefda] outline-none flex items-center justify-center"
                            title="Xem chi tiết"
                          >
                            <Eye className="w-5 h-5" />
                          </button>
                          {isEditingContacts && (
                            <button 
                              onClick={() => removeContact(contact.id)}
                              className="p-1 px-2 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-200 outline-none flex items-center justify-center"
                              title="Xóa"
                            >
                              <Trash2 className="w-5 h-5" />
                            </button>
                          )}
                        </div>
                      </td>`;

code = code.replace(oldTd, newTd);

// 7. Modal
const modalCode = `
      {/* Contact Detail Modal */}
      {isContactModalOpen && selectedContact && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-5 border-b border-gray-100 bg-[#00a651]">
              <h3 className="text-xl font-bold text-white">Thông tin chi tiết</h3>
              <button 
                onClick={() => setIsContactModalOpen(false)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-lg transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-6 bg-gray-50/50">
              <div className="flex items-center gap-4 pb-4 border-b border-gray-200">
                <div className="w-14 h-14 bg-gradient-to-br from-[#eaffe4] to-[#ceefda] rounded-xl flex items-center justify-center text-[#00a651] font-black text-2xl shrink-0 shadow-sm border border-[#00a651]/20">
                  {selectedContact.name ? selectedContact.name.charAt(0) : '?'}
                </div>
                <div>
                   <h4 className="font-black text-gray-900 text-xl tracking-tight">{selectedContact.name}</h4>
                   <p className="text-sm font-bold text-[#00a651] uppercase tracking-wide mt-1">{selectedContact.role} {selectedContact.department ? \`- \${selectedContact.department}\` : ''}</p>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-5 bg-white p-5 rounded-xl border border-gray-100 shadow-sm">
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Info className="w-3.5 h-3.5"/> Công ty / Đối tác</label>
                  <p className="text-[15px] font-bold text-gray-800">{selectedContact.company || partnerDetails.name}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><MapPin className="w-3.5 h-3.5"/> Địa chỉ</label>
                  <p className="text-[15px] font-medium text-gray-800">{selectedContact.address || partnerDetails.country || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Info className="w-3.5 h-3.5"/> SĐT</label>
                  <p className="text-[15px] font-bold text-gray-800">{selectedContact.phone || 'Chưa cập nhật'}</p>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Globe className="w-3.5 h-3.5"/> Email</label>
                  <p className="text-[15px] font-bold text-[#004c91]">{selectedContact.email || 'Chưa cập nhật'}</p>
                </div>
                <div className="col-span-2">
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-1.5 flex items-center gap-1"><Globe className="w-3.5 h-3.5"/> Website</label>
                  <a href={selectedContact.website || partnerDetails.website} target="_blank" rel="noopener noreferrer" className="text-[15px] font-bold text-[#00a651] hover:underline">
                    {selectedContact.website || partnerDetails.website}
                  </a>
                </div>
              </div>
            </div>
            
            <div className="p-5 border-t border-gray-100 bg-white flex justify-end">
              <button 
                onClick={() => setIsContactModalOpen(false)}
                className="px-6 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold rounded-xl transition-colors outline-none"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}`;

const rootEnd = `    </div>
  );
}`;
code = code.replace(rootEnd, modalCode + '\n' + rootEnd);

fs.writeFileSync(filePath, code, 'utf8');
console.log('Update detail');
