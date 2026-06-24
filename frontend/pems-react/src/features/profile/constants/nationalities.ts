// Nationality options for the VISITOR self-service profile (UC-15 §6).
// `value` (stable English) is what gets persisted; `label` is shown; `aliases`
// power case-insensitive search in VN/EN.

export interface NationalityOption {
  label: string;
  value: string;
  aliases: string[];
}

export const NATIONALITY_OPTIONS: NationalityOption[] = [
  { label: 'Việt Nam', value: 'Vietnam', aliases: ['viet nam', 'vietnam', 'việt nam'] },
  { label: 'Hoa Kỳ', value: 'United States', aliases: ['hoa kỳ', 'my', 'mỹ', 'united states', 'usa'] },
  { label: 'Nhật Bản', value: 'Japan', aliases: ['nhật', 'nhat ban', 'japan'] },
  { label: 'Hàn Quốc', value: 'South Korea', aliases: ['hàn', 'han quoc', 'korea', 'south korea'] },
  { label: 'Trung Quốc', value: 'China', aliases: ['trung quốc', 'china'] },
  { label: 'Singapore', value: 'Singapore', aliases: ['singapore'] },
  { label: 'Thái Lan', value: 'Thailand', aliases: ['thái lan', 'thai lan', 'thailand'] },
  { label: 'Malaysia', value: 'Malaysia', aliases: ['malaysia'] },
  { label: 'Indonesia', value: 'Indonesia', aliases: ['indonesia'] },
  { label: 'Philippines', value: 'Philippines', aliases: ['philippines'] },
  { label: 'Ấn Độ', value: 'India', aliases: ['ấn độ', 'an do', 'india'] },
  { label: 'Úc', value: 'Australia', aliases: ['úc', 'uc', 'australia'] },
  { label: 'Canada', value: 'Canada', aliases: ['canada'] },
  { label: 'Vương quốc Anh', value: 'United Kingdom', aliases: ['anh', 'uk', 'united kingdom'] },
  { label: 'Pháp', value: 'France', aliases: ['pháp', 'phap', 'france'] },
  { label: 'Đức', value: 'Germany', aliases: ['đức', 'duc', 'germany'] },
  { label: 'Ý', value: 'Italy', aliases: ['ý', 'y', 'italy'] },
  { label: 'Tây Ban Nha', value: 'Spain', aliases: ['tây ban nha', 'spain'] },
  { label: 'Hà Lan', value: 'Netherlands', aliases: ['hà lan', 'netherlands'] },
  { label: 'Khác', value: 'Other', aliases: ['khác', 'other'] },
];

/** Resolves a stored value back to its display label (falls back to the raw value). */
export function nationalityLabel(value: string | null | undefined): string {
  if (!value) return '';
  return NATIONALITY_OPTIONS.find((o) => o.value === value)?.label ?? value;
}
