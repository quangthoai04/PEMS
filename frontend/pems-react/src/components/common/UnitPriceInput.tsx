import React from 'react';

interface UnitPriceInputProps {
  value: number;
  onChange: (val: number) => void;
  disabled?: boolean;
  step?: number;
  min?: number;
  className?: string;
  placeholder?: string;
}

/**
  * Component nhập đơn giá:
  * - Tự động xóa số 00 đứng đầu (ví dụ 001200 -> 1200).
  * - Tự động định dạng dấu chấm phân cách hàng nghìn khi nhập (ví dụ 120000 -> 120.000).
  * - Đơn vị tăng/giảm qua phím Mũi tên Lên/Xuống hoặc nút stepper mặc định là 1.000.
  */
export function UnitPriceInput({
  value,
  onChange,
  disabled = false,
  step = 1000,
  min = 0,
  className = '',
  placeholder = '0',
}: UnitPriceInputProps) {
  const formatDisplay = (val: number): string => {
    if (!val || isNaN(val) || val <= 0) return '0';
    return Math.floor(val).toLocaleString('vi-VN');
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.replace(/\D/g, '');
    if (!raw) {
      onChange(0);
      return;
    }
    const parsed = parseInt(raw, 10);
    const validVal = isNaN(parsed) ? 0 : Math.max(min, parsed);
    onChange(validVal);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (disabled) return;
    if (e.key === 'ArrowUp') {
      e.preventDefault();
      const current = value || 0;
      onChange(current + step);
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      const current = value || 0;
      onChange(Math.max(min, current - step));
    }
  };

  const handleStepUp = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (disabled) return;
    onChange((value || 0) + step);
  };

  const handleStepDown = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (disabled) return;
    onChange(Math.max(min, (value || 0) - step));
  };

  if (disabled) {
    return <span className={className}>{formatDisplay(value)}</span>;
  }

  return (
    <div className="relative inline-flex items-center w-full group">
      <input
        type="text"
        inputMode="numeric"
        value={formatDisplay(value)}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        disabled={disabled}
        className={className}
      />
      {!disabled && (
        <div className="flex flex-col ml-0.5 shrink-0 select-none opacity-60 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
          <button
            type="button"
            tabIndex={-1}
            onClick={handleStepUp}
            className="text-[8px] leading-tight text-slate-500 hover:text-slate-900 px-1 hover:bg-slate-200 rounded cursor-pointer border border-slate-200 bg-slate-50"
            title={`Tăng ${step.toLocaleString('vi-VN')} ₫`}
          >
            ▲
          </button>
          <button
            type="button"
            tabIndex={-1}
            onClick={handleStepDown}
            className="text-[8px] leading-tight text-slate-500 hover:text-slate-900 px-1 hover:bg-slate-200 rounded cursor-pointer border border-slate-200 bg-slate-50 mt-[1px]"
            title={`Giảm ${step.toLocaleString('vi-VN')} ₫`}
          >
            ▼
          </button>
        </div>
      )}
    </div>
  );
}
