/** Two-letter initials from a name — e.g. "Nvidia" -> "NV", "Swinburne University" -> "SU". */
export function getNameInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  return words.length === 1
    ? words[0].slice(0, 2).toUpperCase()
    : (words[0][0] + words[words.length - 1][0]).toUpperCase();
}
