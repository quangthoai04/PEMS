/**
 * Component GuideStepsSection
 * Hướng dẫn quy trình theo role — dạng timeline/checklist tĩnh, KHÔNG phải task thật.
 */

import React from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import { resolveHomeRoleBucket, HomeRoleBucket } from '../../../shared/auth/resolveHomeRoleBucket';

// Chỉ giữ KEY ở module scope — text thật resolve bằng t() trong component nên đổi ngôn ngữ
// runtime là re-render, không cần reload. Số bước và thứ tự bước giữ nguyên như trước.
const GUIDE_STEP_KEYS: Record<HomeRoleBucket, string[]> = {
  STUDENT: [
    'internal.guide.roles.STUDENT.step1',
    'internal.guide.roles.STUDENT.step2',
    'internal.guide.roles.STUDENT.step3',
    'internal.guide.roles.STUDENT.step4',
  ],
  HO: [
    'internal.guide.roles.HO.step1',
    'internal.guide.roles.HO.step2',
    'internal.guide.roles.HO.step3',
    'internal.guide.roles.HO.step4',
  ],
  ADMIN: [
    'internal.guide.roles.ADMIN.step1',
    'internal.guide.roles.ADMIN.step2',
    'internal.guide.roles.ADMIN.step3',
  ],
  STAFF_LEADER: [
    'internal.guide.roles.STAFF_LEADER.step1',
    'internal.guide.roles.STAFF_LEADER.step2',
    'internal.guide.roles.STAFF_LEADER.step3',
    'internal.guide.roles.STAFF_LEADER.step4',
  ],
  STAFF: [
    'internal.guide.roles.STAFF.step1',
    'internal.guide.roles.STAFF.step2',
    'internal.guide.roles.STAFF.step3',
  ],
  DEPT_LEADER: [
    'internal.guide.roles.DEPT_LEADER.step1',
    'internal.guide.roles.DEPT_LEADER.step2',
    'internal.guide.roles.DEPT_LEADER.step3',
    'internal.guide.roles.DEPT_LEADER.step4',
  ],
  DEPT_STAFF: [
    'internal.guide.roles.DEPT_STAFF.step1',
    'internal.guide.roles.DEPT_STAFF.step2',
    'internal.guide.roles.DEPT_STAFF.step3',
    'internal.guide.roles.DEPT_STAFF.step4',
  ],
  VISITOR: [],
};

interface GuideStepsSectionProps {
  user: AuthUser;
}

export function GuideStepsSection({ user }: GuideStepsSectionProps) {
  const { t } = useTranslation('home');
  const bucket = resolveHomeRoleBucket(user);
  if (!bucket) return null;

  const stepKeys = GUIDE_STEP_KEYS[bucket];
  if (!stepKeys || stepKeys.length === 0) return null;

  return (
    <section className="py-14 sm:py-16 lg:py-20 bg-slate-50 relative overflow-hidden">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="mb-12">
          <h2 className="text-3xl font-bold text-fpt-navy">{t('internal.guide.title')}</h2>
          <div className="w-16 h-1.5 bg-fpt-orange mt-4 rounded-full"></div>
        </div>

        <div className="space-y-4">
          {stepKeys.map((stepKey, idx) => (
            <motion.div
              key={stepKey}
              initial={{ opacity: 0, x: -10 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.4, delay: idx * 0.08 }}
              className="flex items-start gap-4 bg-white rounded-2xl border border-slate-200 p-5 shadow-sm"
            >
              <div className="w-8 h-8 rounded-full bg-fpt-navy text-white font-bold text-sm flex items-center justify-center flex-shrink-0">
                {idx + 1}
              </div>
              <p className="text-slate-700 leading-relaxed pt-1">{t(stepKey)}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
