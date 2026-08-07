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
import viVisitRequestV2 from './locales/vi/visitRequestV2.json';
import viValidation from './locales/vi/validation.json';
import viErrors from './locales/vi/errors.json';
import viToast from './locales/vi/toast.json';
import viLoginModal from './locales/vi/loginModal.json';
import viSearch from './locales/vi/search.json';
import viVisitFptu from './locales/vi/visitFptu.json';
import viNotifications from './locales/vi/notifications.json';
import viVisitFaceScan from './locales/vi/visitFaceScan.json';
import viFiles from './locales/vi/files.json';
import viLegal from './locales/vi/legal.json';

import enCommon from './locales/en/common.json';
import enPublicLayout from './locales/en/publicLayout.json';
import enHome from './locales/en/home.json';
import enNews from './locales/en/news.json';
import enPartners from './locales/en/partners.json';
import enFaq from './locales/en/faq.json';
import enGallery from './locales/en/gallery.json';
import enVisitRequest from './locales/en/visitRequest.json';
import enVisitRequestV2 from './locales/en/visitRequestV2.json';
import enValidation from './locales/en/validation.json';
import enErrors from './locales/en/errors.json';
import enToast from './locales/en/toast.json';
import enLoginModal from './locales/en/loginModal.json';
import enSearch from './locales/en/search.json';
import enVisitFptu from './locales/en/visitFptu.json';
import enNotifications from './locales/en/notifications.json';
import enVisitFaceScan from './locales/en/visitFaceScan.json';
import enFiles from './locales/en/files.json';
import enLegal from './locales/en/legal.json';

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
    visitRequestV2: viVisitRequestV2,
    validation: viValidation,
    errors: viErrors,
    toast: viToast,
    loginModal: viLoginModal,
    search: viSearch,
    visitFptu: viVisitFptu,
    notifications: viNotifications,
    visitFaceScan: viVisitFaceScan,
    files: viFiles,
    legal: viLegal,
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
    visitRequestV2: enVisitRequestV2,
    validation: enValidation,
    errors: enErrors,
    toast: enToast,
    loginModal: enLoginModal,
    search: enSearch,
    visitFptu: enVisitFptu,
    notifications: enNotifications,
    visitFaceScan: enVisitFaceScan,
    files: enFiles,
    legal: enLegal,
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
    // Every namespace listed here must also exist in `resources` above. An unregistered
    // namespace does not fail loudly: i18next strips the `ns:` prefix and renders the
    // bare key segment (t('loginModal:title') -> "title") straight into the UI.
    ns: [
      'common', 'publicLayout', 'home', 'news', 'partners',
      'faq', 'gallery', 'visitRequest', 'visitRequestV2', 'validation', 'errors', 'toast',
      'loginModal', 'search', 'visitFptu', 'notifications', 'visitFaceScan', 'files', 'legal'
    ],
    defaultNS: 'common',
    interpolation: {
      escapeValue: false, // React already safe from xss
    },
    // Missing keys render as the key itself so gaps are visible during development
    // instead of silently collapsing to an empty node — UNLESS an explicit defaultValue
    // was passed to t(key, defaultValue). i18next routes a missing key with a default
    // through this handler, so a handler that ignored the second arg would swallow the
    // fallback (e.g. t('...campusOptions.TH', 'FPT Thanh Hóa') would render the raw key
    // for any campus without a hardcoded translation — which every runtime-created campus
    // is). Respecting defaultValue restores standard i18next semantics.
    parseMissingKeyHandler: (key, defaultValue) =>
      typeof defaultValue === 'string' && defaultValue.length > 0 ? defaultValue : key,
    saveMissing: import.meta.env.DEV,
    missingKeyHandler: (lngs, ns, key) => {
      if (import.meta.env.DEV) {
        console.warn(`[i18n] missing key "${key}" in namespace "${ns}" for ${lngs.join(', ')}`);
      }
    },
  });

export const changeLanguage = (lng: 'vi' | 'en') => {
  localStorage.setItem('pems.language', lng);
  i18n.changeLanguage(lng);
};

export const getCurrentLanguage = (): 'vi' | 'en' => {
  return (i18n.language as 'vi' | 'en') || 'vi';
};

export default i18n;
