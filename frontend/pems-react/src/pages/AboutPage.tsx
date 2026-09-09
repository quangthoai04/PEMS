import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { 
  Building2, 
  Globe2, 
  Users, 
  Calendar, 
  Award, 
  Target, 
  Sparkles, 
  ArrowRight,
  CheckCircle2,
  FileCheck,
  PlaneTakeoff,
  UserCheck,
  MapPin,
  ShieldCheck,
  Code2
} from 'lucide-react';

import thoaiAvatar from '../assets/Member/thoai.jpg';

interface CampusStaffConfig {
  key: string;
  avatarColor: string;
  initials: string;
}

interface DevMember {
  name: string;
  code: string;
  role: string;
  avatar?: string;
  initials: string;
  gradient: string;
}

const campusStaffConfigs: CampusStaffConfig[] = [
  { key: 'hn', avatarColor: 'from-[#f37021] via-amber-500 to-orange-600', initials: 'HN' },
  { key: 'hcm', avatarColor: 'from-blue-600 via-indigo-600 to-blue-800', initials: 'SG' },
  { key: 'dn', avatarColor: 'from-teal-500 via-emerald-600 to-teal-700', initials: 'DN' },
  { key: 'ct', avatarColor: 'from-amber-500 via-orange-500 to-red-500', initials: 'CT' },
  { key: 'qn', avatarColor: 'from-purple-600 via-violet-600 to-indigo-700', initials: 'QN' },
];

const devMembers: DevMember[] = [
  {
    name: 'Nguyen Thi Yen',
    code: 'HE172008',
    role: 'Project Manager, BA',
    initials: 'TY',
    gradient: 'from-amber-400 to-orange-500',
  },
  {
    name: 'Nguyen Quang Thoai',
    code: 'HE180765',
    role: 'Backend Developer',
    avatar: thoaiAvatar,
    initials: 'QT',
    gradient: 'from-blue-500 to-cyan-500',
  },
  {
    name: 'Nguyen Dinh Duy',
    code: 'HE181072',
    role: 'Frontend Developer',
    initials: 'DD',
    gradient: 'from-indigo-500 to-purple-500',
  },
  {
    name: 'Truong Thi Phuong Anh',
    code: 'HE181589',
    role: 'Tester, Frontend Developer',
    initials: 'PA',
    gradient: 'from-rose-400 to-pink-500',
  },
  {
    name: 'Nguyen Van Thang Canh',
    code: 'HE186121',
    role: 'Backend Developer',
    initials: 'TC',
    gradient: 'from-teal-400 to-emerald-500',
  },
];

