import { useTranslation } from 'react-i18next';
import { countryNameToAlpha2, getEnCountryNames, getViCountryNames } from '../utils/countryNames';

export function useCountryTranslation() {
  const { i18n } = useTranslation();
  
  return (countryName: string | null | undefined): string => {
    if (!countryName) return '';
    const code = countryNameToAlpha2(countryName);
    if (!code) return countryName;

    if (i18n.language === 'vi') {
      return getViCountryNames()[code] || countryName;
    } else {
      return getEnCountryNames()[code] || countryName;
    }
  };
}
