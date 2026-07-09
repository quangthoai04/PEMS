import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import viCommon from './locales/vi/common.json';
import viPublicLayout from './locales/vi/publicLayout.json';
import viHome from './locales/vi/home.json';
import viNews from './locales/vi/news.json';
import viPartners from './locales/vi/partners.json';
import viFaq from './locales/vi/faq.json';
import viGallery from './locales/vi/gallery.json';
import viVisitRequest from './locales/vi/visitRequest.json';
import viValidation from './locales/vi/validation.json';
import viErrors from './locales/vi/errors.json';
import viToast from './locales/vi/toast.json';

import enCommon from './locales/en/common.json';
import enPublicLayout from './locales/en/publicLayout.json';
import enHome from './locales/en/home.json';
import enNews from './locales/en/news.json';
import enPartners from './locales/en/partners.json';
import enFaq from './locales/en/faq.json';
import enGallery from './locales/en/gallery.json';
import enVisitRequest from './locales/en/visitRequest.json';
import enValidation from './locales/en/validation.json';
import enErrors from './locales/en/errors.json';
import enToast from './locales/en/toast.json';

const resources = {
  vi: {
    common: viCommon,
    publicLayout: viPublicLayout,
    home: viHome,
    news: viNews,
    partners: viPartners,
    faq: viFaq,
    gallery: viGallery,
    visitRequest: viVisitRequest,
    validation: viValidation,
    errors: viErrors,
    toast: viToast,
  },
  en: {
    common: enCommon,
    publicLayout: enPublicLayout,
    home: enHome,
    news: enNews,
    partners: enPartners,
    faq: enFaq,
    gallery: enGallery,
    visitRequest: enVisitRequest,
    validation: enValidation,
    errors: enErrors,
    toast: enToast,
  },
};

const getInitialLanguage = () => {
  const stored = localStorage.getItem('pems.language');
  if (stored === 'vi' || stored === 'en') {
    return stored;
  }
  const browserLang = navigator.language.split('-')[0];
  if (browserLang === 'vi' || browserLang === 'en') {
    return browserLang;
  }
  return 'vi';
};

i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: getInitialLanguage(),
    fallbackLng: 'vi',
    ns: [
      'common', 'publicLayout', 'home', 'news', 'partners', 
      'faq', 'gallery', 'visitRequest', 'validation', 'errors', 'toast'
    ],
    defaultNS: 'common',
    interpolation: {
      escapeValue: false, // React already safe from xss
    },
    // Don't output raw keys if translation is missing, but show a fallback
    parseMissingKeyHandler: (key) => key,
  });

export const changeLanguage = (lng: 'vi' | 'en') => {
  localStorage.setItem('pems.language', lng);
  i18n.changeLanguage(lng);
};

export const getCurrentLanguage = (): 'vi' | 'en' => {
  return (i18n.language as 'vi' | 'en') || 'vi';
};

export default i18n;