export const AboutPage: React.FC = () => {
  const { t } = useTranslation(['about']);

  return (
    <div className="bg-slate-50 min-h-screen pb-16">
      {/* Hero Banner */}
      <section className="relative bg-gradient-to-r from-[#00386b] via-[#004c91] to-[#0060b8] text-white pt-28 sm:pt-32 pb-16 px-4 sm:px-6 lg:px-8 overflow-hidden">
        <div className="absolute inset-0 opacity-10 bg-[radial-gradient(#fff_1px,transparent_1px)] [background-size:16px_16px]" />
        <div className="max-w-6xl mx-auto relative z-10 text-center">
          <span className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-full bg-white/15 text-orange-300 backdrop-blur-md text-xs font-bold uppercase tracking-wider mb-4 border border-white/20 shadow-sm">
            <Sparkles className="w-4 h-4 text-[#f37021]" /> {t('about:badge')}
          </span>
          <h1 className="text-3xl sm:text-4xl lg:text-5xl font-extrabold tracking-tight mb-4 text-white drop-shadow-sm">
            {t('about:title')}
          </h1>
          <p className="text-base sm:text-lg text-slate-100 max-w-3xl mx-auto font-light leading-relaxed">
            {t('about:subtitle')}
          </p>
        </div>
      </section>

      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 -mt-8 relative z-20 space-y-12">
        {/* Phần 1: Giới thiệu chung về Dự án PEMS */}
        <section className="bg-white rounded-2xl p-6 sm:p-8 shadow-xl shadow-slate-200/60 border border-slate-100">
          <div className="flex items-center gap-3 mb-6 border-b border-slate-100 pb-4">
            <div className="w-12 h-12 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center font-bold shadow-inner">
              <Target className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-[#004c91]">{t('about:sec1.title')}</h2>
              <p className="text-xs text-slate-500">{t('about:sec1.sub')}</p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-slate-50/80 rounded-xl p-5 border border-slate-100 hover:border-orange-200 transition-all">
              <h3 className="text-xs uppercase tracking-wider font-extrabold text-slate-400 mb-2">{t('about:sec1.nameLabel')}</h3>
              <p className="text-xl font-bold text-[#004c91]">PEMS</p>
              <p className="text-sm text-slate-600 font-medium mt-1">{t('about:sec1.nameSub')}</p>
            </div>

            <div className="bg-slate-50/80 rounded-xl p-5 border border-slate-100 hover:border-orange-200 transition-all">
              <h3 className="text-xs uppercase tracking-wider font-extrabold text-slate-400 mb-2">{t('about:sec1.deptLabel')}</h3>
              <p className="text-xl font-bold text-[#f37021]">{t('about:sec1.deptSub')}</p>
            </div>

            <div className="bg-slate-50/80 rounded-xl p-5 border border-slate-100 hover:border-orange-200 transition-all">
              <h3 className="text-xs uppercase tracking-wider font-extrabold text-slate-400 mb-2">{t('about:sec1.purposeLabel')}</h3>
              <p className="text-sm text-slate-700 leading-relaxed font-normal">
                {t('about:sec1.purposeSub')}
              </p>
            </div>
          </div>
        </section>

        {/* Phần 2: Nội dung chi tiết Phòng Hợp tác Quốc tế (IC-FPTU) */}
        <section className="bg-white rounded-2xl p-6 sm:p-8 shadow-xl shadow-slate-200/60 border border-slate-100 space-y-8">
          <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
            <div className="w-12 h-12 rounded-xl bg-blue-50 text-[#004c91] flex items-center justify-center font-bold shadow-inner">
              <Building2 className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-[#004c91]">{t('about:sec2.title')}</h2>
              <p className="text-xs text-slate-500">{t('about:sec2.sub')}</p>
            </div>
          </div>

          {/* Key Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="text-center p-4 bg-gradient-to-br from-orange-50 to-amber-50/40 rounded-xl border border-orange-100">
              <Globe2 className="w-7 h-7 text-[#f37021] mx-auto mb-2" />
              <div className="text-3xl font-extrabold text-[#004c91]">200+</div>
              <div className="text-xs text-slate-600 font-semibold mt-1">{t('about:sec2.statPartners')}</div>
            </div>

            <div className="text-center p-4 bg-gradient-to-br from-blue-50 to-cyan-50/40 rounded-xl border border-blue-100">
              <Award className="w-7 h-7 text-[#004c91] mx-auto mb-2" />
              <div className="text-3xl font-extrabold text-[#004c91]">40+</div>
              <div className="text-xs text-slate-600 font-semibold mt-1">{t('about:sec2.statCountries')}</div>
            </div>

            <div className="text-center p-4 bg-gradient-to-br from-indigo-50 to-purple-50/40 rounded-xl border border-indigo-100">
              <Users className="w-7 h-7 text-indigo-600 mx-auto mb-2" />
              <div className="text-3xl font-extrabold text-[#004c91]">1000+</div>
              <div className="text-xs text-slate-600 font-semibold mt-1">{t('about:sec2.statStudents')}</div>
            </div>

            <div className="text-center p-4 bg-gradient-to-br from-emerald-50 to-teal-50/40 rounded-xl border border-emerald-100">
              <Calendar className="w-7 h-7 text-emerald-600 mx-auto mb-2" />
              <div className="text-3xl font-extrabold text-[#004c91]">50+</div>
              <div className="text-xs text-slate-600 font-semibold mt-1">{t('about:sec2.statVisits')}</div>
            </div>
          </div>

          {/* Details & Support Functions */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-4">
              <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
                <CheckCircle2 className="w-5 h-5 text-[#f37021]" /> {t('about:sec2.historyTitle')}
              </h3>
              <p className="text-slate-600 text-sm leading-relaxed">
                {t('about:sec2.historyP1')}
              </p>
              <p className="text-slate-600 text-sm leading-relaxed">
                {t('about:sec2.historyP2')}
              </p>
            </div>

            <div className="space-y-3">
              <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2">
                <FileCheck className="w-5 h-5 text-[#f37021]" /> {t('about:sec2.supportTitle')}
              </h3>
              <ul className="space-y-2 text-sm text-slate-700">
                <li className="flex items-start gap-2 bg-slate-50 p-3 rounded-xl border border-slate-100">
                  <UserCheck className="w-4 h-4 text-[#f37021] shrink-0 mt-0.5" />
                  <span>{t('about:sec2.support1')}</span>
                </li>
                <li className="flex items-start gap-2 bg-slate-50 p-3 rounded-xl border border-slate-100">
                  <PlaneTakeoff className="w-4 h-4 text-[#f37021] shrink-0 mt-0.5" />
                  <span>{t('about:sec2.support2')}</span>
                </li>
                <li className="flex items-start gap-2 bg-slate-50 p-3 rounded-xl border border-slate-100">
                  <FileCheck className="w-4 h-4 text-[#f37021] shrink-0 mt-0.5" />
                  <span>{t('about:sec2.support3')}</span>
                </li>
              </ul>
            </div>
          </div>
        </section>

        {/* Phần 3: Ban Cán Bộ Phòng Hợp tác Quốc tế (AVATAR TO HƠN & PUBLIC FRIENDLY) */}
        <section className="bg-white rounded-2xl p-6 sm:p-8 shadow-xl shadow-slate-200/60 border border-slate-100 space-y-8">
          <div className="flex items-center gap-3 border-b border-slate-100 pb-4">
            <div className="w-12 h-12 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center font-bold shadow-inner">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-[#004c91]">{t('about:sec3.title')}</h2>
              <p className="text-xs text-slate-500">{t('about:sec3.sub')}</p>
            </div>
          </div>

          {/* Leader HO - Featured Large Card */}
          <div className="bg-gradient-to-r from-slate-900 via-[#00386b] to-[#004c91] rounded-2xl p-6 sm:p-8 text-white shadow-lg relative overflow-hidden flex flex-col md:flex-row items-center gap-8">
            <div className="absolute right-0 top-0 opacity-10 translate-x-10 -translate-y-10 pointer-events-none">
              <Globe2 className="w-72 h-72 text-white" />
            </div>

            {/* HO Avatar: Large 160px */}
            <div className="relative shrink-0">
              <div className="w-36 h-36 sm:w-44 sm:h-44 rounded-full p-1.5 bg-gradient-to-tr from-[#f37021] via-amber-300 to-white shadow-2xl">
                <div className="w-full h-full rounded-full bg-gradient-to-br from-[#004c91] to-blue-900 flex items-center justify-center text-white text-4xl font-black shadow-inner border-2 border-white/20">
                  HO
                </div>
              </div>
              <span className="absolute -bottom-2 right-1/2 translate-x-1/2 bg-[#f37021] text-white text-[11px] font-extrabold uppercase px-3.5 py-1 rounded-full shadow-md whitespace-nowrap">
                {t('about:sec3.hoBadge')}
              </span>
            </div>

            <div className="space-y-2.5 text-center md:text-left relative z-10">
              <span className="inline-block text-xs font-semibold uppercase tracking-wider text-orange-300 bg-white/10 px-3 py-1 rounded-md">
                {t('about:sec3.hoTitle')}
              </span>
              <h3 className="text-2xl sm:text-3xl font-bold text-white">{t('about:sec3.hoName')}</h3>
              <p className="text-xs font-medium text-slate-300 flex items-center justify-center md:justify-start gap-1.5">
                <MapPin className="w-3.5 h-3.5 text-[#f37021]" /> {t('about:sec3.hoCampus')}
              </p>
              <p className="text-sm text-slate-200 max-w-2xl leading-relaxed pt-1">
                {t('about:sec3.hoDesc')}
              </p>
            </div>
          </div>

          {/* 5 Campus Leaders Grid - AVATARS MUCH LARGER (w-28 h-28) */}
          <div>
            <h3 className="text-lg font-bold text-[#004c91] mb-6 flex items-center gap-2">
              <MapPin className="w-5 h-5 text-[#f37021]" /> {t('about:sec3.campusTitle')}
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {campusStaffConfigs.map((cfg) => {
                const name = t(`about:sec3.${cfg.key}Name`);
                const title = t(`about:sec3.${cfg.key}Title`);
                const campus = t(`about:sec3.${cfg.key}Campus`);
                const desc = t(`about:sec3.${cfg.key}Desc`);

                return (
                  <div 
                    key={cfg.key}
                    className="bg-gradient-to-b from-slate-50/90 to-white rounded-2xl p-6 border border-slate-200/80 hover:border-orange-300 hover:shadow-lg transition-all flex flex-col items-center text-center space-y-4"
                  >
                    {/* Large Circular Avatar: w-24 h-24 sm:w-28 sm:h-28 (96px to 112px) */}
                    <div className="relative">
                      <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-full p-1 bg-gradient-to-tr from-[#004c91] via-cyan-400 to-[#f37021] shadow-md">
                        <div className={`w-full h-full rounded-full bg-gradient-to-br ${cfg.avatarColor} text-white font-extrabold flex items-center justify-center text-2xl sm:text-3xl shadow-inner border-2 border-white`}>
                          {cfg.initials}
                        </div>
                      </div>
                    </div>

                    <div className="space-y-1">
                      <h4 className="text-lg font-extrabold text-[#004c91]">{name}</h4>
                      <p className="text-xs font-bold text-[#f37021]">{title}</p>
                      <p className="text-xs font-semibold text-slate-500 flex items-center justify-center gap-1 mt-1">
                        <MapPin className="w-3.5 h-3.5 text-[#f37021] shrink-0" /> {campus}
                      </p>
                    </div>

                    <p className="text-xs text-slate-600 leading-relaxed font-normal pt-2 border-t border-slate-100 w-full">
                      {desc}
                    </p>
                  </div>
                );
              })}
            </div>
          </div>
        </section>

        {/* Phần 4: Đội Ngũ Phát Triển Phần Mềm (PEMS Tech Team) */}
        <section className="bg-white rounded-2xl p-6 sm:p-8 shadow-xl shadow-slate-200/60 border border-slate-100">
          <div className="flex items-center gap-3 mb-6 border-b border-slate-100 pb-4">
            <div className="w-10 h-10 rounded-xl bg-blue-50 text-[#004c91] flex items-center justify-center font-bold shadow-inner">
              <Code2 className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-[#004c91]">{t('about:sec4.title')}</h2>
              <p className="text-xs text-slate-500">{t('about:sec4.sub')}</p>
            </div>
          </div>

          {/* Compact Grid for 5 Dev Members */}
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-5 gap-4">
            {devMembers.map((member) => (
              <div 
                key={member.code} 
                className="group bg-slate-50/70 hover:bg-white rounded-xl p-3.5 border border-slate-200/70 hover:border-orange-300 shadow-sm hover:shadow-md transition-all text-center flex flex-col items-center"
              >
                {/* Dev Avatar */}
                <div className="relative mb-2.5">
                  <div className="w-20 h-20 rounded-full p-0.5 bg-gradient-to-tr from-[#004c91] via-cyan-400 to-[#f37021] shadow-sm group-hover:scale-105 transition-transform duration-300">
                    {member.avatar ? (
                      <img 
                        src={member.avatar} 
                        alt={member.name} 
                        className="w-full h-full rounded-full object-cover border border-white"
                      />
                    ) : (
                      <div className={`w-full h-full rounded-full bg-gradient-to-br ${member.gradient} flex items-center justify-center text-white text-base font-extrabold border border-white shadow-inner`}>
                        {member.initials}
                      </div>
                    )}
                  </div>
                </div>

                <h4 className="text-xs font-extrabold text-[#004c91] group-hover:text-[#f37021] transition-colors leading-tight">
                  {member.name}
                </h4>
                <span className="text-[10px] font-mono text-slate-400 mt-0.5">{member.code}</span>
                <span className="text-[10px] font-medium text-slate-600 mt-1 bg-white px-2 py-0.5 rounded border border-slate-200/80 leading-tight">
                  {member.role}
                </span>
              </div>
            ))}
          </div>
        </section>

        {/* Footer CTA */}
        <div className="text-center py-4">
          <Link 
            to="/" 
            className="inline-flex items-center gap-2 px-6 py-3 rounded-full bg-[#004c91] hover:bg-[#00386b] text-white font-semibold text-sm transition-all shadow-lg shadow-blue-900/20"
          >
            {t('about:backHome')} <ArrowRight className="w-4 h-4" />
          </Link>
        </div>
      </div>
    </div>
  );
};

export default AboutPage;
