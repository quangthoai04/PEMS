import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

const file = path.resolve(projectRoot, 'src/pages/dashboard/partners/PartnerDetail.tsx');
let content = fs.readFileSync(file, 'utf8');

// 1. Add states
const stateOld = '  const [isContactModalOpen, setIsContactModalOpen] = useState(false);';
const stateNew = '  const [isContactModalOpen, setIsContactModalOpen] = useState(false);\n  const [isDeleteContactModalOpen, setIsDeleteContactModalOpen] = useState(false);\n  const [contactToDelete, setContactToDelete] = useState<number | null>(null);';
content = content.replace(stateOld, stateNew);

// 2. Change buttons
const btnsOld = `{isEditingContacts && (
                            <button 
                              onClick={() => removeContact(contact.id)}
                              className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-200 outline-none flex items-center justify-center"
                              title="Xóa"
                            >
                              <Trash2 className="w-5 h-5" />
                            </button>
                          )}`;

const btnsNew = `                          <button 
                            onClick={() => { setContactToDelete(contact.id); setIsDeleteContactModalOpen(true); }}
                            className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-200 outline-none flex items-center justify-center"
                            title="Xóa"
                          >
                            <Trash2 className="w-5 h-5" />
                          </button>`;
content = content.replace(btnsOld, btnsNew);

// 3. Confirm Delete  confirmDelete() => { removeContact(); close.. }
const removeContactOld = `  const removeContact = (id: number) => {
    setContacts(contacts.filter(c => c.id !== id));
  };`;

const removeContactNew = `  const removeContact = (id: number) => {
    setContacts(contacts.filter(c => c.id !== id));
  };
  
  const confirmDeleteContact = () => {
    if (contactToDelete !== null) {
      removeContact(contactToDelete);
      setIsDeleteContactModalOpen(false);
      setContactToDelete(null);
    }
  };`;

content = content.replace(removeContactOld, removeContactNew);

// 4. Modal Delete
const modalDelete = `      {/* Delete Contact Modal */}
      {isDeleteContactModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-6 text-center">
              <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <Trash2 className="w-8 h-8 text-red-500" />
              </div>
              <h3 className="text-xl font-bold text-gray-900 mb-2">Xác nhận xóa</h3>
              <p className="text-gray-500 font-medium">Bạn có chắc chắn muốn xóa người liên hệ này? Hành động này không thể hoàn tác.</p>
            </div>
            
            <div className="p-4 border-t border-gray-100 bg-gray-50 flex justify-end gap-3">
              <button 
                onClick={() => { setIsDeleteContactModalOpen(false); setContactToDelete(null); }}
                className="px-4 py-2 bg-white hover:bg-gray-100 text-gray-700 font-bold rounded-xl transition-colors border border-gray-200 outline-none cursor-pointer"
              >
                Hủy
              </button>
              <button 
                onClick={confirmDeleteContact}
                className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white font-bold rounded-xl transition-colors outline-none cursor-pointer shadow-sm shadow-red-200"
              >
                Xóa ngay
              </button>
            </div>
          </div>
        </div>
      )}`;

const endDiv = `    </div>
  );
}`;

content = content.replace(endDiv, modalDelete + '\n' + endDiv);

fs.writeFileSync(file, content, 'utf8');
