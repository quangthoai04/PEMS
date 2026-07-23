import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ header: [1, 2, 3, false] }],
    [{ list: 'ordered' }, { list: 'bullet' }],
    [{ align: [] }],
    ['link'],
    ['clean'],
  ],
};

export function RichTextEditor({
  value,
  onChange,
  placeholder = 'Nhập ghi chú hoặc nội dung biên bản cuộc họp...',
}: RichTextEditorProps) {
  return (
    <div className="bg-white border border-gray-300 rounded-xl overflow-hidden focus-within:border-[#004c91] focus-within:ring-2 focus-within:ring-[#004c91]/20 transition-all min-h-[160px]">
      {/* @ts-ignore */}
      <ReactQuill
        theme="snow"
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        modules={QUILL_MODULES}
        className="bg-white text-gray-800"
      />
    </div>
  );
}
